namespace GPTCursor;

internal sealed class GlobalShortcut(nint window) : IDisposable
{
    internal int Id { get; private set; }
    private uint modifiers, key;
    internal bool TrySet(uint nextModifiers, uint nextKey)
    {
        if (!Valid(nextModifiers, nextKey)) return false;
        if (Id != 0 && modifiers == nextModifiers && key == nextKey) return true;
        int nextId = Id == 1 ? 2 : 1;
        // Keep the working shortcut registered until its replacement succeeds.
        if (!Native.RegisterHotKey(window, nextId, nextModifiers | 0x4000, nextKey)) return false;
        if (Id != 0) Native.UnregisterHotKey(window, Id);
        Id = nextId; modifiers = nextModifiers; key = nextKey;
        return true;
    }
    internal static bool Valid(uint modifiers, uint key) => (modifiers & ~7u) == 0 && (modifiers & 3) != 0 &&
        (key >= (uint)Keys.A && key <= (uint)Keys.Z || key >= (uint)Keys.D0 && key <= (uint)Keys.D9 ||
         key >= (uint)Keys.F1 && key <= (uint)Keys.F11);
    internal static uint Modifiers(Keys data) =>
        (data.HasFlag(Keys.Alt) ? 1u : 0) | (data.HasFlag(Keys.Control) ? 2u : 0) | (data.HasFlag(Keys.Shift) ? 4u : 0);
    internal static string Format(uint modifiers, uint key, bool german)
    {
        var parts = new List<string>();
        if ((modifiers & 2) != 0) parts.Add(german ? "Strg" : "Ctrl");
        if ((modifiers & 1) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add(german ? "Umschalt" : "Shift");
        parts.Add(key >= (uint)Keys.D0 && key <= (uint)Keys.D9 ? ((char)key).ToString() : ((Keys)key).ToString());
        return string.Join(" + ", parts);
    }
    public void Dispose() { if (Id != 0) Native.UnregisterHotKey(window, Id); Id = 0; }
}
