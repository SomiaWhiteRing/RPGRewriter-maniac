编译
dotnet build RPGRewriter.csproj
运行
.\bin\Debug\RPGRewriter.exe

消息导入可传入 `-widthtable <文件>`。使用 EasyRPG Kai 的 `--measure-widths <目标编码>` 输出作为表，在默认 320×240 消息框内按字形前进宽度断行（有头像 232 px、无头像 304 px）。导入沿用现有换行和四行分页规则。事件可继承不同字体，因此按 Gothic 与 Mincho 中较宽的字形排版。动态人名和变量分别预估六个全角字、六位数字。省略参数时沿用原版导入行为。
