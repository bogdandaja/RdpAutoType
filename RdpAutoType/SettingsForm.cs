using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpAutoType
{
    /// <summary>
    /// Dialog de preferinte: arbore in stanga, pagini in dreapta. Layout bazat pe
    /// TableLayoutPanel -> se aseaza corect la orice DPI si orice lungime de text
    /// (etichetele se auto-dimensioneaza, combo-urile se intind, nimic nu se taie).
    /// </summary>
    internal sealed class SettingsForm : Form
    {
        private readonly Settings m_settings;

        private static readonly object[] ClickActions = new object[]
        {
            "Do nothing",                                 // 0
            "Launch popup menu",                          // 1
            "Run login script of most recent entry",      // 2
            "Run login script of associated entry"        // 3
        };

        private TreeView m_tree;
        private Panel m_pagePopup;
        private Panel m_pageTyping;
        private Panel m_pageShortcut;

        // Popup Menu page
        private CheckBox m_kbCtrl, m_kbShift, m_kbAlt;
        private RadioButton m_rbLeft, m_rbMiddle, m_rbRight;
        private ComboBox m_cboSingle, m_cboDouble;
        private CheckBox m_chkRecent;
        private NumericUpDown m_numRecent;
        private CheckBox m_chkFullPath;
        private ComboBox m_cboTheme;

        // Typing page
        private NumericUpDown m_perChar;
        private NumericUpDown m_keyPress;
        private CheckBox m_autoSubmit;

        // Shortcut page
        private CheckBox m_chkShortcut;
        private CaptureBox m_capShortcut;

        public SettingsForm(Settings settings)
        {
            m_settings = settings;
            BuildUi();
            LoadValues();
        }

        /// <summary>Unitate relativa la font (DPI-independent).</summary>
        private int U(double f) { return (int)Math.Round(Font.Height * f); }

        private void BuildUi()
        {
            Text = "RdpAutoType - Preferences";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(U(46), U(30));

            // host pentru pagini (umple restul)
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;

            // arbore
            m_tree = new TreeView();
            m_tree.Dock = DockStyle.Left;
            m_tree.Width = U(12);
            m_tree.HideSelection = false;
            m_tree.FullRowSelect = true;
            TreeNode root = new TreeNode("Preferences");
            TreeNode nPopup = new TreeNode("Popup Menu"); nPopup.Tag = "popup";
            TreeNode nTyping = new TreeNode("Typing"); nTyping.Tag = "typing";
            TreeNode nShortcut = new TreeNode("Shortcut"); nShortcut.Tag = "shortcut";
            root.Nodes.Add(nPopup); root.Nodes.Add(nTyping); root.Nodes.Add(nShortcut);
            m_tree.Nodes.Add(root);
            root.Expand();
            m_tree.AfterSelect += delegate(object s, TreeViewEventArgs e) { ShowPage(e.Node); };

            // bara OK / Cancel
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = U(3);
            FlowLayoutPanel fb = new FlowLayoutPanel();
            fb.Dock = DockStyle.Right;
            fb.FlowDirection = FlowDirection.LeftToRight;
            fb.AutoSize = true;
            fb.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            fb.WrapContents = false;
            fb.Padding = new Padding(0, U(0.4), U(0.7), U(0.4));
            Button ok = MakeButton("OK"); ok.DialogResult = DialogResult.OK;
            ok.Click += delegate { SaveValues(); };
            Button cancel = MakeButton("Cancel"); cancel.DialogResult = DialogResult.Cancel;
            fb.Controls.Add(ok);
            fb.Controls.Add(cancel);
            bottom.Controls.Add(fb);
            AcceptButton = ok;
            CancelButton = cancel;

            // pagini
            m_pagePopup = BuildPopupPage();
            m_pageTyping = BuildTypingPage();
            m_pageShortcut = BuildShortcutPage();
            m_pageTyping.Visible = false;
            m_pageShortcut.Visible = false;
            host.Controls.Add(m_pagePopup);
            host.Controls.Add(m_pageTyping);
            host.Controls.Add(m_pageShortcut);

            // ordine: Fill primul, apoi Left, apoi Bottom
            Controls.Add(host);
            Controls.Add(m_tree);
            Controls.Add(bottom);

            m_tree.SelectedNode = nPopup;
        }

        private Button MakeButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.AutoSize = true;
            b.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            b.MinimumSize = new Size(U(6), U(2));
            b.Margin = new Padding(U(0.5), 0, 0, 0);
            return b;
        }

        // ---------------- helpers layout ----------------

        private TableLayoutPanel NewPage()
        {
            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Fill;
            t.ColumnCount = 2;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.Padding = new Padding(U(0.8), U(0.6), U(1.1), U(0.6));
            t.AutoScroll = true;
            return t;
        }

        private void AddHeader(TableLayoutPanel t, string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font(Font, FontStyle.Bold);
            l.Margin = new Padding(0, (t.RowCount == 0 ? 0 : U(0.6)), 0, U(0.25));
            int r = NextRow(t);
            t.Controls.Add(l, 0, r);
            t.SetColumnSpan(l, 2);
        }

        private void AddRow(TableLayoutPanel t, string label, Control c)
        {
            Label l = new Label();
            l.Text = label;
            l.AutoSize = true;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, U(0.35), U(0.7), U(0.35));
            int r = NextRow(t);
            t.Controls.Add(l, 0, r);
            c.Margin = new Padding(0, U(0.2), 0, U(0.2));
            t.Controls.Add(c, 1, r);
        }

        private void AddFull(TableLayoutPanel t, Control c)
        {
            int r = NextRow(t);
            c.Margin = new Padding(0, U(0.25), 0, U(0.25));
            t.Controls.Add(c, 0, r);
            t.SetColumnSpan(c, 2);
        }

        private static int NextRow(TableLayoutPanel t)
        {
            int r = t.RowCount;
            t.RowCount = r + 1;
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return r;
        }

        private FlowLayoutPanel Flow()
        {
            FlowLayoutPanel f = new FlowLayoutPanel();
            f.AutoSize = true;
            f.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            f.FlowDirection = FlowDirection.LeftToRight;
            f.WrapContents = false;
            f.Margin = Padding.Empty;
            f.Anchor = AnchorStyles.Left;
            return f;
        }

        private static CheckBox Chk(string text)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Margin = new Padding(0, 4, 12, 4);
            return c;
        }

        private static RadioButton Rad(string text)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.AutoSize = true;
            r.Margin = new Padding(0, 4, 12, 4);
            return r;
        }

        private ComboBox FillCombo(object[] items)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Dock = DockStyle.Fill;
            c.Items.AddRange(items);
            return c;
        }

        private static Label Lbl(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Margin = new Padding(6, 6, 0, 0);
            return l;
        }

        // ---------------- pages ----------------

        private Panel BuildPopupPage()
        {
            TableLayoutPanel t = NewPage();

            AddHeader(t, "Activation key/button");

            FlowLayoutPanel kb = Flow();
            m_kbCtrl = Chk("Ctrl"); m_kbShift = Chk("Shift"); m_kbAlt = Chk("Alt");
            kb.Controls.Add(m_kbCtrl); kb.Controls.Add(m_kbShift); kb.Controls.Add(m_kbAlt);
            AddRow(t, "Keyboard:", kb);

            FlowLayoutPanel mb = Flow();
            m_rbLeft = Rad("Left"); m_rbMiddle = Rad("Middle"); m_rbRight = Rad("Right");
            mb.Controls.Add(m_rbLeft); mb.Controls.Add(m_rbMiddle); mb.Controls.Add(m_rbRight);
            AddRow(t, "Mouse button:", mb);

            m_cboSingle = FillCombo(ClickActions);
            AddRow(t, "Single mouse click:", m_cboSingle);
            m_cboDouble = FillCombo(ClickActions);
            AddRow(t, "Double mouse click:", m_cboDouble);

            Button reset = new Button();
            reset.Text = "Reset activation key/button";
            reset.AutoSize = true;
            reset.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            reset.Anchor = AnchorStyles.Left;
            reset.Click += delegate { m_settings.ResetActivation(); LoadActivation(); };
            AddFull(t, reset);

            AddHeader(t, "Components");

            FlowLayoutPanel disp = Flow();
            m_chkRecent = Chk("Display");
            m_numRecent = new NumericUpDown();
            m_numRecent.Minimum = 1; m_numRecent.Maximum = 50;
            m_numRecent.Width = U(3.5);
            m_numRecent.Margin = new Padding(0, 3, 6, 0);
            disp.Controls.Add(m_chkRecent);
            disp.Controls.Add(m_numRecent);
            disp.Controls.Add(Lbl("recent items"));
            AddFull(t, disp);

            m_chkFullPath = Chk("Show full path");
            AddFull(t, m_chkFullPath);

            m_cboTheme = FillCombo(new object[] { "Auto (follow theme)", "Light", "Dark" });
            AddRow(t, "Menu theme:", m_cboTheme);

            return t;
        }

        private Panel BuildTypingPage()
        {
            TableLayoutPanel t = NewPage();

            AddHeader(t, "Timing");

            FlowLayoutPanel r1 = Flow();
            m_perChar = Num(0, 500);
            r1.Controls.Add(m_perChar); r1.Controls.Add(Lbl("ms"));
            AddRow(t, "Between characters:", r1);

            FlowLayoutPanel r2 = Flow();
            m_keyPress = Num(0, 100);
            r2.Controls.Add(m_keyPress); r2.Controls.Add(Lbl("ms"));
            AddRow(t, "Key press / release:", r2);

            m_autoSubmit = Chk("Auto Submit (press Enter at the end)");
            AddFull(t, m_autoSubmit);

            return t;
        }

        private NumericUpDown Num(int min, int max)
        {
            NumericUpDown n = new NumericUpDown();
            n.Minimum = min; n.Maximum = max;
            n.Width = U(4);
            n.TextAlign = HorizontalAlignment.Right;
            n.Margin = new Padding(0, 3, 6, 0);
            return n;
        }

        private Panel BuildShortcutPage()
        {
            TableLayoutPanel t = NewPage();

            AddFull(t, Bold("Edit Login Script shortcut (active inside KeePass)."));

            m_chkShortcut = Chk("Enable shortcut");
            AddFull(t, m_chkShortcut);

            FlowLayoutPanel sc = Flow();
            m_capShortcut = new CaptureBox();
            m_capShortcut.Width = U(16);
            m_capShortcut.Margin = new Padding(0, 2, 8, 0);
            Button clear = new Button();
            clear.Text = "Clear";
            clear.AutoSize = true;
            clear.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            clear.Click += delegate { m_capShortcut.SetKeys(Keys.None); };
            sc.Controls.Add(m_capShortcut);
            sc.Controls.Add(clear);
            AddRow(t, "Shortcut:", sc);

            Label help = new Label();
            help.Text = "Click the box and press the keys (e.g. Ctrl+Enter).\r\n" +
                        "Works when KeePass is active and an entry is selected.";
            help.AutoSize = true;
            help.ForeColor = SystemColors.GrayText;
            AddFull(t, help);

            return t;
        }

        private Label Bold(string text)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = new Font(Font, FontStyle.Bold);
            return l;
        }

        // ---------------- navigation ----------------

        private void ShowPage(TreeNode node)
        {
            string tag = (node != null) ? (node.Tag as string) : null;
            m_pagePopup.Visible = (tag != "typing" && tag != "shortcut");
            m_pageTyping.Visible = (tag == "typing");
            m_pageShortcut.Visible = (tag == "shortcut");
        }

        // ---------------- load / save ----------------

        private void LoadValues()
        {
            LoadActivation();

            m_chkRecent.Checked = m_settings.ShowRecent;
            m_numRecent.Value = Settings.Clamp(m_settings.RecentCount, 1, 50);
            m_chkFullPath.Checked = m_settings.ShowFullPath;
            m_cboTheme.SelectedIndex = Settings.Clamp(m_settings.MenuTheme, 0, 2);

            m_perChar.Value = Settings.Clamp(m_settings.PerCharDelayMs, 0, 500);
            m_keyPress.Value = Settings.Clamp(m_settings.KeyPressDelayMs, 0, 100);
            m_autoSubmit.Checked = m_settings.SendFinalEnter;

            m_chkShortcut.Checked = m_settings.EditShortcutEnabled;
            m_capShortcut.SetKeys((Keys)m_settings.EditShortcut);
        }

        private void LoadActivation()
        {
            m_kbCtrl.Checked = m_settings.MouseCtrl;
            m_kbShift.Checked = m_settings.MouseShift;
            m_kbAlt.Checked = m_settings.MouseAlt;

            m_rbLeft.Checked = (m_settings.MouseButton == 0);
            m_rbMiddle.Checked = (m_settings.MouseButton == 1);
            m_rbRight.Checked = (m_settings.MouseButton == 2);
            if (!m_rbLeft.Checked && !m_rbMiddle.Checked && !m_rbRight.Checked)
                m_rbMiddle.Checked = true;

            m_cboSingle.SelectedIndex = Settings.Clamp(m_settings.MouseSingleAction, 0, ClickActions.Length - 1);
            m_cboDouble.SelectedIndex = Settings.Clamp(m_settings.MouseDoubleAction, 0, ClickActions.Length - 1);
        }

        private void SaveValues()
        {
            m_settings.MouseCtrl = m_kbCtrl.Checked;
            m_settings.MouseShift = m_kbShift.Checked;
            m_settings.MouseAlt = m_kbAlt.Checked;

            m_settings.MouseButton = m_rbRight.Checked ? 2 : (m_rbLeft.Checked ? 0 : 1);
            m_settings.MouseSingleAction = m_cboSingle.SelectedIndex;
            m_settings.MouseDoubleAction = m_cboDouble.SelectedIndex;

            m_settings.ShowRecent = m_chkRecent.Checked;
            m_settings.RecentCount = (int)m_numRecent.Value;
            m_settings.ShowFullPath = m_chkFullPath.Checked;
            m_settings.MenuTheme = m_cboTheme.SelectedIndex;

            m_settings.PerCharDelayMs = (int)m_perChar.Value;
            m_settings.KeyPressDelayMs = (int)m_keyPress.Value;
            m_settings.SendFinalEnter = m_autoSubmit.Checked;

            m_settings.EditShortcutEnabled = m_chkShortcut.Checked;
            m_settings.EditShortcut = (int)m_capShortcut.Captured;
        }

        /// <summary>Textbox care capteaza o combinatie de taste (inclusiv Enter/Tab).</summary>
        private sealed class CaptureBox : TextBox
        {
            public Keys Captured = Keys.None;

            public CaptureBox()
            {
                ReadOnly = true;
                Cursor = Cursors.Hand;
                Text = "(click and press keys)";
            }

            public void SetKeys(Keys k)
            {
                Captured = k;
                Text = KeysToText(k);
            }

            protected override bool IsInputKey(Keys keyData) { return true; }

            protected override void OnKeyDown(KeyEventArgs e)
            {
                Keys code = e.KeyCode;
                if (code == Keys.ControlKey || code == Keys.ShiftKey || code == Keys.Menu)
                {
                    e.Handled = true; e.SuppressKeyPress = true; return;
                }
                SetKeys(e.KeyData);
                e.Handled = true; e.SuppressKeyPress = true;
            }

            private static string KeysToText(Keys k)
            {
                if (k == Keys.None) return "(none)";
                string s = "";
                if ((k & Keys.Control) == Keys.Control) s += "Ctrl+";
                if ((k & Keys.Shift) == Keys.Shift) s += "Shift+";
                if ((k & Keys.Alt) == Keys.Alt) s += "Alt+";
                Keys code = k & Keys.KeyCode;
                string name = code.ToString();
                if (code == Keys.Return) name = "Enter";
                else if (code == Keys.Space) name = "Space";
                s += name;
                return s;
            }
        }
    }
}
