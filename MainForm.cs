using System.Diagnostics;
using System.Text.Json;

namespace GPTCursor;

internal sealed class MainForm : Form
{
    private readonly CursorEngine engine = new();
    private readonly CursorRenderer renderer = new();
    private readonly FrameTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Preferences preferences;
    private readonly bool persistSettings;
    private readonly List<Action> translations = [];
    private readonly NotifyIcon tray;
    private readonly ToolStripMenuItem trayOpen, trayToggle, trayExit;
    private readonly Button toggle, shortcutButton;
    private readonly CheckBox shortcutEnabled;
    private readonly Label status, sizeValue, shortcutHint, smoothingHint;
    private readonly PreviewPanel preview;
    private readonly ComboBox positionMode, effectsMode, directionMode;
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
    private static readonly Color Ink = Color.FromArgb(28, 29, 31), Muted = Color.FromArgb(107, 108, 110), Paper = Color.FromArgb(247, 247, 244);
    private bool German => preferences.Language == "de";
    private string T(string english, string german) => German ? german : english;

    internal MainForm(Preferences? initialPreferences = null, bool persistSettings = true)
    {
        preferences = initialPreferences ?? Preferences.Load();
        this.persistSettings = persistSettings;
        if (persistSettings) quitSignal = new EventWaitHandle(false, EventResetMode.ManualReset, InstanceControl.QuitEvent);
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96); AutoScaleMode = AutoScaleMode.Dpi;
        Text = "GPT Cursor"; ClientSize = new Size(560, 780); AutoScroll = true;
        FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen; BackColor = Paper;
        Font = new Font("Segoe UI", 10); ForeColor = Ink;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
        timer = new FrameTimer(this) { Fps = preferences.Fps };
        engine.Options = preferences; engine.Size = preferences.Size; engine.AllPointers = preferences.AllPointers;

        void TextFor(Control control, string english, string german)
        {
            translations.Add(() => { control.Text = T(english, german); control.AccessibleName = control is Label ? null : control.Text; });
        }
        Label LabelAt(string english, string german, int x, int y, int width = 220, int height = 26)
        {
            var label = new Label { Location = new Point(x, y), Size = new Size(width, height), ForeColor = Muted };
            TextFor(label, english, german); Controls.Add(label); return label;
        }
        CheckBox Switch(string english, string german, int x, int y, bool value, Action<bool> change, int width = 242)
        {
            var box = new CheckBox { Checked = value, Location = new Point(x, y), Size = new Size(width, 28) };
            TextFor(box, english, german);
            box.CheckedChanged += (_, _) => { if (translating) return; change(box.Checked); Save(); preview?.Invalidate(); };
            Controls.Add(box); return box;
        }
        ComboBox Combo(int x, int y, int width, string english, string german)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(x, y), Size = new Size(width, 30) };
            translations.Add(() => box.AccessibleName = T(english, german)); Controls.Add(box); return box;
        }
        preview = new PreviewPanel { Location = new Point(24, 16), Size = new Size(512, 100), BackColor = Color.FromArgb(29, 30, 32) };
        preview.Paint += PaintPreview;
        preview.MouseDown += (_, e) => { if (!engine.Active && e.Button is MouseButtons.Left or MouseButtons.Right) engine.PreviewClick(e.Button == MouseButtons.Right); };
        Controls.Add(preview);
        LabelAt("Size", "Größe", 24, 133, 95);
        sizeValue = LabelAt($"{preferences.Size} px", $"{preferences.Size} px", 473, 133, 70);
        var size = new TrackBar { Minimum = 24, Maximum = 64, Value = preferences.Size, TickFrequency = 8, Location = new Point(120, 127), Size = new Size(343, 40), BackColor = Paper };
        translations.Add(() => size.AccessibleName = T("Cursor size", "Cursorgröße"));
        size.ValueChanged += (_, _) => { preferences.Size = engine.Size = size.Value; sizeValue.Text = $"{size.Value} px"; Save(); }; Controls.Add(size);

        var movement = new List<CheckBox>();
        Switch("Movement animation", "Bewegungsanimation", 24, 174, preferences.Animation, v => { preferences.Animation = v; foreach (var box in movement) box.Enabled = v; }, 510);
        movement.Add(Switch("Rotation", "Drehung", 24, 205, preferences.Rotation, v => preferences.Rotation = v));
        movement.Add(Switch("Stretch", "Dehnung", 288, 205, preferences.Stretch, v => preferences.Stretch = v));
        movement.Add(Switch("Squash", "Stauchung", 24, 236, preferences.Squash, v => preferences.Squash = v));
        movement.Add(Switch("After-wobble", "Nachwippen", 288, 236, preferences.Wobble, v => preferences.Wobble = v));
        foreach (var box in movement) box.Enabled = preferences.Animation;

        positionMode = Combo(288, 280, 248, "Position smoothing style", "Art der Positionsglättung");
        effectsMode = Combo(288, 318, 248, "Effect smoothing style", "Art der Effektglättung");
        Switch("Smooth position", "Position glätten", 24, 282, preferences.PositionSmoothing, v => { preferences.PositionSmoothing = v; positionMode.Enabled = v; });
        Switch("Smooth animation", "Animation glätten", 24, 320, preferences.EffectsSmoothing, v => { preferences.EffectsSmoothing = v; effectsMode.Enabled = v; });
        positionMode.Enabled = preferences.PositionSmoothing; effectsMode.Enabled = preferences.EffectsSmoothing;
        positionMode.SelectedIndexChanged += (_, _) => { if (!translating) { preferences.PositionMethod = (SmoothingMethod)positionMode.SelectedIndex; Save(); UpdateSmoothingHint(); } };
        effectsMode.SelectedIndexChanged += (_, _) => { if (!translating) { preferences.EffectsMethod = (SmoothingMethod)effectsMode.SelectedIndex; Save(); } };
        smoothingHint = LabelAt("", "", 24, 354, 512, 34); smoothingHint.Font = new Font("Segoe UI", 8.5f);

        Switch("Left-click animation", "Linksklick-Animation", 24, 396, preferences.LeftClick, v => preferences.LeftClick = v);
        Switch("Right-click animation", "Rechtsklick-Animation", 288, 396, preferences.RightClick, v => preferences.RightClick = v);
        Switch("Click bounce", "Beim Klick einfedern", 24, 427, preferences.ClickPulse, v => preferences.ClickPulse = v, 510);
        Switch("Keep small while pressed", "Beim Gedrückthalten klein bleiben", 24, 458, preferences.HoldClickSize, v => preferences.HoldClickSize = v, 510);
        Switch("Show click rings (both buttons)", "Klickringe anzeigen (beide Tasten)", 24, 489, preferences.ClickRings, v => preferences.ClickRings = v, 510);
        Switch("Also replace text, loading and resize cursors", "Auch Text-, Lade- und Größenzeiger ersetzen", 24, 530, preferences.AllPointers, v => { preferences.AllPointers = engine.AllPointers = v; }, 510);

        LabelAt("Frame rate", "Bildrate", 24, 573);
        var fps = Combo(288, 567, 248, "Cursor frame rate", "Cursor-Bildrate");
        foreach (int rate in Preferences.FrameRates) fps.Items.Add($"{rate} FPS");
        fps.SelectedIndex = Array.IndexOf(Preferences.FrameRates, preferences.Fps);
        fps.SelectedIndexChanged += (_, _) => { preferences.Fps = timer.Fps = Preferences.FrameRates[fps.SelectedIndex]; Save(); };

        shortcutEnabled = Switch("Keyboard shortcut", "Tastenkürzel", 24, 581, preferences.ShortcutEnabled, SetShortcutEnabled);
        shortcutButton = new Button { Location = new Point(288, 574), Size = new Size(248, 36), FlatStyle = FlatStyle.Flat, BackColor = Color.White, Font = new Font("Segoe UI Semibold", 10), Enabled = preferences.ShortcutEnabled };
        shortcutButton.FlatAppearance.BorderColor = Color.FromArgb(204, 204, 199);
        shortcutButton.Click += (_, _) => { recording = !recording; ClearNotice(); UpdateShortcut(); };
        shortcutButton.LostFocus += (_, _) => { if (recording) { recording = false; UpdateShortcut(); } };
        Controls.Add(shortcutButton);
        shortcutHint = LabelAt("", "", 24, 615, 512, 27); shortcutHint.Font = new Font("Segoe UI", 8.5f);
        var languageLabel = LabelAt("Language", "Sprache", 24, 652);
        var language = Combo(288, 646, 248, "Language", "Sprache");
        language.Items.AddRange(["English", "Deutsch"]); language.SelectedIndex = German ? 1 : 0;
        language.SelectedIndexChanged += (_, _) => { preferences.Language = language.SelectedIndex == 1 ? "de" : "en"; ApplyLanguage(); Save(); };

        toggle = new Button { Location = new Point(24, 698), Size = new Size(300, 46), BackColor = Ink, ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
        toggle.FlatAppearance.BorderSize = 0; toggle.Click += (_, _) => Toggle(); Controls.Add(toggle);
        var hide = new Button { Location = new Point(336, 698), Size = new Size(200, 46), FlatStyle = FlatStyle.Flat };
        TextFor(hide, "Minimize to tray", "In den Infobereich");
        hide.FlatAppearance.BorderColor = Color.FromArgb(217, 217, 213); hide.Click += (_, _) => Hide(); Controls.Add(hide);
        status = LabelAt("", "", 24, 758, 512, 27); status.Font = new Font("Segoe UI", 9);
        var menu = new ContextMenuStrip();
        trayOpen = new ToolStripMenuItem(); trayOpen.Click += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        trayToggle = new ToolStripMenuItem(); trayToggle.Click += (_, _) => Toggle();
        trayExit = new ToolStripMenuItem(); trayExit.Click += (_, _) => Close();
        menu.Items.AddRange([trayOpen, trayToggle, new ToolStripSeparator(), trayExit]);
        tray = new NotifyIcon { Icon = Icon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => { Show(); Activate(); };
        // Keep cursor options on one page and installation/compatibility settings on another.
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var cursorPage = new TabPage { BackColor = Paper, AutoScroll = true };
        var systemPage = new TabPage { BackColor = Paper, AutoScroll = true };
        translations.Add(() => { cursorPage.Text = T("Cursor", "Cursor"); systemPage.Text = T("System", "System"); });
        tabs.TabPages.AddRange([cursorPage, systemPage]);
        var footer = new Panel { Dock = DockStyle.Bottom, Height = 108, BackColor = Paper };
        foreach (var control in new Control[] { toggle, hide, status })
        { Controls.Remove(control); footer.Controls.Add(control); control.Top -= 682; }
        var systemControls = new Control[] { shortcutEnabled, shortcutButton, shortcutHint, languageLabel, language };
        foreach (Control control in Controls.Cast<Control>().ToArray())
        {
            Controls.Remove(control);
            if (systemControls.Contains(control)) { systemPage.Controls.Add(control); control.Top -= 558; }
            else { cursorPage.Controls.Add(control); if (control.Top >= 174) control.Top += 34; }
        }
        var directionLabel = LabelAt("Direction", "Ausrichtung", 24, 175, 120);
        directionMode = Combo(150, 170, 386, "Direction style", "Art der Ausrichtung");
        directionMode.SelectedIndexChanged += (_, _) => { if (!translating && directionMode.SelectedIndex >= 0) { preferences.Direction = (DirectionStyle)directionMode.SelectedIndex; Save(); } };
        foreach (var control in new Control[] { directionLabel, directionMode })
        { Controls.Remove(control); cursorPage.Controls.Add(control); }
        CheckBox? startup = null;
        bool startupOn = false;
        try { startupOn = Startup.Enabled; } catch { }
        startup = Switch("Start with Windows", "Mit Windows starten", 24, 150, startupOn, v =>
        {
            if (!persistSettings) return;
            try { Startup.SetEnabled(v); ClearNotice(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                translating = true; startup!.Checked = !v; translating = false;
                Notice("Windows startup could not be changed.", "Autostart konnte nicht geändert werden.");
            }
        }, 510);
        var fullscreen = Switch("Use the app cursor in fullscreen apps", "In Vollbild-Apps deren eigenen Cursor verwenden", 24, 190, preferences.PauseFullscreen, v => preferences.PauseFullscreen = v, 510);
        var exclusionsLabel = LabelAt("Excluded apps (e.g. game.exe; other.exe)", "Ausnahmen (z. B. spiel.exe; anderes.exe)", 24, 234, 510);
        var exclusions = new TextBox { Location = new Point(24, 264), Size = new Size(505, 30), Text = preferences.ExcludedApps };
        translations.Add(() => exclusions.AccessibleName = T("Excluded applications", "Ausgeschlossene Anwendungen"));
        exclusions.TextChanged += (_, _) => { preferences.ExcludedApps = exclusions.Text; Save(); };
        var compatibility = LabelAt("Start menu and secure Windows prompts use the native cursor. Fullscreen apps and exclusions temporarily pause this overlay.", "Startmenü und geschützte Windows-Abfragen nutzen den Systemzeiger. In Vollbild-Apps und Ausnahmen pausiert das Overlay.", 24, 312, 505, 82);
        compatibility.Font = new Font("Segoe UI", 9);
        foreach (var control in new Control[] { startup, fullscreen, exclusionsLabel, exclusions, compatibility })
        { Controls.Remove(control); systemPage.Controls.Add(control); }
        var automaticUpdates = Switch("Check for updates automatically", "Automatisch nach Updates suchen", 24, 408, preferences.CheckUpdates, v => preferences.CheckUpdates = v, 505);
        checkUpdateButton = new Button { Location = new Point(24, 448), Size = new Size(244, 36), FlatStyle = FlatStyle.Flat };
        installUpdateButton = new Button { Location = new Point(284, 448), Size = new Size(244, 36), FlatStyle = FlatStyle.Flat, Enabled = false };
        TextFor(checkUpdateButton, "Check for updates", "Nach Updates suchen");
        TextFor(installUpdateButton, "Download and install", "Laden und installieren");
        checkUpdateButton.Click += async (_, _) => await CheckForUpdates();
        installUpdateButton.Click += async (_, _) => await InstallUpdate();
        updateLabel = LabelAt($"Version {UpdateService.CurrentVersion}", $"Version {UpdateService.CurrentVersion}", 24, 496, 505, 64);
        foreach (var control in new Control[] { automaticUpdates, checkUpdateButton, installUpdateButton, updateLabel })
        { Controls.Remove(control); systemPage.Controls.Add(control); }
        Controls.Add(tabs); Controls.Add(footer);
        ApplyLanguage();
        timer.Tick += (_, _) =>
        {
            if (quitSignal?.WaitOne(0) == true) { Close(); return; }
            double now = clock.Elapsed.TotalSeconds;
            if (persistSettings && preferences.CheckUpdates && now >= nextUpdateCheck && !checkingUpdate)
            { nextUpdateCheck = now + 21600; _ = CheckForUpdates(); }
            try
            {
                engine.Tick(now - lastTime);
                if (now - lastPreview >= 1.0 / 60 && Visible && WindowState != FormWindowState.Minimized)
                { preview.Invalidate(); lastPreview = now; }
            }
            catch (Exception e) { HandleError(e); }
            lastTime = now;
        };
        timer.Failed += HandleError;
        Shown += (_, _) =>
        {
            var area = Screen.FromControl(this).WorkingArea;
            if (Height > area.Height) { Height = area.Height; Top = area.Top; }
            shortcut = new GlobalShortcut(Handle);
            if (preferences.ShortcutEnabled && !shortcut.TrySet(preferences.ShortcutModifiers, preferences.ShortcutKey))
                Notice("Shortcut unavailable. Click its keys to choose another.", "Kürzel belegt. Zum Ändern auf die Tastenkombination klicken.");
            timer.Start(); Save();
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
            engine.Active ? T("Active · Closing restores your regular cursor", "Aktiv · Schließen stellt deinen normalen Cursor wieder her") : T("Paused · Your regular cursor is active", "Pausiert · Dein normaler Cursor ist aktiv");
    }
    private void PaintPreview(object? sender, PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var grid = new SolidBrush(Color.FromArgb(62, 63, 65));
        for (int y = 16; y < preview.Height - 30; y += 20) for (int x = 20; x < preview.Width; x += 20) e.Graphics.FillEllipse(grid, x, y, 2, 2);
        using var image = renderer.Render(engine.Pose, preferences.Size);
        e.Graphics.DrawImageUnscaled(image, preview.Width / 2 - image.Width / 2, (preview.Height - 25) / 2 - image.Height / 2);
        using var font = new Font("Segoe UI", 8.5f);
        using var brush = new SolidBrush(Color.FromArgb(185, 186, 188));
        string text = T("Move your mouse · Left- or right-click to preview", "Maus bewegen · Links- oder Rechtsklick für die Vorschau");
        var width = e.Graphics.MeasureString(text, font).Width;
        e.Graphics.DrawString(text, font, brush, (preview.Width - width) / 2, preview.Height - 24);
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

