using System;
using System.Runtime.InteropServices;

namespace RdpAutoType
{
    /// <summary>
    /// Hook global de mouse (WH_MOUSE_LL). Prinde un buton configurabil
    /// (stanga/mijloc/dreapta), optional cu Ctrl/Shift/Alt, oriunde in Windows,
    /// si ridica un eveniment la single sau double click (configurabil separat,
    /// (single sau double click, configurabile separat). Clickul activator este
    /// inghitit (nu ajunge la fereastra de sub cursor).
    ///
    /// WH_MOUSE_LL este un hook low-level care poate fi gazduit in proces
    /// managed (nu trebuie injectat in alte procese), deci merge direct in C#.
    /// </summary>
    internal sealed class MouseActivationEventArgs : EventArgs
    {
        public readonly bool IsDouble;
        public MouseActivationEventArgs(bool isDouble) { IsDouble = isDouble; }
    }

    internal sealed class GlobalMouseHook : IDisposable
    {
        public enum Button { Left, Middle, Right }

        private const int WH_MOUSE_LL = 14;
        private const int HC_ACTION = 0;

        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_MBUTTONDOWN = 0x0207;

        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;
        private const int VK_MENU = 0x12;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private readonly HookProc m_proc;   // tinut ca sa nu fie colectat de GC
        private IntPtr m_hook = IntPtr.Zero;

        private Button m_button;
        private bool m_needCtrl, m_needShift, m_needAlt;
        private bool m_wantSingle, m_wantDouble;

        private uint m_lastDownTime;        // pentru detectia de double click
        private readonly uint m_doubleMs;

        public event EventHandler<MouseActivationEventArgs> Activated;

        public GlobalMouseHook()
        {
            m_proc = HookCallback;
            uint d = GetDoubleClickTime();
            m_doubleMs = (d > 0) ? d : 500;
        }

        public bool Installed { get { return m_hook != IntPtr.Zero; } }

        /// <summary>Configureaza si (re)instaleaza hook-ul.</summary>
        public void Configure(Button button, bool ctrl, bool shift, bool alt,
            bool wantSingle, bool wantDouble)
        {
            m_button = button;
            m_needCtrl = ctrl;
            m_needShift = shift;
            m_needAlt = alt;
            m_wantSingle = wantSingle;
            m_wantDouble = wantDouble;

            if (m_hook == IntPtr.Zero)
                m_hook = SetWindowsHookEx(WH_MOUSE_LL, m_proc,
                    GetModuleHandle(null), 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode == HC_ACTION)
            {
                int msg = (int)wParam;
                if (IsTargetButtonDown(msg) && ModifiersMatch())
                {
                    uint now = TimeOf(lParam);
                    bool isDouble = (now - m_lastDownTime) <= m_doubleMs;
                    m_lastDownTime = now;

                    // double are prioritate pe al doilea click (daca e cerut);
                    // altfel single, daca e cerut.
                    bool fireDouble = isDouble && m_wantDouble;
                    bool fireSingle = !fireDouble && m_wantSingle;

                    if (fireDouble || fireSingle)
                    {
                        EventHandler<MouseActivationEventArgs> h = Activated;
                        if (h != null)
                        {
                            try { h(this, new MouseActivationEventArgs(fireDouble)); }
                            catch { }
                        }
                        return new IntPtr(1); // non-zero = inghite clickul
                    }
                }
            }
            return CallNextHookEx(m_hook, nCode, wParam, lParam);
        }

        private static uint TimeOf(IntPtr lParam)
        {
            try
            {
                MSLLHOOKSTRUCT ms = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(
                    lParam, typeof(MSLLHOOKSTRUCT));
                return ms.time;
            }
            catch { return (uint)Environment.TickCount; }
        }

        private bool IsTargetButtonDown(int msg)
        {
            switch (m_button)
            {
                case Button.Left: return msg == WM_LBUTTONDOWN;
                case Button.Middle: return msg == WM_MBUTTONDOWN;
                case Button.Right: return msg == WM_RBUTTONDOWN;
            }
            return false;
        }

        private bool ModifiersMatch()
        {
            bool ctrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool shift = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;
            bool alt = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            if (m_needCtrl != ctrl) return false;
            if (m_needShift != shift) return false;
            if (m_needAlt != alt) return false;
            return true;
        }

        public void Dispose()
        {
            if (m_hook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(m_hook);
                m_hook = IntPtr.Zero;
            }
        }
    }
}
