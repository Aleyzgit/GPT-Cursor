namespace GPTCursor;

internal static class InstanceControl
{
    internal static string MutexName => @"Local\GPTCursor-1-" + Environment.UserName;
    internal static string QuitEvent => @"Local\GPTCursorQuit-" + Environment.UserName;
    internal static int Stop()
    {
        try
        {
            using var signal = EventWaitHandle.OpenExisting(QuitEvent); signal.Set();
            using var mutex = new Mutex(false, MutexName);
            bool acquired;
            try { acquired = mutex.WaitOne(8000); } catch (AbandonedMutexException) { acquired = true; }
            if (acquired) mutex.ReleaseMutex();
            return acquired ? 0 : 1;
        }
        catch (WaitHandleCannotBeOpenedException) { return 0; }
    }
}
