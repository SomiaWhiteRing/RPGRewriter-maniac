using System;
using System.Collections.Generic;
using System.IO;

namespace RPGRewriter
{
    // Default 12px Kai bitmap fonts. Use the larger advance when an event can
    // inherit either font from another page. Unlisted scalars are full-width.
    sealed class MessageWidths
    {
        readonly Dictionary<int, int> advances = new Dictionary<int, int>();
        readonly char escape;

        public MessageWidths(string path, int encoding)
        {
            escape = encoding == 932 ? '¥' : encoding == 949 ? '₩' : '\\';
            using (var file = File.OpenText(path))
            {
                if (file.ReadLine() != "KAI-WIDTHS-1 " + encoding)
                    throw new InvalidDataException("Invalid Kai width table or output encoding.");
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    var fields = line.Split(' ');
                    if (fields.Length != 3) throw new InvalidDataException("Invalid Kai width row.");
                    int code = int.Parse(fields[0]);
                    int gothic = int.Parse(fields[1]);
                    int mincho = int.Parse(fields[2]);
                    if (code < 0 || code > 0x10ffff || gothic < 0 || gothic > 12 || mincho < 0 || mincho > 12)
                        throw new InvalidDataException("Invalid Kai glyph metrics.");
                    advances.Add(code, Math.Max(gothic, mincho));
                }
            }
        }

        int Advance(int code)
        {
            int width;
            return advances.TryGetValue(code, out width) ? width : 12;
        }

        Unit ReadUnit(string line, ref int pos)
        {
            int start = pos;
            int cp = char.ConvertToUtf32(line, pos);
            pos += cp > 0xffff ? 2 : 1;
            if (cp == '$' && pos < line.Length &&
                ((line[pos] >= 'A' && line[pos] <= 'Z') || (line[pos] >= 'a' && line[pos] <= 'z')))
            {
                ++pos;
                return new Unit(start, 12);
            }
            if ((cp != '\\' && cp != escape) || pos == line.Length)
                return new Unit(start, Advance(cp));

            int command = char.ConvertToUtf32(line, pos);
            pos += command > 0xffff ? 2 : 1;
            if (command > 127 && command != escape) return new Unit(start, 0);
            char c = char.ToLowerInvariant((char)command);
            if (pos < line.Length && line[pos] == '[' && "csvnwef".IndexOf(c) >= 0)
            {
                int depth = 1, end = pos + 1;
                while (end < line.Length && depth > 0)
                {
                    char next = line[end++];
                    if (next == '[') ++depth;
                    if (next == ']') --depth;
                }
                if (depth == 0) pos = end;
            }
            switch (c)
            {
                case 'n': return new Unit(start, 72); // Estimate a runtime actor name at six Han glyphs.
                case 'v': return new Unit(start, 36); // Estimate a runtime variable at six digits.
                case '_': return new Unit(start, Advance(' ') / 2);
                default: return new Unit(start, c == '\\' || c == escape ? Advance(c) : 0);
            }
        }

        struct Unit
        {
            public readonly int Offset;
            public readonly int Width;

            public Unit(int offset, int width)
            {
                Offset = offset;
                Width = width;
            }
        }

        static bool IsWordCharacter(char c)
        {
            return (c < 0x3000 && char.IsLetterOrDigit(c)) || c == '_' || c == '\'' || c == '’' || c == '-';
        }

        public string Wrap(string text, int limit)
        {
            var output = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                var units = new List<Unit>();
                int position = 0;
                while (position < line.Length)
                    units.Add(ReadUnit(line, ref position));

                int start = 0;
                while (start < units.Count)
                {
                    int width = 0, end = start, preferred = -1, boundary = -1;
                    for (; end < units.Count; ++end)
                    {
                        int pos = units[end].Offset;
                        char next = line[pos];
                        if (width > 0 && end > start)
                        {
                            char prev = line[pos - 1];
                            bool canBreak = !(IsWordCharacter(prev) && IsWordCharacter(next))
                                && "，。！？、；：」』）,.!?;:)".IndexOf(next) < 0
                                && "「『（(".IndexOf(prev) < 0 && units[end - 1].Width != 0;
                            if (canBreak)
                            {
                                boundary = end;
                                if (" ，。！？；：、,.!?;:」』）)".IndexOf(prev) >= 0)
                                    preferred = end;
                            }
                        }
                        if (width + units[end].Width > limit) break;
                        width += units[end].Width;
                    }
                    if (end == units.Count) break;
                    int cut = preferred > start ? preferred : boundary > start ? boundary : end;
                    output.Add(line.Substring(units[start].Offset, units[cut].Offset - units[start].Offset));
                    start = cut;
                }
                output.Add(start < units.Count ? line.Substring(units[start].Offset) : "");
            }
            return string.Join("\n", output);
        }
    }
}
