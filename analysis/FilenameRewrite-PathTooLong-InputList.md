# RPGRewriter：`input.txt` 启用后 `-rewrite -all` 在 Sound 重命名阶段崩溃（PathTooLongException）排错报告

## 背景

在提供了完整 `input.txt`（位于游戏目录）后，执行：

`RPGRewriter.exe C:/Users/旻/Downloads/DWVer01_00/GameFIle\RPG_RT.lmt -rewrite -all -log null`

重写在 `Renaming files in Sound folder...` 阶段报：

`Unexpected error occurred. See error.log for details.`

---

## 结论（TL;DR）

这是新的程序问题，根因是“翻译目标文件名未在重命名前做统一规范化”：

- `input.txt` 中存在超长 `uXXXX...` 目标名（尤其 Sound）
- 目标路径超过 .NET Framework 4.0 `MAX_PATH` 限制时，`File.Move()` 抛 `PathTooLongException`
- 旧实现会中断整次重写

已修复为：

1. 在真正重命名前新增“映射预处理”：对 `input` 的目标名统一做 ASCII 白名单规范化（仅 `[A-Za-z0-9_-]`）、长度收敛、去重。  
2. 对空名/保留名/超长名自动生成稳定短名（`<folderCode>_<hash>`），而不是回退原名。  
3. 重命名阶段仍保留异常隔离与二次兜底，确保单文件失败不拖垮整轮流程。

---

## 1) 复现与证据

## 1.1 复现条件

- `input.txt` 需要被实际加载（本次在 `GameFIle` 工作目录下执行，确认日志有 `input.txt loaded as replacement list.`）
- 重命名进入 Sound 文件夹后触发

## 1.2 错误栈

`error.log` 关键内容：

- `System.IO.PathTooLongException`
- `at System.IO.File.InternalMove(...)`
- `at RPGRewriter.M.translateFilenames(String filepath)`
- `at RPGRewriter.M.rewriteAll(String filepath)`

说明崩溃点明确在“文件重命名阶段”，不是地图/数据库解析阶段。

## 1.3 触发样本

本次实际触发的是两条超长日文语音名被替换成更长的 `uXXXX` 形式后，目标路径超出 .NET Framework 4.0 的 `MAX_PATH` 约束（260）：

- `[効果音ラボ]「間もなく開演時刻でございます。お早めにお席にお戻り下さいますようお願いいたします」 `
- 同名 `_2` 版本

---

## 2) 修复内容（不改主流程，新增预处理层）

修改文件：`RPGRewriter.cs`  
函数：`translateFilenames(string filepath)`

关键变更：

1. 新增 `normalizeFilenameTranslationsForDirectory(dir)` 预处理步骤（在 `translateFilenames` 开始处调用）：
   - 规范化目标名字符集
   - 基于目录长度计算安全 `basename` 上限
   - 对超长目标名做“短哈希收敛”
   - 处理重名冲突（去重）
2. 新增统一目标名解析函数 `resolveAvailableDestinationBaseName(...)`：
   - 路径长度/重名冲突/保留名的最终兜底
   - 必要时改写为稳定短名后再 `File.Move`
3. 保留 `try/catch` 防御：
   - `PathTooLongException/IOException/UnauthorizedAccessException`
   - 在冲突/超长场景优先尝试短名 fallback，而非直接回退原名

---

## 3) 修复后验证

同样在加载 `input.txt` 的条件下重跑（`GameFIle` 目录执行）：

- 输出 `Normalized 956 filename translation entries (163 hashed/shortened, 15 deduplicated).`
- 后续 `Rewriting map tree...`、`Rewriting database...`、大量 `Rewriting Mapxxxx.lmu...` 正常进行
- 最终结束为：`All files rewritten.`
- 未生成新的 `error.log`

并验证触发样本已改为 ASCII 安全短名（不保留原日文名）：

- `u52b9u679cu97f3u30e9u30dc_u300cu9593u3082u306au304fu9_752ADED1E6.ogg`
- `u52b9u679cu97f3u30e9u30dc_u300cu9593u3082u306au304fu9_91E706E495.ogg`

---

## 4) 兼容性说明

此修复优先保证“可重写 + 引用一致 + 非 ASCII 不回退”：

- 超长/异常目标名会收敛为稳定短名，而不是保留原名
- 数据重写使用同一映射，避免“文件名与引用不同步”

在 RM2K/2K3 旧工具链与 `MAX_PATH` 约束下，这种“预处理 + 稳定短名”的策略比运行期临时回退更可控。
