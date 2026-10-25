using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace RdpAutoType
{
    /// <summary>Tipul unui element din Login Script.</summary>
    internal enum SeqKind { Text, Field, Key, Delay }

    /// <summary>
    /// Un element dintr-o secventa de autotype (Login Script).
    ///   Text  - text literal de tastat
    ///   Field - placeholder de camp (USERNAME / PASSWORD / URL / TITLE / NOTES
    ///           sau "S:NumeCustom") -> se rezolva la valoarea intrarii
    ///   Key   - o tasta (speciala sau caracter) cu modificatori optionali
    ///   Delay - pauza in milisecunde
    /// </summary>
    internal sealed class SeqElement
    {
        public SeqKind Kind;

        // Text
        public string Text = "";

        // Field: token-ul exact din acolade, fara acolade (ex. "USERNAME", "S:Domain")
        public string FieldToken = "";

        // Key
        public string SpecialKey = "";   // ex. "TAB", "ENTER", "F2"; gol daca e caracter
        public char KeyChar = '\0';      // folosit cand SpecialKey e gol
        public bool Ctrl, Shift, Alt;
        public int Count = 1;

        // Delay
        public int DelayMs = 0;

        // --- constructori ---

        public static SeqElement NewText(string t)
        {
            return new SeqElement { Kind = SeqKind.Text, Text = t ?? "" };
        }

        public static SeqElement NewField(string token)
        {
            return new SeqElement { Kind = SeqKind.Field, FieldToken = token ?? "" };
        }

        public static SeqElement NewSpecial(string name, bool ctrl, bool shift, bool alt, int count)
        {
            return new SeqElement
            {
                Kind = SeqKind.Key,
                SpecialKey = name ?? "",
                Ctrl = ctrl,
                Shift = shift,
                Alt = alt,
                Count = (count < 1) ? 1 : count
            };
        }

        public static SeqElement NewChar(char c, bool ctrl, bool shift, bool alt)
        {
            return new SeqElement
            {
                Kind = SeqKind.Key,
                KeyChar = c,
                Ctrl = ctrl,
                Shift = shift,
                Alt = alt,
                Count = 1
            };
        }

        public static SeqElement NewDelay(int ms)
        {
            return new SeqElement { Kind = SeqKind.Delay, DelayMs = (ms < 0) ? 0 : ms };
        }

        // --- etichete pentru UI ---

        /// <summary>Litera din patratelul din stanga (K / F).</summary>
        public string Badge
        {
            get
            {
                switch (Kind)
                {
                    case SeqKind.Field: return "F";
                    case SeqKind.Key: return "K";
                    case SeqKind.Delay: return "D";
                    default: return "T";
                }
            }
        }

        /// <summary>Textul afisat in lista.</summary>
        public string Display
        {
            get
            {
                switch (Kind)
                {
                    case SeqKind.Text:
                        return "\"" + Text + "\"";
                    case SeqKind.Field:
                        return FieldLabel(FieldToken);
                    case SeqKind.Delay:
                        return "Delay " + DelayMs + " ms";
                    case SeqKind.Key:
                        string mods = "";
                        if (Ctrl) mods += "Ctrl + ";
                        if (Shift) mods += "Shift + ";
                        if (Alt) mods += "Alt + ";
                        string basek = (SpecialKey.Length > 0)
                            ? SeqCodec.PrettyKey(SpecialKey)
                            : KeyChar.ToString().ToUpper(CultureInfo.InvariantCulture);
                        string rep = (Count > 1) ? " x" + Count : "";
                        return mods + basek + rep;
                }
                return "";
            }
        }

        private static string FieldLabel(string token)
        {
            if (string.IsNullOrEmpty(token)) return "(field)";
            string up = token.ToUpperInvariant();
            if (up == "USERNAME") return "Username";
            if (up == "PASSWORD") return "Password";
            if (up == "URL") return "URL";
            if (up == "TITLE") return "Title";
            if (up == "NOTES") return "Notes";
            if (up.StartsWith("S:")) return token.Substring(2);
            return token;
        }
    }
}
