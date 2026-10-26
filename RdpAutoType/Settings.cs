using System;
using System.Windows.Forms;

using KeePass.Plugins;

namespace RdpAutoType
{
    /// <summary>
    /// Setarile pluginului, citite/salvate in configul KeePass (CustomConfig).
    /// Toate delay-urile sunt in milisecunde.
    /// </summary>
    internal sealed class Settings
    {
        /// <summary>Actiuni posibile pentru single/double click.</summary>
        public const int ActDoNothing = 0;
        public const int ActLaunchPopup = 1;
        public const int ActRunMostRecent = 2;
        public const int ActRunAssociated = 3;

        // Chei in configul KeePass (prefix ca sa nu se bata cu alte plugin-uri).
        private const string KeyPerChar = "RdpAutoType.PerCharDelayMs";
        private const string KeyField = "RdpAutoType.FieldDelayMs";
        private const string KeyKeyPress = "RdpAutoType.KeyPressDelayMs";
        private const string KeyFinalEnter = "RdpAutoType.SendFinalEnter";

        // Activator (buton + modificatori + actiuni single/double).
        private const string KeyMouseButton = "RdpAutoType.MouseButton"; // 0=L,1=M,2=R
        private const string KeyMouseCtrl = "RdpAutoType.MouseCtrl";
        private const string KeyMouseShift = "RdpAutoType.MouseShift";
        private const string KeyMouseAlt = "RdpAutoType.MouseAlt";
        private const string KeySingleAction = "RdpAutoType.MouseSingleAction";
        private const string KeyDoubleAction = "RdpAutoType.MouseDoubleAction";

        // Components (ce apare in meniu).
        private const string KeyShowRecent = "RdpAutoType.ShowRecent";
        private const string KeyRecentCount = "RdpAutoType.RecentCount";
        private const string KeyShowFullPath = "RdpAutoType.ShowFullPath";

        // Shortcut pentru "Edit Login Script" (in KeePass).
        private const string KeyEditShortcut = "RdpAutoType.EditShortcut";
        private const string KeyEditShortcutEnabled = "RdpAutoType.EditShortcutEnabled";

        // Tema meniului popup: 0=Auto, 1=Light, 2=Dark.
        private const string KeyMenuTheme = "RdpAutoType.MenuTheme";

        // ---------------- Typing ----------------

        /// <summary>Pauza intre caractere.</summary>
        public int PerCharDelayMs = 20;

        /// <summary>Pauza dupa TAB (trecerea intre campuri).</summary>
        public int FieldDelayMs = 60;

        /// <summary>Pauza intre apasarea (down) si eliberarea (up) fiecarei taste.
        /// 0 = fara pauza; pe RDP imbricat/link lent mareste la 5-15.</summary>
        public int KeyPressDelayMs = 0;

        /// <summary>Trimite Enter dupa parola (submit automat).</summary>
        public bool SendFinalEnter = false;

        // ------------- Activation key/button -------------

        /// <summary>Butonul activator: 0=stanga, 1=mijloc, 2=dreapta.</summary>
        public int MouseButton = 1; // mijloc, implicit

        public bool MouseCtrl = false;
        public bool MouseShift = false;
        public bool MouseAlt = false;

        /// <summary>Actiunea la single click (vezi Act*).</summary>
        public int MouseSingleAction = ActLaunchPopup;

        /// <summary>Actiunea la double click (vezi Act*).</summary>
        public int MouseDoubleAction = ActDoNothing;

        /// <summary>Hook-ul de mouse e necesar daca single sau double fac ceva.</summary>
        public bool MouseEnabled
        {
            get { return MouseSingleAction != ActDoNothing || MouseDoubleAction != ActDoNothing; }
        }

        // ---------------- Components ----------------

        /// <summary>Afiseaza sectiunea "Recent" in meniu.</summary>
        public bool ShowRecent = true;

        /// <summary>Cate intrari recente sa afiseze.</summary>
        public int RecentCount = 8;

        /// <summary>Arata calea grupului langa intrari.</summary>
        public bool ShowFullPath = false;

        /// <summary>Tema meniului popup: 0=Auto, 1=Light, 2=Dark.</summary>
        public int MenuTheme = 0;

        // ---------------- Shortcut ----------------

        /// <summary>Scurtatura pentru Edit Login Script, activa in KeePass.</summary>
        public bool EditShortcutEnabled = true;

        /// <summary>Combinatia ca (int)Keys; implicit Ctrl+Enter.</summary>
        public int EditShortcut = (int)(Keys.Control | Keys.Enter);

        // ---------------- Load / Save ----------------

        public static Settings Load(IPluginHost host)
        {
            Settings s = new Settings();
            if (host == null || host.CustomConfig == null) return s;

            var cfg = host.CustomConfig;
            s.PerCharDelayMs = ParseInt(cfg.GetString(KeyPerChar, null), s.PerCharDelayMs);
            s.FieldDelayMs = ParseInt(cfg.GetString(KeyField, null), s.FieldDelayMs);
            s.KeyPressDelayMs = ParseInt(cfg.GetString(KeyKeyPress, null), s.KeyPressDelayMs);
            s.SendFinalEnter = ParseBool(cfg.GetString(KeyFinalEnter, null), s.SendFinalEnter);

            s.MouseButton = ParseInt(cfg.GetString(KeyMouseButton, null), s.MouseButton);
            s.MouseCtrl = ParseBool(cfg.GetString(KeyMouseCtrl, null), s.MouseCtrl);
            s.MouseShift = ParseBool(cfg.GetString(KeyMouseShift, null), s.MouseShift);
            s.MouseAlt = ParseBool(cfg.GetString(KeyMouseAlt, null), s.MouseAlt);
            s.MouseSingleAction = ParseInt(cfg.GetString(KeySingleAction, null), s.MouseSingleAction);
            s.MouseDoubleAction = ParseInt(cfg.GetString(KeyDoubleAction, null), s.MouseDoubleAction);

            s.ShowRecent = ParseBool(cfg.GetString(KeyShowRecent, null), s.ShowRecent);
            s.RecentCount = ParseInt(cfg.GetString(KeyRecentCount, null), s.RecentCount);
            s.ShowFullPath = ParseBool(cfg.GetString(KeyShowFullPath, null), s.ShowFullPath);
            s.MenuTheme = ParseInt(cfg.GetString(KeyMenuTheme, null), s.MenuTheme);

            s.EditShortcutEnabled = ParseBool(cfg.GetString(KeyEditShortcutEnabled, null), s.EditShortcutEnabled);
            s.EditShortcut = ParseInt(cfg.GetString(KeyEditShortcut, null), s.EditShortcut);
            return s;
        }

        public void Save(IPluginHost host)
        {
            if (host == null || host.CustomConfig == null) return;
            var cfg = host.CustomConfig;
            cfg.SetString(KeyPerChar, Int(PerCharDelayMs));
            cfg.SetString(KeyField, Int(FieldDelayMs));
            cfg.SetString(KeyKeyPress, Int(KeyPressDelayMs));
            cfg.SetString(KeyFinalEnter, Bool(SendFinalEnter));

            cfg.SetString(KeyMouseButton, Int(MouseButton));
            cfg.SetString(KeyMouseCtrl, Bool(MouseCtrl));
            cfg.SetString(KeyMouseShift, Bool(MouseShift));
            cfg.SetString(KeyMouseAlt, Bool(MouseAlt));
            cfg.SetString(KeySingleAction, Int(MouseSingleAction));
            cfg.SetString(KeyDoubleAction, Int(MouseDoubleAction));

            cfg.SetString(KeyShowRecent, Bool(ShowRecent));
            cfg.SetString(KeyRecentCount, Int(RecentCount));
            cfg.SetString(KeyShowFullPath, Bool(ShowFullPath));
            cfg.SetString(KeyMenuTheme, Int(MenuTheme));

            cfg.SetString(KeyEditShortcutEnabled, Bool(EditShortcutEnabled));
            cfg.SetString(KeyEditShortcut, Int(EditShortcut));
        }

        /// <summary>Reset la activatorul implicit: mijloc, fara modificatori,
        /// single = Launch popup menu.</summary>
        public void ResetActivation()
        {
            MouseButton = 1;
            MouseCtrl = false;
            MouseShift = false;
            MouseAlt = false;
            MouseSingleAction = ActLaunchPopup;
            MouseDoubleAction = ActDoNothing;
        }

        // ---------------- helpers ----------------

        private static string Int(int v)
        {
            return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static string Bool(bool v)
        {
            return v ? "true" : "false";
        }

        private static int ParseInt(string s, int def)
        {
            int v;
            if (!string.IsNullOrEmpty(s) &&
                int.TryParse(s, System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture, out v))
                return v;
            return def;
        }

        private static bool ParseBool(string s, bool def)
        {
            if (string.IsNullOrEmpty(s)) return def;
            return s == "true" || s == "True" || s == "1";
        }

        public static int Clamp(int v, int min, int max)
        {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }
    }
}
