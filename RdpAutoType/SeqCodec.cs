using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RdpAutoType
{
    /// <summary>
    /// Conversie intre lista de SeqElement si sintaxa nativa KeePass de autotype.
    /// Subset acoperit: text literal, {USERNAME}/{PASSWORD}/{URL}/{TITLE}/{NOTES},
    /// {S:Nume}, taste speciale ({TAB}, {ENTER}, ...), combo-uri cu modificatori
    /// (+ Shift, ^ Ctrl, % Alt), repetari ({TAB 3}) si {DELAY n}.
    /// </summary>
    internal static class SeqCodec
    {
        /// <summary>Taste speciale: nume canonic -> VK. Numele sunt cele folosite
        /// in acolade de KeePass.</summary>
        private static readonly Dictionary<string, byte> SpecialVk =
            new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase)
        {
            { "TAB", 0x09 }, { "ENTER", 0x0D }, { "SPACE", 0x20 }, { "ESC", 0x1B },
            { "BACKSPACE", 0x08 }, { "BS", 0x08 }, { "BKSP", 0x08 },
            { "DELETE", 0x2E }, { "DEL", 0x2E }, { "INSERT", 0x2D }, { "INS", 0x2D },
            { "HOME", 0x24 }, { "END", 0x23 }, { "PGUP", 0x21 }, { "PGDN", 0x22 },
            { "UP", 0x26 }, { "DOWN", 0x28 }, { "LEFT", 0x25 }, { "RIGHT", 0x27 },
            { "F1", 0x70 }, { "F2", 0x71 }, { "F3", 0x72 }, { "F4", 0x73 },
            { "F5", 0x74 }, { "F6", 0x75 }, { "F7", 0x76 }, { "F8", 0x77 },
            { "F9", 0x78 }, { "F10", 0x79 }, { "F11", 0x7A }, { "F12", 0x7B }
        };

        /// <summary>Lista de taste speciale pentru dropdown-ul din editor (ordine utila).</summary>
        public static readonly string[] SpecialKeyNames = new string[]
        {
            "TAB", "ENTER", "SPACE", "ESC", "BACKSPACE", "DELETE", "INSERT",
            "HOME", "END", "PGUP", "PGDN", "UP", "DOWN", "LEFT", "RIGHT",
            "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
        };

        public static bool IsSpecial(string name)
        {
            return !string.IsNullOrEmpty(name) && SpecialVk.ContainsKey(name);
        }

        public static byte VkFor(string specialName)
        {
            byte vk;
            return SpecialVk.TryGetValue(specialName ?? "", out vk) ? vk : (byte)0;
        }

        public static string PrettyKey(string specialName)
        {
            if (string.IsNullOrEmpty(specialName)) return specialName;
            string up = specialName.ToUpperInvariant();
            switch (up)
            {
                case "PGUP": return "Page Up";
                case "PGDN": return "Page Down";
                case "ESC": return "Esc";
                default:
                    // Title-case simplu (TAB -> Tab)
                    return char.ToUpperInvariant(up[0]) + up.Substring(1).ToLowerInvariant();
            }
        }

        // ---------------- Build (elemente -> string) ----------------

        public static string Build(List<SeqElement> elems)
        {
            if (elems == null) return "";
            StringBuilder sb = new StringBuilder();
            foreach (SeqElement e in elems)
            {
                switch (e.Kind)
                {
                    case SeqKind.Text:
                        sb.Append(EscapeText(e.Text));
                        break;
                    case SeqKind.Field:
                        sb.Append('{').Append(e.FieldToken).Append('}');
                        break;
                    case SeqKind.Delay:
                        sb.Append("{DELAY ").Append(
                            e.DelayMs.ToString(CultureInfo.InvariantCulture)).Append('}');
                        break;
                    case SeqKind.Key:
                        sb.Append(Mods(e));
                        if (e.SpecialKey.Length > 0)
                        {
                            sb.Append('{').Append(e.SpecialKey.ToUpperInvariant());
                            if (e.Count > 1)
                                sb.Append(' ').Append(e.Count.ToString(CultureInfo.InvariantCulture));
                            sb.Append('}');
                        }
                        else
                        {
                            int n = (e.Count < 1) ? 1 : e.Count;
                            for (int i = 0; i < n; i++)
                                sb.Append(EscapeChar(e.KeyChar));
                        }
                        break;
                }
            }
            return sb.ToString();
        }

        private static string Mods(SeqElement e)
        {
            string s = "";
            if (e.Shift) s += "+";
            if (e.Ctrl) s += "^";
            if (e.Alt) s += "%";
            return s;
        }

        private static string EscapeText(string t)
        {
            if (string.IsNullOrEmpty(t)) return "";
            StringBuilder sb = new StringBuilder();
            foreach (char c in t) sb.Append(EscapeChar(c));
            return sb.ToString();
        }

        private static string EscapeChar(char c)
        {
            switch (c)
            {
                case '+': return "{+}";
                case '^': return "{^}";
                case '%': return "{%}";
                case '~': return "{~}";
                case '(': return "{(}";
                case ')': return "{)}";
                case '[': return "{[}";
                case ']': return "{]}";
                case '{': return "{{}";
                case '}': return "{}}";
                default: return c.ToString();
            }
        }

        // ---------------- Parse (string -> elemente) ----------------

        public static List<SeqElement> Parse(string seq)
        {
            List<SeqElement> list = new List<SeqElement>();
            if (string.IsNullOrEmpty(seq)) return list;

            StringBuilder lit = new StringBuilder();
            bool pShift = false, pCtrl = false, pAlt = false;

            int i = 0;
            while (i < seq.Length)
            {
                char c = seq[i];

                if (c == '+' ) { FlushLit(list, lit); pShift = true; i++; continue; }
                if (c == '^' ) { FlushLit(list, lit); pCtrl = true; i++; continue; }
                if (c == '%' ) { FlushLit(list, lit); pAlt = true; i++; continue; }
                if (c == '~' )
                {
                    FlushLit(list, lit);
                    list.Add(SeqElement.NewSpecial("ENTER", pCtrl, pShift, pAlt, 1));
                    pShift = pCtrl = pAlt = false; i++; continue;
                }

                if (c == '{')
                {
                    int end = seq.IndexOf('}', i + 1);
                    if (end < 0) { lit.Append(c); i++; continue; }
                    string inside = seq.Substring(i + 1, end - i - 1);
                    i = end + 1;

                    HandleBrace(list, lit, inside, ref pShift, ref pCtrl, ref pAlt);
                    continue;
                }

                // caracter obisnuit
                if (pShift || pCtrl || pAlt)
                {
                    FlushLit(list, lit);
                    list.Add(SeqElement.NewChar(c, pCtrl, pShift, pAlt));
                    pShift = pCtrl = pAlt = false;
                }
                else
                {
                    lit.Append(c);
                }
                i++;
            }

            FlushLit(list, lit);
            return list;
        }

        private static void HandleBrace(List<SeqElement> list, StringBuilder lit,
            string inside, ref bool pShift, ref bool pCtrl, ref bool pAlt)
        {
            // caractere literale escape-uite: {+} {^} {%} {~} {(} ... {{} {}}
            if (inside.Length == 1 && "+^%~()[]{}".IndexOf(inside[0]) >= 0)
            {
                if (pShift || pCtrl || pAlt)
                {
                    FlushLit(list, lit);
                    list.Add(SeqElement.NewChar(inside[0], pCtrl, pShift, pAlt));
                    pShift = pCtrl = pAlt = false;
                }
                else lit.Append(inside[0]);
                return;
            }

            string token = inside.Trim();
            string upper = token.ToUpperInvariant();

            // DELAY
            if (upper.StartsWith("DELAY"))
            {
                FlushLit(list, lit);
                string rest = token.Substring(5).Trim();
                if (rest.StartsWith("=")) rest = rest.Substring(1).Trim();
                int ms;
                if (int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms))
                    list.Add(SeqElement.NewDelay(ms));
                pShift = pCtrl = pAlt = false;
                return;
            }

            // camp
            if (upper == "USERNAME" || upper == "PASSWORD" || upper == "URL" ||
                upper == "TITLE" || upper == "NOTES" || upper.StartsWith("S:"))
            {
                FlushLit(list, lit);
                list.Add(SeqElement.NewField(token));
                pShift = pCtrl = pAlt = false;
                return;
            }

            // tasta speciala, eventual cu repetare "{TAB 3}"
            string name = upper;
            int count = 1;
            int sp = upper.IndexOf(' ');
            if (sp > 0)
            {
                name = upper.Substring(0, sp);
                int.TryParse(upper.Substring(sp + 1).Trim(),
                    NumberStyles.Integer, CultureInfo.InvariantCulture, out count);
                if (count < 1) count = 1;
            }
            if (IsSpecial(name))
            {
                FlushLit(list, lit);
                list.Add(SeqElement.NewSpecial(name, pCtrl, pShift, pAlt, count));
                pShift = pCtrl = pAlt = false;
                return;
            }

            // token necunoscut -> pastreaza-l ca text (nu pierde nimic)
            lit.Append('{').Append(inside).Append('}');
        }

        private static void FlushLit(List<SeqElement> list, StringBuilder lit)
        {
            if (lit.Length > 0)
            {
                list.Add(SeqElement.NewText(lit.ToString()));
                lit.Length = 0;
            }
        }
    }
}
