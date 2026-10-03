using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace GPTCursor;

internal sealed partial class MainForm
{
    private WebView2? browser;
    private bool browserReady;
    private bool micaAvailable;
    private double lastBrowserState;
    private string? lastBrowserJson;
    private readonly Dictionary<string, Control> browserControls = [];
    private readonly Dictionary<Control, string> browserIds = [];
    private readonly Dictionary<Control, Action> browserActions = [];
    private const string UiAddress = "https://gptcursor.local/index.html";

    private async Task StartBrowserInterface()
    {
        try
        {
            // A dedicated local profile; never use the user's browser profile.
            var environment = await CoreWebView2Environment.CreateAsync(null,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GPTCursor", "WebView2"));
            if (closing) return;
            browser = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.Transparent, Visible = false };
            Controls.Add(browser);
            await browser.EnsureCoreWebView2Async(environment);
            if (closing) return;
            var core = browser.CoreWebView2;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.IsZoomControlEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = false;
            core.Settings.IsNonClientRegionSupportEnabled = true;
            core.SetVirtualHostNameToFolderMapping("gptcursor.local", Path.Combine(AppContext.BaseDirectory, "ui"), CoreWebView2HostResourceAccessKind.DenyCors);
            core.NavigationStarting += (_, e) => { if (e.Uri != UiAddress) e.Cancel = true; };
            core.NewWindowRequested += (_, e) => e.Handled = true;
            core.PermissionRequested += (_, e) => e.State = CoreWebView2PermissionState.Deny;
            core.DownloadStarting += (_, e) => e.Cancel = true;
            core.WebMessageReceived += (_, e) =>
            {
                if (e.Source != UiAddress || closing) return;
                try { HandleBrowserCommand(e.WebMessageAsJson); }
                catch (Exception error) when (error is JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or FormatException)
                { SendBrowserState(force: true); }
            };
            core.NavigationCompleted += (_, e) =>
            {
                if (!e.IsSuccess || closing) { UseNativeInterface(); return; }
                browserReady = true;
                // Keep the existing, tested settings controls as the application model.
                // The web surface uses the very same change handlers and validation.
                Padding = new Padding(6);
                foreach (Control child in Controls) if (child != browser) child.Hide();
                browser.Dock = DockStyle.None;
                browser.Visible = true; browser.BringToFront();
                SetWindowPos(Handle, 0, 0, 0, 0, 0, 0x0027); // frame changed, no move/size/z-order
                LayoutBrowser(); ApplyWindowMaterial(); SendBrowserState(force: true);
            };
            core.ProcessFailed += (_, _) => { if (!closing) UseNativeInterface(); };
            core.Navigate(UiAddress);
        }
        catch (Exception e) when (e is WebView2RuntimeNotFoundException or COMException or InvalidOperationException or IOException or UnauthorizedAccessException)
        { if (!closing) UseNativeInterface(); }
    }

    private void UseNativeInterface()
    {
        browserReady = false; browser?.Hide(); Padding = Padding.Empty;
        foreach (Control child in Controls) if (child != browser) child.Show();
        SetWindowPos(Handle, 0, 0, 0, 0, 0, 0x0027);
        Notice("WebView2 is unavailable. The standard settings view is active.", "WebView2 ist nicht verfügbar. Die Standardansicht ist aktiv.");
    }

    private void ApplyWindowMaterial()
    {
        int dark = UiTheme.Dark ? 1 : 0;
        DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
        int backdrop = 2; // DWMSBT_MAINWINDOW (Mica), as used by the reference on Windows.
        micaAvailable = DwmSetWindowAttribute(Handle, 38, ref backdrop, sizeof(int)) == 0;
        var margins = new GlassMargins { Left = micaAvailable ? -1 : 0 };
        DwmExtendFrameIntoClientArea(Handle, ref margins);
        Invalidate();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        if (browserReady) e.Graphics.Clear(micaAvailable ? Color.Black : UiTheme.Sidebar);
        else base.OnPaintBackground(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (browserReady) LayoutBrowser();
    }

    private void LayoutBrowser()
    {
        if (browser == null) return;
        int edge = WindowState == FormWindowState.Maximized ? 0 : Math.Max(4, (int)(6 * DeviceDpi / 96f));
        browser.Bounds = new Rectangle(edge, edge, Math.Max(1, ClientSize.Width - edge * 2), Math.Max(1, ClientSize.Height - edge * 2));
    }

    private string BrowserId(Control control)
    {
        if (!browserIds.TryGetValue(control, out var id))
        { id = "c" + browserIds.Count; browserIds.Add(control, id); browserControls.Add(id, control); }
        return id;
    }

    private object BrowserControl(Control control) => control switch
    {
        CheckBox c => new { id = BrowserId(c), type = "toggle", text = c.Text, value = (object)c.Checked, enabled = c.Enabled },
        ComboBox c => new { id = BrowserId(c), type = "select", text = c.AccessibleName, value = (object)c.SelectedIndex, enabled = c.Enabled, options = c.Items.Cast<object>().Select(c.GetItemText).ToArray() },
        TrackBar c => new { id = BrowserId(c), type = "range", text = c.AccessibleName, value = (object)c.Value, enabled = c.Enabled, min = c.Minimum, max = c.Maximum },
        TextBox c => new { id = BrowserId(c), type = "input", text = c.AccessibleName, value = (object)c.Text, enabled = c.Enabled },
        Button c => new { id = BrowserId(c), type = c == shortcutButton ? "shortcut" : "button", text = c.Text, enabled = c.Enabled, primary = c == toggle },
        _ => new { id = BrowserId(control), type = "text", text = control.Text, enabled = control.Enabled }
    };

    private static IEnumerable<Control> Descendants(Control parent)
    {
        foreach (Control c in parent.Controls) { yield return c; foreach (var child in Descendants(c)) yield return child; }
    }

    internal string BrowserModelJson()
    {
        var models = pages.Select((page, index) =>
        {
            var flow = (TableLayoutPanel)page.Controls[0];
            var rows = flow.Controls.Cast<Control>().OrderBy(flow.GetRow).Skip(2).Select(c =>
            {
                if (c == preview) return (object)new { type = "preview", text = T("Move your mouse · Click to preview", "Maus bewegen · Klicken für die Vorschau") };
                if (c is Label l) return new { type = l.Font.Size > 10 ? "heading" : "hint", text = l.Text };
                var controls = Descendants(c).Where(x => x is CheckBox or ComboBox or TrackBar or TextBox or Button).ToArray();
                string label = c.Controls.OfType<Label>().FirstOrDefault()?.Text ?? "";
                return new { type = "row", text = label, controls = controls.Select(BrowserControl).ToArray() };
            }).ToArray();
            return new { title = navigation[index].Text, rows };
        }).ToArray();
        return JsonSerializer.Serialize(new
        {
            type = "state", language = preferences.Language, dark = UiTheme.Dark, version = UpdateService.CurrentVersion.ToString(),
            selected = pageIndex, recording, active = engine.Active, status = status.Text,
            settings = T("Settings", "Einstellungen"), pages = models, toggle = BrowserControl(toggle),
            tray = T("Minimize to tray", "In den Infobereich"),
            window = new { minimize = T("Minimize", "Minimieren"), maximize = T("Maximize / restore", "Maximieren / wiederherstellen"), close = T("Close", "Schließen") }
        });
    }

    private void SendBrowserState(bool force = false)
    {
        if (!browserReady || browser?.CoreWebView2 == null || closing) return;
        string json = BrowserModelJson();
        if (force || json != lastBrowserJson) { browser.CoreWebView2.PostWebMessageAsJson(json); lastBrowserJson = json; }
    }

    private void SendBrowserFrame(double now)
    {
        if (now - lastBrowserState >= .25) { SendBrowserState(); lastBrowserState = now; }
        if (pageIndex != 0 || browser?.CoreWebView2 == null) return;
        var pose = engine.Pose;
        browser.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "pose", size = preferences.Size,
            rotation = pose.Rotation, stretch = pose.Stretch, squash = pose.Squash, axis = pose.Axis, scale = pose.Scale, leftRing = pose.LeftRing, rightRing = pose.RightRing }));
    }

    internal void HandleBrowserCommand(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string? action = root.GetProperty("action").GetString();
        switch (action)
        {
            case "ready": SendBrowserState(force: true); return;
            case "page":
                int index = root.GetProperty("value").GetInt32();
                if (index >= 0 && index < pages.Count) SelectPage(index);
                break;
            case "tray": if (persistSettings) Hide(); else WindowState = FormWindowState.Minimized; break;
            case "minimize": WindowState = FormWindowState.Minimized; break;
            case "maximize":
                MaximizedBounds = Screen.FromControl(this).WorkingArea;
                WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
                break;
            case "close": Close(); return;
            case "preview": if (!engine.Active) engine.PreviewClick(root.GetProperty("right").GetBoolean()); break;
            case "cancelShortcut": recording = false; UpdateShortcut(); break;
            case "shortcut":
                if (!recording) return;
                int key = root.GetProperty("key").GetInt32(), modifiers = root.GetProperty("modifiers").GetInt32();
                if (key is < 0 or > 255 || modifiers is < 0 or > 7) return;
                Keys combination = (Keys)key | ((modifiers & 1) != 0 ? Keys.Alt : 0) | ((modifiers & 2) != 0 ? Keys.Control : 0) | ((modifiers & 4) != 0 ? Keys.Shift : 0);
                Message message = new(); ProcessCmdKey(ref message, combination);
                break;
            case "change": case "click":
                if (!browserControls.TryGetValue(root.GetProperty("id").GetString() ?? "", out var control) || !control.Enabled) return;
                if (action == "click") { if (browserActions.TryGetValue(control, out var invoke)) invoke(); break; }
                var value = root.GetProperty("value");
                switch (control)
                {
                    case CheckBox c when value.ValueKind is JsonValueKind.True or JsonValueKind.False: c.Checked = value.GetBoolean(); break;
                    case ComboBox c when value.TryGetInt32(out int selected) && selected >= 0 && selected < c.Items.Count: c.SelectedIndex = selected; break;
                    case TrackBar c when value.TryGetInt32(out int size) && size >= c.Minimum && size <= c.Maximum: c.Value = size; break;
                    case TextBox c when value.ValueKind == JsonValueKind.String && value.GetString()!.Length <= 512: c.Text = value.GetString(); break;
                }
                break;
        }
        if (browserReady) ApplyWindowMaterial();
        SendBrowserState(force: true);
    }

    [StructLayout(LayoutKind.Sequential)] private struct GlassMargins { public int Left, Right, Top, Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(nint window, ref GlassMargins margins);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint window, nint after, int x, int y, int cx, int cy, uint flags);
}
