using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GPTCursor;

internal static class Native
{
    internal const uint Arrow = 32512, Hand = 32649;
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] internal static extern int GetClassName(nint window, System.Text.StringBuilder name, int count);
    [DllImport("user32.dll")] internal static extern bool GetWindowRect(nint window, out Rect rect);
    internal static int InstallCount { get; private set; }
    internal delegate nint MouseHook(int code, nint message, nint data);
    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)] internal static extern nint SetWindowsHookEx(int kind, MouseHook callback, nint module, uint thread);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)] internal static extern nint GetModuleHandle(string? module);
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagInitialize();
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagUninitialize();
    [DllImport("Magnification.dll", SetLastError = true)] internal static extern bool MagShowSystemCursor(bool show);
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] internal struct CursorInfo { public int Size; public uint Flags; public nint Cursor; public Point Position; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { public byte Operation, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] internal struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)] public bool IsIcon;
        public uint XHotspot, YHotspot;
        public nint Mask, Color;
    }
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SetSystemCursor(nint cursor, uint id);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint CreateIconIndirect(ref IconInfo info);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint CreateCursor(nint instance, int hotX, int hotY, int width, int height, byte[] andMask, byte[] xorMask);
    [DllImport("user32.dll")] internal static extern bool DestroyCursor(nint cursor);
    [DllImport("user32.dll")] internal static extern bool DestroyIcon(nint icon);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool GetCursorInfo(ref CursorInfo info);
    [DllImport("user32.dll")] internal static extern nint GetDC(nint window);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(nint window, nint dc);
    [DllImport("gdi32.dll")] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc, nint obj);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(nint window, nint screenDC, ref Point destination, ref Size size, nint sourceDC, ref Point source, uint key, ref Blend blend, uint flags);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window, nint after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] internal static extern bool ShowWindow(nint window, int command);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetGuiResources(nint process, uint flags);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] internal static extern nint LoadCursor(nint instance, nint id);
    [DllImport("user32.dll")] internal static extern nint CopyIcon(nint icon);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool GetIconInfo(nint icon, out IconInfo info);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool SystemParametersInfo(uint action, uint param, nint data, uint flags);
    [DllImport("user32.dll")] internal static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(nint hwnd, int id);

    internal static void RestoreScheme()
    {
        if (!SystemParametersInfo(0x0057, 0, 0, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    // SetSystemCursor takes ownership of a successful handle. Never pass shared handles.
    internal static void Install(nint cursor, uint role)
    {
        if (cursor == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (SetSystemCursor(cursor, role)) { InstallCount++; return; }
        int error = Marshal.GetLastWin32Error();
        DestroyCursor(cursor);
        throw new Win32Exception(error);
    }
}
