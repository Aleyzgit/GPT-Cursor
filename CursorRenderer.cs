using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Reflection;

namespace GPTCursor;

internal sealed class CursorRenderer : IDisposable
{
    private readonly Bitmap source;
    internal CursorRenderer()
    {
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("GPT_Cursor.assets.cursor.png")
            ?? Assembly.GetExecutingAssembly().GetManifestResourceNames()
                .Where(n => n.EndsWith("cursor.png")).Select(n => Assembly.GetExecutingAssembly().GetManifestResourceStream(n)!).First();
        using var original = new Bitmap(stream);
        source = new Bitmap(original);
    }

    internal Bitmap Render(Pose pose, int size)
    {
        // Fixed central hotspot leaves space for rotation in every direction.
        int canvas = size * 3;
        var bitmap = new Bitmap(canvas, canvas, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        void Ring(double progress, bool doubleRing)
        {
            if (progress < 0 || progress >= 1) return;
            float radius = size * (float)(.12 + .65 * (1 - Math.Pow(1 - progress, 2)));
            int alpha = (int)(220 * (1 - progress));
            using var border = new Pen(Color.FromArgb(alpha, 20, 20, 20), 3.5f);
            using var line = new Pen(Color.FromArgb(alpha, 255, 255, 255), 1.5f);
            void Draw(float r)
            {
                var bounds = new RectangleF(canvas / 2f - r, canvas / 2f - r, r * 2, r * 2);
                graphics.DrawEllipse(border, bounds); graphics.DrawEllipse(line, bounds);
            }
            Draw(radius);
            if (doubleRing) Draw(radius * .68f);
        }
        Ring(pose.LeftRing, false); Ring(pose.RightRing, true);
        PointF Map(float x, float y)
        {
            // Visible tip at (4, 5) in the 46 x 48 source. Pin it to the OS hotspot.
            double px = (x - 4) * size / 48.0, py = (y - 5) * size / 48.0;
            Rotate(ref px, ref py, 44);
            px *= pose.Stretch;
            Rotate(ref px, ref py, -44 + pose.Rotation);
            Rotate(ref px, ref py, -pose.Axis);
            py *= pose.Squash;
            Rotate(ref px, ref py, pose.Axis);
            px *= pose.Scale; py *= pose.Scale;
            return new PointF((float)(canvas / 2 + px), (float)(canvas / 2 + py));
        }
        graphics.DrawImage(source, [Map(0, 0), Map(source.Width, 0), Map(0, source.Height)],
            new RectangleF(0, 0, source.Width, source.Height), GraphicsUnit.Pixel);
        return bitmap;
    }
    private static void Rotate(ref double x, ref double y, double degrees)
    {
        double r = degrees * Math.PI / 180, c = Math.Cos(r), s = Math.Sin(r);
        (x, y) = (x * c - y * s, x * s + y * c);
    }
    internal static nint CreateCursor(Bitmap bitmap)
    {
        nint color = bitmap.GetHbitmap(Color.FromArgb(0));
        using var maskBitmap = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format1bppIndexed);
        // With an all-zero alpha image Windows falls back to the monochrome mask.
        // White AND-mask bits preserve the background instead of drawing a black box.
        var maskData = maskBitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format1bppIndexed);
        try
        {
            var bytes = Enumerable.Repeat((byte)255, Math.Abs(maskData.Stride) * maskData.Height).ToArray();
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, maskData.Scan0, bytes.Length);
        }
        finally { maskBitmap.UnlockBits(maskData); }
        nint mask = maskBitmap.GetHbitmap();
        try
        {
            var info = new Native.IconInfo { XHotspot = (uint)bitmap.Width / 2, YHotspot = (uint)bitmap.Height / 2, Color = color, Mask = mask };
            return Native.CreateIconIndirect(ref info);
        }
        finally { Native.DeleteObject(mask); Native.DeleteObject(color); }
    }
    public void Dispose() => source.Dispose();
}
