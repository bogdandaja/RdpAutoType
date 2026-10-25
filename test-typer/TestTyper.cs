using System;
using System.Runtime.InteropServices;

// Exe de test: dupa 3 secunde, tasteaza un text in fereastra activa prin
// keybd_event cu VK + scan code real (ca motorul plugin-ului). Compilat
// atat 32-bit cat si 64-bit, ca sa testam daca bitness-ul procesului conteaza
// pentru tinte speciale (consola VMware).

internal static class TestTyper
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern short VkKeyScan(char ch);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private const uint MAPVK_VK_TO_VSC = 0;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const byte VK_SHIFT = 0x10;
    private const byte VK_CONTROL = 0x11;
    private const byte VK_MENU = 0x12;
    private const int VK_CAPITAL = 0x14;

    private const int SHIFT_FLAG = 0x01;
    private const int CTRL_FLAG = 0x02;
    private const int ALT_FLAG = 0x04;

    private static void KeyDown(byte vk)
    {
        uint scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        uint flags = (scan == 0) ? KEYEVENTF_EXTENDEDKEY : 0;
        keybd_event(vk, (byte)scan, flags, UIntPtr.Zero);
    }

    private static void KeyUp(byte vk)
    {
        uint scan = MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        uint flags = KEYEVENTF_KEYUP | ((scan == 0) ? KEYEVENTF_EXTENDEDKEY : 0);
        keybd_event(vk, (byte)scan, flags, UIntPtr.Zero);
    }

    private static void Tap(byte vk) { KeyDown(vk); KeyUp(vk); }

    private static void Type(string text, int perKeyDelayMs)
    {
        bool caps = (GetKeyState(VK_CAPITAL) & 1) != 0;
        if (caps) Tap(VK_CAPITAL);
        try
        {
            foreach (char ch in text)
            {
                short vks = VkKeyScan(ch);
                if (vks == -1) continue;
                byte vk = (byte)(vks & 0xFF);
                int ss = (vks >> 8) & 0xFF;
                bool sh = (ss & SHIFT_FLAG) != 0;
                bool ct = (ss & CTRL_FLAG) != 0;
                bool al = (ss & ALT_FLAG) != 0;
                if (sh) KeyDown(VK_SHIFT);
                if (ct) KeyDown(VK_CONTROL);
                if (al) KeyDown(VK_MENU);
                KeyDown(vk); KeyUp(vk);
                if (sh) KeyUp(VK_SHIFT);
                if (ct) KeyUp(VK_CONTROL);
                if (al) KeyUp(VK_MENU);
                if (perKeyDelayMs > 0) System.Threading.Thread.Sleep(perKeyDelayMs);
            }
        }
        finally { if (caps) Tap(VK_CAPITAL); }
    }

    private static int Main(string[] args)
    {
        string text = (args.Length > 0) ? string.Join(" ", args) : "TestTyper 123 ABC";
        bool is64 = (IntPtr.Size == 8);
        Console.WriteLine("Proces " + (is64 ? "64-bit" : "32-bit") + ".");
        Console.WriteLine("Ai 3 secunde sa dai focus pe fereastra tinta (ex. notepad in guest VMware)...");
        System.Threading.Thread.Sleep(3000);
        Type(text, 5);
        Console.WriteLine("Trimis: " + text);
        return 0;
    }
}
