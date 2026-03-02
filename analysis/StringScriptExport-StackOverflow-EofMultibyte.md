# RPGRewriter：`-export` 导出触发 `StackOverflowException` 的定位与修复（EOF 被误读为 `0xFF`，导致 multibyte 递归无穷）

## 背景

用户在导出命令下稳定复现崩溃：

`RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`

现象：导出进行到 `Map0491.lmu` 后进程直接终止，报：

`Process is terminated due to StackOverflowException.`

## 结论（TL;DR）

根因是底层读取函数在 EOF 处理上有缺陷：

1. `readByte()` 把 `FileStream.ReadByte()` 的 `-1`（EOF）直接强转成 `byte`，变成 `0xFF`。  
2. `readMultibyte()` 采用递归读取，遇到 `0xFF` 会一直走“继续读取”分支。  
3. 当流已在 EOF 时，每次读取都还是 `-1 -> 0xFF`，递归无终止，最终栈溢出。

修复后，EOF 改为显式抛 `EndOfStreamException`，并将 multibyte 读取改为迭代+长度保护，彻底消除该类栈溢出。

## 复现与收敛

### 1) 原始问题复现（修复前）

- 命令：
  - `RPGRewriter.exe ...RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`
- 结果：
  - 导出到 `Extracting Map0491.lmu...` 后崩溃：`StackOverflowException`。

### 2) 最小复现（单图）

- 命令：
  - `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\Map0491.lmu -single -export -readcode 932 -filereadcode 932 -miscreadcode 932`
- 结果：
  - 同样直接 `StackOverflowException`。

这说明问题可稳定收敛在单个地图读取/导出路径，而不是“全工程累计状态”导致。

## 根因分析

问题在底层字节读取链：

- `RPGRewriter.cs:3201` `readByte(FileStream f)`（修复前）
  - `return (byte)f.ReadByte();`
  - EOF 时 `ReadByte()` 返回 `-1`，强转后变成 `255 (0xFF)`。

- `RPGRewriter.cs:3228` `readMultibyte(FileStream f, int sum = 0)`（修复前）
  - 遇到 `b >= 128` 递归调用自身继续读。
  - 当已到 EOF，`b` 永远是 `0xFF`，递归永不终止，最终触发 `StackOverflowException`。

这也是“看起来像某地图触发，但异常是栈溢出”的直接解释。

## 修复方案

修改文件：`RPGRewriter.cs`

### 1) `readByte()` 增加 EOF 显式异常

- 从“直接强转 byte”改为：
  - 先读取 `int value = f.ReadByte()`
  - `value == -1` 时抛 `EndOfStreamException`
  - 否则再转为 `byte`

### 2) `readMultibyte()` 从递归改为迭代，并加防护

- 改为 `while` 迭代累积值，避免递归栈增长。  
- 增加 `maxDepth`（16）限制，异常数据直接抛 `FormatException`。  
- 增加 `int` 溢出检测，异常值抛 `OverflowException`。

## 编译与调试

编译命令：

- `dotnet build RPGRewriter.csproj -c Release -p:Platform=x86`

输出：

- `bin\Release\RPGRewriter.exe`

并已同步覆盖仓库根目录 `RPGRewriter.exe` 用于原命令回归。

## 验证结果

### A. 单图回归（Map0491）

命令：

- `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\Map0491.lmu -single -export -readcode 932 -filereadcode 932 -miscreadcode 932`

结果：

- 不再崩溃；正常输出：`Map0491.lmu extracted and written to log.txt.`

### B. 全量回归（用户原命令）

命令：

- `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\RPG_RT.lmt -export -readcode 932 -filereadcode 932 -miscreadcode 932`

结果：

- 全流程跑完，不再出现 `StackOverflowException`；末尾输出：`Exported editable files to StringScripts.`

### C. 导出完整性（地图文本）

统计：

- 原工程 `Map*.lmu` 数量：`1761`
- 导出 `StringScripts\Map*.txt` 数量：`1761`

说明地图文本已完整导出，不再因栈溢出中断。

## 经验总结

1. **底层 I/O 的 EOF 语义必须显式处理**：不能把 `-1` 当普通字节继续流转。  
2. **二进制变长字段读取不应使用无保护递归**：应优先迭代，并加长度/溢出保护。  
3. **出现 StackOverflow 时优先检查“递归终止条件是否可被 EOF 破坏”**：这是此类故障的高频根因。
