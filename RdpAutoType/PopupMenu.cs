using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using KeePass.Plugins;
using KeePassLib;
using KeePassLib.Security;

namespace RdpAutoType
{
    /// <summary>
    /// Cursor popup menu:
    ///   level 1: "Recent" + root groups + Cancel
    ///   level 2+: subgroups (recursive) and each group's entries
    ///   entry level: submenu with actions (Login / Username / Password / URL)
    /// Entries and groups show their KeePass icons.
    /// </summary>
    internal sealed class PopupMenu
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder s, int max);

        private readonly IPluginHost m_host;
        private readonly Func<Settings> m_getSettings;

        public PopupMenu(IPluginHost host, Func<Settings> getSettings)
        {
            m_host = host;
            m_getSettings = getSettings;
        }

        public void ShowAtCursor(bool withDelay)
        {
            IntPtr target = GetForegroundWindow();

            PwDatabase db = (m_host != null) ? m_host.Database : null;
            if (db == null || !db.IsOpen)
            {
                ShowInfo("No database is open in KeePass.");
                return;
            }

            Settings st = GetSettings();
            ApplyMenuTheme(st);
            ContextMenuStrip menu = NewMenu();

            // 1. Recent.
            List<PwEntry> all = CollectEntries(db.RootGroup);
            if (all.Count == 0)
            {
                ShowInfo("The database has no entries.");
                return;
            }

            if (st.ShowRecent)
            {
                List<PwEntry> recentSorted = new List<PwEntry>(all);
                recentSorted.Sort(delegate(PwEntry a, PwEntry b)
                {
                    return b.LastAccessTime.CompareTo(a.LastAccessTime);
                });

                ToolStripMenuItem recent = new ToolStripMenuItem("Recent");
                int rc = 0;
                int max = (st.RecentCount > 0) ? st.RecentCount : 8;
                foreach (PwEntry pe in recentSorted)
                {
                    if (rc >= max) break;
                    recent.DropDownItems.Add(MakeEntryMenu(db, pe, target, withDelay));
                    rc++;
                }
                if (recent.DropDownItems.Count > 0)
                {
                    Style(recent.DropDown);
                    menu.Items.Add(recent);
                    menu.Items.Add(new ToolStripSeparator());
                }
            }

            // 2. Root groups (recursive, lazy).
            foreach (ToolStripItem it in BuildGroupItems(db, db.RootGroup, target, withDelay))
                menu.Items.Add(it);

            // 3. Cancel.
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem cancel = new ToolStripMenuItem("Cancel");
            cancel.Click += delegate { menu.Close(); };
            menu.Items.Add(cancel);

            menu.Show(Cursor.Position);
        }

        /// <summary>Tasteaza direct login-ul celei mai recente intrari (fara meniu).</summary>
        public void RunMostRecent(IntPtr target, bool withDelay)
        {
            PwDatabase db = (m_host != null) ? m_host.Database : null;
            if (db == null || !db.IsOpen) { ShowInfo("No database is open in KeePass."); return; }

            List<PwEntry> all = CollectEntries(db.RootGroup);
            if (all.Count == 0) { ShowInfo("The database has no entries."); return; }

            all.Sort(delegate(PwEntry a, PwEntry b)
            {
                return b.LastAccessTime.CompareTo(a.LastAccessTime);
            });
            TypeLogin(all[0], target, withDelay);
        }

        /// <summary>Tasteaza login-ul intrarii potrivite cu fereastra activa
        /// (match pe titlu / host din URL). Fara meniu.</summary>
        public void RunAssociated(IntPtr target, bool withDelay)
        {
            PwDatabase db = (m_host != null) ? m_host.Database : null;
            if (db == null || !db.IsOpen) { ShowInfo("No database is open in KeePass."); return; }

            string wt = WindowTitle(target);
            if (string.IsNullOrEmpty(wt)) { ShowInfo("Could not read the active window title."); return; }

            List<PwEntry> all = CollectEntries(db.RootGroup);
            PwEntry best = null;
            DateTime bt = DateTime.MinValue;
            foreach (PwEntry pe in all)
            {
                if (!MatchesWindow(pe, wt)) continue;
                if (best == null || pe.LastAccessTime.CompareTo(bt) >= 0)
                {
                    best = pe;
                    bt = pe.LastAccessTime;
                }
            }

            if (best == null)
            {
                ShowInfo("No entry matches the active window:\n\"" + wt + "\"");
                return;
            }
            TypeLogin(best, target, withDelay);
        }

        private static bool MatchesWindow(PwEntry pe, string windowTitle)
        {
            string title = pe.Strings.ReadSafe(PwDefs.TitleField);
            if (!string.IsNullOrEmpty(title) &&
                windowTitle.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            string url = pe.Strings.ReadSafe(PwDefs.UrlField);
            if (!string.IsNullOrEmpty(url))
            {
                string host = UrlHost(url);
                if (!string.IsNullOrEmpty(host) &&
                    windowTitle.IndexOf(host, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        private static string UrlHost(string url)
        {
            try { return new Uri(url).Host; }
            catch { return url; }
        }

        private static string WindowTitle(IntPtr hWnd)
        {
            try
            {
                if (hWnd == IntPtr.Zero) return "";
                System.Text.StringBuilder sb = new System.Text.StringBuilder(512);
                GetWindowText(hWnd, sb, sb.Capacity);
                return sb.ToString();
            }
            catch { return ""; }
        }

        // Teme meniu: 0=Auto (urmeaza tema), 1=Light, 2=Dark. Alese din Settings.
        private static readonly ToolStripProfessionalRenderer s_lightRenderer =
            new ThemedMenuRenderer(false, new LightColors());
        private static readonly ToolStripProfessionalRenderer s_darkRenderer =
            new ThemedMenuRenderer(true, new DarkColors());

        private ToolStripProfessionalRenderer m_renderer;
        private Color m_menuBack = Color.White;
        private Color m_menuFore = Color.Black;

        private void ApplyMenuTheme(Settings s)
        {
            int mt = (s != null) ? s.MenuTheme : 0;
            bool dark = (mt == 2) || (mt == 0 && IsSystemDark());
            if (dark)
            {
                m_renderer = s_darkRenderer;
                m_menuBack = Color.FromArgb(45, 45, 48);
                m_menuFore = Color.FromArgb(235, 235, 235);
            }
            else
            {
                m_renderer = s_lightRenderer;
                m_menuBack = Color.White;
                m_menuFore = Color.Black;
            }
        }

        private void Style(ToolStripDropDown dd)
        {
            if (dd == null) return;
            if (m_renderer == null) m_renderer = s_lightRenderer;
            dd.Renderer = m_renderer;
            dd.BackColor = m_menuBack;
            dd.ForeColor = m_menuFore;
        }

        private static bool IsSystemDark()
        {
            try
            {
                object v = Microsoft.Win32.Registry.GetValue(
                    @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    "AppsUseLightTheme", 1);
                if (v is int) return ((int)v) == 0;
            }
            catch { }
            Color c = SystemColors.Menu;
            return (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) < 128;
        }

        private ContextMenuStrip NewMenu()
        {
            ContextMenuStrip m = new ContextMenuStrip();
            m.ShowImageMargin = true;   // loc pentru iconite
            m.ShowCheckMargin = false;
            m.ImageScalingSize = new Size(16, 16);
            m.MaximumSize = new Size(0, Screen.PrimaryScreen.WorkingArea.Height - 40);
            Style(m);
            return m;
        }

        /// <summary>Renderer cu culori fortate (text lizibil), light sau dark.</summary>
        private sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
        {
            private readonly bool m_dark;
            public ThemedMenuRenderer(bool dark, ProfessionalColorTable t) : base(t)
            {
                m_dark = dark;
                RoundedEdges = false;
            }
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
            {
                Color on = m_dark ? Color.FromArgb(235, 235, 235) : Color.Black;
                Color off = m_dark ? Color.FromArgb(130, 130, 130) : Color.FromArgb(140, 140, 140);
                e.TextColor = e.Item.Enabled ? on : off;
                base.OnRenderItemText(e);
            }
        }

        private sealed class LightColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Color.White; } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(245, 245, 245); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(245, 245, 245); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(245, 245, 245); } }
            public override Color MenuItemSelected { get { return Color.FromArgb(205, 225, 250); } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(205, 225, 250); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(205, 225, 250); } }
            public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(230, 240, 250); } }
            public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(230, 240, 250); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(120, 160, 210); } }
            public override Color MenuBorder { get { return Color.FromArgb(160, 160, 160); } }
            public override Color SeparatorDark { get { return Color.FromArgb(210, 210, 210); } }
            public override Color SeparatorLight { get { return Color.White; } }
        }

        private sealed class DarkColors : ProfessionalColorTable
        {
            private static readonly Color Bg = Color.FromArgb(45, 45, 48);
            private static readonly Color Margin = Color.FromArgb(37, 37, 38);
            private static readonly Color Sel = Color.FromArgb(62, 92, 130);
            public override Color ToolStripDropDownBackground { get { return Bg; } }
            public override Color ImageMarginGradientBegin { get { return Margin; } }
            public override Color ImageMarginGradientMiddle { get { return Margin; } }
            public override Color ImageMarginGradientEnd { get { return Margin; } }
            public override Color MenuItemSelected { get { return Sel; } }
            public override Color MenuItemSelectedGradientBegin { get { return Sel; } }
            public override Color MenuItemSelectedGradientEnd { get { return Sel; } }
            public override Color MenuItemPressedGradientBegin { get { return Color.FromArgb(70, 100, 140); } }
            public override Color MenuItemPressedGradientEnd { get { return Color.FromArgb(70, 100, 140); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(90, 120, 160); } }
            public override Color MenuBorder { get { return Color.FromArgb(80, 80, 80); } }
            public override Color SeparatorDark { get { return Color.FromArgb(70, 70, 70); } }
            public override Color SeparatorLight { get { return Color.FromArgb(70, 70, 70); } }
        }

        private List<ToolStripItem> BuildGroupItems(PwDatabase db, PwGroup group,
            IntPtr target, bool withDelay)
        {
            List<ToolStripItem> items = new List<ToolStripItem>();
            if (group == null) return items;

            foreach (PwGroup sub in group.Groups)
            {
                PwGroup capturedSub = sub;
                ToolStripMenuItem gi = new ToolStripMenuItem(SafeName(sub.Name));
                gi.Image = GroupIcon(db, sub);
                gi.DropDownOpening += delegate
                {
                    PopulateGroupSubmenu(gi, db, capturedSub, target, withDelay);
                };
                gi.DropDownItems.Add(new ToolStripMenuItem("")); // placeholder pt sageata
                Style(gi.DropDown);
                items.Add(gi);
            }

            foreach (PwEntry pe in group.Entries)
                items.Add(MakeEntryMenu(db, pe, target, withDelay));

            return items;
        }

        private void PopulateGroupSubmenu(ToolStripMenuItem gi, PwDatabase db,
            PwGroup group, IntPtr target, bool withDelay)
        {
            if (gi.Tag != null) return;
            gi.Tag = true;

            gi.DropDownItems.Clear();
            List<ToolStripItem> children = BuildGroupItems(db, group, target, withDelay);
            if (children.Count == 0)
            {
                ToolStripMenuItem empty = new ToolStripMenuItem("(empty)");
                empty.Enabled = false;
                gi.DropDownItems.Add(empty);
            }
            else
            {
                foreach (ToolStripItem c in children)
                    gi.DropDownItems.Add(c);
            }
        }

        private ToolStripMenuItem MakeEntryMenu(PwDatabase db, PwEntry pe,
            IntPtr target, bool withDelay)
        {
            string title = pe.Strings.ReadSafe(PwDefs.TitleField);
            string user = pe.Strings.ReadSafe(PwDefs.UserNameField);
            string url = pe.Strings.ReadSafe(PwDefs.UrlField);
            string label = string.IsNullOrEmpty(title) ? "(no title)" : title;

            if (GetSettings().ShowFullPath && pe.ParentGroup != null)
            {
                string gp = GroupPath(pe.ParentGroup);
                if (!string.IsNullOrEmpty(gp)) label = label + "  (" + gp + ")";
            }

            ToolStripMenuItem root = new ToolStripMenuItem(label);
            root.Image = EntryIcon(db, pe);

            PwEntry captured = pe;
            bool delay = withDelay;

            // Login: ruleaza scriptul salvat al intrarii (username {TAB} parola ...).
            ToolStripMenuItem login = new ToolStripMenuItem("Login");
            login.Click += delegate { TypeLogin(captured, target, delay); };
            root.DropDownItems.Add(login);

            root.DropDownItems.Add(new ToolStripSeparator());

            // Toate campurile standard (chiar daca sunt goale) + campurile custom,
            // fiecare taparabil individual.
            AddFieldItem(root, "Username", PwDefs.UserNameField, user, false, captured, target, delay);
            AddFieldItem(root, "Password", PwDefs.PasswordField,
                pe.Strings.ReadSafe(PwDefs.PasswordField), true, captured, target, delay);
            AddFieldItem(root, "URL", PwDefs.UrlField, url, false, captured, target, delay);

            foreach (KeyValuePair<string, ProtectedString> kv in pe.Strings)
            {
                if (PwDefs.IsStandardField(kv.Key)) continue;
                AddFieldItem(root, kv.Key, kv.Key, kv.Value.ReadString(),
                    kv.Value.IsProtected, captured, target, delay);
            }

            Style(root.DropDown);
            return root;
        }

        private void AddFieldItem(ToolStripMenuItem root, string label, string field,
            string value, bool secret, PwEntry pe, IntPtr target, bool withDelay)
        {
            string text = label;
            if (!secret && !string.IsNullOrEmpty(value))
                text = label + ": " + Shorten(value);
            ToolStripMenuItem it = new ToolStripMenuItem(text);
            string f = field;
            PwEntry captured = pe;
            it.Click += delegate { TypeField(f, captured, target, withDelay); };
            root.DropDownItems.Add(it);
        }

        // --- icons ---

        private Image EntryIcon(PwDatabase db, PwEntry pe)
        {
            try
            {
                if (pe.CustomIconUuid != null && !pe.CustomIconUuid.Equals(PwUuid.Zero) && db != null)
                {
                    Image ci = db.GetCustomIcon(pe.CustomIconUuid, 16, 16);
                    if (ci != null) return ci;
                }
                return StandardIcon((int)pe.IconId);
            }
            catch { return null; }
        }

        private Image GroupIcon(PwDatabase db, PwGroup g)
        {
            try
            {
                if (g.CustomIconUuid != null && !g.CustomIconUuid.Equals(PwUuid.Zero) && db != null)
                {
                    Image ci = db.GetCustomIcon(g.CustomIconUuid, 16, 16);
                    if (ci != null) return ci;
                }
                return StandardIcon((int)g.IconId);
            }
            catch { return null; }
        }

        private Image StandardIcon(int iconId)
        {
            try
            {
                var mw = m_host.MainWindow;
                if (mw == null || mw.ClientIcons == null) return null;
                var imgs = mw.ClientIcons.Images;
                if (imgs != null && iconId >= 0 && iconId < imgs.Count)
                    return imgs[iconId];
            }
            catch { }
            return null;
        }

        // --- helpers ---

        private static List<PwEntry> CollectEntries(PwGroup root)
        {
            List<PwEntry> list = new List<PwEntry>();
            if (root == null) return list;
            foreach (PwEntry pe in root.GetEntries(true))
                list.Add(pe);
            return list;
        }

        private static string SafeName(string s)
        {
            return string.IsNullOrEmpty(s) ? "(group)" : s;
        }

        private static string GroupPath(PwGroup g)
        {
            List<string> parts = new List<string>();
            PwGroup cur = g;
            // urca pana la radacina; radacina (fara parinte) se omite
            while (cur != null && cur.ParentGroup != null)
            {
                parts.Insert(0, SafeName(cur.Name));
                cur = cur.ParentGroup;
            }
            return string.Join(" / ", parts.ToArray());
        }

        private static string Shorten(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return (s.Length > 40) ? s.Substring(0, 37) + "..." : s;
        }

        // --- typing ---

        private void TypeLogin(PwEntry pe, IntPtr target, bool withDelay)
        {
            Settings s = GetSettings();
            Touch(pe);

            List<SeqElement> elems = BuildSequence(pe, s);
            RunTyping(target, withDelay, s, delegate { RunElements(pe, elems, s); });
        }

        /// <summary>Secventa de rulat: cea salvata pe intrare (Login Script),
        /// altfel default-ul user {TAB} parola (+ Enter daca Auto Submit).</summary>
        private static List<SeqElement> BuildSequence(PwEntry pe, Settings s)
        {
            string seq = SafeSequence(pe);
            if (!string.IsNullOrEmpty(seq))
                return SeqCodec.Parse(seq);

            // Default: Ctrl+A inainte de fiecare camp (goleste ce era),
            // apoi Username {TAB} Ctrl+A Password {ENTER}.
            List<SeqElement> d = new List<SeqElement>();
            d.Add(SeqElement.NewChar('a', true, false, false));   // Ctrl+A
            d.Add(SeqElement.NewField("USERNAME"));
            d.Add(SeqElement.NewSpecial("TAB", false, false, false, 1));
            if (s.FieldDelayMs > 0) d.Add(SeqElement.NewDelay(s.FieldDelayMs));
            d.Add(SeqElement.NewChar('a', true, false, false));   // Ctrl+A
            d.Add(SeqElement.NewField("PASSWORD"));
            d.Add(SeqElement.NewSpecial("ENTER", false, false, false, 1));
            return d;
        }

        private static string SafeSequence(PwEntry pe)
        {
            try { return (pe.AutoType != null) ? pe.AutoType.DefaultSequence : null; }
            catch { return null; }
        }

        private static void RunElements(PwEntry pe, List<SeqElement> elems, Settings s)
        {
            foreach (SeqElement e in elems)
            {
                switch (e.Kind)
                {
                    case SeqKind.Text:
                        if (!string.IsNullOrEmpty(e.Text))
                            ScanCodeTyper.Type(e.Text, s.PerCharDelayMs);
                        break;
                    case SeqKind.Field:
                        string v = ResolveField(pe, e.FieldToken);
                        if (!string.IsNullOrEmpty(v))
                            ScanCodeTyper.Type(v, s.PerCharDelayMs);
                        break;
                    case SeqKind.Key:
                        byte vk = (e.SpecialKey.Length > 0)
                            ? SeqCodec.VkFor(e.SpecialKey)
                            : ScanCodeTyper.VkForChar(e.KeyChar);
                        if (vk != 0)
                            ScanCodeTyper.SendKey(vk, e.Ctrl, e.Shift, e.Alt, e.Count);
                        break;
                    case SeqKind.Delay:
                        if (e.DelayMs > 0)
                            System.Threading.Thread.Sleep(e.DelayMs);
                        break;
                }
            }
        }

        private static string ResolveField(PwEntry pe, string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            string up = token.ToUpperInvariant();
            if (up == "USERNAME") return pe.Strings.ReadSafe(PwDefs.UserNameField);
            if (up == "PASSWORD") return pe.Strings.ReadSafe(PwDefs.PasswordField);
            if (up == "URL") return pe.Strings.ReadSafe(PwDefs.UrlField);
            if (up == "TITLE") return pe.Strings.ReadSafe(PwDefs.TitleField);
            if (up == "NOTES") return pe.Strings.ReadSafe(PwDefs.NotesField);
            if (up.StartsWith("S:")) return pe.Strings.ReadSafe(token.Substring(2));
            return "";
        }

        private void TypeField(string field, PwEntry pe, IntPtr target, bool withDelay)
        {
            string val = pe.Strings.ReadSafe(field);
            Settings s = GetSettings();
            Touch(pe);

            RunTyping(target, withDelay, s, delegate
            {
                if (!string.IsNullOrEmpty(val))
                    ScanCodeTyper.Type(val, s.PerCharDelayMs);
            });
        }

        private void RunTyping(IntPtr target, bool withDelay, Settings s, Action body)
        {
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                System.Threading.Thread.Sleep(150);
                if (target != IntPtr.Zero) SetForegroundWindow(target);
                System.Threading.Thread.Sleep(withDelay ? 3000 : 100);
                try
                {
                    ScanCodeTyper.KeyPressDelayMs = s.KeyPressDelayMs;
                    body();
                }
                catch { }
            });
        }

        private Settings GetSettings()
        {
            return (m_getSettings != null) ? m_getSettings() : new Settings();
        }

        private static void Touch(PwEntry pe)
        {
            try { pe.Touch(false); } catch { }
        }

        private static void ShowInfo(string msg)
        {
            MessageBox.Show(msg, "RdpAutoType", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
