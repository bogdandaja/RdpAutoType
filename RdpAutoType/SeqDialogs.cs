using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace RdpAutoType
{
    /// <summary>Adauga / editeaza un camp (nume, valoare, ascuns).</summary>
    internal sealed class FieldEditDialog : Form
    {
        private TextBox m_name;
        private TextBox m_value;
        private CheckBox m_protect;

        public string FieldName { get { return m_name.Text.Trim(); } }
        public string FieldValue { get { return m_value.Text; } }
        public bool Protect { get { return m_protect.Checked; } }

        public FieldEditDialog(string name, string value, bool protect, bool nameEditable)
        {
            Text = "Field";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(400, 170);

            Label l1 = new Label { Text = "Name", AutoSize = true, Location = new Point(16, 20) };
            Controls.Add(l1);
            m_name = new TextBox { Location = new Point(110, 16), Size = new Size(270, 24), Text = name };
            m_name.Enabled = nameEditable;
            Controls.Add(m_name);

            Label l2 = new Label { Text = "Value", AutoSize = true, Location = new Point(16, 56) };
            Controls.Add(l2);
            m_value = new TextBox { Location = new Point(110, 52), Size = new Size(270, 24), Text = value };
            m_value.UseSystemPasswordChar = protect;
            Controls.Add(m_value);

            // Un singur control pentru ascundere: checkbox-ul. Bifat -> mascat;
            // debifat -> se vede. Starea se salveaza pe camp.
            m_protect = new CheckBox { Text = "Hide password value", AutoSize = true, Location = new Point(110, 88), Checked = protect };
            m_protect.CheckedChanged += delegate { m_value.UseSystemPasswordChar = m_protect.Checked; };
            Controls.Add(m_protect);

            AddOkCancel(this);
        }

        internal static void AddOkCancel(Form f)
        {
            Button ok = new Button { Text = "OK", Size = new Size(90, 30), DialogResult = DialogResult.OK };
            ok.Location = new Point(f.ClientSize.Width - 12 - 90 - 10 - 90, f.ClientSize.Height - 40);
            f.Controls.Add(ok);
            Button cancel = new Button { Text = "Cancel", Size = new Size(90, 30), DialogResult = DialogResult.Cancel };
            cancel.Location = new Point(f.ClientSize.Width - 12 - 90, f.ClientSize.Height - 40);
            f.Controls.Add(cancel);
            f.AcceptButton = ok;
            f.CancelButton = cancel;
        }
    }

    /// <summary>Alege un camp de inserat/editat in script.</summary>
    internal sealed class FieldPickDialog : Form
    {
        private ComboBox m_combo;
        private readonly List<string> m_tokens = new List<string>();

        public string Token
        {
            get
            {
                int i = m_combo.SelectedIndex;
                return (i >= 0 && i < m_tokens.Count) ? m_tokens[i] : "USERNAME";
            }
        }

        public FieldPickDialog(List<string> customNames, string currentToken)
        {
            Text = "Field";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(360, 110);

            Label l = new Label { Text = "Field", AutoSize = true, Location = new Point(16, 24) };
            Controls.Add(l);

            m_combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(90, 20), Size = new Size(250, 24) };
            Controls.Add(m_combo);

            Add("Username", "USERNAME");
            Add("Password", "PASSWORD");
            Add("URL", "URL");
            Add("Title", "TITLE");
            Add("Notes", "NOTES");
            if (customNames != null)
                foreach (string n in customNames)
                    Add(n, "S:" + n);

            int sel = 0;
            if (!string.IsNullOrEmpty(currentToken))
            {
                int idx = m_tokens.IndexOf(currentToken);
                if (idx >= 0) sel = idx;
            }
            if (m_combo.Items.Count > 0) m_combo.SelectedIndex = sel;

            FieldEditDialog.AddOkCancel(this);
        }

        private void Add(string label, string token)
        {
            m_combo.Items.Add(label);
            m_tokens.Add(token);
        }
    }

    /// <summary>Alege o tasta/combo de inserat/editat in script.</summary>
    internal sealed class KeyPickDialog : Form
    {
        private CheckBox m_ctrl, m_shift, m_alt;
        private RadioButton m_rbSpecial, m_rbChar;
        private ComboBox m_special;
        private TextBox m_char;
        private NumericUpDown m_count;

        public SeqElement Result
        {
            get
            {
                bool c = m_ctrl.Checked, s = m_shift.Checked, a = m_alt.Checked;
                int n = (int)m_count.Value;
                if (m_rbChar.Checked && m_char.Text.Length > 0)
                    return SeqElement.NewChar(m_char.Text[0], c, s, a);
                string name = (m_special.SelectedItem != null) ? m_special.SelectedItem.ToString() : "TAB";
                return SeqElement.NewSpecial(name, c, s, a, n);
            }
        }

        public KeyPickDialog(SeqElement preset)
        {
            Text = "Key";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(380, 230);

            Label lm = new Label { Text = "Modifiers", AutoSize = true, Location = new Point(16, 16) };
            Controls.Add(lm);
            m_ctrl = new CheckBox { Text = "Ctrl", AutoSize = true, Location = new Point(110, 14) };
            m_shift = new CheckBox { Text = "Shift", AutoSize = true, Location = new Point(190, 14) };
            m_alt = new CheckBox { Text = "Alt", AutoSize = true, Location = new Point(280, 14) };
            Controls.Add(m_ctrl); Controls.Add(m_shift); Controls.Add(m_alt);

            m_rbSpecial = new RadioButton { Text = "Special key", AutoSize = true, Location = new Point(16, 56), Checked = true };
            Controls.Add(m_rbSpecial);
            m_special = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(150, 52), Size = new Size(200, 24) };
            m_special.Items.AddRange(SeqCodec.SpecialKeyNames);
            if (m_special.Items.Count > 0) m_special.SelectedIndex = 0;
            Controls.Add(m_special);

            m_rbChar = new RadioButton { Text = "Character", AutoSize = true, Location = new Point(16, 96) };
            Controls.Add(m_rbChar);
            m_char = new TextBox { Location = new Point(150, 92), Size = new Size(50, 24), MaxLength = 1, Enabled = false };
            Controls.Add(m_char);

            Label lc = new Label { Text = "Repeat", AutoSize = true, Location = new Point(16, 136) };
            Controls.Add(lc);
            m_count = new NumericUpDown { Location = new Point(150, 132), Size = new Size(60, 24), Minimum = 1, Maximum = 50, Value = 1 };
            Controls.Add(m_count);

            m_rbSpecial.CheckedChanged += delegate { SyncEnabled(); };
            m_rbChar.CheckedChanged += delegate { SyncEnabled(); };

            Preload(preset);
            SyncEnabled();

            FieldEditDialog.AddOkCancel(this);
        }

        public KeyPickDialog() : this(null) { }

        private void Preload(SeqElement e)
        {
            if (e == null || e.Kind != SeqKind.Key) return;
            m_ctrl.Checked = e.Ctrl;
            m_shift.Checked = e.Shift;
            m_alt.Checked = e.Alt;
            if (e.SpecialKey.Length > 0)
            {
                m_rbSpecial.Checked = true;
                int idx = m_special.Items.IndexOf(e.SpecialKey.ToUpperInvariant());
                if (idx >= 0) m_special.SelectedIndex = idx;
                m_count.Value = Settings.Clamp(e.Count, 1, 50);
            }
            else
            {
                m_rbChar.Checked = true;
                m_char.Text = e.KeyChar.ToString();
            }
        }

        private void SyncEnabled()
        {
            m_special.Enabled = m_rbSpecial.Checked;
            m_char.Enabled = m_rbChar.Checked;
            m_count.Enabled = m_rbSpecial.Checked;
            if (m_rbChar.Checked) m_char.Focus();
        }
    }

    /// <summary>Introduce/editeaza o pauza (ms).</summary>
    internal sealed class DelayDialog : Form
    {
        private NumericUpDown m_ms;
        public int DelayMs { get { return (int)m_ms.Value; } }

        public DelayDialog(int def)
        {
            Text = "Delay";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(300, 110);

            Label l = new Label { Text = "Delay", AutoSize = true, Location = new Point(16, 24) };
            Controls.Add(l);
            m_ms = new NumericUpDown { Location = new Point(90, 20), Size = new Size(90, 24), Minimum = 0, Maximum = 60000, Value = Settings.Clamp(def, 0, 60000), TextAlign = HorizontalAlignment.Right };
            Controls.Add(m_ms);
            Label lms = new Label { Text = "ms", AutoSize = true, Location = new Point(188, 24) };
            Controls.Add(lms);

            FieldEditDialog.AddOkCancel(this);
        }
    }

    /// <summary>Introduce/editeaza un text literal in script.</summary>
    internal sealed class TextInputDialog : Form
    {
        private TextBox m_text;
        public string Value { get { return m_text.Text; } }

        public TextInputDialog(string initial)
        {
            Text = "Text";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; MaximizeBox = false; ShowInTaskbar = false;
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(380, 120);

            Label l = new Label { Text = "Text", AutoSize = true, Location = new Point(16, 24) };
            Controls.Add(l);
            m_text = new TextBox { Location = new Point(70, 20), Size = new Size(294, 24), Text = initial ?? "" };
            Controls.Add(m_text);

            FieldEditDialog.AddOkCancel(this);
        }
    }
}
