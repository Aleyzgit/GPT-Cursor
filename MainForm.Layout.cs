using System.Runtime.InteropServices;

namespace GPTCursor;

internal sealed partial class MainForm
{
    private readonly List<Panel> pages = [];
    private readonly List<UiButton> navigation = [];
    private ComboBox themeMode = null!;
    private int pageIndex;

    private void BuildInterface()
    {
        void Translate(Control c, string en, string de) => translations.Add(() => { c.Text = T(en, de); if (c is not System.Windows.Forms.Label) c.AccessibleName = c.Text; });
        Label Label(string en, string de, bool muted = false, float size = 10)
        {
            var l = new Label { AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty, Tag = muted ? "muted" : null, Font = new Font("Segoe UI", size) };
            Translate(l, en, de); return l;
        }
        UiButton Button(string en, string de, EventHandler action)
        {
            var b = new UiButton { Height = 40, Dock = DockStyle.Fill, Margin = Padding.Empty };
            Translate(b, en, de); b.Click += action; browserActions[b] = () => action(b, EventArgs.Empty); return b;
        }
        UiSelect Select(string en, string de)
        {
            var c = new UiSelect { Anchor = AnchorStyles.Left | AnchorStyles.Right, Margin = new Padding(8, 0, 0, 0), Font = Font };
            translations.Add(() => c.AccessibleName = T(en, de)); return c;
        }
        UiToggle Switch(string en, string de, bool value, Action<bool> change)
        {
            var c = new UiToggle { Checked = value, Dock = DockStyle.Fill, Margin = Padding.Empty, MinimumSize = new Size(0, 44) };
            Translate(c, en, de);
            c.CheckedChanged += (_, _) => { if (!translating) { change(c.Checked); Save(); preview?.Invalidate(); } };
            return c;
        }
        void Add(TableLayoutPanel flow, Control child, int height)
        {
            int n = flow.RowCount++; flow.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); child.Dock = DockStyle.Fill; flow.Controls.Add(child, 0, n);
        }
        void Heading(TableLayoutPanel flow, string en, string de)
        { var l = Label(en, de, false, 11); l.Font = new Font("Segoe UI Semibold", 11); l.Padding = new Padding(0, 16, 0, 0); Add(flow, l, 48); }
        void Row(TableLayoutPanel flow, Control left, Control? right = null, int height = 56)
        {
            var row = new UiRow { ColumnCount = right == null ? 1 : 2, RowCount = 1, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 4) };
            row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, right == null ? 100 : 40));
            row.Controls.Add(left, 0, 0); left.Dock = DockStyle.Fill;
            if (right != null) { row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); row.Controls.Add(right, 1, 0); }
            Add(flow, row, height);
        }
        var sidebar = new Panel { Dock = DockStyle.Left, Width = 176, Tag = "sidebar" };
        var shell = new Panel { Dock = DockStyle.Fill };
        var host = new Panel { Dock = DockStyle.Fill };
        var footer = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 100, Padding = new Padding(32, 10, 32, 18), ColumnCount = 3, RowCount = 2 };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        status = Label("", "", true, 9); footer.Controls.Add(status, 0, 0); footer.SetColumnSpan(status, 3);
        toggle = Button("", "", (_, _) => Toggle()); ((UiButton)toggle).Primary = true;
        footer.Controls.Add(toggle, 0, 1);
        footer.Controls.Add(Button("Minimize to tray", "In den Infobereich", (_, _) => Hide()), 2, 1);
        shell.Controls.Add(host); shell.Controls.Add(footer); Controls.Add(shell); Controls.Add(sidebar);

        var caption = Label("Settings", "Einstellungen", false, 12); caption.Dock = DockStyle.None; caption.SetBounds(22, 25, 150, 30); caption.Tag = "sidebar"; caption.Font = new Font("Segoe UI Semibold", 12); sidebar.Controls.Add(caption);
        var version = Label($"v{UpdateService.CurrentVersion}", $"v{UpdateService.CurrentVersion}", true, 9); version.Dock = DockStyle.Bottom; version.Height = 42; version.Padding = new Padding(24, 0, 0, 0); version.Tag = "sidebar"; sidebar.Controls.Add(version);
        TableLayoutPanel Page(string en, string de, string subEn, string subDe)
        {
            int index = pages.Count;
            var button = new UiButton { Glyph = index, Tag = "sidebar", Location = new Point(12, 80 + index * 48), Size = new Size(152, 42) };
            Translate(button, en, de); button.Click += (_, _) => SelectPage(index); sidebar.Controls.Add(button); navigation.Add(button);
            var page = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(32, 20, 32, 20), Visible = false };
            var flow = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 1, RowCount = 0, Margin = Padding.Empty };
            flow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); page.Controls.Add(flow); pages.Add(page); host.Controls.Add(page);
            var title = Label(en, de, false, 22); title.Font = new Font("Segoe UI Semibold", 22); Add(flow, title, 44);
            Add(flow, Label(subEn, subDe, true), 40);
            return flow;
        }
        var cursor = Page("Cursor", "Cursor", "Appearance and direction", "Darstellung und Ausrichtung");
        var motion = Page("Motion", "Bewegung", "Animation and smoothing", "Animation und Glättung");
        var clicks = Page("Clicks", "Klicks", "Feedback for your mouse buttons", "Rückmeldung für deine Maustasten");
        var system = Page("System", "System", "General settings and updates", "Allgemeine Einstellungen und Updates");

        preview = new PreviewPanel { Margin = new Padding(0, 8, 0, 12) };
        preview.Paint += PaintPreview;
        preview.MouseDown += (_, e) => { if (!engine.Active && e.Button is MouseButtons.Left or MouseButtons.Right) engine.PreviewClick(e.Button == MouseButtons.Right); };
        Add(cursor, preview, 188);
        Heading(cursor, "Appearance", "Darstellung");
        var sizeLayout = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Dock = DockStyle.Fill, Margin = Padding.Empty };
        sizeLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        sizeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); sizeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62));
        var slider = new UiSlider { Minimum = 24, Maximum = 64, Value = preferences.Size, Dock = DockStyle.Fill, Margin = Padding.Empty, SmallChange = 1, LargeChange = 4 };
        translations.Add(() => slider.AccessibleName = T("Cursor size", "Cursorgröße"));
        sizeValue = Label("", "", true); sizeValue.TextAlign = ContentAlignment.MiddleRight;
        slider.ValueChanged += (_, _) => { preferences.Size = engine.Size = slider.Value; sizeValue.Text = $"{slider.Value} px"; Save(); preview.Invalidate(); };
        sizeLayout.Controls.Add(slider, 0, 0); sizeLayout.Controls.Add(sizeValue, 1, 0); Row(cursor, Label("Size", "Größe"), sizeLayout);
        var fps = Select("Cursor frame rate", "Cursor-Bildrate"); foreach (int rate in Preferences.FrameRates) fps.Items.Add($"{rate} FPS");
        fps.SelectedIndex = Array.IndexOf(Preferences.FrameRates, preferences.Fps);
        fps.SelectedIndexChanged += (_, _) => { if (fps.SelectedIndex >= 0) { preferences.Fps = timer.Fps = Preferences.FrameRates[fps.SelectedIndex]; Save(); } };
        Row(cursor, Label("Frame rate", "Bildrate"), fps);
        directionMode = Select("Direction style", "Art der Ausrichtung");
        directionMode.SelectedIndexChanged += (_, _) => { if (!translating && directionMode.SelectedIndex >= 0) { preferences.Direction = (DirectionStyle)directionMode.SelectedIndex; Save(); } };
        Row(cursor, Label("Direction", "Ausrichtung"), directionMode);
        Row(cursor, Switch("Also replace text, loading and resize cursors", "Auch Text-, Lade- und Größenzeiger ersetzen", preferences.AllPointers, v => preferences.AllPointers = engine.AllPointers = v), height: 66);

        var movement = new List<CheckBox>();
        Row(motion, Switch("Movement animation", "Bewegungsanimation", preferences.Animation, v => { preferences.Animation = v; foreach (var box in movement) box.Enabled = v; }));
        foreach (var item in new (string En, string De, bool Value, Action<bool> Set)[] {
            ("Rotation", "Drehung", preferences.Rotation, v => preferences.Rotation = v),
            ("Stretch", "Dehnung", preferences.Stretch, v => preferences.Stretch = v),
            ("Squash", "Stauchung", preferences.Squash, v => preferences.Squash = v),
            ("After-wobble", "Nachwippen", preferences.Wobble, v => preferences.Wobble = v) })
        { var box = Switch(item.En, item.De, item.Value, item.Set); box.Enabled = preferences.Animation; movement.Add(box); Row(motion, box); }
        Heading(motion, "Smoothing", "Glättung");
        positionMode = Select("Position smoothing style", "Art der Positionsglättung"); effectsMode = Select("Effect smoothing style", "Art der Effektglättung");
        Row(motion, Switch("Smooth position", "Position glätten", preferences.PositionSmoothing, v => { preferences.PositionSmoothing = v; positionMode.Enabled = v; }), positionMode, 64);
        Row(motion, Switch("Smooth animation", "Animation glätten", preferences.EffectsSmoothing, v => { preferences.EffectsSmoothing = v; effectsMode.Enabled = v; }), effectsMode, 64);
        positionMode.Enabled = preferences.PositionSmoothing; effectsMode.Enabled = preferences.EffectsSmoothing;
        positionMode.SelectedIndexChanged += (_, _) => { if (!translating && positionMode.SelectedIndex >= 0) { preferences.PositionMethod = (SmoothingMethod)positionMode.SelectedIndex; Save(); UpdateSmoothingHint(); } };
        effectsMode.SelectedIndexChanged += (_, _) => { if (!translating && effectsMode.SelectedIndex >= 0) { preferences.EffectsMethod = (SmoothingMethod)effectsMode.SelectedIndex; Save(); } };
        smoothingHint = Label("", "", true, 9); Add(motion, smoothingHint, 52);

        Heading(clicks, "Mouse buttons", "Maustasten");
        Row(clicks, Switch("Left-click animation", "Linksklick-Animation", preferences.LeftClick, v => preferences.LeftClick = v));
        Row(clicks, Switch("Right-click animation", "Rechtsklick-Animation", preferences.RightClick, v => preferences.RightClick = v));
        Heading(clicks, "Click effects", "Klickeffekte");
        Row(clicks, Switch("Click bounce", "Beim Klick einfedern", preferences.ClickPulse, v => preferences.ClickPulse = v));
        Row(clicks, Switch("Keep small while pressed", "Beim Gedrückthalten klein bleiben", preferences.HoldClickSize, v => preferences.HoldClickSize = v));
        Row(clicks, Switch("Show click rings (both buttons)", "Klickringe anzeigen (beide Tasten)", preferences.ClickRings, v => preferences.ClickRings = v));
        Add(clicks, Label("Try your changes in the cursor preview.", "Probiere deine Änderungen in der Cursor-Vorschau aus.", true, 9), 52);

        var language = Select("Language", "Sprache"); language.Items.AddRange(["English", "Deutsch"]); language.SelectedIndex = German ? 1 : 0;
        language.SelectedIndexChanged += (_, _) => { if (!translating) { preferences.Language = language.SelectedIndex == 1 ? "de" : "en"; ApplyLanguage(); Save(); } };
        Row(system, Label("Language", "Sprache"), language);
        themeMode = Select("Appearance", "Farbschema");
        themeMode.SelectedIndexChanged += (_, _) => { if (!translating && themeMode.SelectedIndex >= 0) { preferences.Theme = new[] { "system", "light", "dark" }[themeMode.SelectedIndex]; ApplyTheme(); Save(); } };
        Row(system, Label("Appearance", "Farbschema"), themeMode);
        bool startupOn = false; try { startupOn = Startup.Enabled; } catch { }
        CheckBox? startup = null;
        startup = Switch("Start with Windows", "Mit Windows starten", startupOn, v => {
            if (!persistSettings) return;
            try { Startup.SetEnabled(v); ClearNotice(); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) {
                translating = true; startup!.Checked = !v; translating = false; Notice("Windows startup could not be changed.", "Autostart konnte nicht geändert werden."); }
        });
        Row(system, startup);
        Heading(system, "Shortcut", "Tastenkürzel");
        shortcutEnabled = Switch("Keyboard shortcut", "Tastenkürzel", preferences.ShortcutEnabled, SetShortcutEnabled);
        shortcutButton = Button("", "", (_, _) => { recording = !recording; ClearNotice(); UpdateShortcut(); }); ((UiButton)shortcutButton).Keycaps = true;
        shortcutButton.LostFocus += (_, _) => { if (recording) { recording = false; UpdateShortcut(); } };
        Row(system, shortcutEnabled, shortcutButton, 62); shortcutHint = Label("", "", true, 9); Add(system, shortcutHint, 42);
        Heading(system, "Compatibility", "Kompatibilität");
        Row(system, Switch("Pause in fullscreen apps", "In Vollbild-Apps pausieren", preferences.PauseFullscreen, v => preferences.PauseFullscreen = v));
        Add(system, Label("Excluded apps (e.g. game.exe; other.exe)", "Ausnahmen (z. B. spiel.exe; anderes.exe)", true, 9), 34);
        var field = new Panel { Padding = new Padding(12, 8, 12, 8), Margin = new Padding(0, 0, 0, 8) };
        var exclusions = new TextBox { BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Text = preferences.ExcludedApps };
        translations.Add(() => exclusions.AccessibleName = T("Excluded applications", "Ausgeschlossene Anwendungen"));
        exclusions.TextChanged += (_, _) => { preferences.ExcludedApps = exclusions.Text; Save(); }; field.Controls.Add(exclusions);
        field.Paint += (_, e) => { using var path = UiTheme.Shape(new RectangleF(.5f, .5f, field.Width - 1, field.Height - 1), 10 * field.DeviceDpi / 96f); using var pen = new Pen(exclusions.Focused ? UiTheme.Muted : UiTheme.Line); e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias; e.Graphics.DrawPath(pen, path); };
        exclusions.GotFocus += (_, _) => field.Invalidate(); exclusions.LostFocus += (_, _) => field.Invalidate(); Add(system, field, 46);
        Add(system, Label("Start/search uses a static fallback. Other cursor apps may override it. Secure Windows prompts use the system cursor.", "Start/Suche nutzt einen statischen Fallback. Andere Cursor-Apps können ihn überschreiben. Geschützte Windows-Abfragen nutzen den Systemzeiger.", true, 9), 60);
        Heading(system, "Updates", "Updates");
        Row(system, Switch("Check for updates automatically", "Automatisch nach Updates suchen", preferences.CheckUpdates, v => preferences.CheckUpdates = v));
        var updateActions = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 10, 0, 0) };
        updateActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); updateActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        checkUpdateButton = Button("Check for updates", "Nach Updates suchen", async (_, _) => await CheckForUpdates());
        installUpdateButton = Button("Download and install", "Laden und installieren", async (_, _) => await InstallUpdate()); installUpdateButton.Enabled = false;
        updateActions.Controls.Add(checkUpdateButton, 0, 0); updateActions.Controls.Add(installUpdateButton, 1, 0); Add(system, updateActions, 52);
        updateLabel = Label($"Version {UpdateService.CurrentVersion}", $"Version {UpdateService.CurrentVersion}", true, 9); Add(system, updateLabel, 60);

        var menu = new ContextMenuStrip(); trayOpen = new ToolStripMenuItem(); trayToggle = new ToolStripMenuItem(); trayExit = new ToolStripMenuItem();
        trayOpen.Click += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); }; trayToggle.Click += (_, _) => Toggle(); trayExit.Click += (_, _) => Close();
        menu.Items.AddRange([trayOpen, trayToggle, new ToolStripSeparator(), trayExit]); tray = new NotifyIcon { Icon = Icon, ContextMenuStrip = menu, Visible = persistSettings };
        tray.DoubleClick += (_, _) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        SelectPage(0);
    }
    internal void SelectPage(int index)
    {
        pageIndex = index;
        for (int i = 0; i < pages.Count; i++) { pages[i].Visible = i == index; navigation[i].Selected = i == index; navigation[i].AccessibleDescription = i == index ? T("Selected", "Ausgewählt") : ""; navigation[i].Invalidate(); }
        pages[index].BringToFront();
    }
    private void ApplyTheme()
    {
        UiTheme.Set(preferences.Theme); UiTheme.Apply(this);
        if (IsHandleCreated) { int dark = UiTheme.Dark ? 1 : 0; DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (preferences != null) ApplyTheme(); }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);
}
