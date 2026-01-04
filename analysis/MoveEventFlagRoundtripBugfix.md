# RPGRewriter：`RPG_RT.ldb` 重写后事件“失效/卡死”的定位与修复（Move Event 标志位有损写回）

## 背景

在部分使用 `patch_maniacs_241028`（或其它引擎扩展/闭源补丁）的 RM2K/2K3 工程中，使用 RPGRewriter 进行“文件名重写”后：

- 游戏不报错，但事件会出现“只执行了一部分”“文本不显示且卡死”等行为异常。
- 将未重写的 `RPG_RT.ldb` 放回后事件恢复正常（但文件名重写效果消失），因此更像是 **二进制结构/语义被写坏**，而不是缺文件/缺替换项。

本次定位的关键线索来自你提供的两份文件：

- `D:\研究\アイスさん探訪記\RPG_RT.ldb`（文件名重写成功，但事件异常）
- `D:\研究\アイスさん探訪記_Origin\RPG_RT.ldb`（原始，事件正常）

## 结论（TL;DR）

问题根因不是“文件名替换导致字符串变长”，而是 **`Move Event` 指令（opcode 11330）的 repeat/skip 等标志位字节被当成 bool 处理并写回，造成非 0/1 的原始值丢失**。

修复方式是对该类字段做 **byte-perfect roundtrip**：

- 读取时保留原始字节（raw byte）
- 逻辑/UI 层仍可将 `raw == 1` 解释为 bool
- 写回时优先写回 raw byte，而不是强行写 0/1

对应修复点：`Command.cs:23`、`Command.cs:494`、`Command.cs:3945`。

## 为什么从“事件异常”推到“Move Event 标志位”

### 1) 先排除“缺文件/缺替换”

你已确认：

- 没有 `replacement not found`
- 事件不是抛异常，而是“没按预期执行/卡死”

这类问题在 RM2K/2K3 里更像是：

- 某条事件指令的参数被写错（1~数个字节），引擎仍能继续跑，但语义发生变化；
- 或者某处结构长度/标志位被写坏导致解析分歧（尤其在扩展补丁环境中）。

### 2) 锁定高风险指令：`Move Event`

你举的例子（击飞、等待、浮现文本）通常会依赖事件指令序列中的：

- `Move Event` / `Move All`
- `Wait`
- 并行进程/停止并行
- 以及一些扩展补丁指令（Maniacs 3000+）

其中 `Move Event` 非常典型：它包含一个“动作序列”结构，且工程/补丁常会在某些标志位里塞入非标准编码或保留位。

### 3) 用二进制“证据”定位：扫描 opcode 并解析字段

`Move Event`（opcode `11330`）在本工程的 multibyte 编码序列开头为：

- `d8 42`（即 11330 的 base-128 multibyte 编码）

在 `Command.cs` 的格式是：

- `[opcode multibyte][indent multibyte][00][moveLen multibyte][target multibyte][freq byte][repeat byte][skip byte][route bytes...]`

因此可以用脚本在 `.ldb` 里扫描 `d8 42`，并读取后续字段做统计/对比。

### 4) 发现关键差异：`repeat` 原本是 `0x82`，重写后变成 `0x00`

对两份 `RPG_RT.ldb` 做扫描后发现：

- 原始文件里存在少量 `Move Event` 的 repeat 字节为 `0x82`（不是 0/1）
- 重写后的坏文件里，这些位置被写成了 `0x00`

这说明问题不是“字符串长度”，而是 **固定字段被有损写回**。

## 根因：bool 化导致有损

旧逻辑（修复前）在读取 `Move Event` 时：

- `moveRouteRepeat = (M.readByte(f) == 1);`
- `moveRouteSkip = (M.readByte(f) == 1);`

写回时：

- `M.writeByte(moveRouteRepeat? 1 : 0);`
- `M.writeByte(moveRouteSkip? 1 : 0);`

这等价于把 repeat/skip 当成“严格的 bool”，会把任何非 0/1 的原始值（例如 `0x82`）强行归一化成 `0` 或 `1`，从而改变引擎解释与事件行为。

在打了扩展补丁或历史数据存在保留位时，这种“看似无害的 bool 化”非常危险。

## 修复：保留 raw byte 并写回

修复后的策略：

1. 读取时保存原始字节：
   - `moveRouteRepeatRaw = M.readByte(f);`
   - `moveRouteSkipRaw = M.readByte(f);`
2. 逻辑/UI 仍用 `raw == 1` 得到 bool：
   - `moveRouteRepeat = moveRouteRepeatRaw == 1;`
   - `moveRouteSkip = moveRouteSkipRaw == 1;`
3. 写回时优先写回 raw（roundtrip）：
   - `M.writeByte(moveRouteRepeatRaw != 0 ? moveRouteRepeatRaw : (moveRouteRepeat? 1 : 0));`
   - `M.writeByte(moveRouteSkipRaw != 0 ? moveRouteSkipRaw : (moveRouteSkip? 1 : 0));`

对应代码位置：`Command.cs:23`、`Command.cs:494`、`Command.cs:3945`。

## 关联改动：`Vocab.cs` 扩展 chunk 读取范围（不是本次事件根因）

你最近的提交 `Update Vocab.cs` 做了：

- `Database/Vocab.cs:463`：读取 `0x01..0xFF`
- `Database/Vocab.cs:474`：跳过 `0x01..0xFF`
- `Database/Vocab.cs:655`：写回 `0x01..0xFF`

这类改动会让 `RPG_RT.ldb` 的重写覆盖面变大（因此文件大小变化是合理的），并能避免“有未解析 chunk 留在流里导致后续解析错位”的灾难性问题。

但本次“事件执行异常”的直接根因最终落在 `Move Event` repeat/skip 字节被 bool 化的有损写回上。

## 验证方法（建议保留为日常回归手段）

### A. 结构性验证：扫描 `.ldb` 内所有 `Move Event` 的 repeat 字节分布

目标：确认重写前后的 repeat/skip raw byte 分布一致（至少不应把 `0x82` 变为 `0x00`）。

可用 Python 快速统计（伪代码）：

- 扫描 `d8 42` 序列
- 解析 `[00][moveLen][target][freq][repeat][skip]`
- 统计 `repeat` 值分布

### B. 行为验证：回归触发事件

复测你指出的链路（例如公共事件 31 的“击飞”相关调用），确认：

- 击飞动作与金币获取同时发生
- 文本浮现/等待输入时不再卡死

## 经验总结：如何避免同类“重写后不报错但行为异常”

1. **能解析 ≠ 能无损重写**  
   所有“看起来像 bool 的字段”都应优先按 raw byte/bitfield roundtrip。

2. **扩展补丁/闭源引擎下优先 byte-perfect**  
   未修改时重写输出应尽量保持与输入一致（或至少保持关键字段不被归一化）。

3. **先找二进制证据再改代码**  
   用 opcode 扫描与字段统计可以快速把问题从“宏观现象”缩到“具体字节/具体函数”。

