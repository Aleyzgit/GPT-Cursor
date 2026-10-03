using System.Diagnostics;
using System.Text;

namespace GPTCursor;

// Shell surfaces use the native rendering path with GPT artwork; games/exclusions
// restore their own cursor. Normal overlays cannot cover protected shell bands.
internal sealed class DesktopPolicy
{
    private long nextCheck;
    private nint lastWindow;
    private bool paused;
    private string reason = "";
    internal string Reason => reason;
    internal static bool IsShell(string process, string windowClass) =>
        process.Equals("StartMenuExperienceHost", StringComparison.OrdinalIgnoreCase) ||
        process.Equals("SearchHost", StringComparison.OrdinalIgnoreCase) ||
        process.Equals("SearchApp", StringComparison.OrdinalIgnoreCase) ||
        process.Equals("ShellExperienceHost", StringComparison.OrdinalIgnoreCase) ||
        process.Equals("TextInputHost", StringComparison.OrdinalIgnoreCase) ||
        (process.Equals("explorer", StringComparison.OrdinalIgnoreCase) && windowClass == "XamlExplorerHostIslandWindow");
    internal static bool Excluded(string process, string list) => (list ?? "").Split([';', ',', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Any(item => string.Equals(Path.GetFileNameWithoutExtension(item), process, StringComparison.OrdinalIgnoreCase));
    internal static bool Covers(Rectangle window, Rectangle monitor) => window.Left <= monitor.Left && window.Top <= monitor.Top && window.Right >= monitor.Right && window.Bottom >= monitor.Bottom;
    internal bool ShouldPause(AnimationOptions options)
    {
        nint window = Native.GetForegroundWindow();
        long now = Environment.TickCount64;
        if (window == lastWindow && now < nextCheck) return paused;
        lastWindow = window; nextCheck = now + 100;
        paused = false; reason = "";
        if (window == 0) return false;
        Native.GetWindowThreadProcessId(window, out uint pid);
        if (pid == Environment.ProcessId) return false;
        string name = "";
        try { using var process = Process.GetProcessById((int)pid); name = process.ProcessName; }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { reason = "unavailable"; return paused = true; }
        var className = new StringBuilder(256); Native.GetClassName(window, className, className.Capacity);
        if (Excluded(name, options.ExcludedApps)) { reason = "excluded"; return paused = true; }
        if (IsShell(name, className.ToString())) { reason = "shell"; return paused = true; }
        if (options.PauseFullscreen && className.ToString() is not ("Progman" or "WorkerW" or "Shell_TrayWnd") && Native.GetWindowRect(window, out var rect))
        {
            var bounds = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            if (Covers(bounds, Screen.FromHandle(window).Bounds)) { reason = "fullscreen"; return paused = true; }
        }
        return false;
    }
}
