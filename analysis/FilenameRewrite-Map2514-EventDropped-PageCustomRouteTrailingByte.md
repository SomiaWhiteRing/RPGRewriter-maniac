# RPGRewriter：`Map2514.lmu` 在文件名重写后出现 `Unknown event` / 事件不可见 的定位与修复报告

## 背景

用户复现命令：

`RPGRewriter.exe D:\gamebugfx\もしもコレクション7 - 副本\RPG_RT.lmt -rewrite -all -log null`

现象：

1. 重写后游玩 `Map2514.lmu` 会出现大量 `Unknown event` 类错误。
2. 其中一个事件会完全无法显示。
3. 对照原工程 `D:\gamebugfx\もしもコレクション7`，同一地图在重写前可正常运行。

这说明问题不是“原地图本身坏了”，而是 **RPGRewriter 在读取/写回该地图时丢失了事件结构**。

---

## 结论（TL;DR）

根因不是“文件名替换把字符串改长了”，而是 **旧版地图解析器在读取原始 `Map2514.lmu` 时，就已经在 Event #2 / Page #3 的 `PageCustomRoute` 结构里少读了 1 个尾随原始字节**。

具体表现是：

- `0x0C` 自定义移动路线 payload 声明的长度里，除了可识别的 move route 数据外，还多带了尾随原始字节；
- 旧逻辑只调用 `MoveRoute(f, moveLength, "Custom")`，没有保留剩余字节；
- 读取停在 `0x1837` 后，下一字节是 `0x1B`，旧逻辑却把它当成“应该读到的 `0x00` 终止符”，于是报 `Byte check failed`；
- 该事件随后进入恢复/跳过路径，没有被加入内存中的事件列表；
- 一旦执行 `-rewrite -all`，程序就会把“缺了 Event #2 的地图”写回，于是游戏里出现 `Unknown event` 和事件不可见。

修复方式是对该区域做 **byte-perfect roundtrip**：

1. 保留 `PageCustomRoute 0x0C` payload 内未被现有 `MoveRoute` 解析器消费的尾随原始字节；
2. 写回时把这些尾随字节拼回去，并把长度一起写对；
3. 额外保留页面级 `0x1B` 扩展 chunk 的原始数据；
4. 如果地图加载过程中确实发生过事件恢复/跳过，则拒绝保存，避免静默写坏地图。

---

## 1) 先证实：原始地图在“重写前”就已被旧解析器漏读

这一步非常关键，因为它直接决定问题性质。

我先用仓库根目录里的旧版 `RPGRewriter.exe` 去提取 **未重写的原始文件**：

```powershell
.\RPGRewriter.exe "D:\gamebugfx\もしもコレクション7\Map2514.lmu" `
  -extract -single -actions true -messages true -log before_map2514_oldcheck
```

旧版输出直接报出：

- `Warning! Byte check failed at 6199 (0x1837) (read 27 (0x1B), expected 0 (0x00)).`
- `Context: File=Map2514.lmu, Event=Event 2 (8,26) #2, Page 3, Line=Line 1.`

而导出的 `before_map2514_oldcheck.txt` 中：

- **没有** `***** Event #2 (8,26) - 弟子五郎 *****`
- 事件编号直接从 `Event #1` 跳到 `Event #3`

这说明：

- 原始地图文件本身并没有坏到不能运行；
- 真正坏的是 **RPGRewriter 旧版对这张地图的解析结果**；
- 后续的重写只是把“解析时已经丢失的事件”持久化了。

---

## 2) 修复后验证：原始地图已能完整提取出 Event #2

使用修复后的 `bin\Release\RPGRewriter.exe` 再提取同一个原始文件：

```powershell
.\bin\Release\RPGRewriter.exe "D:\gamebugfx\もしもコレクション7\Map2514.lmu" `
  -extract -single -actions true -messages true -log map2514_extract_verify
```

验证结果：

1. 不再出现 `Warning! Byte check failed`
2. `map2514_extract_verify.txt` 中明确出现：
   - `***** Event #2 (8,26) - 弟子五郎 *****`

这证明修复后的读取链路已经能完整保留原始 `Map2514.lmu` 的事件结构。

---

## 3) 二进制定位：问题落在 `PageCustomRoute` 的 `0x0C` payload

结合旧版报错位置与代码跟踪，问题被定位到：

- `Map2514`
- `Event #2 (8,26)`
- `Page #3`
- `PageCustomRoute`
- chunk `0x0C`

旧逻辑是：

1. 读取 `0x0C` 的 `moveLength`
2. 调用 `new MoveRoute(f, moveLength, "Custom")`
3. 紧接着继续按正常结构读取后续字段，并最终期待 `0x00` 终止符

但在这张图里，`0x0C` 声明的 payload 实际上包含两部分：

1. 现有 `MoveRoute` 解析器能识别的移动路线字节
2. **额外的尾随原始字节**

旧版 `MoveRoute` 读完第 1 部分后就停下了，文件指针停在 `6199 (0x1837)`，此时剩余的下一个字节正是：

- `0x1B`

旧代码没有意识到“这仍然属于 `0x0C` payload 的剩余内容”，而是把它误当成“本段已经结束、现在应该读到 `0x00` terminator”，于是触发：

- `read 27 (0x1B), expected 0 (0x00)`

随后事件进入恢复逻辑，被跳过，不再加入 `events` 列表。  
之后一旦写回，地图就永久少了这个事件。

---

## 4) 代码修复点

修改文件：

- `Page.cs`
- `Map.cs`

### 4.1 `PageCustomRoute 0x0C` 尾随字节保留

`Page.cs` 中新增了对 `0x0C` payload 剩余字节的保留逻辑：

- 读取时记录 `routeEnd = f.Position + moveLength`
- 先按现有逻辑解析 `MoveRoute`
- 如果 `MoveRoute` 没有吃完整个 payload，则把剩余字节保存到 `moveRouteTrailingBytes`
- 写回时：
  - `moveLength = moveRoute.getLength() + trailingByteCount`
  - 先写回 `moveRoute`
  - 再原样附加 `moveRouteTrailingBytes`

对应位置：

- `Page.cs:380`
- `Page.cs:408-414`
- `Page.cs:450-483`

这一步是本次 `Map2514` 修复的核心。

### 4.2 页面级 `0x1B` 扩展 chunk 原样保留

同样在 `Page.cs` 中，新增了页面顶层 `0x1B` chunk 的原始数据保留：

- 读取：`extensionChunk1B = M.skipLengthBytes(f);`
- 写回：按原长度和原字节数组写回

对应位置：

- `Page.cs:17`
- `Page.cs:73`
- `Page.cs:208-211`

这不是触发 `Map2514` 事件丢失的唯一根因，但属于同一区域的扩展数据兼容补强，能避免后续页面结构 roundtrip 再丢字节。

### 4.3 地图保存安全护栏

`Map.cs` 中新增 `hadEventRecovery`：

- 只要事件加载过程中发生过恢复、跳过、越界保护等情况，就记为 `true`
- 写文件前若 `hadEventRecovery == true`，直接拒绝保存：
  - `... refusing to save to avoid corrupting the map.`

对应位置：

- `Map.cs:46`
- `Map.cs:184`
- `Map.cs:240`
- `Map.cs:451-453`

这能避免以后再出现“明明解析已经告警，但程序还是继续把残缺地图写回”的静默破坏。

---

## 5) 回归验证

## 5.1 原始地图提取回归

命令：

```powershell
.\bin\Release\RPGRewriter.exe "D:\gamebugfx\もしもコレクション7\Map2514.lmu" `
  -extract -single -actions true -messages true -log map2514_extract_verify
```

结果：

- 无 `Byte check failed`
- `map2514_extract_verify.txt` 含 `***** Event #2 (8,26) - 弟子五郎 *****`

## 5.2 整项目 `-rewrite -all` 回归

先从原始工程镜像出一份全新的临时目录：

- `D:\gamebugfx\tmp_map2514_fix_verify_all3`

测试映射文件：

```txt
***PANORAMA
異空間
VERIFY_PANO_2514
```

执行：

```powershell
.\bin\Release\RPGRewriter.exe "D:\gamebugfx\tmp_map2514_fix_verify_all3\RPG_RT.lmt" `
  -rewrite -all -input verify_input_2514 -log null
```

结果：

1. 整体流程结束于 `All files rewritten.`
2. `D:\gamebugfx\tmp_map2514_fix_verify_all3\Panorama\VERIFY_PANO_2514.png` 存在，说明资源重命名生效
3. 再提取重写后的 `Map2514.lmu`：
   - 出现 `Parallax: VERIFY_PANO_2514, Horizontal Loop (3), Vertical Loop (3)`
   - 仍然包含 `***** Event #2 (8,26) - 弟子五郎 *****`
   - 无 `Warning! Byte check failed`

这说明修复后，`-rewrite -all` 已不会再把 `Map2514` 的 `Event #2` 丢掉。

---

## 6) 额外说明

在本次整项目回归中，控制台仍会出现一条独立信息：

- `Rewriting database...`
- `Aborting due to error.`

但后续地图重写仍继续执行，并最终输出 `All files rewritten.`。  
从本次验证结果看，这条数据库阶段的报错 **不是 `Map2514` 事件损坏问题的根因**，需要单独立案排查。

---

## 最终判定

本问题属于 **地图事件解析/写回兼容性缺陷**，不是简单的“文件名重写替换失败”。

更准确地说：

- 旧版程序在读取原始 `Map2514.lmu` 时，就会因为 `PageCustomRoute 0x0C` payload 内的尾随原始字节而漏掉 `Event #2`
- 后续 `-rewrite -all` 只是把这个漏读结果写回磁盘，导致游戏运行时报大量 `Unknown event`

修复后已确认：

1. 原始 `Map2514.lmu` 可被完整提取
2. `Event #2 (8,26) - 弟子五郎` 不再丢失
3. 整项目 `-rewrite -all` 后，`Map2514.lmu` 仍保持正常结构
