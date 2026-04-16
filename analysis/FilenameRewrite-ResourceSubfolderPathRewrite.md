# RPGRewriter：文件名重写后大量 `Image not found` 的二次定位与修复（资源子目录未进入重写闭环）

## 背景

用户在使用：

`RPGRewriter.exe D:\0000フレイム冒険記1.74\0000フレイム冒険記1.7 -\RPG_RT.lmt -rewrite -all -log null`

对 `フレイム冒険記 1.74` 做文件名重写后，游玩过程中仍出现大量运行期缺图。  
`EasyRPG.log` 中的典型报错包括：

- `Cannot find: CharSet/僀儀儞僩儕僗僩\u307fu3093u306au4e00u7dd2u306au30e2u30fcu30c9`
- `Cannot find: Picture/僒僽儊僯儏乕攚宨\拠娫僉儍儔\001`
- `Cannot find: Picture/僒僽儊僯儏乕攚宨\u80ccu666fu8d64u306eu68ee`
- `Cannot find: Picture/梔惛偺嶨壿壆\u5996u7cbeu306eu96d1u8ca8u5c4b80uff5e41`

这些字符串的共同特征是：

- 一部分仍是日文路径段在非日文环境下的乱码显示；
- 另一部分已经被重写为 `uXXXX`；
- 即运行时拿到的是“**路径前半仍旧、路径后半已改名**”的混合引用。

---

## 结论（TL;DR）

这是**程序缺陷**，不是游戏特性。

根因不是单一“某个文件漏改”，而是**资源子目录从未真正进入文件名重写闭环**：

1. 旧实现虽然已经递归枚举文件，但**只重命名文件，不重命名资源子目录**。  
2. 路径型字符串重写只处理**最后一个 basename**，中间目录段（如 `サブメニュー背景\\仲間キャラ\\`、`妖精の雑貨屋\\`）会原样保留。  
3. `check` 旧逻辑对路径型值实际上只按 basename 递归找文件，**无法发现“目录段没同步”**。  
4. 在本项目这种超大 `input.txt` 下，文件名查找使用 `List.Contains + IndexOf`，导致重写阶段极慢，不利于按“恢复备份 -> 重跑 -> 验证”流程精确迭代。  
5. 命令行模式下即使传了 `-log null`，流程末尾仍会卡在 `Write missing translations to null.txt? (Y/N)`，阻塞无人值守重写。

本次修复后：

- 资源子目录会先统一重命名为 ASCII；
- 路径型资源引用会把**目录段 + basename** 一起重写；
- `check` 会按完整相对路径验证存在性；
- 大型 `input.txt` 的文件名查找改为字典索引；
- 命令行 `-log null` 不再应当在末尾询问 `Y/N`。

---

## 证据链

### 1) 运行期缺失项不是“纯旧名”，而是“旧目录段 + 新文件名”的混合串

从 `EasyRPG.log` 可见：

- `CharSet/僀儀儞僩儕僗僩\u307fu3093u306au4e00u7dd2u306au30e2u30fcu30c9`
- `Picture/僒僽儊僯儏乕攚宨\拠娫僉儍儔\001`
- `Picture/梔惛偺嶨壿壆\u5996u7cbeu306eu96d1u8ca8u5c4b80uff5e41`

其中：

- `僒僽儊僯儏乕攚宨` 对应 `サブメニュー背景`
- `梔惛偺嶨壿壆` 对应 `妖精の雑貨屋`

说明运行时请求的不是“完全旧日文路径”，而是**子目录没改、末端资源名已改**。

### 2) 重写后的资源目录确实长期保留了大量日文子目录

对旧现场扫描可见，`Picture` 下仍存在：

- `Picture\サブメニュー背景`
- `Picture\妖精の雑貨屋`
- `Picture\図鑑\★`
- `Picture\アイテム合成\レベル11`

而这些目录中的文件 basename 已大量变成 `uXXXX`。  
这与运行期“目录段是乱码、末端文件名是 `uXXXX`”完全吻合。

### 3) 旧实现只改文件，不改目录

修复前逻辑里：

- `generateFilenames()` 已递归扫文件；
- `translateFilenames()` 也递归扫文件；
- 但整个流程没有任何“资源子目录重命名”步骤。

即使 `Picture\サブメニュー背景\背景赤の森.bmp` 的 basename 被改成 `u80ccu666fu8d64u306eu68ee.bmp`，目录 `サブメニュー背景` 仍会保留日文。

### 4) 旧路径型改写只改 basename，不改目录段

`RewritePathLikeFilename()` 的旧策略是：

- 用最后一个 `/` 或 `\` 拆成 `prefix + baseName`
- 只重写 `baseName`
- `prefix` 原样返回

因此：

- `サブメニュー背景\仲間キャラ\001`  
  会被当成 `prefix=サブメニュー背景\仲間キャラ\`、`baseName=001`
- 因 `001` 本来就是 ASCII，有映射也通常不变
- 最终目录段照旧，运行时仍然请求旧目录

### 5) `Command.cs` 中的嵌入式资源路径也存在同样盲区

自由文本路径处理（Maniacs 扩展字符串等）原本虽然能匹配：

- `Picture\...`
- `../CharSet/...`

但实际只把匹配结果当作“folder + basename”处理，没有把子目录整段交给统一重写逻辑。  
这会导致数据库/地图中以字符串形式嵌入的路径同样残留未重写目录段。

### 6) `check` 对路径型值是“按 basename 递归找文件”，会漏报目录段问题

修复前 `checkStringValidForMode()` 对路径型值最终仍只是检查：

- 某模式下是否存在同 basename 的任意文件

这意味着：

- 数据里写的是 `サブメニュー背景\仲間キャラ\001`
- 磁盘上哪怕只有别处存在 `001.bmp`

也可能被误判为“存在”，从而让目录段残留长期不可见。

---

## 修复内容

修改文件：

- `RPGRewriter.cs`
- `Command.cs`

### 1) 新增资源子目录重命名阶段

在真正重命名前新增：

- `normalizeResourceDirectorySegment()`：把资源目录段规范化为 ASCII / `uXXXX`
- `translateResourceSubdirectories()`：按“**从深到浅**”顺序重命名所有资源子目录
- `resolveAvailableDestinationDirectoryName()`：处理目录名冲突与路径长度

对应代码位置：

- `RPGRewriter.cs:2809`
- `RPGRewriter.cs:2993`
- `RPGRewriter.cs:3019`

效果：

- `Picture\サブメニュー背景` -> `Picture\u30b5u30d6u30e1u30cbu30e5u30fcu80ccu666f`
- `Picture\妖精の雑貨屋` -> `Picture\u5996u7cbeu306eu96d1u8ca8u5c4b`
- `CharSet\イベントリスト` -> `CharSet\u30a4u30d9u30f3u30c8u30eau30b9u30c8`

### 2) 路径型资源名改写升级为“目录段 + basename”一起处理

新增：

- `RewritePathDirectoryPrefix()`：重写路径前缀中的资源子目录段
- `ExtractModeRelativePath()`：从路径型值中提取模式内相对路径
- `FileExistsRelativePathInMode()`：按完整相对路径校验资源存在性

并将 `RewritePathLikeFilename()` 改为：

- 先重写目录前缀
- 再重写 basename
- 若 basename 不变但目录段可改，仍返回新路径

对应代码位置：

- `RPGRewriter.cs:4131`
- `RPGRewriter.cs:4216`
- `RPGRewriter.cs:4257`

### 3) `check` 改为按完整相对路径验证

`checkStringValidForMode()` 对路径型值不再只看 basename，而是：

- 推断 mode
- 提取相对路径
- 用 `FileExistsRelativePathInMode()` 检查

对应代码位置：

- `RPGRewriter.cs:4308`
- `RPGRewriter.cs:4328`

这样 `Picture\u30b5...\\u4ef2...\\001` 和 `Picture\001` 不会再被混为一谈。

### 4) `Command.cs` 的嵌入式资源路径统一走完整路径重写

修复：

- `RewritePictureFilenameFallback()`：路径型 picture 参数直接走 `M.rewriteString(mode, wholePath)`
- `CheckPictureFilenameFallback()`：路径型值直接走 `M.checkStringValidForMode(wholePath, mode)`
- `RewriteEmbeddedResourcePaths()`：匹配到的 `name` 改为整段相对路径，不再只处理 basename

对应代码位置：

- `Command.cs:4356`
- `Command.cs:4389`
- `Command.cs:4430`

### 5) 大型 `input.txt` 的文件名索引改为字典

新增：

- `transSourceIndexByMode`
- `GetTranslationIndexInMode()`

并把文件名模式下的：

- `Contains`
- `IndexOf`

替换为字典查找，避免在超大映射表下反复线性扫描。

对应代码位置：

- `RPGRewriter.cs:163`
- `RPGRewriter.cs:2324`

### 6) 命令行 `-log null` 的末尾提示改为自动跳过

`logSave()` 在 `commandLineMode` 下改为：

- `LogFilename == "null"` -> 不写文件，也不再询问
- 非 `null` -> 直接写出

对应代码位置：

- `RPGRewriter.cs:5995`

---

## 编译

命令：

- `msbuild RPGRewriter.csproj /p:Configuration=Release /p:Platform=x86`

结果：

- `0 error / 0 warning`

---

## 验证

### 验证 A：按分析流程在独立工作副本重跑

为避免污染现场，先执行：

- `backup -> work_fix_subdirs`（镜像副本）

然后在**游戏目录本身**启动重写，确保 `input.txt` 可被加载：

- `C:\Users\旻\Documents\GitHub\RPGRewriter-maniac\RPGRewriter.exe RPG_RT.lmt -rewrite -all -log null`

日志显示：

- `input.txt loaded as replacement list.`
- `Renaming subdirectories in ... folder...`
- `Normalized 581 filename translation entries (360 hashed/shortened, 3 deduplicated).`
- 后续一直重写到 `Map0484.lmu`

说明：

- `input.txt` 已正确生效；
- 资源子目录重命名已进入主流程；
- 地图/数据库回写链路能继续跑通。

### 验证 B：资源子目录已变成 ASCII

在工作副本与回写后的主目录中都可见：

- `Picture\u30b5u30d6u30e1u30cbu30e5u30fcu80ccu666f`
- `Picture\u5996u7cbeu306eu96d1u8ca8u5c4b`
- `CharSet\u30a4u30d9u30f3u30c8u30eau30b9u30c8`

并且 `CharSet\u30a4u30d9u30f3u30c8u30eau30b9u30c8` 下已存在：

- `u307fu3093u306au4e00u7dd2u306au30e2u30fcu30c9.png`

### 验证 C：二进制数据中已写入完整 ASCII 路径

对工作副本 `RPG_RT.ldb` 做 ASCII 串检查，可直接检出：

- `u30b5u30d6u30e1u30cbu30e5u30fcu80ccu666f\u4ef2u9593u30adu30e3u30e9\001`
- `u30b5u30d6u30e1u30cbu30e5u30fcu80ccu666f\u80ccu666fu8d64u306eu68ee`
- `u5996u7cbeu306eu96d1u8ca8u5c4b\u5996u7cbeu306eu96d1u8ca8u5c4b80uff5e41`

说明原先报错的三类路径都已进入数据库重写结果。

### 验证 D：`Map0075.lmu` 中的 CharSet 路径已不再是“乱码前缀 + uXXXX 后缀”

对 `Map0075.lmu` 扫描，能找到：

- `u30a4u30d9u30f3u30c8u30eau30b9u30c8\u307fu3093u306au4e00u7dd2u306au30e2u30fcu30c9`

这说明该处已从“旧目录段残留”修复为完整 ASCII 相对路径。

### 验证 E：主目录已同步修复结果，`backup` 保持不变

完成工作副本验证后，再执行：

- `work_fix_subdirs -> 0000フレイム冒険記1.7 -`（镜像同步）

确认：

- 主目录下已能看到新 ASCII 子目录与重写后的数据文件；
- `backup` 未被修改，仍可作为回滚基线。

---

## 补充说明

### 1) `input.txt` 的加载依赖工作目录

本次实测再次确认：

- 若在仓库目录启动 `RPGRewriter.exe`，会出现 `input.txt not found. No replacement list loaded.`
- 只有在游戏目录下启动，才会加载游戏目录里的 `input.txt`

因此实战流程必须是：

1. 进入游戏目录  
2. 再执行 `RPGRewriter.exe RPG_RT.lmt -rewrite -all -log null`

### 2) 末尾 `-log null` 卡住的问题属于“收尾交互缺陷”

本项目工作副本第一次完整重写时，主体写回已经完成，但旧可执行文件仍在最后卡在：

- `Write missing translations to null.txt? (Y/N)`

这不会影响已经写入的 `RPG_RT.ldb / Map*.lmu` 结果，但会阻塞无人值守流程。  
因此本次在代码层额外修复了 `logSave()` 的命令行分支。

---

## 最终判定

本次大量运行期缺图的根因是：

- **资源子目录没有进入文件名重写闭环**
- **路径型资源引用只改 basename，不改目录段**

并且旧 `check` 无法暴露这一问题，导致它会在深流程游玩时才集中爆发。  

本次已将：

- 子目录重命名
- 路径型完整重写
- 检查期完整路径校验
- 大输入映射性能
- 命令行 `-log null` 收尾阻塞

统一补齐。对 `フレイム冒険記 1.74` 已按“恢复备份 -> 独立副本重写 -> 二进制验证 -> 回写主目录”的完整流程处理完毕。
