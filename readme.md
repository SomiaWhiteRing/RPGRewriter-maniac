编译
dotnet build RPGRewriter.csproj
运行
.\bin\Debug\RPGRewriter.exe

消息导入可传入 `-widthtable <文件>`。使用 EasyRPG Kai 的 `--measure-widths <目标编码> --project-path <游戏目录>` 测量译文用到的字符，在默认 320×240 消息框内按字形前进宽度断行（有头像 232 px、无头像 304 px）。导入沿用现有换行和四行分页规则。在字宽表末尾追加同一命令带 `--message-font-slots` 的输出，可按数据库或事件设置选择字体槽。字体槽不确定时，分别计算两种字体的整行宽度，取较宽值。动态控制码 `\N[...]`、`\V[...]`、`\T[...]` 连同嵌套参数整体计零；展开后的显示效果由人工校对。省略参数时沿用原版导入行为。
