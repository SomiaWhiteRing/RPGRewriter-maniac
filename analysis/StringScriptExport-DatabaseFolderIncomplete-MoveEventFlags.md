# RPGRewriter：`StringScripts\Database` 文件组偏少的二次定位报告（程序缺陷 + 导出设置差异）

## 背景

用户反馈：

- `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`
- 虽然可导出，但 `StringScripts\Database` 下文件数量明显少于平时。

要求：通过二进制分析判断这是游戏特性还是程序问题；若为程序问题则修复并给出报告。

## 结论（TL;DR）

这是**两类因素叠加**：

1. **程序问题（已修）**：数据库公共事件命令流中，`Move Event (11330)` 结构被错误解析，导致 `RPG_RT.ldb` 中途错位，数据库导出提前终止，`Database` 文件组不完整。  
2. **非程序问题（设计行为）**：`System/Switches/Variables` 在 StringScript 模式下属于“附加导出项”，默认 `StringScriptExtraneous=0` 时不会写出，因此看起来会比“平时某些配置下”的文件更少。

修复后，`RPG_RT.ldb -extract -single` 与原始 `RPG_RT.lmt -export` 均可跑通，`Database` 主体文件组恢复完整，`Commons` 也完整导出。

## 1) 二进制证据：`11330` 错读导致命令流错位

### 证据 A：Common 15 附近（`0x7D0016`）

关键片段：

- `D8 42 03 00 05 CE 11 08 00 01 06 0A 03 00 00 81 AB 7B ...`

其中：

- `D8 42` = opcode `11330`（Move Event）
- 后续若按旧逻辑（`freq + repeat + skip` 三字节）读取，会把 `06` 错留给下一条命令，下一条从 `0x7D0020` 开始被读成 `opcode=6`，随后 `argCount=5627`，明显异常。
- 观察后续字节 `81 AB 7B`，它实际上是 `22011 (C_FORKEND)` 的合法 multibyte 编码，说明应当从 `0x7D0021` 边界开始才正确。

即：旧解析在该位置**少消费 1 字节**，导致后续错位。

### 证据 B：Common 793 附近（`0x877259`）

关键片段：

- `... D8 42 06 00 09 A0 5A 08 82 00 00 17 17 17 17 17 0A ...`
- 后续可见 `81 AD 42`（即 `22210`）等合法 opcode 序列。

旧解析在该段同样把命令提前结束，随后在 `CommonEvent.load` 末尾 `byteCheck(0x00)` 处读到 `0xAD`（即下一条命令中间字节），触发：

- `Byte check failed at 0x877282 (read 0xAD, expected 0x00)`

说明本质仍是 `11330` 解析边界错误。

## 2) 根因与修复

## 根因

`Command.cs` 对 `11330` 的结构假设错误：

- 旧逻辑按 `target + freq + repeat + skip + route` 处理；
- 实际数据存在 `freq + 扩展 flags(变长) + route` 的编码形态，且长度计数遵循 Move 命令的“特殊计数”语义（高位扩展字节不完全计入长度）。

此外，公共事件字符串化阶段还出现一处越界风险：

- `CommonEvents.cs` 中 `eventTriggers[eventTrigger]` 对扩展值无保护。

## 修复内容

1. `Command.cs`
- 将 `11330` 解析改为：`target -> freq -> flags(multibyte) -> route`。
- 新增 Move 专用计数函数（用于 `moveLength` 折算）。
- `repeat/skip` 由 `flags` 的位提取（保留未知位，写回时不丢失）。
- 写回长度计算改为与读取一致的 Move 计数语义。

2. `Database/CommonEvents.cs`
- `Event Trigger` 文本输出改为安全索引，越界时输出 `Unknown(n)`，避免导出阶段异常中断。

3. 关联的前置修复（本次问题链路中同样关键，已在此前提交）
- `RPGRewriter.cs`：EOF 读取与 `readMultibyte` 栈溢出修复。
- `Database` 多个 tab 增补未知 chunk 兼容：
  - `Items` `0x1E`
  - `Monsters` `0x0F`
  - `Conditions` `0x28`
  - `System` `0x7C`/`0x7E`

## 3) 验证结果

### A. 数据库单文件提取

命令：

- `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\RPG_RT.ldb -extract -single -readcode 932 -filereadcode 932 -miscreadcode 932`

结果：

- 正常结束：`RPG_RT.ldb extracted and written to log.txt.`
- 不再出现 `Aborting due to error.` / `Byte check failed` / `StackOverflowException`。

### B. 用户原始全量导出命令

命令：

- `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`

结果：

- 正常结束：`Exported editable files to StringScripts.`
- 地图导出完整：
  - `Map*.lmu = 1761`
  - `StringScripts\Map*.txt = 1761`

### C. `StringScripts\Database` 实际输出

当前输出文件（14 个）：

- `Hero.txt`
- `Skills.txt`
- `Items.txt`
- `Monsters.txt`
- `Troops.txt`
- `Terrain.txt`
- `Attributes.txt`
- `Conditions.txt`
- `Animations.txt`
- `ChipSet.txt`
- `Vocab.txt`
- `BattleSettings.txt`
- `Classes.txt`
- `BattlerAnimations.txt`

并且：

- `StringScripts\Database\Commons\Common*.txt = 1000`

## 4) 关于“为什么还是比平时少几个文件”

这是**配置行为**，不是本次 bug：

- `System.txt`、`Switches.txt`、`Variables.txt` 在 StringScript 导出中受 `StringScriptExtraneous` 控制。
- 当前环境无 `UserSettings.txt`，程序按默认值 `StringScriptExtraneous=0` 运行，因此不会导出这三项。

所以：

- “之前只导出到少数几个 tab”是程序错误（已修）；
- “现在比某些历史导出少 `System/Switches/Variables`”是设置差异（默认行为）。
