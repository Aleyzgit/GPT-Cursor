namespace GPTCursor;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.FirstOrDefault() == "--watchdog")
        {
            try { CursorEngine.Watch(int.Parse(args[1]), args[2], args[3] == "all"); return 0; }
            catch { return 1; }
        }
        ApplicationConfiguration.Initialize();
        if (args.FirstOrDefault() == "--quit") return InstanceControl.Stop();
        if (args.FirstOrDefault() == "--self-test") return Checks.Run(args.ElementAtOrDefault(1) ?? "test-output");
        if (args.FirstOrDefault() == "--ui-preview") return Checks.UiPreview(args.ElementAtOrDefault(1) ?? "test-output");
        if (args.FirstOrDefault() == "--ui-demo")
        {
            using var demo = new MainForm(new Preferences { ShortcutEnabled = false }, persistSettings: false);
            demo.Text = "GPT Cursor — UI preview";
            Application.Run(demo); return 0;
        }
        if (args.FirstOrDefault() == "--shell-test") return Checks.Shell(args.ElementAtOrDefault(1) ?? "test-output");
        if (args.FirstOrDefault() == "--snapshot") { Checks.Snapshot(args[1]); return 0; }
        if (args.FirstOrDefault() == "--overlay-test") return Checks.Overlay(args.ElementAtOrDefault(1) ?? "test-output");
        if (args.FirstOrDefault() == "--frame-test") return Checks.FrameRate(args.ElementAtOrDefault(1) ?? "test-output");
        using var mutex = new Mutex(true, InstanceControl.MutexName, out bool first);
        if (!first && args.Contains("--autostart")) return 0;
        if (!first) { MessageBox.Show(Preferences.Load().Language == "de" ? "GPT Cursor läuft bereits. Du findest es im Infobereich neben der Uhr." : "GPT Cursor is already running. You can find it in the system tray.", "GPT Cursor"); return 0; }
        if (args.FirstOrDefault() == "--restore") { Native.RestoreScheme(); return 0; }
        if (args.FirstOrDefault() is "--smoke-test" or "--crash-probe")
        {
            try
            {
                using var engine = new CursorEngine();
                engine.Start();
                if (args[0] == "--crash-probe")
                {
                    File.WriteAllText(args[1], Environment.ProcessId.ToString());
                    for (int i = 0; i < 1800; i++) { Application.DoEvents(); engine.Tick(1.0 / 60); Thread.Sleep(16); }
                }
                else
                {
                    int startInstalls = Native.InstallCount;
                    for (int i = 0; i < 180; i++)
                    {
                        Application.DoEvents(); engine.Tick(1.0 / 60); Thread.Sleep(16);
                        if (i == 90) Checks.ActualSnapshot(args[1] + "-active");
                    }
                    int frameInstalls = Native.InstallCount - startInstalls;
                    engine.Stop();
                    File.WriteAllText(args[1], $"PASS: activate, 180 updates, restore, watchdog stopped.\nSystem cursor installations during animation: {frameInstalls}\nVisible overlay frames: {engine.VisibleFrames}\nHidden / other cursor frames: {engine.HiddenFrames}\nMouseX running: {CursorEngine.HasCursorX()}");
                    if (frameInstalls != 0) throw new InvalidOperationException("Per-frame cursor replacement detected.");
                    if (engine.VisibleFrames == 0) throw new InvalidOperationException("Kein sichtbares Cursor-Overlay während des Tests.");
                }
                return 0;
            }
            catch (Exception e) { File.WriteAllText(args[1], "FAIL: " + e); return 1; }
        }
        using var form = new MainForm();
        if (args.Contains("--activate") || args.Contains("--autostart")) form.Shown += (_, _) => form.ActivateCursor();
        if (args.Contains("--autostart"))
        {
            form.WindowState = FormWindowState.Minimized; form.ShowInTaskbar = false;
            form.Shown += (_, _) => { form.Hide(); form.WindowState = FormWindowState.Normal; form.ShowInTaskbar = true; };
        }
        Application.ThreadException += (_, e) => form.HandleError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, _) => { try { form.StopCursor(); } catch { } };
        Application.Run(form);
        return 0;
    }
}
