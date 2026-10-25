using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RdpAutoType
{
    /// <summary>
    /// Inregistreaza un hotkey global (RegisterHotKey) si ridica un eveniment
    /// cand e apasat, oriunde in Windows. Foloseste o fereastra message-only
    /// care prinde WM_HOTKEY. KeePass 2.61 nu are HotKeyManager public.
    /// </summary>
    internal sealed class GlobalHotkey : IDisposable
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0xA711;

        // Fereastra message-only (parinte special HWND_MESSAGE = -3).
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        private readonly MessageWindow m_window;
        private bool m_registered = false;

        public event EventHandler HotkeyPressed;

        public int LastError { get; private set; }

        public GlobalHotkey(uint modifiers, uint vk)
        {
            m_window = new MessageWindow(this);
            m_registered = RegisterHotKey(m_window.Handle, HOTKEY_ID,
                modifiers | MOD_NOREPEAT, vk);
            if (!m_registered)
                LastError = Marshal.GetLastWin32Error();
        }

        public bool Registered { get { return m_registered; } }

        private void OnHotkey()
        {
            EventHandler h = HotkeyPressed;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (m_registered)
            {
                UnregisterHotKey(m_window.Handle, HOTKEY_ID);
                m_registered = false;
            }
            try { m_window.DestroyHandle(); } catch { }
        }

        private sealed class MessageWindow : NativeWindow
        {
            private readonly GlobalHotkey m_owner;

            public MessageWindow(GlobalHotkey owner)
            {
                m_owner = owner;
                CreateParams cp = new CreateParams();
                cp.Parent = HWND_MESSAGE; // fereastra message-only
                CreateHandle(cp);
            }

            protected override void WndProc(ref Message m)
            {
                if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
                    m_owner.OnHotkey();
                base.WndProc(ref m);
            }
        }
    }
}
