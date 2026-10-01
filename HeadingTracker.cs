namespace GPTCursor;

// Estimate a tangent from a spatial trail, rather than quantized per-frame pixels.
// Zero movement never advances the filter; one-pixel sensor jitter is ignored.
internal sealed class HeadingTracker
{
    private readonly List<(double X, double Y, double Distance)> trail = new(32);
    private double lastX, lastY, distance, angle = -135;
    private bool hasDirection;
    private bool paused;
    private double sampleX, sampleY, restX, restY;
    internal double Rotation { get; private set; }
    internal bool Changed { get; private set; }
    internal void Reset() { trail.Clear(); distance = 0; angle = -135; Rotation = 0; Changed = hasDirection = paused = false; }
    internal double Update(double x, double y)
    {
        Changed = false;
        if (trail.Count == 0) { sampleX = lastX = x; sampleY = lastY = y; trail.Add((x, y, 0)); return Rotation; }
        if (x == sampleX && y == sampleY && !paused) { paused = true; restX = x; restY = y; }
        sampleX = x; sampleY = y;
        if (paused)
        {
            if ((x - restX) * (x - restX) + (y - restY) * (y - restY) < 9) return Rotation;
            paused = false;
        }
        double dx = x - lastX, dy = y - lastY, length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 2) return Rotation;
        distance += length; lastX = x; lastY = y;
        trail.Add((x, y, distance));
        double start = Math.Max(0, distance - 24);
        while (trail.Count > 2 && trail[1].Distance <= start) trail.RemoveAt(0);
        if (distance < 4) return Rotation;
        var a = trail[0]; var b = trail[1];
        double t = Math.Clamp((start - a.Distance) / Math.Max(.001, b.Distance - a.Distance), 0, 1);
        dx = x - (a.X + (b.X - a.X) * t); dy = y - (a.Y + (b.Y - a.Y) * t);
        if (dx * dx + dy * dy < 4) return Rotation;
        double target = Math.Atan2(dy, dx) * 180 / Math.PI;
        if (!hasDirection) { hasDirection = true; angle = target; Rotation = target + 135; Changed = true; return Rotation; }
        double delta = (target - angle + 540) % 360 - 180;
        // Blend as a function of travelled distance, independent of frame rate.
        double step = delta * (1 - Math.Exp(-length / 5));
        Rotation += step; angle = (angle + step + 540) % 360 - 180;
        Changed = true;
        return Rotation;
    }
}
