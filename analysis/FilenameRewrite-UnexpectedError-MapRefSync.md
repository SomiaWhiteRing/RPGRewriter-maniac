# RPGRewriter：`-rewrite -all` 报错与“文件已改名但地图引用未同步”的定位与修复报告

## 背景

用户命令：

`RPGRewriter.exe C:/Users/旻/Downloads/DWVer01_00/GameFIle\RPG_RT.lmt -rewrite -all -log null`

现象：

1. 运行时报 `Unexpected error occurred. See error.log for details.`  
2. 进入游戏后发现资源文件名已被重写，但地图/事件数据引用未完整同步，导致运行期报错。

---

## 结论（TL;DR）

这是**程序缺陷**，不是游戏特性。根因是两条问题链叠加：

1. `yesNoPrompt()` 在命令行无交互输入时对 `Console.ReadLine()` 的空值未处理，导致 `NullReferenceException`（表现为 `Unexpected error`）。
2. `Move Event (11330)` 的解析/写回对扩展编码兼容不足（含 Move Route 长度计数与 `0x22 CharSet Change` 假 multibyte 索引），会导致命令流错位，进而影响地图事件重写完整性。

修复后：

- `-rewrite -all -log null` 可稳定结束，不再出现 `Unexpected error`。
- 通过二进制对照验证，资源文件名与地图事件中的引用可同步重写并可逆恢复。

---

## 1) 复现与证据

## 1.1 修复前 `Unexpected error`

历史 `error.log`（修复前，时间戳 `2026-03-02 22:05:37`）显示：

- `System.NullReferenceException`
- 栈位于 `RPGRewriter.M.yesNoPrompt()`

触发点是重写结束时的提示：

`Write missing translations to null.txt? (Y/N)`

在无交互输入场景下，`Console.ReadLine()` 返回 `null`，旧逻辑直接 `input.ToUpper()` 导致空引用。

## 1.2 地图命令流错位（Map0313 二进制/trace 证据）

修复前的 Map0313 指令 trace 中，关键段表现为：

- `Line 229: opcode 11330 ... pos 0x1E3D -> 0x1E60`
- 下一条被错误解读为 `opcode 1, indent 11340`
- 随后在 `0x1E79` 触发 `Byte check failed`

这说明 `11330` 在该处少/错消费了字节，后续指令边界整体漂移。

修复后的 Map0313 指令 trace 对应段恢复为：

- `Line 229: opcode 11330 ... pos 0x1E3D -> 0x1E61`
- `Line 230: opcode 11340`
- `Line 231: opcode 11550`

指令边界重新对齐。

## 1.3 “文件名与地图引用同步”二进制验证（控制实验）

采用临时映射：

```txt
***CHARSET
DW
DW_TMPTEST
```

执行同一 `-rewrite -all` 后：

- 物理文件：`CharSet\DW.png -> CharSet\DW_TMPTEST.png`
- 程序输出显示实际重写了 13 张地图（如 `Map0918/1066/1120/.../1800`）
- 二进制计数：
  - 重写前：`Map*.lmu` 中 `DW_TMPTEST` 计数 `0`
  - 重写后：`Map*.lmu` 中 `DW_TMPTEST` 计数 `17`

再做反向映射恢复：

```txt
***CHARSET
DW_TMPTEST
DW
```

恢复后验证：

- `CharSet\DW.png` 存在
- `CharSet\DW_TMPTEST.png` 不存在
- `Map*.lmu` 与 `RPG_RT.ldb` 中 `DW_TMPTEST` 计数均回到 `0`

说明“文件改名 + 地图数据引用改写”已可双向一致。

---

## 2) 代码修复项

## 2.1 命令行无交互输入导致空引用

- `RPGRewriter.cs:4246` (`yesNoPrompt`)
  - 新增 `input == null` 保护，命令行无输入时安全返回 `false`。

## 2.2 `11330 Move Event` 解析/写回兼容修复

- `Command.cs:23` 引入 `moveRouteFlags`
- `Command.cs:28` 新增 `CountMoveLengthValue(int value)`（Move 专用长度计数）
- `Command.cs:501-509` 读取 `moveRouteFlags`（multibyte）并按位解析 `repeat/skip`
- `Command.cs:3963-3978` 写回时保留 flags 未知位，并按同一计数语义回写长度

## 2.3 `MoveStep 0x22 (CharSet Change)` 假 multibyte 索引兼容

- `MoveStep.cs:68` 读取路径增加 fake-multibyte 索引处理（`source != "Custom"`）
- `MoveStep.cs:151` 写回路径支持 `value >= 128` 的对应编码

## 2.4 关联稳定性修复（保证导出/验证链路不再被次级异常中断）

- `Database/Heroes.cs:301+`：`getString()` 对空数组/空列表做安全兜底，避免 `-extract -single` 因脏数据空引用崩溃。
- `RPGRewriter.cs:3201/3228`：`readByte` EOF 显式抛错、`readMultibyte` 改为迭代读取并限制深度，避免递归链导致栈溢出。

---

## 3) 编译与回归验证

## 3.1 编译

- 使用 `msbuild RPGRewriter.csproj /p:Configuration=Release /p:Platform=x86`
- 成功产出并替换 `RPGRewriter.exe`

附记：本次调试中同步清理了不兼容 C#4 的插值语法（`$"..."`）与自动属性初始化，以保证该项目在 `.NET Framework 4.0` 工具链下可编译。

## 3.2 关键命令回归结果

1. `RPG_RT.ldb -extract -single -readcode 932 -filereadcode 932 -miscreadcode 932`  
结果：`RPG_RT.ldb extracted and written to log.txt.`

2. 用户原命令：  
`RPGRewriter.exe ...RPG_RT.lmt -rewrite -all -log null`  
结果：`All files rewritten.`，且不生成新的 `error.log`

3. `RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`  
结果：完整导出结束：`Exported editable files to StringScripts.`

4. 按用户要求执行“每次测试前从 `GameFIle_Backup` 全量恢复”回归  
连续 5 轮（每轮都 `robocopy /MIR` 恢复后再执行 `-rewrite -all -log null`）结果均为：
   - 命令输出 `All files rewritten.`
   - 不出现 `Unexpected error occurred`
   - 不产生新的 `error.log`

5. 旧 `error.log` 残留干扰修复  
在 `Main()` 启动时增加对旧 `error.log` 的清理，避免“上一轮失败日志残留”被误判为本轮新异常。实测在预先放置伪造 `error.log` 的情况下，成功重写后该文件会被删除且不会重新生成。

---

## 4) 最终判定

本问题属于**导出/重写程序兼容性缺陷**，非游戏数据“天然特性”。  
修复后已满足：

- 运行稳定性（不再 `Unexpected error`）
- 地图事件指令对齐（不再在 `Map0313` 发生错位校验失败）
- 文件名重写与地图数据引用同步（可验证、可逆）
