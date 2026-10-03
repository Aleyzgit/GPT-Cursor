namespace GPTCursor;

// Protected shell surfaces cover normal overlays. Use our resting artwork as a
// native pointer only while that shell is foreground, then restore exact copies.
internal sealed class ShellCursor : IDisposable
{
    private readonly Dictionary<uint, nint> originals = [];
    private int size;
    private long nextCheck;
    private bool retried;
    internal bool Active => originals.Count != 0;
    internal bool Overridden { get; private set; }
    internal void Show(CursorRenderer renderer, int requestedSize, long? clock = null)
    {
        long now = clock ?? Environment.TickCount64;
        if (Active && size == requestedSize)
        {
            if (Overridden || now < nextCheck) return;
            nextCheck = now + 250;
            if (Matches(Native.Arrow) && Matches(Native.Hand)) return;
            // A shell transition may reload the Windows scheme once. Retry once,
            // then yield to another cursor owner instead of fighting every frame.
            if (retried) { Overridden = true; return; }
            retried = true;
        }
        if (!Active)
        {
            foreach (uint role in new[] { Native.Arrow, Native.Hand })
            {
                nint copy = Native.CopyIcon(Native.LoadCursor(0, (nint)role));
                if (copy == 0) { Dispose(); throw new InvalidOperationException("Could not save the shell cursor."); }
                originals.Add(role, copy);
            }
        }
        using var bitmap = renderer.Render(new Pose(0, 1, 1, 0), requestedSize);
        size = requestedSize;
        foreach (uint role in originals.Keys) Native.Install(CursorRenderer.CreateCursor(bitmap), role);
        nextCheck = now + 250;
    }
    private bool Matches(uint role)
    {
        if (!Native.GetIconInfo(Native.LoadCursor(0, (nint)role), out var info)) return false;
        try { return info.Color != 0 && info.XHotspot == size * 3 / 2 && info.YHotspot == size * 3 / 2; }
        finally { Native.DeleteObject(info.Color); Native.DeleteObject(info.Mask); }
    }
    internal void Restore()
    {
        // Preserve a theme selected by the user or another app while Start was open.
        foreach (var pair in originals)
            if (Matches(pair.Key)) Native.Install(Native.CopyIcon(pair.Value), pair.Key);
        Dispose();
    }
    public void Dispose()
    {
        foreach (nint cursor in originals.Values) Native.DestroyCursor(cursor);
        originals.Clear(); size = 0; nextCheck = 0; retried = Overridden = false;
    }
}
