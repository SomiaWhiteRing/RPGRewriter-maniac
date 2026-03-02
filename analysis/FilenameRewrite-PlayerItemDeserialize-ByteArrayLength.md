# RPGRewriter：重命名后游戏弹窗 `playerItem_deserialize ... 0x4a, 0x120, 0x20` 并闪退的定位与修复

## 背景

在“文件名重写流程可跑完”之后，游戏启动时出现弹窗并闪退（原版未重命名时无此问题）：

- `playerItem_deserialize`
- `1410(0x582), 74(0x4a), 288(0x120), 32(0x20)`

这说明运行时反序列化遇到了“期望长度 288、实际读到 32”的结构不一致。

---

## 结论（TL;DR）

根因是程序内部对“字节数组长度”的读写协议不一致：

- `readByteArray()` 用的是 **multibyte 长度**（可表示 >127）
- `writeByteArray()` 之前却写成了 **单字节长度**

当数据库里出现长度 `288 (0x120)` 的字节数组（本例就是 `0x49/0x4A` 这一组）时：

- 旧写法会把长度写成 `0x20`（32）
- 导致序列化结构从 `288` 被截断成 `32`
- 触发游戏侧 `playerItem_deserialize` 报错并崩溃

这不是“资源找不到”问题，而是**数据库二进制结构被写坏**。

---

## 1) 二进制证据

用最小映射强制触发 `RPG_RT.ldb` 写回后，修复前后同一位置对比：

## 修复前（错误）

`... 49 02 82 20 4A 20 ...`

- `0x49` 对应值为 `0x120`（288）
- `0x4A` 长度却变成 `0x20`（32）

与弹窗 `0x4a / 0x120 / 0x20` 完全一致。

## 修复后（正确）

`... 49 02 82 20 4A 82 20 ...`

- `0x4A` 长度同样为 multibyte `0x120`（288）
- 结构恢复一致

统计验证：

- 修复后 `49 02 82 20 4A` 组合中，`bad_pairs(4A 20)=0`
- `good_pairs(4A 82 20)=7`

---

## 2) 根因代码与修复

修改文件：`RPGRewriter.cs`

### 根因

- `readByteArray(FileStream f)`：`int length = readMultibyte(f);`
- `writeByteArray(int[] bytes)`（旧）：`writeByte(bytes.Length);`

读写不对称。

### 修复

将 `writeByteArray` 改为与读取协议一致：

- 写长度：`writeMultibyte(bytes.Length)`
- 空数组安全处理（`null -> length 0`）
- 按 `int` 写每个字节值

即：

- 修复前：单字节长度写回（会截断）
- 修复后：multibyte 长度写回（与读取一致）

---

## 3) 回归验证

1. 强制改写数据库（确保 `RPG_RT.ldb` 被实际写回）  
   结果：二进制已从 `4A 20` 修复为 `4A 82 20`

2. `RPG_RT.ldb -extract -single` 回归  
   结果：`RPG_RT.ldb extracted and written to log.txt.`，无 `error.log`

3. `-rewrite -all`（含 input）流程可完整结束  
   结果：`All files rewritten.`

4. 按当前实战流程复测（先恢复备份再重写）  
   - 备份恢复：`GameFIle_Backup -> GameFIle`（镜像）  
   - 重写命令：  
     `RPGRewriter.exe C:\Users\旻\Downloads\DWVer01_00\GameFIle\RPG_RT.lmt -rewrite -all -log null`  
   结果：完整输出到 `All files rewritten.`，且 `GameFIle\error.log` 不存在。  
   额外二进制核查：`good_pairs=7`、`bad_pairs=0`（未再出现 `4A 20` 截断对）。

---

## 4) 对当前崩溃现场的处理建议

如果你本地是在“旧缺陷版本”上已经重写过一次，建议：

1. 先从 `GameFIle_Backup` 全量恢复  
2. 用本次修复后的 `RPGRewriter.exe` 重新执行重写  
3. 再测试启动

否则旧的损坏 `RPG_RT.ldb` 仍会触发同样崩溃。
