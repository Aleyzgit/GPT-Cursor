using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GPTCursor;

// A one-shot high-resolution wait follows absolute deadlines, avoiding timer drift.
// Only one UI callback may be pending, so a busy desktop never queues stale frames.
internal sealed class FrameTimer(Control owner) : IDisposable
{
    private readonly ManualResetEvent stop = new(false);
    private Thread? worker;
    private int pending, generation, fps = 240;
    internal int Fps { get => Volatile.Read(ref fps); set => Volatile.Write(ref fps, Math.Clamp(value, 30, 360)); }
    internal event EventHandler? Tick;
    internal event Action<Exception>? Failed;
    internal void Start()
    {
        if (worker != null) return;
        var handle = CreateWaitableTimerEx(0, 0, 2, 0x1F0003);
        if (handle.IsInvalid) { handle.Dispose(); throw new Win32Exception(Marshal.GetLastWin32Error()); }
        stop.Reset(); pending = 0;
        int current = ++generation;
        worker = new Thread(() => Run(handle, current)) { IsBackground = true, Name = "GPT Cursor frame clock" };
        worker.Start();
    }
    private void Run(SafeWaitHandle handle, int current)
    {
        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset);
        signal.SafeWaitHandle = handle;
        WaitHandle[] waits = [stop, signal];
        double next = Stopwatch.GetTimestamp();
        try
        {
            while (!stop.WaitOne(0))
            {
                double period = (double)Stopwatch.Frequency / Fps;
                next += period;
                double now = Stopwatch.GetTimestamp();
                if (next < now) next = now + period;
                long due = -Math.Max(1L, (long)((next - now) * 10_000_000 / Stopwatch.Frequency));
                if (!SetWaitableTimer(handle, ref due, 0, 0, 0, false)) throw new Win32Exception(Marshal.GetLastWin32Error());
                if (WaitHandle.WaitAny(waits) == 0) return;
                if (Interlocked.CompareExchange(ref pending, 1, 0) != 0) continue;
                owner.BeginInvoke((Action)(() =>
                {
                    if (current != generation) return;
                    try { Tick?.Invoke(this, EventArgs.Empty); }
                    finally { if (current == generation) Interlocked.Exchange(ref pending, 0); }
                }));
            }
        }
        catch (Exception e)
        {
            if (stop.WaitOne(0)) return;
            try { owner.BeginInvoke((Action)(() => { if (current == generation) Failed?.Invoke(e); })); }
            catch (InvalidOperationException) { }
        }
    }
    internal void Stop()
    {
        generation++; stop.Set(); worker?.Join(); worker = null;
    }
    public void Dispose() { Stop(); stop.Dispose(); }
    [DllImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", SetLastError = true)]
    private static extern SafeWaitHandle CreateWaitableTimerEx(nint attributes, nint name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long due, int period, nint callback, nint arg, bool resume);
}
