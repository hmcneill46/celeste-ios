namespace CelesteIOSFoundation;

public enum TouchControlVisibility { Automatic, Always, Off }
public enum TouchMovementMode { Fixed, Floating }
public enum TouchGrabStyle { Toggle, HoldButton, ShoulderHold }
public enum TouchSlideMode { Off, JumpDash }
public enum TouchPhase { Pressed, Moved, Released }
public enum TouchDirection { Neutral, East, NorthEast, North, NorthWest, West, SouthWest, South, SouthEast }
public enum TouchOwnedControl { None, Movement, Jump, Dash, Grab, ShoulderGrab, Pause, Journal }
public enum TouchHapticAction { None, Jump, Dash, GrabToggle }

public readonly record struct TouchPreferences(
    TouchControlVisibility Visibility,
    TouchMovementMode Movement,
    TouchGrabStyle Grab,
    TouchSlideMode Sliding,
    int OpacityPercent,
    int SizePercent,
    bool Haptics)
{
    public static TouchPreferences Default => new(
        TouchControlVisibility.Automatic,
        TouchMovementMode.Fixed,
        TouchGrabStyle.Toggle,
        TouchSlideMode.Off,
        70,
        100,
        true);

    public TouchPreferences Validated() => new(
        Visibility is TouchControlVisibility.Automatic or TouchControlVisibility.Always or TouchControlVisibility.Off
            ? Visibility : Default.Visibility,
        Movement is TouchMovementMode.Fixed or TouchMovementMode.Floating
            ? Movement : Default.Movement,
        Grab is TouchGrabStyle.Toggle or TouchGrabStyle.HoldButton or TouchGrabStyle.ShoulderHold
            ? Grab : Default.Grab,
        Sliding is TouchSlideMode.Off or TouchSlideMode.JumpDash
            ? Sliding : Default.Sliding,
        OpacityPercent is >= 0 and <= 100 && OpacityPercent % 10 == 0 ? OpacityPercent : Default.OpacityPercent,
        SizePercent is >= 70 and <= 130 && SizePercent % 10 == 0 ? SizePercent : Default.SizePercent,
        Haptics);
}

public static class TouchPreferencePolicy
{
    public const string VisibilityKey = "CelesteIOS.TouchControls.Visibility.v1";
    public const string MovementKey = "CelesteIOS.TouchControls.Movement.v1";
    public const string GrabKey = "CelesteIOS.TouchControls.Grab.v1";
    public const string SlidingKey = "CelesteIOS.TouchControls.Sliding.v1";
    public const string OpacityKey = "CelesteIOS.TouchControls.Opacity.v1";
    public const string SizeKey = "CelesteIOS.TouchControls.Size.v1";
    public const string HapticsKey = "CelesteIOS.TouchControls.Haptics.v1";

    public static TouchControlVisibility ParseVisibility(string? value) => value switch
    {
        "Automatic" => TouchControlVisibility.Automatic,
        "Always" => TouchControlVisibility.Always,
        "Off" => TouchControlVisibility.Off,
        _ => TouchPreferences.Default.Visibility,
    };

    public static TouchMovementMode ParseMovement(string? value) => value switch
    {
        "Fixed" => TouchMovementMode.Fixed,
        "Floating" => TouchMovementMode.Floating,
        _ => TouchPreferences.Default.Movement,
    };

    public static TouchGrabStyle ParseGrab(string? value) => value switch
    {
        "Toggle" => TouchGrabStyle.Toggle,
        "HoldButton" => TouchGrabStyle.HoldButton,
        "ShoulderHold" => TouchGrabStyle.ShoulderHold,
        _ => TouchPreferences.Default.Grab,
    };

    public static TouchSlideMode ParseSliding(string? value) => value switch
    {
        "Off" => TouchSlideMode.Off,
        "JumpDash" => TouchSlideMode.JumpDash,
        _ => TouchPreferences.Default.Sliding,
    };

    public static int ParseOpacity(string? value) =>
        int.TryParse(value, out int parsed) && parsed is >= 0 and <= 100 && parsed % 10 == 0
            ? parsed : TouchPreferences.Default.OpacityPercent;

    public static int ParseSize(string? value) =>
        int.TryParse(value, out int parsed) && parsed is >= 70 and <= 130 && parsed % 10 == 0
            ? parsed : TouchPreferences.Default.SizePercent;

    public static bool ParseHaptics(string? value) => value switch
    {
        "On" => true,
        "Off" => false,
        _ => TouchPreferences.Default.Haptics,
    };

    public static string Store(TouchControlVisibility value) => value switch
    {
        TouchControlVisibility.Always => "Always",
        TouchControlVisibility.Off => "Off",
        _ => "Automatic",
    };

    public static string Store(TouchMovementMode value) =>
        value == TouchMovementMode.Floating ? "Floating" : "Fixed";

    public static string Store(TouchGrabStyle value) => value switch
    {
        TouchGrabStyle.HoldButton => "HoldButton",
        TouchGrabStyle.ShoulderHold => "ShoulderHold",
        _ => "Toggle",
    };

    public static string Store(TouchSlideMode value) =>
        value == TouchSlideMode.JumpDash ? "JumpDash" : "Off";
    public static string StorePercent(int value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public static string StoreHaptics(bool enabled) => enabled ? "On" : "Off";
}

public readonly record struct TouchPoint(double X, double Y)
{
    public static TouchPoint operator -(TouchPoint a, TouchPoint b) => new(a.X - b.X, a.Y - b.Y);
    public double Length => Math.Sqrt(X * X + Y * Y);
}

public readonly record struct TouchCircle(TouchPoint Center, double Radius, double HitRadius)
{
    public bool Contains(TouchPoint point)
    {
        TouchPoint delta = point - Center;
        return delta.X * delta.X + delta.Y * delta.Y <= HitRadius * HitRadius;
    }
}

public readonly record struct TouchRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;
    public double Bottom => Y + Height;
    public bool Contains(TouchPoint point) =>
        point.X >= X && point.X <= Right && point.Y >= Y && point.Y <= Bottom;
}

public readonly record struct TouchControlLayout(
    double Width,
    double Height,
    SafeAreaMetrics SafeArea,
    TouchCircle Movement,
    TouchCircle Jump,
    TouchCircle Dash,
    TouchCircle Grab,
    TouchRect Shoulder,
    TouchRect Pause,
    TouchRect Journal,
    TouchRect MovementAcquisition,
    bool IsPad,
    double UserScale)
{
    public static TouchControlLayout Create(
        double width,
        double height,
        SafeAreaMetrics safeArea,
        bool isPad,
        double userScale)
    {
        if (!safeArea.IsValidFor(width, height))
            throw new ArgumentException("Invalid safe-area metrics.", nameof(safeArea));
        userScale = Math.Clamp(userScale, 0.70, 1.30);
        double scale = userScale * (isPad ? 1.18 : 1.0);

        TouchPoint movementCenter = new(
            safeArea.Left + 106 * scale,
            height - safeArea.Bottom - 104 * scale);
        TouchCircle movement = new(movementCenter, 72 * scale, 94 * scale);

        TouchPoint jumpCenter = new(
            width - safeArea.Right - 72 * scale,
            height - safeArea.Bottom - 69 * scale);
        TouchCircle jump = new(jumpCenter, 46 * scale, 58 * scale);
        TouchCircle dash = new(
            new TouchPoint(jumpCenter.X - 87 * scale, jumpCenter.Y - 91 * scale),
            40 * scale,
            51 * scale);
        TouchCircle grab = new(
            new TouchPoint(jumpCenter.X - 157 * scale, jumpCenter.Y - 8 * scale),
            34 * scale,
            45 * scale);

        double pauseWidth = 58 * scale;
        double pauseHeight = 40 * scale;
        TouchRect pause = new(
            width * 0.5 - pauseWidth * 0.5,
            safeArea.Top + 15 * scale,
            pauseWidth,
            pauseHeight);
        double journalWidth = 50 * scale;
        double journalHeight = 40 * scale;
        TouchRect journal = new(
            safeArea.Left + 15 * scale,
            safeArea.Top + 15 * scale,
            journalWidth,
            journalHeight);
        double shoulderStart = Math.Max(width * 0.69, safeArea.Left + width * 0.52);
        TouchRect shoulder = new(
            shoulderStart,
            safeArea.Top + 24 * scale,
            Math.Max(80, width - safeArea.Right - shoulderStart - 12 * scale),
            82 * scale);
        TouchRect acquisition = new(
            safeArea.Left,
            safeArea.Top + 78 * scale,
            Math.Max(1, width * 0.48 - safeArea.Left),
            Math.Max(1, height - safeArea.Bottom - (safeArea.Top + 78 * scale)));

        TouchControlLayout result = new(
            width, height, safeArea, movement, jump, dash, grab,
            shoulder, pause, journal, acquisition, isPad, userScale);
        if (!result.HasNonOverlappingActionHitAreas())
            throw new InvalidOperationException("Touch action hit areas overlap.");
        return result;
    }

    public bool HasNonOverlappingActionHitAreas() =>
        !CirclesOverlap(Jump, Dash) && !CirclesOverlap(Jump, Grab) && !CirclesOverlap(Dash, Grab) &&
        !CircleIntersectsRect(Jump, Pause) && !CircleIntersectsRect(Dash, Pause) &&
        !CircleIntersectsRect(Grab, Pause) && !CircleIntersectsRect(Movement, Pause) &&
        !CircleIntersectsRect(Jump, Journal) && !CircleIntersectsRect(Dash, Journal) &&
        !CircleIntersectsRect(Grab, Journal) && !CircleIntersectsRect(Movement, Journal) &&
        !RectsOverlap(Pause, Journal);

    private static bool CirclesOverlap(TouchCircle a, TouchCircle b)
    {
        TouchPoint delta = a.Center - b.Center;
        double radius = a.HitRadius + b.HitRadius;
        return delta.X * delta.X + delta.Y * delta.Y < radius * radius;
    }

    private static bool CircleIntersectsRect(TouchCircle circle, TouchRect rect)
    {
        double closestX = Math.Clamp(circle.Center.X, rect.X, rect.Right);
        double closestY = Math.Clamp(circle.Center.Y, rect.Y, rect.Bottom);
        double dx = circle.Center.X - closestX;
        double dy = circle.Center.Y - closestY;
        return dx * dx + dy * dy < circle.HitRadius * circle.HitRadius;
    }

    private static bool RectsOverlap(TouchRect a, TouchRect b) =>
        a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;
}

public static class TouchDirectionPolicy
{
    private const double SectorDegrees = 45.0;

    public static TouchDirection Classify(
        TouchPoint delta,
        double padRadius,
        double deadzoneRatio,
        double hysteresisDegrees,
        TouchDirection previous)
    {
        deadzoneRatio = Math.Clamp(deadzoneRatio, 0.0, 0.9);
        hysteresisDegrees = Math.Clamp(hysteresisDegrees, 0.0, 20.0);
        if (padRadius <= 0 || delta.Length <= padRadius * deadzoneRatio)
            return TouchDirection.Neutral;

        double angle = NormalizeDegrees(Math.Atan2(-delta.Y, delta.X) * 180.0 / Math.PI);
        if (previous != TouchDirection.Neutral &&
            AngularDistance(angle, DirectionAngle(previous)) <= SectorDegrees * 0.5 + hysteresisDegrees)
            return previous;

        int sector = ((int)Math.Floor((angle + SectorDegrees * 0.5) / SectorDegrees)) & 7;
        return sector switch
        {
            0 => TouchDirection.East,
            1 => TouchDirection.NorthEast,
            2 => TouchDirection.North,
            3 => TouchDirection.NorthWest,
            4 => TouchDirection.West,
            5 => TouchDirection.SouthWest,
            6 => TouchDirection.South,
            _ => TouchDirection.SouthEast,
        };
    }

    public static (int X, int Y) Axis(TouchDirection direction) => direction switch
    {
        TouchDirection.East => (1, 0),
        TouchDirection.NorthEast => (1, -1),
        TouchDirection.North => (0, -1),
        TouchDirection.NorthWest => (-1, -1),
        TouchDirection.West => (-1, 0),
        TouchDirection.SouthWest => (-1, 1),
        TouchDirection.South => (0, 1),
        TouchDirection.SouthEast => (1, 1),
        _ => (0, 0),
    };

    private static double DirectionAngle(TouchDirection direction) => direction switch
    {
        TouchDirection.East => 0,
        TouchDirection.NorthEast => 45,
        TouchDirection.North => 90,
        TouchDirection.NorthWest => 135,
        TouchDirection.West => 180,
        TouchDirection.SouthWest => 225,
        TouchDirection.South => 270,
        TouchDirection.SouthEast => 315,
        _ => 0,
    };

    private static double NormalizeDegrees(double value)
    {
        value %= 360.0;
        return value < 0 ? value + 360.0 : value;
    }

    private static double AngularDistance(double a, double b)
    {
        double difference = Math.Abs(NormalizeDegrees(a) - NormalizeDegrees(b));
        return Math.Min(difference, 360.0 - difference);
    }
}

public static class TouchVisibilityPolicy
{
    public static bool IsVisible(TouchControlVisibility mode, bool physicalControllerConnected) => mode switch
    {
        TouchControlVisibility.Always => true,
        TouchControlVisibility.Off => false,
        _ => !physicalControllerConnected,
    };

    public static bool NeedsRecovery(TouchControlVisibility mode, bool physicalControllerConnected, bool editing) =>
        mode == TouchControlVisibility.Off && !physicalControllerConnected && !editing;
}

public static class TouchHapticsPolicy
{
    public static TouchHapticAction ForPress(TouchOwnedControl control, TouchGrabStyle grabStyle, bool enabled)
    {
        if (!enabled) return TouchHapticAction.None;
        return control switch
        {
            TouchOwnedControl.Jump => TouchHapticAction.Jump,
            TouchOwnedControl.Dash => TouchHapticAction.Dash,
            TouchOwnedControl.Grab when grabStyle == TouchGrabStyle.Toggle => TouchHapticAction.GrabToggle,
            _ => TouchHapticAction.None,
        };
    }
}

public static class TouchGrabPolicy
{
    // A touch-only player gets the exact final state requested by the selected
    // touch Grab style, independent of Celeste's controller Grab Mode. When a
    // physical controller can participate, preserve vanilla controller
    // semantics and combine either input source deterministically.
    public static bool Resolve(
        bool touchGrab,
        bool touchControlsVisible,
        bool physicalControllerConnected,
        bool controllerGrab) =>
        touchControlsVisible && !physicalControllerConnected
            ? touchGrab
            : touchGrab || controllerGrab;
}

public sealed class TouchInteractionState
{
    public const int MaximumTouches = 8;
    public const double DeadzoneRatio = 0.18;
    public const double HysteresisDegrees = 8.0;

    private readonly FingerOwner[] owners = new FingerOwner[MaximumTouches];
    private TouchControlLayout layout;
    private bool jump;
    private bool dash;
    private bool pause;
    private bool journal;
    private bool grabHold;
    private bool grabToggle;
    private bool grabActionPressed;
    private bool previousJump;
    private bool previousDash;
    private bool previousPause;
    private bool previousJournal;
    private bool previousGrab;
    private TouchDirection direction;
    private TouchDirection previousDirection;
    private TouchPoint movementCenter;

    public TouchInteractionState(TouchControlLayout layout)
    {
        this.layout = layout;
        movementCenter = layout.Movement.Center;
    }

    public TouchMovementMode MovementMode { get; set; } = TouchMovementMode.Fixed;
    public TouchGrabStyle GrabStyle { get; set; } = TouchGrabStyle.Toggle;
    public TouchSlideMode SlideMode { get; set; } = TouchSlideMode.Off;
    public TouchDirection Direction => direction;
    public TouchPoint MovementCenter => movementCenter;
    public int MoveX => TouchDirectionPolicy.Axis(direction).X;
    public int MoveY => TouchDirectionPolicy.Axis(direction).Y;
    public bool Jump => jump;
    public bool Dash => dash;
    public bool Pause => pause;
    public bool Journal => journal;
    public bool Grab => GrabStyle == TouchGrabStyle.Toggle ? grabToggle : grabHold;
    // This is the physical touch-control edge, deliberately separate from
    // GrabPressed. Toggle Grab can be latched already, but another press is
    // still a new Grab action for Celeste's hidden input listeners.
    public bool GrabActionPressed => grabActionPressed;
    public bool JumpPressed => jump && !previousJump;
    public bool DashPressed => dash && !previousDash;
    public bool PausePressed => pause && !previousPause;
    public bool JournalPressed => journal && !previousJournal;
    public bool GrabPressed => Grab && !previousGrab;
    public bool JumpReleased => !jump && previousJump;
    public bool DashReleased => !dash && previousDash;
    public bool PauseReleased => !pause && previousPause;
    public bool JournalReleased => !journal && previousJournal;
    public bool GrabReleased => !Grab && previousGrab;
    public bool LeftPressed => MoveX < 0 && TouchDirectionPolicy.Axis(previousDirection).X >= 0;
    public bool RightPressed => MoveX > 0 && TouchDirectionPolicy.Axis(previousDirection).X <= 0;
    public bool UpPressed => MoveY < 0 && TouchDirectionPolicy.Axis(previousDirection).Y >= 0;
    public bool DownPressed => MoveY > 0 && TouchDirectionPolicy.Axis(previousDirection).Y <= 0;
    public bool LeftReleased => MoveX >= 0 && TouchDirectionPolicy.Axis(previousDirection).X < 0;
    public bool RightReleased => MoveX <= 0 && TouchDirectionPolicy.Axis(previousDirection).X > 0;
    public bool UpReleased => MoveY >= 0 && TouchDirectionPolicy.Axis(previousDirection).Y < 0;
    public bool DownReleased => MoveY <= 0 && TouchDirectionPolicy.Axis(previousDirection).Y > 0;
    public int ActiveOwnerCount => owners.Count(owner => owner.Active);

    public void BeginFrame()
    {
        grabActionPressed = false;
        previousJump = jump;
        previousDash = dash;
        previousPause = pause;
        previousJournal = journal;
        previousGrab = Grab;
        previousDirection = direction;
    }

    public TouchHapticAction Apply(int fingerId, TouchPhase phase, TouchPoint point, bool hapticsEnabled)
    {
        int slot = FindOwner(fingerId);
        if (phase == TouchPhase.Pressed)
        {
            if (slot >= 0) ReleaseOwner(slot);
            slot = FindFreeOwner();
            if (slot < 0) return TouchHapticAction.None;
            TouchOwnedControl control = HitTest(point);
            owners[slot] = new FingerOwner(fingerId, control, point, true);
            if (control is TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab)
                grabActionPressed = true;
            if (control == TouchOwnedControl.Grab && GrabStyle == TouchGrabStyle.Toggle)
                grabToggle = !grabToggle;
            if (control == TouchOwnedControl.Movement)
            {
                movementCenter = MovementMode == TouchMovementMode.Floating ? point : layout.Movement.Center;
                UpdateDirection(point);
            }
            RecomputeHeld();
            return TouchHapticsPolicy.ForPress(control, GrabStyle, hapticsEnabled);
        }

        if (slot < 0) return TouchHapticAction.None;
        if (phase == TouchPhase.Released)
        {
            ReleaseOwner(slot);
            return TouchHapticAction.None;
        }

        FingerOwner owner = owners[slot] with { Point = point };
        if (owner.Control == TouchOwnedControl.Movement)
        {
            owners[slot] = owner;
            UpdateDirection(point);
            return TouchHapticAction.None;
        }
        if (SlideMode == TouchSlideMode.JumpDash &&
            owner.Control is TouchOwnedControl.Jump or TouchOwnedControl.Dash)
        {
            TouchOwnedControl replacement = layout.Jump.Contains(point)
                ? TouchOwnedControl.Jump
                : layout.Dash.Contains(point) ? TouchOwnedControl.Dash : owner.Control;
            owner = owner with { Control = replacement };
        }
        owners[slot] = owner;
        RecomputeHeld();
        return TouchHapticAction.None;
    }

    public void ReleaseMissingOwners(ReadOnlySpan<int> activeFingerIds)
    {
        bool changed = false;
        for (int slot = 0; slot < owners.Length; slot++)
        {
            if (!owners[slot].Active) continue;
            bool found = false;
            for (int index = 0; index < activeFingerIds.Length; index++)
            {
                if (activeFingerIds[index] != owners[slot].FingerId) continue;
                found = true;
                break;
            }
            if (found) continue;
            if (owners[slot].Control == TouchOwnedControl.Movement)
            {
                direction = TouchDirection.Neutral;
                movementCenter = layout.Movement.Center;
            }
            owners[slot] = default;
            changed = true;
        }
        if (changed) RecomputeHeld();
    }

    public void UpdateLayout(TouchControlLayout newLayout)
    {
        layout = newLayout;
        // Geometry or preference changes are explicit input-reset boundaries.
        // Never carry a hidden Toggle Grab latch through a style/size change
        // and unexpectedly resurrect it if Toggle is selected again.
        Reset();
    }

    public void Reset(bool clearToggle = true)
    {
        Array.Clear(owners);
        direction = TouchDirection.Neutral;
        movementCenter = layout.Movement.Center;
        jump = dash = pause = journal = grabHold = false;
        if (clearToggle) grabToggle = false;
        previousJump = previousDash = previousPause = previousJournal = previousGrab = false;
        grabActionPressed = false;
        previousDirection = TouchDirection.Neutral;
    }

    private TouchOwnedControl HitTest(TouchPoint point)
    {
        if (layout.Pause.Contains(point)) return TouchOwnedControl.Pause;
        if (layout.Journal.Contains(point)) return TouchOwnedControl.Journal;
        if (GrabStyle == TouchGrabStyle.ShoulderHold && layout.Shoulder.Contains(point)) return TouchOwnedControl.ShoulderGrab;
        if (layout.Jump.Contains(point)) return TouchOwnedControl.Jump;
        if (layout.Dash.Contains(point)) return TouchOwnedControl.Dash;
        if (GrabStyle != TouchGrabStyle.ShoulderHold && layout.Grab.Contains(point)) return TouchOwnedControl.Grab;
        if (HasMovementOwner()) return TouchOwnedControl.None;
        if (MovementMode == TouchMovementMode.Fixed)
            return layout.Movement.Contains(point) ? TouchOwnedControl.Movement : TouchOwnedControl.None;
        return layout.MovementAcquisition.Contains(point) ? TouchOwnedControl.Movement : TouchOwnedControl.None;
    }

    private void ReleaseOwner(int slot)
    {
        if (owners[slot].Control == TouchOwnedControl.Movement)
        {
            direction = TouchDirection.Neutral;
            movementCenter = layout.Movement.Center;
        }
        owners[slot] = default;
        RecomputeHeld();
    }

    private void RecomputeHeld()
    {
        jump = owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.Jump);
        dash = owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.Dash);
        pause = owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.Pause);
        journal = owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.Journal);
        grabHold = owners.Any(owner => owner.Active &&
            owner.Control is TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab);
    }

    private void UpdateDirection(TouchPoint point) => direction = TouchDirectionPolicy.Classify(
        point - movementCenter,
        layout.Movement.Radius,
        DeadzoneRatio,
        HysteresisDegrees,
        direction);

    private bool HasMovementOwner() => owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.Movement);

    private int FindOwner(int fingerId)
    {
        for (int i = 0; i < owners.Length; i++)
            if (owners[i].Active && owners[i].FingerId == fingerId) return i;
        return -1;
    }

    private int FindFreeOwner()
    {
        for (int i = 0; i < owners.Length; i++)
            if (!owners[i].Active) return i;
        return -1;
    }

    private readonly record struct FingerOwner(int FingerId, TouchOwnedControl Control, TouchPoint Point, bool Active);
}
