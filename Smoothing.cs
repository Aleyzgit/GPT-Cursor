namespace GPTCursor;

internal enum SmoothingMethod { Sine, Spring, Responsive }

// All profiles use elapsed seconds, independent of the selected output FPS.
internal sealed class SmoothValue
{
    private readonly List<(double Time, double Value)> history = new(64);
    private bool initialized;
    private SmoothingMethod method;
    private double value, first, velocity, time;
    internal void Reset(double target)
    {
        value = first = target; velocity = time = 0; initialized = true;
        history.Clear(); history.Add((0, target));
    }
    internal double Update(double target, double dt, SmoothingMethod profile, bool effects = false)
    {
        if (!initialized || method != profile || dt > .15)
        { Reset(target); method = profile; return target; }
        dt = Math.Max(0, dt);
        if (dt == 0) return value;
        if (profile == SmoothingMethod.Sine)
        {
            // Causal Hann window: zero weight at both ends gives gentle starts/stops,
            // finite settling time and no overshoot. Integrate piecewise constant samples.
            double window = effects ? .065 : .032;
            time += dt; history.Add((time, target));
            double begin = time - window;
            while (history.Count > 2 && history[1].Time <= begin) history.RemoveAt(0);
            double Primitive(double age) => age / 2 - window * Math.Sin(2 * Math.PI * age / window) / (4 * Math.PI);
            double sum = 0;
            for (int i = 0; i < history.Count; i++)
            {
                double start = i == 0 ? begin : Math.Max(begin, history[i].Time);
                double end = i + 1 < history.Count ? history[i + 1].Time : time;
                if (end <= start) continue;
                sum += history[i].Value * (Primitive(time - start) - Primitive(time - end));
            }
            value = sum / (window / 2);
        }
        else if (profile == SmoothingMethod.Spring)
        {
            // Exact critically damped spring for a constant target within this frame.
            double omega = effects ? 55 : 95;
            double offset = value - target, c = velocity + omega * offset, decay = Math.Exp(-omega * dt);
            value = target + (offset + c * dt) * decay;
            velocity = (velocity - omega * c * dt) * decay;
        }
        else
        {
            // Two analytic low-pass stages provide ease-in as well as ease-out.
            double rate = effects ? 85 : 180, decay = Math.Exp(-rate * dt), oldFirst = first;
            first = target + (first - target) * decay;
            value = target + (value - target + rate * (oldFirst - target) * dt) * decay;
        }
        return value;
    }
}

internal sealed class CursorSmoothing
{
    private readonly SmoothValue x = new(), y = new(), rotation = new(), stretch = new(), squash = new(), axis = new();
    private double lastAxis, unwrappedAxis;
    internal (double X, double Y) Position(double targetX, double targetY, double dt, AnimationOptions options, bool direct = false)
    {
        if (!options.PositionSmoothing || direct) { x.Reset(targetX); y.Reset(targetY); return (targetX, targetY); }
        return (x.Update(targetX, dt, options.PositionMethod), y.Update(targetY, dt, options.PositionMethod));
    }
    internal Pose Effects(Pose pose, double dt, AnimationOptions options)
    {
        double difference = (pose.Axis - lastAxis + 540) % 360 - 180;
        unwrappedAxis += difference; lastAxis = pose.Axis;
        // Reset disabled channels immediately: smoothing must never override a switch.
        if (!options.EffectsSmoothing || (!options.Animation && !options.FaceMovement))
        {
            rotation.Reset(pose.Rotation); stretch.Reset(pose.Stretch); squash.Reset(pose.Squash); axis.Reset(unwrappedAxis); return pose;
        }
        double Filter(SmoothValue filter, double target, bool enabled)
        {
            if (!enabled) { filter.Reset(target); return target; }
            return filter.Update(target, dt, options.EffectsMethod, true);
        }
        return pose with {
            Rotation = Filter(rotation, pose.Rotation, options.FaceMovement || options.Rotation || options.Wobble),
            Stretch = Filter(stretch, pose.Stretch, options.Stretch),
            Squash = Filter(squash, pose.Squash, options.Squash),
            Axis = Filter(axis, unwrappedAxis, options.Squash)
        };
    }
    internal void Reset(double targetX, double targetY)
    {
        x.Reset(targetX); y.Reset(targetY); rotation.Reset(0); stretch.Reset(1); squash.Reset(1); axis.Reset(0);
        lastAxis = unwrappedAxis = 0;
    }
}
