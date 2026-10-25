using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Exe de test pentru metoda clipboard + paste.
// Pune textul in clipboard, asteapta 3s (timp sa dai focus pe tinta, ex.
// Notepad in guest VMware), trimite Ctrl+V prin scan-code, apoi curata
// clipboard-ul dupa o scurta pauza.
//
// Functioneaza acolo unde injectarea de taste individuale nu ajunge
// (consola VMware, RDP, RDP imbricat), pentru ca paste-ul foloseste
// sincronizarea de clipboard a VMware Tools / RDP.

internal static class TestPaste
{
    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKey(uint uCode, uint uMapType);

    private const uint MAPVK_VK_TO_VSC = 0;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    private const byte VK_CONTROL = 0x11;
    private const byte VK_V = 0x56;

    private static void Down(byte vk)
    {
        byte scan = (byte)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        keybd_event(vk, scan, 0, UIntPtr.Zero);
    }

    private static void Up(byte vk)
    {
        byte scan = (byte)MapVirtualKey(vk, MAPVK_VK_TO_VSC);
        keybd_event(vk, scan, KEYEVENTF_KEYUP, UIntPtr.Zero);
    }

    private static void SendCtrlV()
    {
        Down(VK_CONTROL);
        Thread.Sleep(20);
        Down(VK_V);
        Thread.Sleep(20);
        Up(VK_V);
        Thread.Sleep(20);
        Up(VK_CONTROL);
    }

    [STAThread]
    private static int Main(string[] args)
    {
        string text = (args.Length > 0) ? string.Join(" ", args) : "RdpAutoType prin PASTE 123 ABC";

        // Salveaza clipboard-ul vechi (text), ca sa-l restauram.
        string oldClip = null;
        try { if (Clipboard.ContainsText()) oldClip = Clipboard.GetText(); } catch { }

        try { Clipboard.SetText(text); } catch (Exception ex)
        {
            Console.WriteLine("Nu pot scrie in clipboard: " + ex.Message);
            return 1;
        }

        Console.WriteLine("Text in clipboard. Ai 3 secunde sa dai focus pe tinta (ex. notepad in guest VMware)...");
        Thread.Sleep(3000);

        SendCtrlV();
        Console.WriteLine("Ctrl+V trimis.");

        // Lasa guest-ul sa citeasca clipboard-ul sincronizat inainte de a-l
        // restaura (sincronizarea VMware/RDP are latenta).
        Thread.Sleep(1500);
        try
        {
            if (oldClip != null) Clipboard.SetText(oldClip);
            else Clipboard.Clear();
        }
        catch { }

        Console.WriteLine("Gata.");
        return 0;
    }
}
