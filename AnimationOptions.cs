namespace GPTCursor;

internal enum DirectionStyle { Original, ReturnToRest, KeepDirection }

internal class AnimationOptions
{
    public bool Animation { get; set; } = true;
    public bool Rotation { get; set; } = true;
    public bool FaceMovement { get; set; }
    public bool ReturnToRest { get; set; }
    // Keep the existing FaceMovement setting compatible with pre-1.3 preferences.
    internal DirectionStyle Direction
    {
        get => !FaceMovement ? DirectionStyle.Original : ReturnToRest ? DirectionStyle.ReturnToRest : DirectionStyle.KeepDirection;
        set { FaceMovement = value != DirectionStyle.Original; ReturnToRest = value == DirectionStyle.ReturnToRest; }
    }
    internal bool HoldHeadingAtRest => FaceMovement && !ReturnToRest;
    public bool PauseFullscreen { get; set; } = true;
    public string ExcludedApps { get; set; } = "";
    public bool Stretch { get; set; } = true;
    public bool Squash { get; set; } = true;
    public bool Wobble { get; set; } = true;
    public bool LeftClick { get; set; } = true;
    public bool RightClick { get; set; } = true;
    public bool ClickPulse { get; set; } = true;
    public bool ClickRings { get; set; } = true;
    public bool PositionSmoothing { get; set; } = true;
    public SmoothingMethod PositionMethod { get; set; } = SmoothingMethod.Sine;
    public bool EffectsSmoothing { get; set; } = true;
    public SmoothingMethod EffectsMethod { get; set; } = SmoothingMethod.Responsive;
}

internal sealed class ClickMotion
{
    private double left = 1, right = 1;
    internal void Trigger(bool rightButton)
    {
        if (rightButton) right = 0; else left = 0;
    }
    internal Pose Apply(Pose pose, double dt, AnimationOptions options)
    {
        if (!options.LeftClick) left = 1;
        if (!options.RightClick) right = 1;
        double age = Math.Min(left, right);
        double scale = options.ClickPulse && age < 1
            ? 1 - .20 * Math.Sin(age * Math.PI * 2) * Math.Pow(1 - age, 2) : 1;
        var result = pose with { Scale = scale,
            LeftRing = options.ClickRings && left < 1 ? left : -1,
            RightRing = options.ClickRings && right < 1 ? right : -1 };
        double step = Math.Max(0, dt) / .42;
        left = Math.Min(1, left + step); right = Math.Min(1, right + step);
        return result;
    }
    internal void Reset() { left = right = 1; }
}
