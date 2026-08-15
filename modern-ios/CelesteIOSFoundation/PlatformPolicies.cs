namespace CelesteIOSFoundation;

public enum FoundationAudioLane { SimulatorNoFmod, DeviceFmod }

public readonly record struct SafeAreaMetrics(double Top, double Left, double Bottom, double Right)
{
    public bool IsValidFor(double width, double height) =>
        double.IsFinite(width) && double.IsFinite(height) && width > 0 && height > 0 &&
        double.IsFinite(Top) && double.IsFinite(Left) && double.IsFinite(Bottom) && double.IsFinite(Right) &&
        Top >= 0 && Left >= 0 && Bottom >= 0 && Right >= 0 &&
        Left + Right < width && Top + Bottom < height;
}

public static class PlatformPolicies
{
    public static FoundationAudioLane AudioLane(bool isSimulator) =>
        isSimulator ? FoundationAudioLane.SimulatorNoFmod : FoundationAudioLane.DeviceFmod;

    public static bool SupportsOrientation(bool landscapeLeft, bool landscapeRight, bool portrait) =>
        landscapeLeft && landscapeRight && !portrait;
}

public sealed class TouchControllerState
{
    public float Horizontal { get; private set; }
    public float Vertical { get; private set; }
    public bool Jump { get; private set; }
    public bool Dash { get; private set; }
    public bool Grab { get; private set; }

    public void Update(float horizontal, float vertical, bool jump, bool dash, bool grab)
    {
        Horizontal = Math.Clamp(horizontal, -1f, 1f);
        Vertical = Math.Clamp(vertical, -1f, 1f);
        Jump = jump;
        Dash = dash;
        Grab = grab;
    }

    public void Reset() => Update(0, 0, false, false, false);
}
