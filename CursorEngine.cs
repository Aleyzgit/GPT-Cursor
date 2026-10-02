using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GPTCursor;

internal sealed class CursorEngine : IDisposable
{
    internal static readonly uint[] AllRoles = [32512, 32513, 32514, 32515, 32516, 32642, 32643, 32644, 32645, 32646, 32648, 32649, 32650];
    private readonly CursorRenderer renderer = new();
    private readonly Motion motion = new();
    private readonly ClickMotion clicks = new();
    private readonly CursorSmoothing smoothing = new();
    private readonly DesktopPolicy desktop = new();
    private bool refreshVisibility, magnificationChangesFlags;
    private MouseClickListener? clickListener;
    private EventWaitHandle? guardStop;
    private Process? guard;
    private CursorOverlay? overlay;
    private bool initialized, cursorHidden;
    private readonly HashSet<nint> specialPointers = [];
    private (Pose, int)? lastFrame;
    internal bool Active { get; private set; }
    internal int Size { get; set; } = 32;
    internal AnimationOptions Options { get; set; } = new();
    internal bool Animate { get => Options.Animation; set => Options.Animation = value; }
    internal bool AllPointers { get; set; }
    internal Pose Pose { get; private set; } = new(0, 1, 1, 0);
    internal int VisibleFrames { get; private set; }
    internal int HiddenFrames { get; private set; }
    internal static bool HasCursorX()
    {
        var processes = Process.GetProcessesByName("cursorqt_core");
        bool found = processes.Length != 0;
        foreach (var process in processes) process.Dispose();
        return found;
    }
    internal void Start()
    {
        if (Active) return;
        string token = Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\GPTCursorReady-" + token);
        guardStop = new EventWaitHandle(false, EventResetMode.ManualReset, @"Local\GPTCursorStop-" + token);
        try
        {
            if (!Native.MagInitialize()) throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
            start.ArgumentList.Add("--watchdog"); start.ArgumentList.Add(Environment.ProcessId.ToString());
            start.ArgumentList.Add(token); start.ArgumentList.Add("visibility");
            guard = Process.Start(start) ?? throw new InvalidOperationException("Cursor recovery could not be started.");
            if (!ready.WaitOne(TimeSpan.FromSeconds(5))) throw new TimeoutException("Cursor recovery is not ready.");
            specialPointers.Clear();
            foreach (uint role in AllRoles.Where(r => r != Native.Arrow && r != Native.Hand)) specialPointers.Add(Native.LoadCursor(0, (nint)role));
            overlay = new CursorOverlay();
            if (!Native.GetCursorPos(out var pointer)) throw new InvalidOperationException("Please start this app in your interactive Windows session.");
            using (var first = renderer.Render(new Pose(0, 1, 1, 0), Size)) overlay.Present(first, pointer);
            var beforeHide = new Native.CursorInfo { Size = Marshal.SizeOf<Native.CursorInfo>() };
            Native.GetCursorInfo(ref beforeHide);
            SetHidden(true);
            var afterHide = new Native.CursorInfo { Size = Marshal.SizeOf<Native.CursorInfo>() };
            Native.GetCursorInfo(ref afterHide);
            magnificationChangesFlags = (beforeHide.Flags & 1) != 0 && (afterHide.Flags & 1) == 0;
            clicks.Reset();
            smoothing.Reset(pointer.X, pointer.Y);
            clickListener = new MouseClickListener(PreviewClick);
            Active = true; motion.Reset(); lastFrame = null;
            Tick(1.0 / 60);
        }
        catch { Stop(); throw; }
    }
    private void SetHidden(bool hidden)
    {
        if (cursorHidden == hidden && !refreshVisibility) return;
        if (!Native.MagShowSystemCursor(!hidden)) throw new Win32Exception(Marshal.GetLastWin32Error());
        cursorHidden = hidden;
        refreshVisibility = false;
    }
    internal void Tick(double dt)
    {
        if (!Native.GetCursorPos(out var point)) { SuspendForDesktop(); return; }
        var previous = Pose;
        var rawPose = motion.Update(point.X, point.Y, dt, Options);
        if (Options.HoldHeadingAtRest && !motion.HeadingChanged) smoothing.HoldRotation(previous.Rotation);
        var smoothedPose = smoothing.Effects(rawPose, dt, Options);
        if (Options.HoldHeadingAtRest && !motion.HeadingChanged) smoothedPose = smoothedPose with { Rotation = previous.Rotation, Axis = previous.Axis };
        Pose = clicks.Apply(smoothedPose, dt, Options);
        bool dragging = (Native.GetAsyncKeyState(1) & 0x8000) != 0 || (Native.GetAsyncKeyState(2) & 0x8000) != 0 || (Native.GetAsyncKeyState(4) & 0x8000) != 0;
        var position = smoothing.Position(point.X, point.Y, dt, Options, dragging);
        if (!Active) return;
        if (desktop.ShouldPause(Options))
        {
            smoothing.Position(point.X, point.Y, dt, Options, true);
            SetHidden(false); overlay?.Conceal(); HiddenFrames++; return;
        }
        var info = new Native.CursorInfo { Size = Marshal.SizeOf<Native.CursorInfo>() };
        if (!Native.GetCursorInfo(ref info)) { SuspendForDesktop(); return; }
        bool special = !AllPointers && specialPointers.Contains(info.Cursor);
        if (special || info.Cursor == 0 || (info.Flags & 2) != 0 || (!magnificationChangesFlags && (info.Flags & 1) == 0))
        {
            smoothing.Position(point.X, point.Y, dt, Options, true);
            SetHidden(false); HiddenFrames++; overlay?.Conceal(); return;
        }
        SetHidden(true);
        var visualPoint = new Native.Point { X = (int)Math.Round(position.X), Y = (int)Math.Round(position.Y) };
        VisibleFrames++;
        var frame = (new Pose(Math.Round(Pose.Rotation * 2) / 2, Math.Round(Pose.Stretch, 2),
            Math.Round(Pose.Squash, 2), Math.Abs(Pose.Squash - 1) > .005 ? Math.Round(Pose.Axis / 3) * 3 : 0,
            Math.Round(Pose.Scale, 3), Math.Round(Pose.LeftRing, 2), Math.Round(Pose.RightRing, 2)), Size);
        if (lastFrame == frame) { overlay?.MoveTo(visualPoint); return; }
        using var bitmap = renderer.Render(Pose, Size);
        overlay!.Present(bitmap, visualPoint); lastFrame = frame;
    }
    private void SuspendForDesktop()
    {
        overlay?.Conceal();
        if (initialized) Native.MagShowSystemCursor(true);
        refreshVisibility = true;
    }
    internal void Stop()
    {
        clickListener?.Dispose(); clickListener = null; clicks.Reset();
        // Do not modify any cursor image managed by Windows or MouseX.
        if (initialized)
        {
            if (!Native.MagShowSystemCursor(true)) throw new Win32Exception(Marshal.GetLastWin32Error());
            cursorHidden = false;
        }
        Active = false; overlay?.Dispose(); overlay = null;
        guardStop?.Set(); guardStop?.Dispose(); guardStop = null;
        if (guard != null) { guard.WaitForExit(1500); guard.Dispose(); guard = null; }
        if (initialized) { Native.MagUninitialize(); initialized = false; }
        lastFrame = null;
    }
    internal void PreviewClick(bool rightButton)
    {
        if (Active && Native.GetCursorPos(out var point)) smoothing.Position(point.X, point.Y, 0, Options, true);
        if (rightButton ? Options.RightClick : Options.LeftClick) clicks.Trigger(rightButton);
    }
    public void Dispose() { Stop(); renderer.Dispose(); }
    internal static bool IsTransparentPointer(nint cursor)
    {
        if (!Native.GetIconInfo(cursor, out var info)) return false;
        try
        {
            if (info.Color != 0 || info.XHotspot != 0 || info.YHotspot != 0) return false;
            using var mask = Bitmap.FromHbitmap(info.Mask);
            for (int y = 0; y < mask.Height; y++) for (int x = 0; x < mask.Width; x++)
                if (mask.GetPixel(x, y).R != (y < mask.Height / 2 ? 255 : 0)) return false;
            return true;
        }
        finally { Native.DeleteObject(info.Mask); Native.DeleteObject(info.Color); }
    }
    internal static void Watch(int parentId, string token, bool unused)
    {
        using var stop = EventWaitHandle.OpenExisting(@"Local\GPTCursorStop-" + token);
        using var ready = EventWaitHandle.OpenExisting(@"Local\GPTCursorReady-" + token);
        if (!Native.MagInitialize()) return;
        try
        {
            using var parent = Process.GetProcessById(parentId);
            ready.Set();
            while (!stop.WaitOne(200))
            {
                if (!parent.HasExited) continue;
                Native.MagShowSystemCursor(true); return;
            }
        }
        finally { Native.MagUninitialize(); }
    }
}
