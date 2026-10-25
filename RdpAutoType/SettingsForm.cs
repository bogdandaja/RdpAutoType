using System;
using System.Drawing;
using System.Windows.Forms;

namespace RdpAutoType
{
    /// <summary>
    /// Dialog de preferinte: arbore in stanga, panou in dreapta.
    /// Pagina "Popup Menu" reproduce "Activation key/button" + "Components".
    /// Pagina "Typing" tine timing-ul de tastare. DPI-safe (AutoScaleMode.Font),
    /// coloane largi ca sa nu se suprapuna etichetele cu combo-urile.
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

        // Popup Menu page
        private CheckBox m_kbCtrl, m_kbShift, m_kbAlt;
        private RadioButton m_rbLeft, m_rbMiddle, m_rbRight;
        private ComboBox m_cboSingle, m_cboDouble;
        private CheckBox m_chkRecent;
        private NumericUpDown m_numRecent;
        private CheckBox m_chkFullPath;

        // Typing page
        private NumericUpDown m_perChar;
        private NumericUpDown m_keyPress;
        private CheckBox m_autoSubmit;

        // Shortcut page
        private Panel m_pageShortcut;
        private CheckBox m_chkShortcut;
        private CaptureBox m_capShortcut;

        public SettingsForm(Settings settings)
        {
            m_settings = settings;
            BuildUi();
            LoadValues();
        }

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
            ClientSize = new Size(720, 470);

            // --- left tree ---
            m_tree = new TreeView();
            m_tree.Location = new Point(12, 12);
            m_tree.Size = new Size(180, 408);
            m_tree.HideSelection = false;
            m_tree.FullRowSelect = true;
            TreeNode root = new TreeNode("Preferences");
            TreeNode nPopup = new TreeNode("Popup Menu");
            TreeNode nTyping = new TreeNode("Typing");
            TreeNode nShortcut = new TreeNode("Shortcut");
            nPopup.Tag = "popup";
            nTyping.Tag = "typing";
            nShortcut.Tag = "shortcut";
            root.Nodes.Add(nPopup);
            root.Nodes.Add(nTyping);
            root.Nodes.Add(nShortcut);
            m_tree.Nodes.Add(root);
            root.Expand();
            m_tree.AfterSelect += delegate(object s, TreeViewEventArgs e) { ShowPage(e.Node); };
            Controls.Add(m_tree);

            // --- right pages ---
            Point area = new Point(204, 12);
            Size areaSize = new Size(504, 408);

            m_pagePopup = new Panel();
            m_pagePopup.Location = area;
            m_pagePopup.Size = areaSize;
            BuildPopupPage(m_pagePopup);
            Controls.Add(m_pagePopup);

            m_pageTyping = new Panel();
            m_pageTyping.Location = area;
            m_pageTyping.Size = areaSize;
            m_pageTyping.Visible = false;
            BuildTypingPage(m_pageTyping);
            Controls.Add(m_pageTyping);

            m_pageShortcut = new Panel();
            m_pageShortcut.Location = area;
            m_pageShortcut.Size = areaSize;
            m_pageShortcut.Visible = false;
            BuildShortcutPage(m_pageShortcut);
            Controls.Add(m_pageShortcut);

            // --- OK / Cancel ---
            int by = ClientSize.Height - 42;
            Button ok = new Button();
            ok.Text = "OK";
            ok.Size = new Size(90, 30);
            ok.DialogResult = DialogResult.OK;
            ok.Location = new Point(ClientSize.Width - 12 - 90 - 10 - 90, by);
            ok.Click += delegate { SaveValues(); };
            Controls.Add(ok);

            Button cancel = new Button();
            cancel.Text = "Cancel";
            cancel.Size = new Size(90, 30);
            cancel.DialogResult = DialogResult.Cancel;
            cancel.Location = new Point(ClientSize.Width - 12 - 90, by);
            Controls.Add(cancel);

            AcceptButton = ok;
            CancelButton = cancel;

            m_tree.SelectedNode = nPopup;
        }

        // ---------------- Popup Menu page ----------------

        private void BuildPopupPage(Panel p)
        {
            Label head = new Label();
            head.Text = "Configure popup menu settings.";
            head.Font = new Font(Font, FontStyle.Bold);
            head.AutoSize = true;
            head.Location = new Point(2, 0);
            p.Controls.Add(head);

            // Activation key/button
            GroupBox gbAct = new GroupBox();
            gbAct.Text = "Activation key/button";
            gbAct.Location = new Point(2, 26);
            gbAct.Size = new Size(498, 214);
            p.Controls.Add(gbAct);

            AddLabel(gbAct, "Keyboard:", 16, 34);
            m_kbCtrl = Check(gbAct, "Ctrl", 190, 32);
            m_kbShift = Check(gbAct, "Shift", 300, 32);
            m_kbAlt = Check(gbAct, "Alt", 410, 32);

            AddLabel(gbAct, "Mouse button:", 16, 72);
            m_rbLeft = Radio(gbAct, "Left", 190, 70);
            m_rbMiddle = Radio(gbAct, "Middle", 300, 70);
            m_rbRight = Radio(gbAct, "Right", 410, 70);

            AddLabel(gbAct, "Single mouse click:", 16, 112);
            m_cboSingle = ClickCombo(gbAct, 190, 108);

            AddLabel(gbAct, "Double mouse click:", 16, 150);
            m_cboDouble = ClickCombo(gbAct, 190, 146);

            Button reset = new Button();
            reset.Text = "Reset activation key/button";
            reset.Size = new Size(220, 28);
            reset.Location = new Point(258, 182);
            reset.Click += delegate
            {
                m_settings.ResetActivation();
                LoadActivation();
            };
            gbAct.Controls.Add(reset);

            // Components
            GroupBox gbComp = new GroupBox();
            gbComp.Text = "Components";
            gbComp.Location = new Point(2, 248);
            gbComp.Size = new Size(498, 96);
            p.Controls.Add(gbComp);

            m_chkRecent = new CheckBox();
            m_chkRecent.Text = "Display";
            m_chkRecent.AutoSize = true;
            m_chkRecent.Location = new Point(16, 32);
            gbComp.Controls.Add(m_chkRecent);

            m_numRecent = new NumericUpDown();
            m_numRecent.Minimum = 1;
            m_numRecent.Maximum = 50;
            m_numRecent.Size = new Size(60, 24);
            m_numRecent.Location = new Point(120, 30);
            gbComp.Controls.Add(m_numRecent);

            AddLabel(gbComp, "recent items", 190, 34);

            m_chkFullPath = new CheckBox();
            m_chkFullPath.Text = "Show full path";
            m_chkFullPath.AutoSize = true;
            m_chkFullPath.Location = new Point(16, 64);
            gbComp.Controls.Add(m_chkFullPath);
        }

        private static void AddLabel(Control parent, string text, int x, int y)
        {
            Label l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Location = new Point(x, y);
            parent.Controls.Add(l);
        }

        private static CheckBox Check(Control parent, string text, int x, int y)
        {
            CheckBox c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Location = new Point(x, y);
            parent.Controls.Add(c);
            return c;
        }

        private static RadioButton Radio(Control parent, string text, int x, int y)
        {
            RadioButton r = new RadioButton();
            r.Text = text;
            r.AutoSize = true;
            r.Location = new Point(x, y);
            parent.Controls.Add(r);
            return r;
        }

        private static ComboBox ClickCombo(Control parent, int x, int y)
        {
            ComboBox c = new ComboBox();
            c.DropDownStyle = ComboBoxStyle.DropDownList;
            c.Items.AddRange(ClickActions);
            c.Location = new Point(x, y);
            c.Size = new Size(290, 24);
            parent.Controls.Add(c);
            return c;
        }

        // ---------------- Typing page ----------------

        private void BuildTypingPage(Panel p)
        {
            Label head = new Label();
            head.Text = "Configure typing settings.";
            head.Font = new Font(Font, FontStyle.Bold);
            head.AutoSize = true;
            head.Location = new Point(2, 0);
            p.Controls.Add(head);

            GroupBox gb = new GroupBox();
            gb.Text = "Timing";
            gb.Location = new Point(2, 26);
            gb.Size = new Size(498, 112);
            p.Controls.Add(gb);

            m_perChar = TimingRow(gb, "Between characters", 34, 0, 500);
            m_keyPress = TimingRow(gb, "Key press / release", 70, 0, 100);

            m_autoSubmit = new CheckBox();
            m_autoSubmit.Text = "Auto Submit (press Enter at the end)";
            m_autoSubmit.AutoSize = true;
            m_autoSubmit.Location = new Point(6, 152);
            p.Controls.Add(m_autoSubmit);
        }

        private NumericUpDown TimingRow(GroupBox gb, string text, int y, int min, int max)
        {
            AddLabel(gb, text, 16, y + 2);

            NumericUpDown n = new NumericUpDown();
            n.Minimum = min;
            n.Maximum = max;
            n.Size = new Size(70, 24);
            n.Location = new Point(280, y);
            n.TextAlign = HorizontalAlignment.Right;
            gb.Controls.Add(n);

            AddLabel(gb, "ms", 358, y + 2);
            return n;
        }

        // ---------------- navigation ----------------

        private void ShowPage(TreeNode node)
        {
            string tag = (node != null) ? (node.Tag as string) : null;
            m_pagePopup.Visible = (tag != "typing" && tag != "shortcut");
            m_pageTyping.Visible = (tag == "typing");
            m_pageShortcut.Visible = (tag == "shortcut");
        }

        // ---------------- Shortcut page ----------------

        private void BuildShortcutPage(Panel p)
        {
            Label head = new Label();
            head.Text = "Edit Login Script shortcut (active inside KeePass).";
            head.Font = new Font(Font, FontStyle.Bold);
            head.AutoSize = true;
            head.Location = new Point(2, 0);
            p.Controls.Add(head);

            m_chkShortcut = new CheckBox();
            m_chkShortcut.Text = "Enable shortcut";
            m_chkShortcut.AutoSize = true;
            m_chkShortcut.Location = new Point(6, 34);
            p.Controls.Add(m_chkShortcut);

            AddLabel(p, "Shortcut:", 6, 76);
            m_capShortcut = new CaptureBox();
            m_capShortcut.Location = new Point(90, 72);
            m_capShortcut.Size = new Size(220, 24);
            p.Controls.Add(m_capShortcut);

            Button clear = new Button();
            clear.Text = "Clear";
            clear.Size = new Size(70, 26);
            clear.Location = new Point(320, 72);
            clear.Click += delegate { m_capShortcut.SetKeys(Keys.None); };
            p.Controls.Add(clear);

            Label help = new Label();
            help.Text = "Click the box and press the keys (e.g. Ctrl+Enter).\r\n" +
                        "Works when KeePass is active and an entry is selected.";
            help.AutoSize = true;
            help.ForeColor = SystemColors.GrayText;
            help.Location = new Point(6, 112);
            p.Controls.Add(help);
        }

        // ---------------- load / save ----------------

        private void LoadValues()
        {
            LoadActivation();

            m_chkRecent.Checked = m_settings.ShowRecent;
            m_numRecent.Value = Settings.Clamp(m_settings.RecentCount, 1, 50);
            m_chkFullPath.Checked = m_settings.ShowFullPath;

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

            // Revendica toate tastele (Enter/Tab/sageti) ca sa ajunga la KeyDown,
            // nu la AcceptButton/navigare.
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
