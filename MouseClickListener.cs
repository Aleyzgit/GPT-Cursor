using System.ComponentModel;
using System.Runtime.InteropServices;

namespace GPTCursor;

// Observe button-down messages without consuming, delaying or synthesizing input.
internal sealed class MouseClickListener : IDisposable
{
    private readonly Native.MouseHook callback;
    private nint hook;
    internal MouseClickListener(Action<bool> pressed)
    {
        callback = (code, message, data) =>
        {
            if (code >= 0)
            {
                if (message == 0x0201) pressed(false);
                else if (message == 0x0204) pressed(true);
            }
            return Native.CallNextHookEx(0, code, message, data);
        };
        hook = Native.SetWindowsHookEx(14, callback, Native.GetModuleHandle(null), 0);
        if (hook == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public void Dispose()
    {
        if (hook == 0) return;
        Native.UnhookWindowsHookEx(hook); hook = 0;
        GC.KeepAlive(callback);
    }
}
