using System.Drawing.Drawing2D;
using System.Diagnostics;
using System.ComponentModel;
using Microsoft.Win32;

namespace GPTCursor;

// Native controls keep their keyboard/accessibility behavior. Only their drawing
// changes. Palette and corner treatment are based on the inspected desktop CSS.
internal static class UiTheme
{
    internal static bool Dark { get; private set; }
    internal static Color Canvas => Dark ? Color.FromArgb(33, 33, 33) : Color.White;
    internal static Color Sidebar => Dark ? Color.FromArgb(24, 24, 24) : Color.FromArgb(249, 249, 249);
    internal static Color Soft => Dark ? Color.FromArgb(48, 48, 48) : Color.FromArgb(243, 243, 243);
    internal static Color Hover => Dark ? Color.FromArgb(57, 57, 57) : Color.FromArgb(235, 235, 235);
    internal static Color Ink => Dark ? Color.FromArgb(243, 243, 243) : Color.FromArgb(28, 28, 28);
    internal static Color Muted => Dark ? Color.FromArgb(175, 175, 175) : Color.FromArgb(93, 93, 93);
    internal static Color Faint => Dark ? Color.FromArgb(132, 132, 132) : Color.FromArgb(143, 143, 143);
    internal static Color Line => Dark ? Color.FromArgb(57, 57, 57) : Color.FromArgb(232, 232, 232);
    internal static Color Inverse => Dark ? Color.FromArgb(20, 20, 20) : Color.White;
    internal static void Set(string mode)
    {
        bool systemDark = false;
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); systemDark = key?.GetValue("AppsUseLightTheme") is int value && value == 0; }
        catch (Exception e) when (e is UnauthorizedAccessException or System.Security.SecurityException) { }
        Dark = mode == "dark" || mode == "system" && systemDark;
    }
    internal static void Apply(Control root)
    {
        root.BackColor = root.Tag as string == "sidebar" ? Sidebar : Canvas;
        root.ForeColor = root.Tag as string == "muted" ? Muted : Ink;
        foreach (Control child in root.Controls) Apply(child);
        root.Invalidate();
    }
    // Local superelliptic corners with straight edges. CSS superellipse(1.5)
    // corresponds to an exponent of 2^1.5; regular round corners use exponent 2.
    internal static GraphicsPath Shape(RectangleF bounds, float radius, bool smooth = true)
    {
        var path = new GraphicsPath();
        radius = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
        if (radius < .1f) { path.AddRectangle(bounds); return path; }
        double exponent = smooth ? Math.Pow(2, 1.5) : 2;
        var points = new List<PointF>();
        (float X, float Y)[] centers = [(bounds.Right - radius, bounds.Top + radius), (bounds.Right - radius, bounds.Bottom - radius), (bounds.Left + radius, bounds.Bottom - radius), (bounds.Left + radius, bounds.Top + radius)];
        for (int corner = 0; corner < 4; corner++)
            for (int step = 0; step <= 20; step++)
            {
                double a = (-90 + corner * 90 + step * 4.5) * Math.PI / 180;
                double x = Math.Cos(a), y = Math.Sin(a);
                points.Add(new(centers[corner].X + radius * (float)(Math.Sign(x) * Math.Pow(Math.Abs(x), 2 / exponent)), centers[corner].Y + radius * (float)(Math.Sign(y) * Math.Pow(Math.Abs(y), 2 / exponent))));
            }
        path.AddPolygon(points.ToArray()); return path;
    }
    internal static void Fill(Graphics g, RectangleF bounds, float radius, Color color, bool smooth = true)
    { using var path = Shape(bounds, radius, smooth); using var brush = new SolidBrush(color); g.FillPath(brush, path); }
    internal static void Focus(Graphics g, Rectangle bounds, float scale)
    { using var path = Shape(RectangleF.Inflate(bounds, -2 * scale, -2 * scale), 10 * scale); using var pen = new Pen(Muted, scale); g.DrawPath(pen, path); }
}

internal sealed class UiButton : Button
{
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Primary { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Selected { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Keycaps { get; set; }
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal int Glyph { get; set; } = -1;
    private bool hovered, pressed;
    internal UiButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0; Cursor = Cursors.Hand; UseVisualStyleBackColor = false;
    }
    protected override void OnMouseEnter(EventArgs e) { hovered = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hovered = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f;
        e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Canvas); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        Color bg = Primary && Enabled ? UiTheme.Ink : Selected || pressed ? UiTheme.Hover : hovered ? UiTheme.Soft : Keycaps ? UiTheme.Soft : BackColor;
        Color fg = !Enabled ? UiTheme.Faint : Primary ? UiTheme.Inverse : UiTheme.Ink;
        UiTheme.Fill(e.Graphics, new RectangleF(1, 1, Width - 2, Height - 2), (Primary ? 22 : 12) * s, bg, !Primary);
        if (Keycaps && !Text.Contains('…'))
        {
            string[] keys = Text.Split(" + ");
            int gap = (int)(5 * s), h = (int)(26 * s);
            int[] widths = keys.Select(k => TextRenderer.MeasureText(k, Font).Width + (int)(12 * s)).ToArray();
            int x = (Width - widths.Sum() - gap * (keys.Length - 1)) / 2;
            for (int i = 0; i < keys.Length; i++)
            {
                var rect = new Rectangle(x, (Height - h) / 2, widths[i], h);
                UiTheme.Fill(e.Graphics, rect, 5 * s, UiTheme.Canvas);
                TextRenderer.DrawText(e.Graphics, keys[i], Font, rect, fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                x += widths[i] + gap;
            }
        }
        else
        {
            var bounds = new Rectangle((int)((Glyph < 0 ? 10 : 42) * s), 0, Width - (int)((Glyph < 0 ? 20 : 50) * s), Height);
            TextRenderer.DrawText(e.Graphics, Text, Font, bounds, fg, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | (Glyph < 0 ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left));
            if (Glyph >= 0)
            {
                using var pen = new Pen(fg, 1.5f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
                var state = e.Graphics.Save(); e.Graphics.TranslateTransform(15 * s, Height / 2f - 9 * s); e.Graphics.ScaleTransform(s, s);
                if (Glyph == 0) e.Graphics.DrawPolygon(pen, [new(2, 1), new(15, 8), new(9, 10), new(6, 16)]);
                if (Glyph == 1) e.Graphics.DrawBezier(pen, 1, 12, 5, -4, 10, 23, 17, 5);
                if (Glyph == 2) { e.Graphics.DrawEllipse(pen, 3, 3, 12, 12); e.Graphics.DrawLine(pen, 9, 0, 9, 3); e.Graphics.DrawLine(pen, 0, 9, 3, 9); }
                if (Glyph == 3) { e.Graphics.DrawLine(pen, 1, 5, 17, 5); e.Graphics.DrawLine(pen, 1, 13, 17, 13); using var cover = new SolidBrush(bg); e.Graphics.FillRectangle(cover, 5, 2, 4, 6); e.Graphics.FillRectangle(cover, 11, 10, 4, 6); e.Graphics.DrawEllipse(pen, 4, 2, 6, 6); e.Graphics.DrawEllipse(pen, 10, 10, 6, 6); }
                e.Graphics.Restore(state);
            }
        }
        if (Focused && ShowFocusCues) UiTheme.Focus(e.Graphics, ClientRectangle, s);
    }
}

internal sealed class UiToggle : CheckBox
{
    private readonly System.Windows.Forms.Timer animation = new() { Interval = 15 };
    private readonly Stopwatch transition = new();
    private double position, from;
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal bool Compact { get; set; }
    internal UiToggle()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        Cursor = Cursors.Hand;
        animation.Tick += (_, _) => { double t = Math.Min(1, transition.Elapsed.TotalMilliseconds / 150); position = from + ((Checked ? 1 : 0) - from) * (.5 - .5 * Math.Cos(Math.PI * t)); Invalidate(); if (t >= 1) animation.Stop(); };
    }
    protected override void OnCheckedChanged(EventArgs e)
    { from = position; if (IsHandleCreated && Visible) { transition.Restart(); animation.Start(); } else position = Checked ? 1 : 0; base.OnCheckedChanged(e); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f; e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int w = (int)(36 * s), h = (int)(22 * s), x = Width - w - (int)(2 * s), y = (Height - h) / 2;
        UiTheme.Fill(e.Graphics, new Rectangle(x, y, w, h), h / 2f, Checked && Enabled ? UiTheme.Ink : UiTheme.Hover, false);
        float d = 16 * s, cx = x + 3 * s + (float)position * (w - d - 6 * s);
        using var thumb = new SolidBrush(Checked && Enabled ? UiTheme.Inverse : UiTheme.Canvas); e.Graphics.FillEllipse(thumb, cx, y + 3 * s, d, d);
        if (!Compact) TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, x - (int)(16 * s), Height), Enabled ? ForeColor : UiTheme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        if (Focused && ShowFocusCues) UiTheme.Focus(e.Graphics, ClientRectangle, s);
    }
    protected override void Dispose(bool disposing) { if (disposing) animation.Dispose(); base.Dispose(disposing); }
}

internal sealed class UiSelect : ComboBox
{
    internal UiSelect()
    {
        DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat;
        ItemHeight = 28; IntegralHeight = false; DropDownHeight = 240;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); ItemHeight = (int)(28 * DeviceDpi / 96f); }
    protected override void OnDpiChangedAfterParent(EventArgs e) { base.OnDpiChangedAfterParent(e); ItemHeight = (int)(28 * DeviceDpi / 96f); Invalidate(); }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f; e.Graphics.Clear(Parent?.BackColor ?? UiTheme.Canvas); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        UiTheme.Fill(e.Graphics, new RectangleF(0, 0, Width - 1, Height - 1), 10 * s, UiTheme.Soft);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)(12 * s), 0, Width - (int)(38 * s), Height), Enabled ? UiTheme.Ink : UiTheme.Faint, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        using var pen = new Pen(Enabled ? UiTheme.Muted : UiTheme.Faint, 1.4f * s) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        float x = Width - 18 * s, y = Height / 2f; e.Graphics.DrawLines(pen, new PointF[] {new(x - 4 * s, y - 2 * s), new(x, y + 2 * s), new(x + 4 * s, y - 2 * s)});
        if (Focused && ShowFocusCues) UiTheme.Focus(e.Graphics, ClientRectangle, s);
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        using var brush = new SolidBrush((e.State & DrawItemState.Selected) != 0 ? UiTheme.Hover : UiTheme.Canvas); e.Graphics.FillRectangle(brush, e.Bounds);
        var bounds = Rectangle.Inflate(e.Bounds, -(int)(10 * DeviceDpi / 96f), 0);
        TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, bounds, UiTheme.Ink, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
    protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
}

internal sealed class UiSlider : TrackBar
{
    internal UiSlider()
    { AutoSize = false; TickStyle = TickStyle.None; SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e)
    {
        float s = DeviceDpi / 96f, start = 12 * s, end = Width - 12 * s, y = Height / 2f;
        float x = start + (end - start) * (Value - Minimum) / (Maximum - Minimum);
        e.Graphics.Clear(BackColor); e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        UiTheme.Fill(e.Graphics, new RectangleF(start, y - 2 * s, end - start, 4 * s), 2 * s, UiTheme.Line, false);
        UiTheme.Fill(e.Graphics, new RectangleF(start, y - 2 * s, Math.Max(1, x - start), 4 * s), 2 * s, UiTheme.Ink, false);
        using var fill = new SolidBrush(UiTheme.Ink); e.Graphics.FillEllipse(fill, x - 7 * s, y - 7 * s, 14 * s, 14 * s);
        if (Focused && ShowFocusCues) UiTheme.Focus(e.Graphics, ClientRectangle, s);
    }
    protected override void OnMouseDown(MouseEventArgs e) { Focus(); UpdateValue(e.X); base.OnMouseDown(e); }
    protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) UpdateValue(e.X); base.OnMouseMove(e); }
    private void UpdateValue(int x) { float inset = 12 * DeviceDpi / 96f; Value = Math.Clamp((int)Math.Round(Minimum + (Maximum - Minimum) * (x - inset) / Math.Max(1, Width - inset * 2)), Minimum, Maximum); Invalidate(); }
    protected override void OnValueChanged(EventArgs e) { base.OnValueChanged(e); Invalidate(); }
}

internal sealed class UiRow : TableLayoutPanel
{
    internal UiRow() { DoubleBuffered = true; SetStyle(ControlStyles.ResizeRedraw, true); }
    protected override void OnPaint(PaintEventArgs e) { base.OnPaint(e); using var pen = new Pen(UiTheme.Line); e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1); }
}
