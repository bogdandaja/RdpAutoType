using System;
using System.Runtime.InteropServices;

namespace RdpAutoType
{
    /// <summary>
    /// Motorul de injectare taste (componenta A), conform specificatiei de
    /// comportament (clean-room):
    ///
    ///   keybd_event(vk, scanReal, extendedFlag, 0)
    ///
    /// adica VK pastrat + scan code REAL din MapVirtualKey. Consola VMware
    /// traduce scan code-ul din lParam in stream PS/2 pentru guest, deci are
    /// nevoie de scan code real; dar pastreaza si VK-ul (nu e scan pur).
    ///
    /// In plus, inainte si dupa secventa se cere ferestrei foreground sa-si
    /// aduca layout-ul la cel dorit prin WM_INPUTLANGCHANGEREQUEST. Pentru
    /// tinte "speciale" (VMware console) asta poate fi elementul care lipsea.
    /// </summary>
    internal static class ScanCodeTyper
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern short VkKeyScan(char ch);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern IntPtr GetKeyboardLayout(uint idThread);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const uint MAPVK_VK_TO_VSC = 0;

        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;

        private const byte VK_SHIFT = 0x10;
        private const byte VK_CONTROL = 0x11;
        private const byte VK_MENU = 0x12; // ALT
        private const int VK_CAPITAL = 0x14;
        private const byte VK_TAB = 0x09;

        private const int SHIFT_FLAG = 0x01;
        private const int CTRL_FLAG = 0x02;
        private const int ALT_FLAG = 0x04;

        private const byte VK_RETURN = 0x0D;

        /// <summary>Pauza intre apasare (down) si eliberare (up) a fiecarei
        /// taste. Setat din Settings inainte de tastare. 0 = fara pauza.</summary>
        public static int KeyPressDelayMs = 0;

        public static void Type(string text, int perKeyDelayMs)
        {
            if (string.IsNullOrEmpty(text)) return;

            IntPtr target = GetForegroundWindow();

            // Salveaza layout-ul firului tinta si cere-i layout-ul curent al
            // nostru, ca scan codes sa fie interpretate la fel pe tinta.
            IntPtr originalHkl = IntPtr.Zero;
            IntPtr ourHkl = IntPtr.Zero;
            uint targetThread = 0;
            if (target != IntPtr.Zero)
            {
                uint pid;
                targetThread = GetWindowThreadProcessId(target, out pid);
                originalHkl = GetKeyboardLayout(targetThread);
                ourHkl = GetKeyboardLayout(0); // layout-ul firului curent
                if (ourHkl != IntPtr.Zero)
                    PostMessage(target, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, ourHkl);
            }

            bool capsWasOn = (GetKeyState(VK_CAPITAL) & 1) != 0;
            if (capsWasOn)
                TapKey(VK_CAPITAL);

            try
            {
                foreach (char ch in text)
                {
                    short vks = VkKeyScan(ch);
                    if (vks == -1)
                        continue;

                    byte vk = (byte)(vks & 0xFF);
                    int shiftState = (vks >> 8) & 0xFF;

                    bool needShift = (shiftState & SHIFT_FLAG) != 0;
                    bool needCtrl = (shiftState & CTRL_FLAG) != 0;
                    bool needAlt = (shiftState & ALT_FLAG) != 0;

                    if (needShift) KeyDown(VK_SHIFT);
                    if (needCtrl) KeyDown(VK_CONTROL);
                    if (needAlt) KeyDown(VK_MENU);

                    KeyDown(vk);
                    KeyUp(vk);

                    if (needShift) KeyUp(VK_SHIFT);
                    if (needCtrl) KeyUp(VK_CONTROL);
                    if (needAlt) KeyUp(VK_MENU);

                    if (perKeyDelayMs > 0)
                        System.Threading.Thread.Sleep(perKeyDelayMs);
                }
            }
            finally
            {
                if (capsWasOn)
                    TapKey(VK_CAPITAL);

                // Restaureaza layout-ul original al firului tinta.
                if (target != IntPtr.Zero && originalHkl != IntPtr.Zero)
                    PostMessage(target, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, originalHkl);
            }
        }

        // keybd_event(vk, scanReal, extended, 0) - VK pastrat + scan real.
        private static void KeyDown(byte vk)
        {
            uint scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
            uint flags = (scan == 0) ? KEYEVENTF_EXTENDEDKEY : 0;
            keybd_event(vk, (byte)scan, flags, UIntPtr.Zero);
            if (KeyPressDelayMs > 0)
                System.Threading.Thread.Sleep(KeyPressDelayMs);
        }

        private static void KeyUp(byte vk)
        {
            uint scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
            uint flags = KEYEVENTF_KEYUP | ((scan == 0) ? KEYEVENTF_EXTENDEDKEY : 0);
            keybd_event(vk, (byte)scan, flags, UIntPtr.Zero);
        }

        private static void TapKey(byte vk)
        {
            KeyDown(vk);
            KeyUp(vk);
        }

        /// <summary>Trimite tasta TAB (pentru a trece intre campuri).</summary>
        public static void TypeTab()
        {
            TapKey(VK_TAB);
        }

        /// <summary>Trimite tasta Enter (submit).</summary>
        public static void TypeEnter()
        {
            TapKey(VK_RETURN);
        }

        /// <summary>Trimite o tasta (VK) cu modificatori optionali, de "count" ori.
        /// Pentru taste speciale (TAB, ENTER, F-uri) si combo-uri (Ctrl+A etc.).</summary>
        public static void SendKey(byte vk, bool ctrl, bool shift, bool alt, int count)
        {
            if (count < 1) count = 1;
            for (int i = 0; i < count; i++)
            {
                if (shift) KeyDown(VK_SHIFT);
                if (ctrl) KeyDown(VK_CONTROL);
                if (alt) KeyDown(VK_MENU);

                KeyDown(vk);
                KeyUp(vk);

                if (alt) KeyUp(VK_MENU);
                if (ctrl) KeyUp(VK_CONTROL);
                if (shift) KeyUp(VK_SHIFT);
            }
        }

        /// <summary>VK-ul pentru un caracter (pentru combo-uri gen Ctrl+A),
        /// cu layout-ul curent. Fara starea de shift.</summary>
        public static byte VkForChar(char c)
        {
            short vks = VkKeyScan(c);
            if (vks == -1) return 0;
            return (byte)(vks & 0xFF);
        }
    }
}
