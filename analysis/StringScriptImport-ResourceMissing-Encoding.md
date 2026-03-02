# RPGRewriter：`-import` 后大量图片/音效缺失的定位（文件名编码读写不一致导致资源引用“码乱”）

## 背景

你将原工程完整拷贝到新目录后执行：

`RPGRewriter.exe <Project>\\RPG_RT.lmt -import -writecode 936 -nolimit 1`

导入文本后游戏可运行，但出现大量资源缺失（FaceSet/Sound 等）。

你提供了导入前后两份游玩日志 `testplay.log`，以及导入前后的工程文件用于对照。

## 结论（TL;DR）

根因不是资源文件被删除，而是 **`-import` 会重写并写回地图/数据库文件；在写回过程中，资源引用（文件名字符串）用错误的“文件名编码”（默认 932）做了解码/再编码，导致引用字符串发生不可逆的码乱**，引擎按码乱后的名字去找文件自然缺失。

解决方式：

- 在导入时同时指定 **文件名** 的读写编码（例如本工程需 `936`）：
  - `-filereadcode 936 -filewritecode 936`
- 并建议同步设置主字符串与 misc 字符串编码，避免其它字段同类损坏：
  - `-readcode 936 -writecode 936 -miscreadcode 936 -miscwritecode 936`
- 或在 `UserSettings.txt` 里把 `FilenameReadEncoding/FilenameWriteEncoding`（以及 Main/Misc）设为正确值。

## 1) 从日志现象反推：资源名被“改坏/码乱”

对比两份 `testplay.log`：

- 导入前：基本只有 Font/Logo 的缺失提示，没有 FaceSet/Sound 的缺失。
- 导入后：出现大量：
  - `Cannot find: FaceSet/...`、`Image not found: FaceSet/...`
  - `Cannot find: Sound/...`、`Sound not found: ...`

并且导入后缺失名呈现明显的“码乱特征”，例如：

- `FaceSet/@エE啸丒`（原本应为 `@エレキバリアB` 之类）
- `Sound/黑雷磥E`（原本对应 `黑雷打1/2/3`）
- `Sound/觼E`（原本对应 `鱼1/2/3/4`）
- `Sound/襾E`（原本对应 `音/音2`）

这类“看起来像乱码、但仍是合法字符串”的资源名，最典型来源就是 **用错代码页读写文件名字段**。

## 2) 二进制证据：Map 文件中 FaceSet 名被写回时改变了字节序列

以 `Map0006.lmu` 的一条 `Change Face`（opcode 10130，base-128 multibyte 为 `CF 12`）为例：

- 导入前，该 Face 名字节（长度 `0E`）为（`CP936/GBK` 可正确解码成 `@エレキバリアB`）：
  - `0E 40 A5 A8 A5 EC A5 AD A5 D0 A5 EA A5 A2 42`
- 导入后，同位置同长度（仍为 `0E`）却变为（`GB18030` 解码为日志里看到的码乱字符串）：
  - `0E 40 A5 A8 A5 81 45 AD A5 D0 A5 81 45 A2 42`

关键点：

- 长度字段没变（仍 14 字节），说明不是“长度错导致解析错位”，而是 **字符串内容本身被重写为另一组字节**。

## 3) 根因：默认文件名编码 932 会把 `CP936` 字节解码成“半角假名 + 中点”等，再编码回去就改变了原字节

在 .NET 的 `Encoding(932)` 下，导入前的 `CP936` 字节序列会被解释成：

- `@･ｨ･・ｭ･ﾐ･・｢B`

这不是你在脚本里写的内容，而是 **用错编码读二进制字节得到的“错误字符串”**。

随后 `-import` 触发地图/数据库写回时，如果仍用 `Encoding(932)` 写回这个错误字符串，就会得到：

- `40 A5 A8 A5 81 45 AD A5 D0 A5 81 45 A2 42`

这恰好就是导入后的实际字节序列（见上节），因此可以确定：

- 工程的资源名在数据文件里实际是按 `936`（或兼容的 `GB18030`）存储
- 但导入时程序仍按默认 `932` 去读/写文件名字段
- 导致资源引用被“码乱化”，引擎按码乱名查找文件，出现图片/音效缺失

同理，Sound 缺失名可用相同的“936 -> 932 -> 18030 视角”解释并复现对应关系（例如 `黑雷打1/2/3 -> 黑雷磥E`）。

## 4) 修复建议

### A. 立刻可用的命令行修复（推荐）

对该工程，用同一套代码页处理主文本、文件名与 misc：

`RPGRewriter.exe <Project>\\RPG_RT.lmt -import -readcode 936 -writecode 936 -filereadcode 936 -filewritecode 936 -miscreadcode 936 -miscwritecode 936 -nolimit 1`

要点：

- `-writecode` 只影响“要翻译”的字符串，不会自动影响文件名字段
- 资源引用能否保持不坏，主要取决于 `-filereadcode/-filewritecode`

### B. 固化到 `UserSettings.txt`

如果你习惯不在命令行带一堆参数，可以在运行目录放 `UserSettings.txt`，把：

- `MainReadEncoding/MainWriteEncoding`
- `FilenameReadEncoding/FilenameWriteEncoding`
- `MiscReadEncoding/MiscWriteEncoding`

都设为正确代码页（本工程为 936）。

### C. 程序侧改进（已做的最小保护）

本仓库的 `RPGRewriter.cs` 已加入一条命令行警告：当你设置了 `-readcode/-writecode` 但没有设置 `-file(read/write)code`，且两者编码不一致时，会提示可能在 `-import/-rewrite` 写回时破坏资源引用。

## 5) 验证方法

1. 用“修复后的参数”重新在干净拷贝上执行 `-import`。
2. 运行同一段测试游玩链路，确认 `testplay.log` 不再出现：
   - `Cannot find: FaceSet/...`
   - `Cannot find: Sound/...`
3. 可选：对比导入前后 `Map0006.lmu` 中 `CF 12 00 0E ...` 后的字节序列应保持为 `@エレキバリアB` 的 `CP936` 表示（不再出现 `A5 81 45` 这类 932 roundtrip 痕迹）。

