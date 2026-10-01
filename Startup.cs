using Microsoft.Win32;

namespace GPTCursor;

internal static class Startup
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "GPTCursor";
    internal static string Command(string executable) => $"\"{executable}\" --autostart";
    internal static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return string.Equals(key?.GetValue(ValueName) as string, Command(Environment.ProcessPath!), StringComparison.OrdinalIgnoreCase);
        }
    }
    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (enabled) key.SetValue(ValueName, Command(Environment.ProcessPath!), RegistryValueKind.String);
        else key.DeleteValue(ValueName, false);
    }
}
