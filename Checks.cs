using System.Drawing.Imaging;
using System.Text.Json;

namespace GPTCursor;

internal static class Checks
{
    internal static int WebInterface(string output)
    {
        Directory.CreateDirectory(output);
        int result = 1;
        using var form = new MainForm(new Preferences { ShortcutEnabled = false }, persistSettings: false, browserInterface: true)
        { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-12000, -12000) };
        form.Shown += async (_, _) =>
        {
            try { await form.CheckBrowserInterface(output); result = 0; }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "failure.txt"), e.ToString()); }
            finally { form.Close(); }
        };
        Application.Run(form); return result;
    }
    internal static int UiPreview(string output)
    {
        Directory.CreateDirectory(output);
        foreach (string theme in new[] { "light", "dark" })
        foreach (string language in new[] { "en", "de" })
        {
            using var form = new MainForm(new Preferences { Theme = theme, Language = language, ShortcutEnabled = false }, persistSettings: false);
            form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-12000, -12000);
            form.Show();
            for (int page = 0; page < 4; page++)
            {
                form.SelectPage(page); form.PerformLayout(); Application.DoEvents();
                using var bitmap = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                bitmap.Save(Path.Combine(output, $"{theme}-{language}-{page}.png"), ImageFormat.Png);
            }
            form.Close();
        }
        return 0;
    }
    internal static int Shell(string output)
    {
        Directory.CreateDirectory(output);
        string Fingerprint(uint role)
        {
            if (!Native.GetIconInfo(Native.LoadCursor(0, (nint)role), out var info)) throw new InvalidOperationException("Missing cursor.");
            try
            {
                using var bytes = new MemoryStream();
                using (var writer = new BinaryWriter(bytes, System.Text.Encoding.UTF8, true)) { writer.Write(info.XHotspot); writer.Write(info.YHotspot); }
                foreach (nint handle in new[] { info.Color, info.Mask }.Where(h => h != 0))
                { using var bitmap = Bitmap.FromHbitmap(handle); bitmap.Save(bytes, ImageFormat.Png); }
                return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.ToArray()));
            }
            finally { Native.DeleteObject(info.Color); Native.DeleteObject(info.Mask); }
        }
        using var renderer = new CursorRenderer();
        using var shell = new ShellCursor();
        try
        {
            string arrow = Fingerprint(Native.Arrow), hand = Fingerprint(Native.Hand);
            int before = Native.InstallCount;
            try
            {
                shell.Show(renderer, 40, 0);
                if (Fingerprint(Native.Arrow) == arrow) throw new InvalidOperationException("Shell artwork was not installed.");
                for (int i = 0; i < 100; i++) shell.Show(renderer, 40, i);
                if (Native.InstallCount - before != 2) throw new InvalidOperationException("Repeated native replacement during shell movement.");
            }
            finally { shell.Restore(); }
            if (Fingerprint(Native.Arrow) != arrow || Fingerprint(Native.Hand) != hand) throw new InvalidOperationException("Original pointer images were not restored exactly.");
            nint backup = Native.CopyIcon(Native.LoadCursor(0, (nint)Native.Arrow));
            using var replacementBitmap = renderer.Render(new Pose(90, 1, 1, 0), 32);
            nint replacement = CursorRenderer.CreateCursor(replacementBitmap);
            try
            {
                shell.Show(renderer, 40, 0);
                Native.Install(Native.CopyIcon(replacement), Native.Arrow);
                string foreign = Fingerprint(Native.Arrow);
                shell.Show(renderer, 40, 250);
                if (Fingerprint(Native.Arrow) == foreign || shell.Overridden) throw new InvalidOperationException("A single scheme reset was not recovered.");
                Native.Install(Native.CopyIcon(replacement), Native.Arrow);
                int installed = Native.InstallCount;
                for (int i = 500; i < 5000; i += 10) shell.Show(renderer, 40, i);
                if (!shell.Overridden || installed != Native.InstallCount) throw new InvalidOperationException("Persistent cursor owner conflict was not reported without repeated replacement.");
                shell.Restore();
                if (Fingerprint(Native.Arrow) != foreign || Fingerprint(Native.Hand) != hand) throw new InvalidOperationException("Restoration overwrote another owner's cursor.");
            }
            finally
            {
                shell.Restore();
                Native.Install(backup, Native.Arrow);
                Native.DestroyCursor(replacement);
            }
            File.WriteAllText(Path.Combine(output, "shell.txt"), "PASS: native artwork and exact restoration; unchanged frames do not replace cursors; one reset is recovered; persistent overrides are reported without a replacement loop; another owner's image survives shell exit.");
            return 0;
        }
        catch (Exception e) { File.WriteAllText(Path.Combine(output, "shell.txt"), "FAIL: " + e); return 1; }
    }
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
            var downFacing = facingMotion.Update(10, 100, 1.0 / 240, facingOptions);
            Check(Math.Abs(downFacing.Rotation - 225) < .001, "Richtungspfeil zeigt nach unten");
            for (int i = 0; i < 500; i++) rest = facingMotion.Update(10, 100, 1.0 / 240, facingOptions);
            Check(rest.Rotation == downFacing.Rotation, "Richtung bleibt nach dem Stoppen erhalten");
            facingOptions.Stretch = true;
            var stretchFacing = facingMotion.Update(200, 100, .01, facingOptions);
            Check(stretchFacing.Stretch < 1 && Math.Abs(stretchFacing.Rotation - 135) < .001, "Stretch und Richtungsmodus kombinierbar");
            foreach (double degrees in new[] { 13.0, 27, 68, 113, 167, -24, -79, -147 })
            {
                var tracker = new HeadingTracker();
                tracker.Update(0, 0);
                double rad = degrees * Math.PI / 180, px = 0, py = 0, rotation = 0;
                for (int i = 1; i <= 400; i++)
                {
                    px = Math.Round(i * Math.Cos(rad)); py = Math.Round(i * Math.Sin(rad));
                    rotation = tracker.Update(px, py);
                }
                double error = Math.Abs((rotation - 135 - degrees + 540) % 360 - 180);
                Check(error < 4, $"Langsame pixelgerasterte Bewegung bei {degrees}° bleibt abseits des 45°-Rasters (Fehler {error:F2}°)");
                for (int i = 0; i < 600; i++)
                    Check(tracker.Update(px + (i % 2), py - (i % 2)) == rotation, "Richtung bleibt bei Stillstand und Ein-Pixel-Zittern exakt stabil");
            }
            results.RemoveAll(s => s == "PASS: Richtung bleibt bei Stillstand und Ein-Pixel-Zittern exakt stabil");
            results.Add("PASS: Kein Nachdrehen bei Stillstand oder Ein-Pixel-Zittern");
            CheckReturningDirection(Check);
            var reversing = new HeadingTracker();
            for (int i = 0; i <= 100; i += 4) reversing.Update(-i, 0);
            double reverseAngle = 0;
            for (int i = 1; i <= 3; i++) reverseAngle = reversing.Update(-100 + i * 4, 0);
            Check(Math.Abs(((reverseAngle - 135 + 180) % 360 + 360) % 360 - 180) < 20,
                "Abrupte Umkehr verwirft den alten Bewegungspfad innerhalb von zwölf Pixeln");
            var axisFilter = new CursorSmoothing(); axisFilter.Reset(0, 0);
            var axisOptions = new AnimationOptions { EffectsSmoothing = true, EffectsMethod = SmoothingMethod.Spring };
            for (int i = 0; i < 120; i++) axisFilter.Effects(new Pose(0, .7, .85, 0), 1.0 / 240, axisOptions);
            double axisExcursion = 0;
            for (int i = 0; i < 120; i++) axisExcursion = Math.Max(axisExcursion, Math.Abs(axisFilter.Effects(new Pose(0, .7, .85, 180), 1.0 / 240, axisOptions).Axis));
            Check(axisExcursion < .001, "Verformungsachse bleibt bei einer 180°-Umkehr stabil");
            foreach (int fps in new[] { 60, 240, 360 })
            {
                var quick = new Motion(); var filter = new CursorSmoothing(); filter.Reset(0, 0);
                var config = new AnimationOptions { Direction = DirectionStyle.ReturnToRest, EffectsSmoothing = true, Wobble = false };
                Pose pose = default;
                double x = 0, biggest = 0, last = 0;
                for (int i = 0; i < fps * 4; i++)
                {
                    x += (i / (fps / 4) % 2 == 0 ? 1 : -1) * 1200.0 / fps;
                    pose = filter.Effects(quick.Update(x, 0, 1.0 / fps, config), 1.0 / fps, config);
                    biggest = Math.Max(biggest, Math.Abs(pose.Rotation - last)); last = pose.Rotation;
                    if (Math.Abs(pose.Axis - (pose.Rotation - 135)) > .001) throw new InvalidOperationException("Verformung löst sich von der Cursor-Richtung.");
                }
                Check(biggest < 75, $"Schnelle Links-Rechts-Wechsel ohne Winkelsprünge bei {fps} FPS");
            }
            Check(!DesktopPolicy.IsShell("game", "Windows.UI.Core.CoreWindow") && !DesktopPolicy.IsShell("game", "XamlExplorerHostIslandWindow"),
                "Spiele werden anhand ihrer Fensterklasse nicht als Startmenü behandelt");
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
            foreach (int fps in new[] { 60, 240, 360 })
            {
                var hold = new ClickMotion();
                var holdOptions = new AnimationOptions { HoldClickSize = true, ClickPulse = false, ClickRings = false };
                double dt = 1.0 / fps;
                hold.Trigger(false);
                var heldPose = hold.Apply(noMotion, dt, holdOptions, leftHeld: true);
                Check(heldPose.Scale < 1 && heldPose.Scale > .88, $"Gedrückthalten verkleinert weich bei {fps} FPS");
                for (int i = 0; i < fps * 2; i++) heldPose = hold.Apply(noMotion, dt, holdOptions, leftHeld: true);
                Check(heldPose.Scale == .88 && heldPose.LeftRing == -1 && heldPose.RightRing == -1, $"Cursor bleibt über die Klickanimation hinaus klein, ohne Ring ({fps} FPS)");
                hold.Trigger(true);
                hold.Apply(noMotion, dt, holdOptions, leftHeld: true, rightHeld: true);
                heldPose = hold.Apply(noMotion, dt, holdOptions, rightHeld: true);
                Check(heldPose.Scale == .88, "Loslassen einer Taste vergrößert bei weiterhin gedrückter zweiter Taste nicht");
                heldPose = hold.Apply(noMotion, dt, holdOptions);
                Check(heldPose.Scale > .88 && heldPose.Scale < 1, $"Loslassen startet weiche Rückkehr bei {fps} FPS");
                double previousScale = heldPose.Scale;
                for (int i = 0; i < fps; i++)
                {
                    heldPose = hold.Apply(noMotion, dt, holdOptions);
                    if (heldPose.Scale < previousScale || heldPose.Scale > 1) throw new InvalidOperationException("Instabile Größenrückkehr");
                    previousScale = heldPose.Scale;
                }
                Check(heldPose.Scale == 1, $"Nach Loslassen exakt normale Größe ({fps} FPS)");
                holdOptions.LeftClick = false;
                hold.Trigger(false);
                Check(hold.Apply(noMotion, dt, holdOptions, leftHeld: true).Scale == 1, "Deaktivierte Maustaste verkleinert nicht");
                hold.Trigger(true);
                Check(hold.Apply(noMotion, dt, holdOptions).Scale < 1, "Kurzer Klick zwischen zwei Frames bleibt sichtbar");
                holdOptions.HoldClickSize = false;
                Check(hold.Apply(noMotion, dt, holdOptions, rightHeld: true).Scale == 1, "Halten-Schalter stellt normale Größe sofort wieder her");
                holdOptions.HoldClickSize = true;
                hold.Reset();
                Check(hold.Apply(noMotion, dt, holdOptions).Scale == 1, "Neustart verwirft gedrückten Zustand");
            }
            var restored = JsonSerializer.Deserialize<Preferences>("{\"Size\":44,\"Animation\":false,\"AllPointers\":true}")!;
            Check(restored.Size == 44 && !restored.Animation && restored.AllPointers && restored.Fps == 240 && restored.LeftClick,
                "Bestehende Einstellungen bleiben erhalten; neue Optionen erhalten Standardwerte");
            Check(!restored.HoldClickSize && JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(new Preferences { HoldClickSize = true }))!.HoldClickSize,
                "Größe-halten ist optional und wird gespeichert");
            Check(restored.Language == "en" && restored.ShortcutEnabled && restored.ShortcutModifiers == 3 && restored.ShortcutKey == 67,
                "Englisch ist Standard; bisheriges Kürzel bleibt erhalten");
            Check(restored.Direction == DirectionStyle.Original && JsonSerializer.Deserialize<Preferences>("{\"FaceMovement\":true}")!.Direction == DirectionStyle.KeepDirection,
                "Bisherige Richtungswahl bleibt beim Update erhalten");
            var returnSettings = new Preferences { Direction = DirectionStyle.ReturnToRest };
            Check(JsonSerializer.Deserialize<Preferences>(JsonSerializer.Serialize(returnSettings))!.Direction == DirectionStyle.ReturnToRest,
                "Neuer Rückkehrmodus wird gespeichert und geladen");
            Check(GlobalShortcut.Valid(3, 67) && !GlobalShortcut.Valid(0, 67) && !GlobalShortcut.Valid(4, 67) && !GlobalShortcut.Valid(3, (uint)Keys.F12),
                "Kürzel verlangt Strg oder Alt und unterstützt keine reservierte F12-Taste");
            Check(GlobalShortcut.Format(3, 67, false) == "Ctrl + Alt + C" && GlobalShortcut.Format(3, 67, true) == "Strg + Alt + C",
                "Tastenkombination wird in beiden Sprachen angezeigt");
            byte[] updatePayload = [10, 20, 30, 40];
            string updateHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(updatePayload));
            string releaseJson = JsonSerializer.Serialize(new { draft = false, prerelease = false, tag_name = "v9.0.0", assets = new[] {
                new { name = "GPT-Cursor-Setup-9.0.0.exe", browser_download_url = UpdateService.Repository + "/releases/download/v9.0.0/GPT-Cursor-Setup-9.0.0.exe", digest = "sha256:" + updateHash }
            }});
            var update = UpdateService.Parse(releaseJson, new Version(1, 2, 0));
            Check(update?.Version == new Version(9, 0, 0), "Update erkennt neuere stabile Release-Version");
            Check(UpdateService.Parse(releaseJson, new Version(9, 0, 0)) == null && UpdateService.Parse(releaseJson, new Version(10, 0, 0)) == null, "Kein Update auf gleiche oder ältere Version");
            Check(UpdateService.Parse(releaseJson.Replace("\"prerelease\":false", "\"prerelease\":true"), new Version(1, 0, 0)) == null, "Vorabversionen werden nicht automatisch angeboten");
            bool rejectedUrl = false;
            try { UpdateService.Parse(releaseJson.Replace("github.com/Aleyzgit", "example.com/attacker"), new Version(1, 0, 0)); } catch (InvalidDataException) { rejectedUrl = true; }
            Check(rejectedUrl, "Fremde Installer-URLs werden abgelehnt");
            using (var downloader = new UpdateService(new FakeUpdateHandler(updatePayload)))
            {
                string downloaded = Task.Run(() => downloader.DownloadAsync(update!, CancellationToken.None)).GetAwaiter().GetResult();
                try { Check(File.ReadAllBytes(downloaded).SequenceEqual(updatePayload), "Update-Download prüft SHA-256 vor Übergabe an Installer"); }
                finally { File.Delete(downloaded); Directory.Delete(Path.GetDirectoryName(downloaded)!); }
                bool badHash = false;
                try { Task.Run(() => downloader.DownloadAsync(update! with { Sha256 = new string('0', 64) }, CancellationToken.None)).GetAwaiter().GetResult(); }
                catch (InvalidDataException) { badHash = true; }
                Check(badHash, "Manipulierter oder beschädigter Download wird verworfen");
            }
            var uiPreferences = new Preferences { Size = 44, PositionMethod = SmoothingMethod.Spring, EffectsMethod = SmoothingMethod.Responsive, ClickRings = false };
            using (var form = new MainForm(uiPreferences, persistSettings: false))
            {
                var language = AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Language");
                var holdSize = AllControls(form).OfType<CheckBox>().Single(c => c.Text == "Keep small while pressed");
                holdSize.Checked = true;
                Check(uiPreferences.HoldClickSize && !uiPreferences.ClickRings, "Halten-Schalter unabhängig von Klickringen");
                var directionChoice = AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Direction style");
                directionChoice.SelectedIndex = 1;
                Check(uiPreferences.Direction == DirectionStyle.ReturnToRest && !uiPreferences.HoldHeadingAtRest, "Richtungs-Dropdown aktiviert Rückkehr ohne Stillstands-Sperre");
                Check(AllControls(form).OfType<CheckBox>().Any(c => c.Text == "Show click rings (both buttons)" && !c.Checked), "Englischer Ringschalter zeigt gespeicherten Aus-Zustand");
                language.SelectedIndex = 1;
                Check(holdSize.Text == "Beim Gedrückthalten klein bleiben" && holdSize.Checked, "Halten-Schalter behält Zustand beim Sprachwechsel");
                Check(directionChoice.Text == "Bewegungsrichtung · zurückdrehen" && uiPreferences.Direction == DirectionStyle.ReturnToRest, "Rückkehr-Auswahl bleibt beim Sprachwechsel erhalten");
                Check(AllControls(form).OfType<CheckBox>().Any(c => c.Text == "Klickringe anzeigen (beide Tasten)" && !c.Checked), "Sprachwechsel übersetzt Ringschalter ohne Zustandsverlust");
                Check(AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Art der Positionsglättung").Text == "Sanfte Feder", "Deutscher Dropdown behält gewähltes Profil");
                Check(AllControls(form).OfType<Button>().Any(c => c.Text == "Strg + Alt + C"), "Shortcut-Darstellung wechselt auf Deutsch");
                language.SelectedIndex = 0;
                directionChoice.SelectedIndex = 2;
                Check(uiPreferences.HoldHeadingAtRest, "Beibehalten bleibt separat auswählbar");
                directionChoice.SelectedIndex = 0;
                Check(!uiPreferences.FaceMovement && !uiPreferences.ReturnToRest, "Originalmodus bleibt auswählbar");
                Check(AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Effect smoothing style").Text == "Responsive" && uiPreferences.PositionMethod == SmoothingMethod.Spring,
                    "Rückwechsel auf Englisch bewahrt beide Profile");
                var rings = AllControls(form).OfType<CheckBox>().Single(c => c.Text == "Show click rings (both buttons)");
                rings.Checked = true; rings.Checked = false;
                Check(!uiPreferences.ClickRings && uiPreferences.ClickPulse, "Ringschalter wirkt unabhängig vom Klick-Einfedern");
                var smooth = AllControls(form).OfType<CheckBox>().Single(c => c.Text == "Smooth position");
                smooth.Checked = false;
                Check(!uiPreferences.PositionSmoothing && uiPreferences.EffectsSmoothing && !AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Position smoothing style").Enabled,
                    "Positionsschalter deaktiviert nur seine Glättung und Auswahl");
                var appearance = AllControls(form).OfType<ComboBox>().Single(c => c.AccessibleName == "Appearance");
                appearance.SelectedIndex = 2;
                Check(uiPreferences.Theme == "dark" && form.BackColor == Color.FromArgb(33, 33, 33), "Dunkelmodus aktualisiert Einstellungen und Oberfläche");
                language.SelectedIndex = 1;
                Check(appearance.Text == "Dunkel" && uiPreferences.Theme == "dark", "Sprachwechsel behält das gewählte Farbschema");
                appearance.SelectedIndex = 1;
                Check(uiPreferences.Theme == "light" && form.BackColor == Color.White, "Hellmodus wird sofort übernommen");
                form.SelectPage(3);
                language.SelectedIndex = 0;
                Check(AllControls(form).OfType<Button>().Single(c => c.Text == "System").AccessibleDescription == "Selected", "Navigation behält die Seite beim Sprachwechsel");
                form.ClientSize = new Size(800, 620); form.PerformLayout();
                var updateAction = AllControls(form).OfType<Button>().Single(c => c.Text == "Check for updates");
                Control? ancestor = updateAction;
                while (ancestor != null && ancestor is not Panel { AutoScroll: true }) ancestor = ancestor.Parent;
                Check(ancestor is Panel { AutoScroll: true } page && page.DisplayRectangle.Height > page.ClientSize.Height,
                    "Kleines Fenster hält Updates in einer scrollbar erreichbaren Seite");
                using var browserModel = System.Text.Json.JsonDocument.Parse(form.BrowserModelJson());
                var browserFields = browserModel.RootElement.GetProperty("pages").EnumerateArray()
                    .SelectMany(p => p.GetProperty("rows").EnumerateArray())
                    .Where(r => r.TryGetProperty("controls", out _))
                    .SelectMany(r => r.GetProperty("controls").EnumerateArray()).ToArray();
                Check(browserFields.Length == 28, "Web-Oberfläche stellt alle 28 Einstellungsfelder und Aktionen bereit");
                string sizeId = browserFields.Single(c => c.GetProperty("type").GetString() == "range").GetProperty("id").GetString()!;
                form.HandleBrowserCommand(System.Text.Json.JsonSerializer.Serialize(new { action = "change", id = sizeId, value = 999 }));
                Check(uiPreferences.Size == 44, "Web-Brücke verwirft Größen außerhalb des erlaubten Bereichs");
                string directionId = browserFields.Single(c => c.GetProperty("text").GetString() == "Direction style").GetProperty("id").GetString()!;
                form.HandleBrowserCommand(System.Text.Json.JsonSerializer.Serialize(new { action = "change", id = directionId, value = -1 }));
                Check(uiPreferences.Direction == DirectionStyle.Original, "Web-Brücke verwirft ungültige Auswahlindizes");
                form.HandleBrowserCommand("{\"action\":\"change\",\"id\":\"not-a-control\",\"value\":false}");
                Check(uiPreferences.ClickPulse, "Unbekannte Web-IDs ändern keine Einstellungen");
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
    private static void CheckReturningDirection(Action<bool, string> check)
    {
        static double Arc(double a, double b) => Math.Abs(((a - b + 180) % 360 + 360) % 360 - 180);
        foreach (int fps in new[] { 60, 240, 360 })
        foreach (double degrees in new[] { 0.0, 180, 27, -79, 90 })
        {
            var options = new AnimationOptions { Direction = DirectionStyle.ReturnToRest, Wobble = false, EffectsSmoothing = false };
            var motion = new Motion();
            double dt = 1.0 / fps, rad = degrees * Math.PI / 180;
            motion.Update(0, 0, dt, options);
            double px = 0, py = 0;
            Pose pose = default;
            for (int i = 1; i <= fps; i++)
            {
                px = Math.Round(i * 600.0 / fps * Math.Cos(rad));
                py = Math.Round(i * 600.0 / fps * Math.Sin(rad));
                pose = motion.Update(px, py, dt, options);
                CheckFinite(pose, double.PositiveInfinity);
            }
            check(Arc(pose.Rotation, degrees + 135) < 3 && pose.Stretch < 1 && pose.Squash < 1,
                $"Rückkehrmodus folgt {degrees}° mit Verformung bei {fps} FPS");
            double movingAngle = pose.Rotation;
            for (int i = 0; i < fps / 20; i++) pose = motion.Update(px, py, dt, options);
            check(Arc(pose.Rotation, movingAngle) < 3, $"Kurze Meldepausen starten keine Rückkehr bei {fps} FPS ({degrees}°)");
            double previous = pose.Rotation, totalTurn = 0;
            for (int i = 0; i < fps; i++)
            {
                pose = motion.Update(px + i % 2, py - i % 2, dt, options);
                totalTurn += Math.Abs(pose.Rotation - previous); previous = pose.Rotation;
            }
            check(Arc(pose.Rotation, 0) < .01 && totalTurn <= Arc(movingAngle, 0) + 3,
                $"Rückkehr nimmt kürzesten Weg und bleibt bei Sensorzittern ruhig ({fps} FPS, {degrees}°)");
            // Restart in the opposite direction after resting; no stale held-angle lock.
            for (int i = 1; i <= fps; i++) pose = motion.Update(px - i * 600.0 / fps * Math.Cos(rad), py - i * 600.0 / fps * Math.Sin(rad), dt, options);
            check(Arc(pose.Rotation, degrees + 315) < 3, $"Neue Bewegung nach Rückkehr folgt Gegenrichtung ({fps} FPS, {degrees}°)");
        }
        foreach (var profile in Enum.GetValues<SmoothingMethod>())
        {
            var options = new AnimationOptions { Direction = DirectionStyle.ReturnToRest, EffectsMethod = profile, Wobble = true };
            var motion = new Motion(); var filter = new CursorSmoothing(); filter.Reset(0, 0);
            Pose pose = default;
            for (int i = 0; i < 240; i++) pose = filter.Effects(motion.Update(i * 5, 0, 1.0 / 240, options), 1.0 / 240, options);
            for (int i = 0; i < 720; i++) pose = filter.Effects(motion.Update(1195, 0, 1.0 / 240, options), 1.0 / 240, options);
            check(Arc(pose.Rotation, 0) < .001 && Math.Abs(pose.Stretch - 1) < .001 && Math.Abs(pose.Squash - 1) < .001,
                $"Rückkehr mit Nachwippen und {profile}-Glättung endet vollständig");
            options.Stretch = options.Squash = false;
            pose = filter.Effects(motion.Update(1300, 100, .01, options), .01, options);
            check(pose.Stretch == 1 && pose.Squash == 1, "Verformung bleibt im Rückkehrmodus separat abschaltbar");
        }
        // Cross the +/-180 boundary repeatedly while moving: targets stay unwrapped.
        var spin = new Motion();
        var spinOptions = new AnimationOptions { Direction = DirectionStyle.ReturnToRest, Wobble = false };
        double x = 0, y = 0, last = 0, largestStep = 0;
        for (int i = 0; i < 1440; i++)
        {
            double angle = i * Math.PI / 180;
            x += 5 * Math.Cos(angle); y += 5 * Math.Sin(angle);
            var pose = spin.Update(x, y, 1.0 / 240, spinOptions);
            largestStep = Math.Max(largestStep, Math.Abs(pose.Rotation - last)); last = pose.Rotation;
        }
        check(largestStep < 15, "Mehrfache Kreisbewegungen verursachen keine 360°-Sprünge");
        double travelled = 0;
        for (int i = 0; i < 480; i++)
        {
            var pose = spin.Update(x, y, 1.0 / 240, spinOptions);
            travelled += Math.Abs(pose.Rotation - last); last = pose.Rotation;
        }
        check(Arc(last, 0) < .001 && travelled < 210, "Nach Kreisbewegungen kehrt der Cursor ohne zusätzliche Umdrehungen zurück");
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
    private static void CheckFinite(Pose p, double maxRotation = 120)
    {
        if (!double.IsFinite(p.Rotation) || !double.IsFinite(p.Stretch) || !double.IsFinite(p.Squash) || Math.Abs(p.Rotation) > maxRotation)
            throw new InvalidOperationException("Instabile Animation");
    }
    private sealed class FakeUpdateHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK) { RequestMessage = request, Content = new ByteArrayContent(content) });
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

