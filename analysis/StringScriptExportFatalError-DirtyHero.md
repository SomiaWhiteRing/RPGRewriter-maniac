# RPGRewriter：`-export` 导出在 DirtyHero 工程中“刚开始就致命错误退出”的定位与修复（Vocab 扩展 chunk + Variable 操作码越界）

## 背景

你执行命令：

`RPGRewriter.exe D:\028_DirtyHero\RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`

期望导出 `StringScripts`（可编辑文本脚本），但导出刚开始就报致命错误并中断。

该工程明显带有 2003/Maniacs 类扩展（地图事件里出现 3000+ opcode；`RPG_RT.ldb` 里也存在 0x99 以后的 Vocab 扩展 chunk）。

## 结论（TL;DR）

这个“刚开始就挂”的导出失败，实际由 **两个层面的兼容性问题叠加**导致：

1) **数据库 `Vocab`（0x15）里存在 0x99 之后的扩展条目（0xA1+）**，旧解析只读到 0x99 后就 `byteCheck(0x00)`，会把后续扩展 chunk 的 multibyte 前缀字节 `0x81` 误当成“应该为 0 的终止字节”，从而触发 `Byte check failed` 并中止数据库导出。

2) 即便修复/绕过数据库阶段，导出地图时仍可能因为 **事件指令参数出现“超出旧枚举范围”的值**而崩溃（本次是 `Change Variable (10220)` 的 `operation` 值越界导致 `IndexOutOfRangeException`）。

修复策略：

- `Vocab` 读取时 **扩展扫描范围到 0x01..0xFF**，避免把扩展 chunk 留在流里导致错位（并保持写回也覆盖 0x01..0xFF）。
- 对 `Command.cs` 里所有“数组枚举直接下标访问”的地方，用 `SafeLabel(...)` 做 **越界容错**（导出时宁可输出 `Unknown(n)` 也不能让整个导出崩溃）。

## 1) 从 `Byte check failed (read 0x81 expected 0x00)` 反推：Vocab 扩展 chunk 未被消费导致流错位

导出数据库阶段出现类似输出：

- `Byte check failed at 0xB824 (read 0x81, expected 0x00)`

对 `D:\028_DirtyHero\RPG_RT.ldb` 在 `0xB820` 附近做十六进制观察，可以看到非常典型的 multibyte 结构：

- `81 21 00 81 22 00 81 23 00 ...`

其中：

- `0xA1` 的 multibyte 编码是 `81 21`（因为 `0xA1 = 1*128 + 0x21`）
- 后面的 `00` 则很像该 chunk 的长度/空值字段（取决于具体结构）

这说明 `Vocab` 表里 **确实存在 0xA1、0xA2…这样的扩展 chunk**。如果解析只读到 `0x99` 就停止并立刻 `byteCheck(0x00)`，那么下一字节就是扩展 chunk 的第一字节 `0x81`，自然会触发失败。

对应修复点在 `Database/Vocab.cs`：

- `load()`：把读取/跳过的 chunk 范围扩大到 `0x01..0xFF`，并将 `str` 扩成 256，以保证不会把扩展 chunk 留在流里，最终能正确读到结尾终止字节 `0x00`。
- `write()`：写回也覆盖 `0x01..0xFF`，保证 roundtrip 时不丢扩展条目。

## 2) 从 `IndexOutOfRangeException` 反推：事件指令参数出现“扩展枚举值”

当使用新编译的可执行文件继续导出时，出现未处理异常（示例）：

- `System.IndexOutOfRangeException` at `Command.command10220Variable()`

根因是 `Command.cs` 里大量指令会把事件参数当作“官方枚举”直接用于数组下标：

- 例如 `operations[operation]`、`messagePositions[position]`、`comparisons[v4]` 等。

在打了 Maniacs/扩展补丁的工程中，这类枚举值很可能扩展（或者出现历史脏数据/保留位），导出阶段不应该因为“显示字符串用的标签数组越界”就崩溃。

因此修复采用 **统一的容错输出**：

- 用 `SafeLabel(arr, index)` 替代直接下标访问：越界时输出 `Unknown(index)`，保证导出流程不中断。

## 3) 编译注意：需要用 VS/BuildTools 的 MSBuild（否则 C# 语言特性不兼容）

仓库代码里已使用 `$"..."` 等较新的 C# 语法，`C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe` 会调用旧编译器，导致 `CS1056 Unexpected character '$'`。

应使用 Visual Studio Build Tools 自带的 MSBuild（示例路径）：

- `C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools\MSBuild\Current\Bin\MSBuild.exe`

## 4) 验证

修复后重复运行原命令：

`RPGRewriter.exe D:\028_DirtyHero\RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`

导出可完整结束，并在 `D:\028_DirtyHero\StringScripts\` 生成各地图与数据库脚本文本。

