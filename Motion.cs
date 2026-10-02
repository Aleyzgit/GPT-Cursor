namespace GPTCursor;

// Parameters and fixed-step integration from cursor-chat-144c8348ce0a.js.
internal sealed class Spring(double initial, double response, double damping)
{
    internal double Value = initial, Target = initial, Velocity, Force;
    private double time, simulated;
    internal void Step(double dt)
    {
        const double step = 1.0 / 240;
        double stiffness = Math.Min(Math.Pow(2 * Math.PI / response, 2), 1 / (2 * step * step));
        double drag = 2 * Math.Sqrt(stiffness) * damping;
        time += Math.Max(0, dt);
        if (time - simulated > 1) simulated = time - 1.0 / 60;
        while (simulated < time)
        {
            double v = Velocity + Force * step / 2;
            Value += v * step;
            Force = -v * drag + (Target - Value) * stiffness;
            Velocity = v + Force * step / 2;
            simulated += step;
        }
        if (Math.Max(Velocity * Velocity, Force * Force) <= .06 * .06 &&
            (Target == 0 || Math.Abs(Target - Value) <= Math.Abs(Target * .01))) Value = Target;
    }
    internal void Reset(double value) { Value = Target = value; Velocity = Force = time = simulated = 0; }
}

internal readonly record struct Pose(double Rotation, double Stretch, double Squash, double Axis,
    double Scale = 1, double LeftRing = -1, double RightRing = -1);

internal sealed class Motion
{
    private readonly Spring turn = new(0, .055, .82), squash = new(1, .12, .86), stretch = new(1, .2, .85);
    private double lastX, lastY, idle, direction;
    private bool initialized, moving;
    private readonly HeadingTracker heading = new();
    private readonly Spring returningTurn = new(0, .18, 1);
    private double headingIdle;
    private bool wasReturning;
    internal bool HeadingChanged => heading.Changed;
    internal Pose Current { get; private set; } = new(0, 1, 1, 0);

    internal Pose Update(double x, double y, double dt, AnimationOptions? options = null)
    {
        dt = Math.Clamp(dt, 0, .1);
        if (!initialized) { lastX = x; lastY = y; initialized = true; }
        double dx = x - lastX, dy = y - lastY, distance = Math.Sqrt(dx * dx + dy * dy);
        lastX = x; lastY = y;
        double facingRotation = heading.Update(x, y);
        if (distance > .1)
        {
            // Physical mice stream targets continuously. Keep the real click position and
            // drive the original directional deformation from velocity instead of a queued path.
            double speed = distance / Math.Max(dt, 1.0 / 240);
            double amount = Math.Clamp(speed / 1800, 0, 1);
            turn.Target = Math.Clamp(dx / distance * .75 - dy / distance * .62, -1, 1) * 70 * amount;
            direction = Math.Atan2(dy, dx) * 180 / Math.PI;
            squash.Target = 1 - .15 * amount;
            stretch.Target = Math.Clamp(1 - speed / 5500, .65, 1);
            idle = 0; moving = true;
        }
        else
        {
            if (moving) idle += dt;
            turn.Target = 0; squash.Target = stretch.Target = 1;
        }
        turn.Step(dt); squash.Step(dt); stretch.Step(dt);
        double wobble = moving && idle > 0 && idle < 1.41
            ? Math.Sin(idle / .66 * Math.PI * 2) * Math.Sin(idle / 1.41 * Math.PI) * 12.5 : 0;
        bool animate = options?.Animation ?? true;
        bool returning = options?.Direction == DirectionStyle.ReturnToRest;
        double rotation = facingRotation;
        if (returning)
        {
            if (!wasReturning) { returningTurn.Reset(Current.Rotation); headingIdle = .10; }
            headingIdle = heading.Changed ? 0 : headingIdle + dt;
            // Tolerate gaps between mouse reports, then return along the shortest arc.
            // Quantized one-pixel resting jitter must not restart the directional turn.
            double target = headingIdle < .10 ? facingRotation : 0;
            double delta = ((target - returningTurn.Value + 180) % 360 + 360) % 360 - 180;
            returningTurn.Target = returningTurn.Value + delta;
            returningTurn.Step(dt);
            rotation = returningTurn.Value;
            if (animate && options!.Wobble && headingIdle >= .10)
            {
                double age = headingIdle - .10;
                if (age < 1.41) rotation += Math.Sin(age / .66 * Math.PI * 2) * Math.Sin(age / 1.41 * Math.PI) * 12.5;
            }
        }
        wasReturning = returning;
        Current = new(
            options?.FaceMovement == true ? rotation :
                (animate && (options?.Rotation ?? true) ? turn.Value : 0) + (animate && (options?.Wobble ?? true) ? wobble : 0),
            animate && (options?.Stretch ?? true) ? stretch.Value : 1,
            animate && (options?.Squash ?? true) ? squash.Value : 1, returning ? facingRotation - 135 : direction);
        return Current;
    }
    internal void Reset() { initialized = moving = wasReturning = false; idle = headingIdle = 0; heading.Reset(); returningTurn.Reset(0); turn.Reset(0); squash.Reset(1); stretch.Reset(1); Current = new(0, 1, 1, 0); }
}
