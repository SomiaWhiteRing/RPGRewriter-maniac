# RPGRewriter：深流程地图剧情仍出现未重命名图片（`Effect\\Ark\\...`）排错报告

## 背景

现象（用户运行后日志）：

- `Image not found: Picture/Effect\\Ark\\ARK墘弌梡`
- 触发点在进入新地图剧情（`Map0720`）后。

这类问题看起来与前一份报告（路径型引用覆盖）相似，但实际仍会复发。

---

## 结论（TL;DR）

这是**程序覆盖盲区**导致，不是游戏特性：

1. 之前修复了“路径型字符串重写”，但**文件清单生成/文件重命名仍只扫资源根目录，不扫子目录**。  
2. `Picture\\Effect\\Ark\\...` 这类子目录资源因此可能长期不在 `input` 覆盖范围内，形成“深流程才触发”的残留。  
3. 本次修复后：
   - 文件清单与重命名都已覆盖子目录；
   - 对“输入漏项但磁盘已是 `uXXXX` 文件名”的场景增加了路径型兜底；
   - `check` 对路径型值不再因非递归查找产生误判。

---

## 证据链

### 1) 触发资源来自子目录路径

原始脚本文本（`Map0720`）包含：

- `Show Picture: ..., Effect\\Ark\\ARK演出用...`

即资源实际位于 `Picture\\Effect\\Ark\\`，不是 `Picture` 根目录。

### 2) 旧实现对子目录覆盖不足

修复前关键代码（本次修改前）：

- `generateFilenames()` 使用 `Directory.EnumerateFiles(folder, "*.*")`（非递归）
- `translateFilenames()` 使用 `Directory.EnumerateFiles(folder, "*.*")`（非递归）

这会导致子目录文件名无法稳定进入/匹配重命名流程。

### 3) `input` 缺项会在深流程暴露

对本项目 `input.txt` 检索时，`ARK演出用*` 原先不在输入映射中；而剧情里实际会调用这些图片。  
因此后续资源名策略一旦变化（例如外部批处理已改名、或后续流程统一改名），就会在深流程报 `Image not found`。

---

## 修复内容

修改文件：`RPGRewriter.cs`

### 1) 子目录覆盖：清单生成与重命名改为递归

- `generateFilenames()` 改为 `SearchOption.AllDirectories`
- `translateFilenames()` 改为 `SearchOption.AllDirectories`
- 增加 `isInUnusedSubfolder()`，跳过 `Unused` 子目录与 `Thumbs.db`
- 重命名目标路径改为“当前文件所在目录”，并按当前目录计算 `maxLen`，避免子目录路径长度误判

对应代码位置：

- `isInUnusedSubfolder`: `2644`
- `generateFilenames`: `2664`（递归枚举在 `2679`）
- `translateFilenames`: 递归枚举在 `3025`

### 2) 路径型漏项兜底：按磁盘 `uXXXX` 名自动重写

新增：

- `buildAsciiUEscapedName()`：非 ASCII -> `uXXXX`
- `FileExistsInMode(..., includeSubfolders)`

并在 `RewritePathLikeFilename()` 里加兜底：

- 若路径型 basename 无映射且含非 ASCII，则尝试 `uXXXX` 候选；
- 若磁盘（递归）存在候选文件，则直接改写引用。

对应代码位置：

- `buildAsciiUEscapedName`: `3967`
- `FileExistsInMode`: `3983`
- `RewritePathLikeFilename` 兜底分支：`4023-4024`

### 3) `check` 路径型有效性校验同步改进

`checkStringValidForMode()` 对路径型值使用递归存在性检查，避免子目录资源被误报缺失。

对应代码位置：

- `checkStringValidForMode`: `4032`
- 递归检查调用：`4058`、`4077`

---

## 编译与验证

## 编译

- `msbuild RPGRewriter.csproj /p:Configuration=Release /p:Platform=x86`
- 结果：`0 error / 0 warning`

## 验证 A：子目录文件名能进入清单

- 运行：`-filelist`
- 结果：`filelist.txt` 中出现
  - `ARK演出用`
  - `ARK演出用1`
  - `ARK演出用2`
  - `ARK演出用3`
  - `ARK演出用4`

说明子目录资源已被递归纳入。

## 验证 B：子目录文件可被程序实际重命名

临时追加 `input` 映射后执行 `-rewrite`，`Picture\\Effect\\Ark` 下结果变为：

- `ARKu6f14u51fau7528.png`
- `ARKu6f14u51fau75281.png`
- `ARKu6f14u51fau75282.png`
- `ARKu6f14u51fau75283.png`
- `ARKu6f14u51fau75284.png`

说明递归重命名生效。

## 验证 C：输入漏项场景的路径型兜底

在不新增 `input` 映射的前提下，仅把磁盘文件先改成 `uXXXX`，再执行 `-rewrite`：

- `Map0720.lmu` 中 `Effect\\Ark\\ARK演出用` 计数：`0`
- `Map0720.lmu` 中 `Effect\\Ark\\ARKu6f14u51fau7528` 计数：`5`

说明“磁盘已改名但输入漏项”的残留可被自动修复。

---

## 最终判定

前次修复解决了“路径型字符串改写入口”，但本次问题根因在于**子目录资源名未完整纳入文件名重写闭环**。  
本次已将“清单生成 + 文件重命名 + 路径型兜底 + 检查期验证”统一补齐，Map0720 这类深剧情触发点不再是盲区。
