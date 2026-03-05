# RPGRewriter：剧情战斗 `Could not open file` 闪退（文件名重写覆盖盲区）排错报告

## 背景

现象：重命名后在特定剧情战斗中弹出 `Could not open file: ...` 并闪退；原版未重命名时不出现该问题。  
目标：判定是“游戏特性”还是“重写程序遗漏”，并修复到可稳定重写。

---

## 结论（TL;DR）

这是**程序覆盖盲区**，不是游戏特性。

根因不是单一某个文件，而是“路径型资源名（如 `../Battle/...`、`System/...`）的处理逻辑分散在少数命令分支里”：

1. 全局 `rewriteString()` 原本只做“整串精确匹配”，对路径型值不做统一重写。  
2. `checkStringValidForMode()` 原本对含 `/` 或 `\` 的字符串直接跳过，导致检查期存在盲区。  

这会造成“只在特定剧情指令路径触发”的漏改风险。

---

## 1) 证据链

## 1.1 稳定复现链路

每轮都按用户要求先恢复备份：

`GameFIle_Backup -> GameFIle (robocopy /MIR)`

再执行：

- `RPGRewriter.exe ...RPG_RT.lmt -rewrite -all -log null`
- `RPGRewriter.exe ...RPG_RT.lmt -check -all -log check_rewrite_new`

重写过程本身稳定结束，无 `error.log`。

## 1.2 二进制与检查对照（是否“重写后新增缺失”）

对比：

- 原版检查：`check_orig_new.txt`（在 `GameFIle_Backup` 生成）
- 重写后检查：`check_rewrite_new.txt`（在 `GameFIle` 生成）

统计结果：

- `orig_missing_count=944`
- `rewrite_missing_count=561`
- `only_in_rewrite_count=0`
- `only_in_orig_count=383`

即：**重写后没有新增缺失条目**（`only_in_rewrite=0`）。

## 1.3 路径型旧名残留扫描（RPG_RT.ldb）

按 `input.txt` 的映射，生成多种路径形态（如 `Folder/旧名`、`../Folder/旧名`、`Folder\旧名`）对 `RPG_RT.ldb` 做字节串扫描，结果：

- `ldb_old_path_pattern_hits=0`

说明“路径+旧名”模式在数据库中未残留。

关键样本（战斗图路径）二进制计数也符合预期：

- 原版 `../Battle/ラクト`：`1`
- 重写后 `../Battle/ラクト`：`0`
- 重写后 `../Battle/u30e9u30afu30c8`：`1`

---

## 2) 修复内容

修改文件：`RPGRewriter.cs`

## 2.1 新增全局路径型重写能力

新增方法（核心）：

- `TryGetMappedFilenameInMode`（约 `3835+`）
- `TryGetMappedFilenameAcrossFolders`（约 `3858+`）
- `InferFolderModeFromPathPrefix`（约 `3885+`）
- `TrySplitPathLikeValue`（约 `3907+`）
- `RewritePathLikeFilename`（约 `3931+`）

作用：当字符串形如 `../Folder/name` 或 `Folder\\sub\\name` 时，自动拆分“路径前缀 + basename”，优先按路径推断目录重写 basename，不再依赖单个命令分支。

## 2.2 `check` 不再跳过路径型字符串

`checkStringValidForMode`（约 `3952+`）改为：

- 路径型值先拆分 basename
- 从路径前缀推断目录（如 `Battle` / `System`）
- 再执行存在性检查与 unused-file 消耗

避免了原先“含分隔符直接 return”带来的静默盲区。

## 2.3 `rewriteString` 增加路径型兜底

`rewriteString`（约 `4031+`）在“精确匹配失败”后新增：

- `RewritePathLikeFilename(mode, str)` 尝试路径型重写
- 仅对非路径型且无映射的非法名继续记录 `replacement not found`

这保证未知位置出现路径型资源引用时，仍能进入统一重写流程。

---

## 3) 编译与回归

## 3.1 编译

`msbuild RPGRewriter.csproj /p:Configuration=Release /p:Platform=x86`  
结果：0 error / 0 warning。

## 3.2 回归结果

1. `-rewrite -all -log null`：正常结束，`All files rewritten.`  
2. 无新 `error.log`。  
3. `-check -all`：未出现 `Failed command parse` / `opcode 11110` 异常。  
4. 原版 vs 重写后缺失差分：`only_in_rewrite_count=0`。  
5. `RPG_RT.ldb` 路径型旧名扫描：`ldb_old_path_pattern_hits=0`。

---

## 4) 最终判定

本问题属于**重写程序对路径型文件名引用的覆盖不完整**导致的运行期漏改风险，不是游戏“天然就会报错”的特性。  
修复后已将路径型文件名处理从“命令级特判”提升为“全局兜底 + 检查期可见”，可显著降低同类剧情战斗闪退复发概率。
