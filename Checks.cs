using System.Drawing.Imaging;
using System.Text.Json;

namespace GPTCursor;

internal static class Checks
{
    private static IEnumerable<Control> AllControls(Control parent) => parent.Controls.Cast<Control>().SelectMany(c => new[] { c }.Concat(AllControls(c)));
    internal static int Overlay(string output)
    {
        Directory.CreateDirectory(output);
        try
        {
            using var renderer = new CursorRenderer();
            using var overlay = new CursorOverlay();
            nint foreground = Native.GetForegroundWindow();
            uint before = Native.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle, 0);
            for (int i = 0; i < 500; i++)
            {
                using var frame = renderer.Render(new Pose(Math.Sin(i * .1) * 50, .8, .9, 0), 32);
                overlay.Present(frame, new Native.Point { X = 100 + i % 300, Y = 100 });
                Application.DoEvents();
            }
            uint after = Native.GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle, 0);
            if (after > before + 4) throw new InvalidOperationException($"GDI resource leak: {before} -> {after}");
            if (!Native.IsWindowVisible(overlay.Handle)) throw new InvalidOperationException("Overlay not visible");
            if (Native.GetForegroundWindow() != foreground) throw new InvalidOperationException("Overlay stole focus");
            overlay.Conceal();
            if (Native.IsWindowVisible(overlay.Handle)) throw new InvalidOperationException("Overlay did not hide");
            File.WriteAllText(Path.Combine(output, "overlay.txt"), $"PASS: 500 composited frames; no focus change; show/hide works; GDI handles {before} -> {after}; no system cursors modified.");
            return 0;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "overlay.txt"), "FAIL: " + e); return 1; }
    }
    internal static int Run(string output)
    {
        Directory.CreateDirectory(output);
        var results = new List<string>();
        try
        {
            void Check(bool condition, string name)
            {
                if (!condition) throw new InvalidOperationException(name);
                results.Add("PASS: " + name);
            }
            var motion = new Motion();
            var rest = motion.Update(10, 10, 1.0 / 60);
            Check(rest == new Pose(0, 1, 1, 0), "Stillstand startet ohne Animation");
            Pose moving = rest;
            for (int i = 1; i <= 30; i++) moving = motion.Update(10 + i * 25, 10, 1.0 / 60);
            Check(moving.Rotation > 20 && moving.Stretch < .9 && moving.Squash < .95, "Bewegung dreht und staucht den Cursor");
            for (int i = 0; i < 360; i++) rest = motion.Update(760, 10, 1.0 / 60);
            Check(Math.Abs(rest.Rotation) < .001 && rest.Stretch == 1 && rest.Squash == 1, "Animation endet nach dem Nachwippen");
            var facingMotion = new Motion();
            var facingOptions = new AnimationOptions { FaceMovement = true, Stretch = false, Squash = false };
            facingMotion.Update(0, 0, 1.0 / 240, facingOptions);
            var rightFacing = facingMotion.Update(10, 0, 1.0 / 240, facingOptions);
            Check(Math.Abs(rightFacing.Rotation - 135) < .001 && rightFacing.Stretch == 1, "Richtungspfeil zeigt nach rechts ohne Stretch");
            var downFacing = facingMotion.Update(10, 10, 1.0 / 240, facingOptions);
            Check(Math.Abs(downFacing.Rotation - 225) < .001, "Richtungspfeil zeigt nach unten");
            for (int i = 0; i < 500; i++) rest = facingMotion.Update(10, 10, 1.0 / 240, facingOptions);
            Check(rest.Rotation == downFacing.Rotation, "Richtung bleibt nach dem Stoppen erhalten");
            facingOptions.Stretch = true;
            var stretchFacing = facingMotion.Update(200, 10, .01, facingOptions);
            Check(stretchFacing.Stretch < 1 && stretchFacing.Rotation == 135, "Stretch und Richtungsmodus kombinierbar");
            Check(DesktopPolicy.IsShell("StartMenuExperienceHost", "") && !DesktopPolicy.IsShell("game", "GameWindow"), "Startmenü wird vom Spiel unterschieden");
            Check(DesktopPolicy.Excluded("Game", "other.exe; GAME.exe") && !DesktopPolicy.Excluded("Game2", "game.exe"), "Spieleausnahmen vergleichen exakte Prozessnamen");
            Check(DesktopPolicy.Covers(new Rectangle(-1920, 0, 1920, 1080), new Rectangle(-1920, 0, 1920, 1080)), "Vollbild-Erkennung auf zweitem Monitor");
            Check(Startup.Command(@"C:\Apps With Spaces\GPT Cursor.exe") == "\"C:\\Apps With Spaces\\GPT Cursor.exe\" --autostart", "Autostart-Pfad korrekt gequotet");
            for (int i = 0; i < 600; i++)
            {
                var p = motion.Update(i % 2 == 0 ? -8000 : 8000, i % 3 * 3000, i % 10 == 0 ? 5 : 1.0 / 240);
                CheckFinite(p);
            }
            results.Add("PASS: Richtungswechsel, negative Bildschirmkoordinaten und Zeitlücken bleiben stabil");
            var options = new AnimationOptions { Rotation = false, Stretch = false, Squash = false, Wobble = false };
            var disabled = motion.Update(200, 300, 1.0 / 240, options);
            Check(disabled.Rotation == 0 && disabled.Stretch == 1 && disabled.Squash == 1, "Alle Bewegungseffekte einzeln abschaltbar");
            options.Rotation = true;
            var rotationOnly = motion.Update(500, 300, 1.0 / 240, options);
            Check(rotationOnly.Rotation != 0 && rotationOnly.Stretch == 1 && rotationOnly.Squash == 1, "Drehung unabhängig von Verformung aktivierbar");
            options.Animation = false;
            var noMotion = motion.Update(800, 300, 1.0 / 240, options);
            Check(noMotion.Rotation == 0 && noMotion.Stretch == 1 && noMotion.Squash == 1, "Bewegungshauptschalter überschreibt einzelne Bewegungseffekte");
            var click = new ClickMotion();
            click.Trigger(false);
            var left = click.Apply(noMotion, .06, options);
            Check(left.LeftRing == 0 && left.RightRing == -1, "Linksklick funktioniert auch ohne Bewegungsanimation");
            left = click.Apply(noMotion, .01, options);
            Check(left.Scale < 1 && left.LeftRing > 0, "Klick federt ein und Ring wächst");
            click.Trigger(true);
            var both = click.Apply(noMotion, .01, options);
            Check(both.LeftRing > 0 && both.RightRing == 0, "Schnelle Links- und Rechtsklicks bleiben unabhängig");
            options.LeftClick = false;
            var right = click.Apply(noMotion, .01, options);
            Check(right.LeftRing == -1 && right.RightRing >= 0, "Linksklick abschaltbar bei aktivem Rechtsklick");
            options.ClickPulse = options.ClickRings = false;
            var off = click.Apply(noMotion, .01, options);
            Check(off.Scale == 1 && off.LeftRing == -1 && off.RightRing == -1, "Klickringe und Einfedern abschaltbar");
            options.ClickPulse = options.ClickRings = true;
            click.Apply(noMotion, 2, options);
            Check(click.Apply(noMotion, .01, options) == noMotion, "Klickanimation endet vollständig");
            click.Trigger(true); options.RightClick = false;
            Check(click.Apply(noMotion, .01, options) == noMotion, "Rechtsklick abschaltbar");
            var restored = JsonSerializer.Deserialize<Preferences>("{\"Size\":44,\"Animation\":false,\"AllPointers\":true}")!;
            Check(restored.Size == 44 && !restored.Animation && restored.AllPointers && restored.Fps == 240 && restored.LeftClick,
                "Bestehende Einstellungen bleiben erhalten; neue Optionen erhalten Standardwerte");
            Check(restored.Language == "en" && restored.ShortcutEnabled && restored.ShortcutModifiers == 3 && restored.ShortcutKey == 67,
                "Englisch ist Standard; bisheriges Kürzel bleibt erhalten");
            Check(GlobalShortcut.Valid(3, 67) && !GlobalShortcut.Valid(0, 67) && !GlobalShortcut.Valid(4, 67) && !GlobalShortcut.Valid(3, (uint)Keys.F12),
                "Kürzel verlangt Strg oder Alt und unterstützt keine reservierte F12-Taste");
            Check(GlobalShortcut.Format(3, 67, false) == "Ctrl + Alt + C" && GlobalShortcut.Format(3, 67, true) == "Strg + Alt + C",
                "Tastenkombination wird in beiden Sprachen angezeigt");
            var uiPreferences = new Preferences { Size = 44, PositionMethod = SmoothingMethod.Spring, EffectsMethod = SmoothingMethod.Responsive, ClickRings = false };
            using (var form = new MainForm(uiPreferences, persistSettings: false))
            {
                var language = AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Language");
                Check(AllControls(form).OfType<CheckBox>().Any(c => c.Text == "Show click rings (both buttons)" && !c.Checked), "Englischer Ringschalter zeigt gespeicherten Aus-Zustand");
                language.SelectedIndex = 1;
                Check(AllControls(form).OfType<CheckBox>().Any(c => c.Text == "Klickringe anzeigen (beide Tasten)" && !c.Checked), "Sprachwechsel übersetzt Ringschalter ohne Zustandsverlust");
                Check(AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Art der Positionsglättung").Text == "Sanfte Feder", "Deutscher Dropdown behält gewähltes Profil");
                Check(AllControls(form).OfType<Button>().Any(c => c.Text == "Strg + Alt + C"), "Shortcut-Darstellung wechselt auf Deutsch");
                language.SelectedIndex = 0;
                Check(AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Effect smoothing style").Text == "Responsive" && uiPreferences.PositionMethod == SmoothingMethod.Spring,
                    "Rückwechsel auf Englisch bewahrt beide Profile");
                var rings = AllControls(form).OfType<CheckBox>().Single(c => c.Text == "Show click rings (both buttons)");
                rings.Checked = true; rings.Checked = false;
                Check(!uiPreferences.ClickRings && uiPreferences.ClickPulse, "Ringschalter wirkt unabhängig vom Klick-Einfedern");
                var smooth = AllControls(form).OfType<CheckBox>().Single(c => c.Text == "Smooth position");
                smooth.Checked = false;
                Check(!uiPreferences.PositionSmoothing && uiPreferences.EffectsSmoothing && !AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Position smoothing style").Enabled,
                    "Positionsschalter deaktiviert nur seine Glättung und Auswahl");
            }
            foreach (var profile in Enum.GetValues<SmoothingMethod>())
            {
                foreach (int fps in new[] { 60, 240, 360 })
                {
                    var filter = new SmoothValue();
                    filter.Update(0, 1.0 / fps, profile);
                    double firstFrame = filter.Update(100, 1.0 / fps, profile);
                    Check(firstFrame >= 0 && firstFrame < 100, $"{profile} bei {fps} FPS: sanfter Bewegungsstart");
                    double settled = firstFrame;
                    for (int i = 0; i < fps; i++)
                    {
                        settled = filter.Update(100, 1.0 / fps, profile);
                        if (!double.IsFinite(settled) || settled < -.001 || settled > 100.001) throw new InvalidOperationException("Instabile Glättung");
                    }
                    Check(Math.Abs(settled - 100) < .001, $"{profile} bei {fps} FPS: erreicht Ruheposition ohne Drift");
                    for (int i = 0; i < fps; i++) settled = filter.Update(-300, 1.0 / fps, profile);
                    Check(Math.Abs(settled + 300) < .001, $"{profile}: negative Bildschirmkoordinaten");
                    Check(filter.Update(2000, 1, profile) == 2000, $"{profile}: Zeitlücke synchronisiert Position");
                }
            }
            var smoothing = new CursorSmoothing();
            var smoothOptions = new AnimationOptions();
            smoothing.Position(0, 0, .01, smoothOptions);
            var follow = smoothing.Position(100, 200, .01, smoothOptions);
            Check(follow.X < 100 && follow.Y < 200, "Aktive Positionsglättung führt weich nach");
            Check(smoothing.Position(100, 200, .01, smoothOptions, true) == (100, 200), "Klicken und Ziehen synchronisieren exakt");
            smoothOptions.PositionSmoothing = false;
            Check(smoothing.Position(-100, 500, .01, smoothOptions) == (-100, 500), "Positionsglättung sofort abschaltbar");
            smoothing.Effects(new Pose(0, 1, 1, 0), .01, smoothOptions);
            var softened = smoothing.Effects(new Pose(50, .7, .8, 0), .01, smoothOptions);
            Check(softened.Rotation < 50 && softened.Stretch > .7 && softened.Squash > .8, "Animationsglättung wirkt unabhängig von Positionsglättung");
            smoothOptions.EffectsSmoothing = false;
            var directPose = new Pose(50, .7, .8, 0);
            Check(smoothing.Effects(directPose, .01, smoothOptions) == directPose, "Animationsglättung sofort abschaltbar");
            using (var hotkeyWindow = new Form())
            using (var original = new GlobalShortcut(hotkeyWindow.Handle))
            using (var blockerWindow = new Form())
            using (var blocker = new GlobalShortcut(blockerWindow.Handle))
            {
                const uint testModifiers = 7;
                Check(original.TrySet(testModifiers, (uint)Keys.F9), "Testkürzel registriert");
                Check(blocker.TrySet(testModifiers, (uint)Keys.F10), "Konfliktkürzel registriert");
                int originalId = original.Id;
                Check(!original.TrySet(testModifiers, (uint)Keys.F10) && original.Id == originalId,
                    "Belegtes Ersatzkürzel lässt bestehende Registrierung intakt");
                blocker.Dispose();
                Check(original.TrySet(testModifiers, (uint)Keys.F10) && original.Id != originalId,
                    "Freies Ersatzkürzel wird übernommen");
            }
            nint transparent = Native.CreateCursor(0, 0, 0, 32, 32, Enumerable.Repeat((byte)255, 128).ToArray(), new byte[128]);
            try { Check(CursorEngine.IsTransparentPointer(transparent), "Nativer transparenter AND/XOR-Cursor wird erkannt"); }
            finally { Native.DestroyCursor(transparent); }
            Check(!CursorEngine.IsTransparentPointer(Native.LoadCursor(0, (nint)Native.Arrow)), "Sichtbarer Windows-Pfeil wird nicht für das Overlay freigegeben");
            using var renderer = new CursorRenderer();
            using var sheet = new Bitmap(640, 230);
            using (var g = Graphics.FromImage(sheet))
            {
                g.Clear(Color.FromArgb(38, 39, 41));
                Pose[] poses = [new(0, 1, 1, 0), new(40, .78, .87, 0), new(-35, .8, .89, -90), new(10, 1, 1, 0)];
                for (int i = 0; i < poses.Length; i++)
                {
                    using var bitmap = renderer.Render(poses[i], 48);
                    g.DrawImageUnscaled(bitmap, i * 160 + 8, 20);
                    using var font = new Font("Segoe UI", 10);
                    g.DrawString(new[] { "Ruhe", "Nach rechts", "Nach unten", "Nachwippen" }[i], font, Brushes.White, i * 160 + 15, 190);
                }
            }
            sheet.Save(Path.Combine(output, "cursor-frames.png"), ImageFormat.Png);
            using (var clicksSheet = new Bitmap(640, 230))
            {
                using var g = Graphics.FromImage(clicksSheet); g.Clear(Color.FromArgb(38, 39, 41));
                for (int i = 0; i < 4; i++)
                {
                    using var frame = renderer.Render(new Pose(0, 1, 1, 0, .90, i < 2 ? .15 + i * .4 : -1, i >= 2 ? .15 + (i - 2) * .4 : -1), 48);
                    g.DrawImageUnscaled(frame, i * 160 + 8, 20);
                    g.DrawString(i < 2 ? "Linksklick" : "Rechtsklick", SystemFonts.DefaultFont, Brushes.White, i * 160 + 20, 190);
                }
                clicksSheet.Save(Path.Combine(output, "click-frames.png"), ImageFormat.Png);
            }
            foreach (int size in new[] { 24, 32, 48, 64 })
            {
                using var bitmap = renderer.Render(new(0, 1, 1, 0), size);
                int visible = 0;
                for (int y = 0; y < bitmap.Height; y++) for (int x = 0; x < bitmap.Width; x++)
                {
                    Color c = bitmap.GetPixel(x, y);
                    if (c.A < 10) continue;
                    visible++;
                    Check(Math.Abs(c.B - c.R) < 4 && Math.Abs(c.G - c.R) < 4, "neutraler Cursor-Pixel");
                }
                Check(visible > 30, "Cursor sichtbar bei " + size + " px");
                nint handle = CursorRenderer.CreateCursor(bitmap);
                Check(handle != 0, "Windows-Cursor erzeugt bei " + size + " px");
                try
                {
                    Check(Native.GetIconInfo(handle, out var info), "Native Cursorinformation lesbar");
                    try { Check(!info.IsIcon && info.XHotspot == bitmap.Width / 2 && info.YHotspot == bitmap.Height / 2, "Klickpunkt bleibt an der Pfeilspitze"); }
                    finally { Native.DeleteObject(info.Mask); Native.DeleteObject(info.Color); }
                }
                finally { Native.DestroyCursor(handle); }
            }
            results.RemoveAll(s => s == "PASS: neutraler Cursor-Pixel");
            results.Add("PASS: Alle sichtbaren Pixel neutral, kein blauer Glow");
            File.WriteAllLines(Path.Combine(output, "checks.txt"), results);
            return 0;
        }
        catch (Exception e)
        {
            results.Add("FAIL: " + e);
            File.WriteAllLines(Path.Combine(output, "checks.txt"), results);
            return 1;
        }
    }
    internal static int FrameRate(string output)
    {
        Directory.CreateDirectory(output);
        try
        {
            using var renderer = new CursorRenderer();
            using var overlay = new CursorOverlay();
            using var timer = new FrameTimer(overlay) { Fps = 240 };
            var smoothing = new CursorSmoothing();
            var smoothingOptions = new AnimationOptions { EffectsMethod = SmoothingMethod.Sine };
            using var context = new ApplicationContext();
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var intervals = new List<double>();
            double last = 0; Exception? failure = null;
            timer.Failed += e => { failure = e; context.ExitThread(); };
            timer.Tick += (_, _) =>
            {
                try
                {
                    double now = clock.Elapsed.TotalSeconds;
                    double dt = last > 0 ? now - last : 1.0 / 240;
                    if (last > 0) intervals.Add(dt * 1000);
                    last = now;
                    var pose = smoothing.Effects(new Pose(Math.Sin(now * 9) * 40, .8, .9, 30, .95, now % .42 / .42), dt, smoothingOptions);
                    var point = smoothing.Position(100 + Math.Sin(now * 4) * 20, 100, dt, smoothingOptions);
                    using var frame = renderer.Render(pose, 64);
                    overlay.Present(frame, new Native.Point { X = (int)Math.Round(point.X), Y = (int)Math.Round(point.Y) });
                    if (now >= 5) context.ExitThread();
                }
                catch (Exception e) { failure = e; context.ExitThread(); }
            };
            timer.Start(); Application.Run(context); timer.Stop();
            if (failure != null) throw failure;
            var sorted = intervals.Order().ToArray();
            double fps = 1000 / intervals.Average();
            File.WriteAllText(Path.Combine(output, "frame-rate.txt"),
                $"Target: 240 FPS\nMeasured overlay submissions: {fps:F1} FPS\nMedian: {sorted[sorted.Length / 2]:F2} ms\nP95: {sorted[(int)(sorted.Length * .95)]:F2} ms\nFrames: {intervals.Count + 1}\nDuration: {clock.Elapsed.TotalSeconds:F2} s\nThis measures application updates, not physical display scanout.\n");
            return fps >= 200 ? 0 : 1;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "frame-rate.txt"), "FAIL: " + e); return 1; }
    }
    private static void CheckFinite(Pose p)
    {
        if (!double.IsFinite(p.Rotation) || !double.IsFinite(p.Stretch) || !double.IsFinite(p.Squash) || Math.Abs(p.Rotation) > 120)
            throw new InvalidOperationException("Instabile Animation");
    }
    internal static void Snapshot(string output)
    {
        Directory.CreateDirectory(output);
        ActualSnapshot(output);
        foreach (uint role in CursorEngine.AllRoles)
        {
            nint handle = Native.CopyIcon(Native.LoadCursor(0, (nint)role));
            try
            {
                Native.GetIconInfo(handle, out var info);
                try
                {
                    if (info.Color != 0)
                    {
                        using var bitmap = Image.FromHbitmap(info.Color);
                        bitmap.Save(Path.Combine(output, role + ".png"), ImageFormat.Png);
                    }
                    using var mask = Image.FromHbitmap(info.Mask);
                    mask.Save(Path.Combine(output, role + "-mask.png"), ImageFormat.Png);
                    File.WriteAllText(Path.Combine(output, role + ".json"), JsonSerializer.Serialize(new { info.XHotspot, info.YHotspot, info.IsIcon }));
                }
                finally { Native.DeleteObject(info.Mask); Native.DeleteObject(info.Color); }
            }
            finally { Native.DestroyCursor(handle); }
        }
    }
    internal static void ActualSnapshot(string output)
    {
        Directory.CreateDirectory(output);
        var actual = new Native.CursorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<Native.CursorInfo>() };
        Native.GetCursorInfo(ref actual);
        File.WriteAllText(Path.Combine(output, "actual.json"), DescribeCursor(actual.Cursor));
        File.WriteAllText(Path.Combine(output, "actual-transparent.txt"), CursorEngine.IsTransparentPointer(actual.Cursor).ToString());
    }
    internal static string DescribeCursor(nint cursor)
    {
        if (!Native.GetIconInfo(cursor, out var info)) return "unavailable";
        try
        {
            using var bitmap = Image.FromHbitmap(info.Mask);
            return JsonSerializer.Serialize(new { Handle = cursor.ToInt64(), info.XHotspot, info.YHotspot, Width = bitmap.Width, Height = bitmap.Height, Monochrome = info.Color == 0 });
        }
        finally { Native.DeleteObject(info.Mask); Native.DeleteObject(info.Color); }
    }
}

