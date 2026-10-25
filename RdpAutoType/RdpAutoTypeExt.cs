using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

using KeePass.Plugins;
using KeePassLib;

namespace RdpAutoType
{
    /// <summary>
    /// RdpAutoType - plugin KeePass pentru autotype prin RDP imbricat.
    ///
    /// Flux: hotkey global (Ctrl+Alt+Space) -> meniu la cursor cu intrarile ->
    /// la selectie tasteaza user {TAB} parola (optional {ENTER}) prin motorul
    /// scan-code, cu delay-uri reglabile din Setari.
    /// </summary>
    public sealed class RdpAutoTypeExt : Plugin
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private const uint ActivationVk = 0x20; // VK_SPACE

        private IPluginHost m_host = null;
        private GlobalHotkey m_hotkey = null;
        private GlobalMouseHook m_mouseHook = null;
        private PopupMenu m_popup = null;
        private Settings m_settings = null;
        private ToolStripMenuItem m_editMenuItem = null;

        /// <summary>Fisier de version-info pentru verificarea de update din KeePass.</summary>
        public override string UpdateUrl
        {
            get { return "https://raw.githubusercontent.com/bogdandaja/RdpAutoType/main/version.txt"; }
        }

        public override bool Initialize(IPluginHost host)
        {
            if (host == null) return false;
            m_host = host;
            m_settings = Settings.Load(m_host);
            m_popup = new PopupMenu(m_host, delegate { return m_settings; });

            Form mainForm = m_host.MainWindow;
            if (mainForm != null && mainForm.IsHandleCreated && mainForm.InvokeRequired)
                mainForm.Invoke((MethodInvoker)delegate { CreateActivators(); });
            else
                CreateActivators();

            return true;
        }

        private void CreateActivators()
        {
            // Hotkey global pe tastatura.
            m_hotkey = new GlobalHotkey(
                GlobalHotkey.MOD_CONTROL | GlobalHotkey.MOD_ALT,
                ActivationVk);
            m_hotkey.HotkeyPressed += OnHotkey;

            // Activator pe mouse (optional, configurabil din Settings).
            m_mouseHook = new GlobalMouseHook();
            m_mouseHook.Activated += OnMouseActivated;
            ApplyMouseSettings();
        }

        /// <summary>(Re)aplica setarea mouse: instaleaza/configureaza hook-ul daca
        /// e activat, altfel il dezinstaleaza.</summary>
        private void ApplyMouseSettings()
        {
            if (m_mouseHook == null) return;
            if (m_settings != null && m_settings.MouseEnabled)
            {
                GlobalMouseHook.Button b;
                switch (m_settings.MouseButton)
                {
                    case 0: b = GlobalMouseHook.Button.Left; break;
                    case 2: b = GlobalMouseHook.Button.Right; break;
                    default: b = GlobalMouseHook.Button.Middle; break;
                }
                bool wantSingle = (m_settings.MouseSingleAction != Settings.ActDoNothing);
                bool wantDouble = (m_settings.MouseDoubleAction != Settings.ActDoNothing);
                m_mouseHook.Configure(b, m_settings.MouseCtrl,
                    m_settings.MouseShift, m_settings.MouseAlt,
                    wantSingle, wantDouble);
            }
            else
            {
                m_mouseHook.Dispose(); // dezinstaleaza daca era pornit
            }
        }

        public override void Terminate()
        {
            if (m_hotkey != null)
            {
                m_hotkey.HotkeyPressed -= OnHotkey;
                m_hotkey.Dispose();
                m_hotkey = null;
            }
            if (m_mouseHook != null)
            {
                m_mouseHook.Activated -= OnMouseActivated;
                m_mouseHook.Dispose();
                m_mouseHook = null;
            }
            m_popup = null;
            m_host = null;
        }

        public override ToolStripMenuItem GetMenuItem(PluginMenuType t)
        {
            // Right-click pe o intrare: "Edit Login Script..."
            if (t == PluginMenuType.Entry)
            {
                ToolStripMenuItem edit = new ToolStripMenuItem("Edit Login Script...");
                try { edit.Image = LoginScriptForm.MakeIconBitmap(16); } catch { }
                edit.Click += delegate { ShowLoginScriptEditor(); };
                return edit;
            }

            if (t != PluginMenuType.Main) return null;

            ToolStripMenuItem root = new ToolStripMenuItem("RdpAutoType");
            try { root.Image = LoginScriptForm.MakeIconBitmap(16); } catch { }

            ToolStripMenuItem popup = new ToolStripMenuItem();
            if (m_hotkey != null && m_hotkey.Registered)
                popup.Text = "Entries popup (Ctrl+Alt+Space) - or click here (3s)";
            else
            {
                int err = (m_hotkey != null) ? m_hotkey.LastError : -1;
                popup.Text = "Entries popup (hotkey unavailable, err=" + err + ") - click here (3s)";
            }
            try { popup.Image = LoginScriptForm.MakeIconBitmap(16); } catch { }
            popup.Click += delegate { ShowPopup(true); };
            root.DropDownItems.Add(popup);

            ToolStripMenuItem editScript = new ToolStripMenuItem("Edit Login Script (selected entry)...");
            try { editScript.Image = LoginScriptForm.MakeIconBitmap(16); } catch { }
            editScript.Click += delegate { ShowLoginScriptEditor(); };
            root.DropDownItems.Add(editScript);
            m_editMenuItem = editScript;
            ApplyEditShortcut();

            ToolStripMenuItem settings = new ToolStripMenuItem("Settings...");
            try { settings.Image = LoginScriptForm.MakeIconBitmap(16); } catch { }
            settings.Click += delegate { ShowSettings(); };
            root.DropDownItems.Add(settings);

            ToolStripMenuItem diag = new ToolStripMenuItem("Diagnostics...");
            diag.Click += delegate { ShowDiagnostics(); };
            root.DropDownItems.Add(diag);

            return root;
        }

        private void ShowLoginScriptEditor()
        {
            try
            {
                PwEntry pe = m_host.MainWindow.GetSelectedEntry(true);
                if (pe == null)
                {
                    MessageBox.Show("Select an entry first.", "RdpAutoType",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                PwDatabase db = m_host.Database;
                using (LoginScriptForm f = new LoginScriptForm(pe, db))
                {
                    if (f.ShowDialog(m_host.MainWindow) == DialogResult.OK)
                        m_host.MainWindow.UpdateUI(false, null, false, null, true, null, true);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eroare: " + ex.Message, "RdpAutoType",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnHotkey(object sender, EventArgs e)
        {
            ShowPopup(false);
        }

        /// <summary>
        /// Clickul activator a fost prins. Ruleaza in callback-ul hook-ului
        /// low-level, care NU are voie sa blocheze (altfel Windows arunca hook-ul).
        /// Capturam fereastra tinta acum (e cea de sub click), alegem actiunea
        /// dupa single/double, si o programam pe thread-ul UI.
        /// </summary>
        private void OnMouseActivated(object sender, MouseActivationEventArgs e)
        {
            try
            {
                int action = e.IsDouble ? m_settings.MouseDoubleAction
                                        : m_settings.MouseSingleAction;
                if (action == Settings.ActDoNothing) return;

                IntPtr target = GetForegroundWindow();

                Form mainForm = (m_host != null) ? m_host.MainWindow : null;
                if (mainForm != null && mainForm.IsHandleCreated)
                    mainForm.BeginInvoke((MethodInvoker)delegate { Dispatch(action, target); });
            }
            catch { }
        }

        private void Dispatch(int action, IntPtr target)
        {
            try
            {
                if (m_popup == null) return;
                switch (action)
                {
                    case Settings.ActLaunchPopup:
                        m_popup.ShowAtCursor(false);
                        break;
                    case Settings.ActRunMostRecent:
                        m_popup.RunMostRecent(target, false);
                        break;
                    case Settings.ActRunAssociated:
                        m_popup.RunAssociated(target, false);
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eroare: " + ex.Message, "RdpAutoType",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Aplica scurtatura "Edit Login Script" pe item-ul de meniu
        /// (nativ KeePass): activa cand KeePass e in fata si o intrare e selectata.</summary>
        private void ApplyEditShortcut()
        {
            if (m_editMenuItem == null || m_settings == null) return;
            try
            {
                if (m_settings.EditShortcutEnabled && m_settings.EditShortcut != 0)
                {
                    m_editMenuItem.ShortcutKeys = (Keys)m_settings.EditShortcut;
                    m_editMenuItem.ShowShortcutKeys = true;
                }
                else m_editMenuItem.ShortcutKeys = Keys.None;
            }
            catch { try { m_editMenuItem.ShortcutKeys = Keys.None; } catch { } }
        }

        private void ShowDiagnostics()
        {
            try
            {
                Settings s = m_settings ?? new Settings();
                string btn = (s.MouseButton == 0) ? "Left" : (s.MouseButton == 2) ? "Right" : "Middle";
                string mods = (s.MouseCtrl ? "Ctrl " : "") + (s.MouseShift ? "Shift " : "") + (s.MouseAlt ? "Alt " : "");
                if (mods.Length == 0) mods = "(none)";

                string msg =
                    "Mouse activator\r\n" +
                    "  Single action: " + s.MouseSingleAction + " (1=Launch popup)\r\n" +
                    "  Double action: " + s.MouseDoubleAction + "\r\n" +
                    "  Button: " + btn + "\r\n" +
                    "  Modifiers: " + mods.Trim() + "\r\n" +
                    "  Hook installed: " + (m_mouseHook != null && m_mouseHook.Installed) + "\r\n\r\n" +
                    "Keyboard hotkey registered: " + (m_hotkey != null && m_hotkey.Registered) + "\r\n\r\n" +
                    "Note: if another app has a global hook on the same mouse\r\n" +
                    "button/modifier, it may consume the click before this plugin.";

                MessageBox.Show(msg, "RdpAutoType - Diagnostics",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eroare: " + ex.Message, "RdpAutoType",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowPopup(bool withDelay)
        {
            try
            {
                if (m_popup != null) m_popup.ShowAtCursor(withDelay);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eroare: " + ex.Message, "RdpAutoType",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowSettings()
        {
            try
            {
                using (SettingsForm f = new SettingsForm(m_settings))
                {
                    if (f.ShowDialog(m_host.MainWindow) == DialogResult.OK)
                    {
                        m_settings.Save(m_host);
                        ApplyMouseSettings();   // aplica imediat noua setare de mouse
                        ApplyEditShortcut();    // si noua scurtatura
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Eroare: " + ex.Message, "RdpAutoType",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
