# RPGRewriter：剧情战斗 `Image not found: Frame/僾儗僴3` 问题排错报告

## 背景

用户反馈重命名后进入特定剧情战斗仍会闪退，日志为：

- `Image not found: Frame/僾儗僴3`

弹窗中的 `僾儗僴3` 是 `プレハ3` 在非日文代码页下的乱码显示，说明运行时仍在请求旧日文文件名。

---

## 结论（TL;DR）

这是**程序问题**，不是游戏特性。

根因在数据库 `Terrain` 的 2003 字段读取模式：

- `0x15 backgroundName`
- `0x1f foregroundName`

旧实现按 `M_BACKDROP` 处理，导致这两类 **Frame 资源名**未正确套用 `***FRAME` 映射，出现“Frame 文件已改名但数据库引用残留旧名”的情况。

---

## 1) 证据

## 1.1 用户运行期证据

- 日志：`Image not found: Frame/僾儗僴3`
- 对应真实名：`プレハ3`

## 1.2 重写后数据证据（修复前表现）

- `Terrain #56` 导出中仍出现：`Background: プレハ3`
- 同时 Frame 目录已存在重命名目标（`u30...`），会触发运行期找不到旧名。

---

## 2) 修复

修改文件：`Database/Terrains.cs`

- `Terrain.load()` 中：
  - `0x15 backgroundName` 从 `M.M_BACKDROP` 改为 `M.M_FRAME`
  - `0x1f foregroundName` 从 `M.M_BACKDROP` 改为 `M.M_FRAME`

对应行：

- `Database/Terrains.cs:142`
- `Database/Terrains.cs:155`

---

## 3) 回归验证

按用户要求，每轮先执行：

- `GameFIle_Backup -> GameFIle` 全量恢复（`robocopy /MIR`）

然后执行：

- `RPGRewriter.exe ...RPG_RT.lmt -rewrite -all -log null`

验证结果：

1. Frame 文件名重写正确：
   - `Frame/u30d7u30ecu30cf1.png`
   - `Frame/u30d7u30ecu30cf2.png`
   - `Frame/u30d7u30ecu30cf3.png`
2. `RPG_RT.ldb` 字节计数：
   - `プレハ1=0, プレハ2=0, プレハ3=0`
   - `u30d7u30ecu30cf1=1, u30d7u30ecu30cf2=3, u30d7u30ecu30cf3=1`
3. `-extract` 后 `Scripts/Database/Terrain.txt`：
   - `Background: u30d7u30ecu30cf2`
   - `Background: u30d7u30ecu30cf3`

说明 `Terrain` 中 Frame 相关引用已跟随重命名同步更新。

---

## 4) 最终判定

本次闪退由**地形 Frame 字段重写模式错误**导致。修复后，`Frame/プレハ3` 类旧引用不会残留，已满足该问题链路的修复条件。

