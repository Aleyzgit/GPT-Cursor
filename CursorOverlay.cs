using System.ComponentModel;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace GPTCursor;

// Render through one composited, input-transparent surface without replacing system cursor images.
internal sealed class CursorOverlay : Form
{
    private bool displayed;
    private int canvas;
    internal CursorOverlay()
    {
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Text = "GPT Cursor overlay";
        _ = Handle;
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00080000 | 0x00000020 | 0x00000080 | 0x08000000;
            return cp;
        }
    }
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == 0x0084) { m.Result = -1; return; } // HTTRANSPARENT
        if (m.Msg == 0x0021) { m.Result = 3; return; } // MA_NOACTIVATE
        base.WndProc(ref m);
    }
    internal void Present(Bitmap bitmap, Native.Point pointer)
    {
        // GDI's alpha blending requires premultiplied channels.
        using var pixels = bitmap.Clone(new Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppPArgb);
        nint screen = Native.GetDC(0), dc = Native.CreateCompatibleDC(screen);
        nint dib = pixels.GetHbitmap(Color.FromArgb(0));
        nint previous = Native.SelectObject(dc, dib);
        try
        {
            var destination = new Native.Point { X = pointer.X - bitmap.Width / 2, Y = pointer.Y - bitmap.Height / 2 };
            var size = new Native.Size { Width = bitmap.Width, Height = bitmap.Height };
            var source = new Native.Point();
            var blend = new Native.Blend { Alpha = 255, Format = 1 };
            if (!Native.UpdateLayeredWindow(Handle, screen, ref destination, ref size, dc, ref source, 0, ref blend, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            canvas = bitmap.Width;
            MoveTo(pointer);
        }
        finally
        {
            Native.SelectObject(dc, previous); Native.DeleteObject(dib);
            Native.DeleteDC(dc); Native.ReleaseDC(0, screen);
        }
    }
    internal void MoveTo(Native.Point pointer)
    {
        Native.SetWindowPos(Handle, -1, pointer.X - canvas / 2, pointer.Y - canvas / 2, 0, 0, 0x0010 | 0x0001);
        if (!displayed) { Native.ShowWindow(Handle, 4); displayed = true; }
    }
    internal void Conceal() { if (displayed) Native.ShowWindow(Handle, 0); displayed = false; }
}
