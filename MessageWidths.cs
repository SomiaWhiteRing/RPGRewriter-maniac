using System;
using System.Collections.Generic;
using System.IO;

namespace RPGRewriter
{
    sealed class MessageWidths
    {
        readonly Dictionary<int, Width> advances = new Dictionary<int, Width>();
        readonly Dictionary<string, string> pages = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public char InheritedSlot { get; private set; }

        struct Width
        {
            public readonly int Gothic, Mincho;
            public bool IsZero { get { return Gothic == 0 && Mincho == 0; } }
            public Width(int gothic, int mincho) { Gothic = gothic; Mincho = mincho; }
        }

        public string PageSlots(string source, int eventId, int page)
        {
            string slots;
            return pages.TryGetValue(source + " " + eventId + " " + page, out slots) ? slots : "";
        }
        readonly char escape;

        public MessageWidths(string path, int encoding)
        {
            InheritedSlot = '?';
            escape = encoding == 932 ? '¥' : encoding == 949 ? '₩' : '\\';
            using (var file = File.OpenText(path))
            {
                if (file.ReadLine() != "KAI-WIDTHS-1 " + encoding)
                    throw new InvalidDataException("Invalid Kai width table or output encoding.");
                string line;
                while ((line = file.ReadLine()) != null)
                {
                    var fields = line.Split(' ');
                    if (fields.Length == 2 && fields[0] == "FONT" && fields[1].Length == 1 && "01?".Contains(fields[1]))
                    {
                        InheritedSlot = fields[1][0];
                        continue;
                    }
                    if (fields.Length == 4 && fields[3].Trim('0', '1', '?').Length == 0)
                    {
                        pages.Add(fields[0] + " " + fields[1] + " " + fields[2], fields[3]);
                        continue;
                    }
                    if (fields.Length != 3) throw new InvalidDataException("Invalid Kai width row.");
                    int code = int.Parse(fields[0]);
                    int gothic = int.Parse(fields[1]);
                    int mincho = int.Parse(fields[2]);
                    if (code < 0 || code > 0x10ffff || gothic < 0 || mincho < 0)
                        throw new InvalidDataException("Invalid Kai glyph metrics.");
                    advances.Add(code, new Width(gothic, mincho));
                }
            }
        }

        Width Advance(int code)
        {
            Width width;
            return advances.TryGetValue(code, out width) ? width : new Width(12, 12);
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
            if (pos < line.Length && line[pos] == '[' && "csvnwetf".IndexOf(c) >= 0)
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
            if (c == '_')
            {
                Width space = Advance(' ');
                return new Unit(start, new Width(space.Gothic / 2, space.Mincho / 2));
            }
            if (c == '\\' || c == escape) return new Unit(start, Advance(c));
            return new Unit(start, 0);
        }

        struct Unit
        {
            public readonly int Offset;
            public readonly Width Advance;
            public Unit(int offset, Width advance) { Offset = offset; Advance = advance; }
            public Unit(int offset, int width) : this(offset, new Width(width, width)) { }
        }

        static bool IsWordCharacter(char c)
        {
            return (c < 0x3000 && char.IsLetterOrDigit(c)) || c == '_' || c == '\'' || c == '’' || c == '-';
        }

        public string Wrap(string text, int limit, char slot)
        {
            var output = new List<string>();
            foreach (string line in text.Split('\n'))
            {
                var units = new List<Unit>();
                int position = 0;
                while (position < line.Length)
                {
                    Unit unit = ReadUnit(line, ref position);
                    if (slot == '0') unit = new Unit(unit.Offset, unit.Advance.Gothic);
                    if (slot == '1') unit = new Unit(unit.Offset, unit.Advance.Mincho);
                    units.Add(unit);
                }

                int start = 0;
                while (start < units.Count)
                {
                    long gothic = 0, mincho = 0;
                    int end = start, preferred = -1, boundary = -1;
                    for (; end < units.Count; ++end)
                    {
                        int pos = units[end].Offset;
                        char next = line[pos];
                        if ((gothic > 0 || mincho > 0) && end > start)
                        {
                            char prev = line[pos - 1];
                            bool canBreak = !(IsWordCharacter(prev) && IsWordCharacter(next))
                                && "，。！？、；：」』）,.!?;:)".IndexOf(next) < 0
                                && "「『（(".IndexOf(prev) < 0 && !units[end - 1].Advance.IsZero;
                            if (canBreak)
                            {
                                boundary = end;
                                if (" ，。！？；：、,.!?;:」』）)".IndexOf(prev) >= 0)
                                    preferred = end;
                            }
                        }
                        Width advance = units[end].Advance;
                        long nextGothic = gothic + advance.Gothic;
                        long nextMincho = mincho + advance.Mincho;
                        if ((gothic > 0 || mincho > 0) && !advance.IsZero
                            && Math.Max(nextGothic, nextMincho) > limit) break;
                        gothic = nextGothic;
                        mincho = nextMincho;
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
