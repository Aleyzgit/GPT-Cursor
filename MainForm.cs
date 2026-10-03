using System.Diagnostics;
using System.Text.Json;

namespace GPTCursor;

internal sealed partial class MainForm : Form
{
    private readonly CursorEngine engine = new();
    private readonly CursorRenderer renderer = new();
    private readonly FrameTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Preferences preferences;
    private readonly bool persistSettings;
    private readonly List<Action> translations = [];
    private NotifyIcon tray = null!;
    private ToolStripMenuItem trayOpen = null!, trayToggle = null!, trayExit = null!;
    private Button toggle = null!, shortcutButton = null!;
    private CheckBox shortcutEnabled = null!;
    private Label status = null!, sizeValue = null!, shortcutHint = null!, smoothingHint = null!;
    private PreviewPanel preview = null!;
    private ComboBox positionMode = null!, effectsMode = null!, directionMode = null!;
    private GlobalShortcut? shortcut;
    private readonly EventWaitHandle? quitSignal;
    private readonly UpdateService updates = new();
    private readonly CancellationTokenSource updateCancellation = new();
    private Button checkUpdateButton = null!, installUpdateButton = null!;
    private Label updateLabel = null!;
    private AppUpdate? availableUpdate;
    private bool checkingUpdate;
    private double nextUpdateCheck = 5;
    private double lastTime, lastPreview, ignoreHotkeyUntil;
    private bool closing, translating, recording;
    private string? noticeEn, noticeDe;
    private bool German => preferences.Language == "de";
    private string T(string english, string german) => German ? german : english;

    internal MainForm(Preferences? initialPreferences = null, bool persistSettings = true, bool? browserInterface = null)
    {
        preferences = initialPreferences ?? Preferences.Load();
        this.persistSettings = persistSettings;
        if (persistSettings) quitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, InstanceControl.QuitEvent);
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "GPT Cursor"; ClientSize = new Size(900, 760);
        MinimumSize = new Size(800, 620); FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10); Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        timer = new FrameTimer(this) { Fps = preferences.Fps };
        engine.Options = preferences; engine.Size = preferences.Size; engine.AllPointers = preferences.AllPointers;
        BuildInterface(); ApplyLanguage(); ApplyTheme();
        timer.Tick += (_, _) =>
        {
            if (quitSignal?.WaitOne(0) == true) { Close(); return; }
            double now = clock.Elapsed.TotalSeconds;
            if (persistSettings && preferences.CheckUpdates && now >= nextUpdateCheck && !checkingUpdate)
            { nextUpdateCheck = now + 21600; _ = CheckForUpdates(); }
            try
            {
                bool hadConflict = engine.ShellConflict;
                engine.Tick(now - lastTime);
                if (!hadConflict && engine.ShellConflict) UpdateStatus();
                if (now - lastPreview >= 1.0 / 60 && Visible && WindowState != FormWindowState.Minimized)
                { if (browserReady) SendBrowserFrame(now); else preview.Invalidate(); lastPreview = now; }
            }
            catch (Exception e) { HandleError(e); }
            lastTime = now;
        };
        timer.Failed += HandleError;
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            MinimumSize = new Size(Math.Min(MinimumSize.Width, area.Width), Math.Min(MinimumSize.Height, area.Height));
            if (Height > area.Height) { Height = area.Height; Top = area.Top; }
            if (Width > area.Width) { Width = area.Width; Left = area.Left; }
            shortcut = new GlobalShortcut(Handle);
            if (preferences.ShortcutEnabled && !shortcut.TrySet(preferences.ShortcutModifiers, preferences.ShortcutKey))
                Notice("Shortcut unavailable. Click its keys to choose another.", "Kürzel belegt. Zum Ändern auf die Tastenkombination klicken.");
            timer.Start(); Save();
            if (browserInterface ?? persistSettings) _ = StartBrowserInterface();
        };
        Deactivate += (_, _) => { if (recording) { recording = false; UpdateShortcut(); } };
        ResumeLayout(true);
    }
    private void ApplyLanguage()
    {
        translating = true;
        try
        {
            foreach (var update in translations) update();
            themeMode.Items.Clear(); themeMode.Items.AddRange(German ? ["System", "Hell", "Dunkel"] : ["System", "Light", "Dark"]);
            themeMode.SelectedIndex = preferences.Theme == "dark" ? 2 : preferences.Theme == "light" ? 1 : 0;
            string[] styles = German ? ["Sinus-Easing", "Sanfte Feder", "Reaktionsschnell"] : ["Sine easing", "Soft spring", "Responsive"];
            positionMode.Items.Clear(); positionMode.Items.AddRange(styles); positionMode.SelectedIndex = (int)preferences.PositionMethod;
            effectsMode.Items.Clear(); effectsMode.Items.AddRange(styles); effectsMode.SelectedIndex = (int)preferences.EffectsMethod;
            directionMode.Items.Clear();
            directionMode.Items.AddRange(German
                ? ["Original", "Bewegungsrichtung · zurückdrehen", "Bewegungsrichtung · beibehalten"]
                : ["Original", "Follow movement · return at rest", "Follow movement · keep direction"]);
            directionMode.SelectedIndex = (int)preferences.Direction;
            sizeValue.Text = $"{preferences.Size} px";
            trayOpen.Text = T("Open settings", "Einstellungen öffnen");
            trayToggle.Text = T("Toggle cursor", "Cursor umschalten");
            trayExit.Text = T("Exit and restore cursor", "Beenden und Cursor wiederherstellen");
            UpdateSmoothingHint(); UpdateShortcut(); UpdateStatus(); preview.Invalidate();
            SelectPage(pageIndex);
        }
        finally { translating = false; }
    }
    private void UpdateSmoothingHint()
    {
        smoothingHint.Text = T("Soft start and stop. Clicks and dragging use the exact mouse position.", "Weiches Anfahren und Stoppen. Klicken und Ziehen bleiben direkt.");
    }
    private void UpdateShortcut()
    {
        shortcutButton.Text = recording ? T("Press your shortcut…", "Tastenkürzel drücken…") : GlobalShortcut.Format(preferences.ShortcutModifiers, preferences.ShortcutKey, German);
        shortcutButton.AccessibleName = T("Change shortcut: ", "Tastenkürzel ändern: ") + shortcutButton.Text;
        shortcutButton.Enabled = true;
        shortcutHint.Text = recording ? T("Use Ctrl or Alt + a letter, digit or F1–F11. Esc cancels.", "Strg oder Alt + Buchstabe, Ziffer oder F1–F11. Esc bricht ab.") : T("Click the keys to change the shortcut.", "Zum Ändern auf die Tastenkombination klicken.");
        if (!preferences.ShortcutEnabled) shortcutHint.Text = T("Keyboard shortcut is disabled.", "Tastenkürzel ist ausgeschaltet.");
    }
    private void SetShortcutEnabled(bool enabled)
    {
        recording = false;
        if (enabled && shortcut != null && !shortcut.TrySet(preferences.ShortcutModifiers, preferences.ShortcutKey))
        {
            translating = true; shortcutEnabled.Checked = false; translating = false;
            preferences.ShortcutEnabled = false;
            Notice("Shortcut unavailable. Choose another combination.", "Kürzel belegt. Bitte eine andere Kombination wählen.");
        }
        else
        {
            preferences.ShortcutEnabled = enabled;
            if (!enabled) shortcut?.Dispose();
            ClearNotice();
        }
        // The key editor remains usable when disabled, allowing conflicts to be fixed.
        UpdateShortcut(); shortcutButton.Enabled = true;
    }
    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (!recording) return base.ProcessCmdKey(ref msg, keyData);
        var key = keyData & Keys.KeyCode;
        if (key == Keys.Escape) { recording = false; ClearNotice(); UpdateShortcut(); return true; }
        if (key is Keys.ControlKey or Keys.ShiftKey or Keys.Menu or Keys.LControlKey or Keys.RControlKey or Keys.LShiftKey or Keys.RShiftKey or Keys.LMenu or Keys.RMenu) return true;
        uint modifiers = GlobalShortcut.Modifiers(keyData);
        if (!GlobalShortcut.Valid(modifiers, (uint)key))
        { Notice("Use Ctrl or Alt with a letter, digit or F1–F11.", "Strg oder Alt mit Buchstabe, Ziffer oder F1–F11 verwenden."); return true; }
        if (preferences.ShortcutEnabled && shortcut != null && !shortcut.TrySet(modifiers, (uint)key))
        { Notice("That shortcut is already in use. Try another.", "Dieses Kürzel ist belegt. Bitte ein anderes wählen."); return true; }
        preferences.ShortcutModifiers = modifiers; preferences.ShortcutKey = (uint)key;
        recording = false; ignoreHotkeyUntil = clock.Elapsed.TotalSeconds + .35;
        ClearNotice(); UpdateShortcut(); Save(); return true;
    }
    private void Save()
    {
        if (!persistSettings) return;
        try { Directory.CreateDirectory(Path.GetDirectoryName(Preferences.FilePath)!); File.WriteAllText(Preferences.FilePath, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { Notice("Settings apply for this session (folder is read-only).", "Einstellungen gelten für diese Sitzung (Ordner schreibgeschützt)."); }
    }
    private async Task CheckForUpdates()
    {
        if (checkingUpdate || closing) return;
        checkingUpdate = true; checkUpdateButton.Enabled = installUpdateButton.Enabled = false;
        updateLabel.Text = T("Checking GitHub releases…", "GitHub-Releases werden geprüft…");
        try
        {
            availableUpdate = await updates.CheckAsync(updateCancellation.Token);
            if (closing) return;
            updateLabel.Text = availableUpdate == null
                ? T($"Version {UpdateService.CurrentVersion} · No newer published version.", $"Version {UpdateService.CurrentVersion} · Keine neuere veröffentlichte Version.")
                : T($"Version {availableUpdate.Version} is available. Your settings will be kept.", $"Version {availableUpdate.Version} ist verfügbar. Deine Einstellungen bleiben erhalten.");
            installUpdateButton.Enabled = availableUpdate != null;
        }
        catch (Exception e) when (e is HttpRequestException or IOException or JsonException or OperationCanceledException or KeyNotFoundException or InvalidOperationException)
        {
            if (!closing) updateLabel.Text = T("Could not check for updates. Please try again later.", "Updates konnten nicht geprüft werden. Bitte später erneut versuchen.");
        }
        finally { checkingUpdate = false; if (!closing) checkUpdateButton.Enabled = true; }
    }
    private async Task InstallUpdate()
    {
        if (availableUpdate == null || checkingUpdate || closing) return;
        checkingUpdate = true; checkUpdateButton.Enabled = installUpdateButton.Enabled = false;
        updateLabel.Text = T("Downloading and verifying the installer…", "Installer wird geladen und geprüft…");
        try
        {
            string path = await updates.DownloadAsync(availableUpdate, updateCancellation.Token);
            if (closing) return;
            var start = new ProcessStartInfo(path) { UseShellExecute = true };
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "installed.flag")))
                start.ArgumentList.Add("/DIR=" + AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
            else
            {
                string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPTCursor", "settings.json");
                if (!File.Exists(settings))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
                    File.WriteAllText(settings, JsonSerializer.Serialize(preferences));
                }
            }
            start.ArgumentList.Add("/LANG=" + (German ? "german" : "english"));
            Process.Start(start); Close();
        }
        catch (Exception e) when (e is HttpRequestException or IOException or OperationCanceledException or System.ComponentModel.Win32Exception or UnauthorizedAccessException)
        {
            if (!closing) updateLabel.Text = T("Update was not installed. Please try again or use the setup from GitHub.", "Update nicht installiert. Bitte erneut versuchen oder das Setup von GitHub verwenden.");
        }
        finally { checkingUpdate = false; if (!closing) { checkUpdateButton.Enabled = true; installUpdateButton.Enabled = true; } }
    }
    private void Notice(string english, string german) { noticeEn = english; noticeDe = german; UpdateStatus(); }
    private void ClearNotice() { noticeEn = noticeDe = null; UpdateStatus(); }
    private void Toggle() { try { if (engine.Active) engine.Stop(); else engine.Start(); ClearNotice(); } catch (Exception e) { HandleError(e); } }
    private void UpdateStatus()
    {
        toggle.Text = engine.Active ? T("Disable cursor", "Cursor deaktivieren") : T("Enable cursor", "Cursor aktivieren");
        toggle.AccessibleName = toggle.Text;
        tray.Text = "GPT Cursor · " + (engine.Active ? T("active", "aktiv") : T("paused", "pausiert"));
        status.Text = noticeEn != null ? T(noticeEn, noticeDe!) :
            preferences.ShortcutEnabled && shortcut is { Id: 0 } ? T("Shortcut unavailable. Click its keys to choose another.", "Kürzel belegt. Zum Ändern auf die Tastenkombination klicken.") :
            engine.Active && engine.ShellConflict ? T("Active · Windows cursor fallback was overridden", "Aktiv · Windows-Cursor-Fallback wurde überschrieben") :
            engine.Active ? T("Active · Closing restores your regular cursor", "Aktiv · Schließen stellt deinen normalen Cursor wieder her") : T("Paused · Your regular cursor is active", "Pausiert · Dein normaler Cursor ist aktiv");
    }
    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        float scale = preview.DeviceDpi / 96f;
        e.Graphics.Clear(UiTheme.Canvas);
        UiTheme.Fill(e.Graphics, new RectangleF(0, 0, preview.Width - 1, preview.Height - 1), 24 * scale, UiTheme.Soft);
        using var grid = new SolidBrush(UiTheme.Line);
        for (float y = 20 * scale; y < preview.Height - 40 * scale; y += 20 * scale)
            for (float x = 20 * scale; x < preview.Width - 16 * scale; x += 20 * scale)
                e.Graphics.FillEllipse(grid, x, y, scale, scale);
        using var image = renderer.Render(engine.Pose, preferences.Size);
        e.Graphics.DrawImageUnscaled(image, preview.Width / 2 - image.Width / 2, (preview.Height - (int)(25 * scale)) / 2 - image.Height / 2);
        TextRenderer.DrawText(e.Graphics, T("Move your mouse · Click to preview", "Maus bewegen · Klicken für die Vorschau"), Font,
            new Rectangle(12, preview.Height - (int)(38 * scale), preview.Width - 24, (int)(28 * scale)), UiTheme.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
    }
    internal void StopCursor() => engine.Stop();
    internal void ActivateCursor() { if (!engine.Active) Toggle(); }
    internal void HandleError(Exception exception)
    {
        timer.Stop();
        try { engine.Stop(); } catch { }
        UpdateStatus();
        MessageBox.Show(this, T("The cursor has been paused.\n\n", "Der Cursor wurde angehalten.\n\n") + exception.Message, "GPT Cursor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        if (!closing) timer.Start();
    }
    protected override void WndProc(ref Message m)
    {
        if (browserReady && m.Msg == 0x0083 && m.WParam != 0) { m.Result = 0; return; }
        if (browserReady && m.Msg == 0x0084 && WindowState != FormWindowState.Maximized)
        {
            long coordinates = m.LParam.ToInt64();
            var point = PointToClient(new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff)));
            int edge = Math.Max(4, (int)(6 * DeviceDpi / 96f));
            bool left = point.X < edge, right = point.X >= ClientSize.Width - edge;
            bool top = point.Y < edge, bottom = point.Y >= ClientSize.Height - edge;
            int hit = top ? left ? 13 : right ? 14 : 12 : bottom ? left ? 16 : right ? 17 : 15 : left ? 10 : right ? 11 : 1;
            if (hit != 1) { m.Result = hit; return; }
        }
        if (m.Msg is 0x001A or 0x031A && preferences?.Theme == "system" && themeMode != null) ApplyTheme();
        if (m.Msg == 0x0312 && shortcut?.Id != 0 && m.WParam == shortcut?.Id && !recording && clock.Elapsed.TotalSeconds >= ignoreHotkeyUntil) Toggle();
        base.WndProc(ref m);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        ReleaseResources();
        base.OnFormClosing(e);
    }
    private void ReleaseResources()
    {
        if (!closing)
        {
            closing = true; timer.Stop(); shortcut?.Dispose();
            browser?.Dispose();
            updateCancellation.Cancel(); updates.Dispose();
            engine.Dispose(); renderer.Dispose(); tray.Visible = false; tray.Dispose(); timer.Dispose();
            quitSignal?.Dispose();
        }
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) ReleaseResources();
        base.Dispose(disposing);
    }
    private sealed class PreviewPanel : Panel { internal PreviewPanel() { DoubleBuffered = true; } }
}

