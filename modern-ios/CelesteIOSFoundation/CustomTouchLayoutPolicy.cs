using System.Globalization;

namespace CelesteIOSFoundation;

public enum TouchActionLayout { SeparateButtons, SplitRegion }
public enum TouchSplitOrientation { TopLeftToBottomRight, TopRightToBottomLeft, Vertical, Horizontal }
public enum TouchGrabShape { Circle, Rectangle }
public enum TouchLayoutControl { Movement, FloatingRegion, Jump, Dash, SplitRegion, Grab, Pause, Journal }
public enum TouchExtraControlKind { Jump, Dash, Grab, Pause, Journal, CrouchDash, QuickRestart }
public enum AppleInputSource { Touch, Controller, Keyboard }
public enum AppleGrabMode { Hold, Invert, Toggle }

public static class GrabSourceVisibilityPolicy
{
    public static bool GameplayValue(AppleInputSource source, bool touchVisible, bool effective) =>
        effective && (source != AppleInputSource.Touch || touchVisible);
}

public readonly record struct TouchControlOpacityProfile(
    int Movement,
    int FloatingRegion,
    int Jump,
    int Dash,
    int SplitRegion,
    int Grab,
    int Pause,
    int Journal)
{
    public static TouchControlOpacityProfile Default => new(100, 100, 100, 100, 100, 100, 100, 100);

    public int For(TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.Movement => Movement,
        TouchLayoutControl.FloatingRegion => FloatingRegion,
        TouchLayoutControl.Jump => Jump,
        TouchLayoutControl.Dash => Dash,
        TouchLayoutControl.SplitRegion => SplitRegion,
        TouchLayoutControl.Grab => Grab,
        TouchLayoutControl.Pause => Pause,
        _ => Journal,
    };

    public TouchControlOpacityProfile With(TouchLayoutControl control, int value) => control switch
    {
        TouchLayoutControl.Movement => this with { Movement = value },
        TouchLayoutControl.FloatingRegion => this with { FloatingRegion = value },
        TouchLayoutControl.Jump => this with { Jump = value },
        TouchLayoutControl.Dash => this with { Dash = value },
        TouchLayoutControl.SplitRegion => this with { SplitRegion = value },
        TouchLayoutControl.Grab => this with { Grab = value },
        TouchLayoutControl.Pause => this with { Pause = value },
        _ => this with { Journal = value },
    };
}

public readonly record struct TouchExtraControl(
    bool Enabled,
    TouchExtraControlKind Kind,
    TouchGrabShape Shape,
    int OpacityPercent,
    TouchRect Rect)
{
    public static TouchExtraControl Empty => new(
        false, TouchExtraControlKind.Grab, TouchGrabShape.Rectangle, 100,
        new TouchRect(0.44, 0.44, 0.12, 0.12));
}

public readonly record struct TouchLayoutProfile(
    int SchemaVersion,
    TouchMovementMode MovementMode,
    TouchActionLayout ActionLayout,
    TouchSlideMode Sliding,
    TouchSplitOrientation SplitOrientation,
    bool JumpOnFirstHalf,
    TouchGrabShape GrabShape,
    TouchRect Movement,
    TouchRect FloatingRegion,
    TouchRect Jump,
    TouchRect Dash,
    TouchRect SplitRegion,
    TouchRect Grab,
    TouchRect Pause,
    TouchRect Journal,
    TouchControlOpacityProfile Opacity,
    bool JournalEnabled,
    TouchExtraControl Extra0,
    TouchExtraControl Extra1,
    TouchExtraControl Extra2,
    TouchExtraControl Extra3)
{
    public const int CurrentSchemaVersion = 2;
    public const int MaximumExtraControls = 4;

    public TouchRect Rect(TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.Movement => Movement,
        TouchLayoutControl.FloatingRegion => FloatingRegion,
        TouchLayoutControl.Jump => Jump,
        TouchLayoutControl.Dash => Dash,
        TouchLayoutControl.SplitRegion => SplitRegion,
        TouchLayoutControl.Grab => Grab,
        TouchLayoutControl.Pause => Pause,
        _ => Journal,
    };

    public TouchLayoutProfile WithRect(TouchLayoutControl control, TouchRect value) => control switch
    {
        TouchLayoutControl.Movement => this with { Movement = value },
        TouchLayoutControl.FloatingRegion => this with { FloatingRegion = value },
        TouchLayoutControl.Jump => this with { Jump = value },
        TouchLayoutControl.Dash => this with { Dash = value },
        TouchLayoutControl.SplitRegion => this with { SplitRegion = value },
        TouchLayoutControl.Grab => this with { Grab = value },
        TouchLayoutControl.Pause => this with { Pause = value },
        _ => this with { Journal = value },
    };

    public int OpacityFor(TouchLayoutControl control) => Opacity.For(control);
    public TouchLayoutProfile WithOpacity(TouchLayoutControl control, int value) =>
        this with { Opacity = Opacity.With(control, value) };

    public TouchExtraControl Extra(int index) => index switch
    {
        0 => Extra0,
        1 => Extra1,
        2 => Extra2,
        3 => Extra3,
        _ => TouchExtraControl.Empty,
    };

    public TouchLayoutProfile WithExtra(int index, TouchExtraControl value) => index switch
    {
        0 => this with { Extra0 = value },
        1 => this with { Extra1 = value },
        2 => this with { Extra2 = value },
        3 => this with { Extra3 = value },
        _ => this,
    };
}

public readonly record struct GrabModeProfiles(
    AppleGrabMode Touch,
    AppleGrabMode Controller,
    AppleGrabMode Keyboard)
{
    public AppleGrabMode For(AppleInputSource source) => source switch
    {
        AppleInputSource.Controller => Controller,
        AppleInputSource.Keyboard => Keyboard,
        _ => Touch,
    };

    public GrabModeProfiles With(AppleInputSource source, AppleGrabMode mode) => source switch
    {
        AppleInputSource.Controller => this with { Controller = mode },
        AppleInputSource.Keyboard => this with { Keyboard = mode },
        _ => this with { Touch = mode },
    };

    public GrabModeProfiles Validated(AppleGrabMode fallback) => new(
        Touch is AppleGrabMode.Hold or AppleGrabMode.Invert or AppleGrabMode.Toggle ? Touch : AppleGrabMode.Toggle,
        Controller is AppleGrabMode.Hold or AppleGrabMode.Invert or AppleGrabMode.Toggle ? Controller : fallback,
        Keyboard is AppleGrabMode.Hold or AppleGrabMode.Invert or AppleGrabMode.Toggle ? Keyboard : fallback);
}

public readonly record struct TouchLayoutValidation(bool IsValid, string Message)
{
    public static TouchLayoutValidation Valid => new(true, "");
}

public readonly record struct TouchLayoutSelection(TouchLayoutControl Primary, int ExtraIndex = -1)
{
    public bool IsExtra => ExtraIndex >= 0;
}

public readonly record struct TouchD2MigrationResult(TouchLayoutProfile Layout, GrabModeProfiles GrabModes);

public static class TouchD2MigrationPolicy
{
    public static TouchD2MigrationResult Migrate(
        double width,
        double height,
        SafeAreaMetrics safeArea,
        bool isPad,
        TouchPreferences legacy,
        AppleGrabMode canonicalGrab)
    {
        TouchLayoutProfile layout = TouchLayoutPolicy.Factory(
            width, height, safeArea, isPad, legacy.SizePercent / 100.0, legacy.Grab) with
        {
            MovementMode = legacy.Movement,
            Sliding = legacy.Sliding,
        };
        AppleGrabMode touch = legacy.Grab == TouchGrabStyle.Toggle
            ? AppleGrabMode.Toggle
            : AppleGrabMode.Hold;
        return new TouchD2MigrationResult(layout, new GrabModeProfiles(touch, canonicalGrab, canonicalGrab));
    }
}

public static class DirectionalHapticPolicy
{
    public static bool ShouldPulse(TouchDirection before, TouchDirection after, bool enabled) =>
        enabled && after != TouchDirection.Neutral && after != before;
}

public readonly record struct TouchRuntimeLayout(
    double Width,
    double Height,
    SafeAreaMetrics SafeArea,
    TouchLayoutProfile Profile,
    TouchCircle Movement,
    TouchRect FloatingRegion,
    TouchCircle Jump,
    TouchCircle Dash,
    TouchRect SplitRegion,
    TouchCircle GrabCircle,
    TouchRect GrabRect,
    TouchRect Pause,
    TouchRect Journal,
    TouchRuntimeExtraControl Extra0,
    TouchRuntimeExtraControl Extra1,
    TouchRuntimeExtraControl Extra2,
    TouchRuntimeExtraControl Extra3)
{
    public TouchMovementMode MovementMode => Profile.MovementMode;
    public TouchActionLayout ActionLayout => Profile.ActionLayout;
    public TouchSlideMode SlideMode => Profile.Sliding;
    public TouchGrabShape GrabShape => Profile.GrabShape;
    public TouchRuntimeExtraControl Extra(int index) => index switch
    {
        0 => Extra0,
        1 => Extra1,
        2 => Extra2,
        3 => Extra3,
        _ => default,
    };
}

public readonly record struct TouchRuntimeExtraControl(
    bool Enabled,
    TouchExtraControlKind Kind,
    TouchGrabShape Shape,
    int OpacityPercent,
    TouchRect Rect,
    TouchCircle Circle);

public static class TouchLayoutPolicy
{
    public const double MovementVisualRatio = 72.0 / 94.0;
    public const double JumpVisualRatio = 46.0 / 58.0;
    public const double DashVisualRatio = 40.0 / 51.0;
    public const double GrabVisualRatio = 34.0 / 45.0;

    public static TouchLayoutProfile Factory(
        double width,
        double height,
        SafeAreaMetrics safeArea,
        bool isPad,
        double migratedScale = 1.0,
        TouchGrabStyle migratedGrab = TouchGrabStyle.Toggle)
    {
        TouchControlLayout legacy = TouchControlLayout.Create(width, height, safeArea, isPad, migratedScale);
        TouchRect safe = Usable(width, height, safeArea);
        TouchRect canvas = FullCanvas(width, height);
        TouchRect grab = migratedGrab == TouchGrabStyle.ShoulderHold
            ? legacy.Shoulder
            : CircleRect(legacy.Grab);
        TouchGrabShape shape = migratedGrab == TouchGrabStyle.ShoulderHold
            ? TouchGrabShape.Rectangle
            : TouchGrabShape.Circle;

        double splitLeft = Math.Max(
            Math.Min(legacy.Jump.Center.X - legacy.Jump.HitRadius, legacy.Dash.Center.X - legacy.Dash.HitRadius),
            grab.Right + 8.0);
        double splitRight = Math.Max(splitLeft + 100.0, legacy.Jump.Center.X + legacy.Jump.HitRadius);
        double splitTop = Math.Min(legacy.Jump.Center.Y - legacy.Jump.HitRadius, legacy.Dash.Center.Y - legacy.Dash.HitRadius);
        double splitBottom = Math.Max(legacy.Jump.Center.Y + legacy.Jump.HitRadius, legacy.Dash.Center.Y + legacy.Dash.HitRadius);
        splitRight = Math.Min(splitRight, safe.Right);
        splitTop = Math.Max(splitTop, safe.Y);
        splitBottom = Math.Min(splitBottom, safe.Bottom);

        return new TouchLayoutProfile(
            TouchLayoutProfile.CurrentSchemaVersion,
            TouchMovementMode.Fixed,
            TouchActionLayout.SeparateButtons,
            TouchSlideMode.Off,
            TouchSplitOrientation.TopLeftToBottomRight,
            true,
            shape,
            Normalize(FitInside(CircleRect(legacy.Movement), canvas), canvas),
            Normalize(FitInside(legacy.MovementAcquisition, canvas), canvas),
            Normalize(FitInside(CircleRect(legacy.Jump), canvas), canvas),
            Normalize(FitInside(CircleRect(legacy.Dash), canvas), canvas),
            Normalize(FitInside(new TouchRect(splitLeft, splitTop, splitRight - splitLeft, splitBottom - splitTop), canvas), canvas),
            Normalize(FitInside(grab, canvas), canvas),
            Normalize(FitInside(legacy.Pause, canvas), canvas),
            Normalize(FitInside(legacy.Journal, canvas), canvas),
            TouchControlOpacityProfile.Default,
            true,
            TouchExtraControl.Empty,
            TouchExtraControl.Empty,
            TouchExtraControl.Empty,
            TouchExtraControl.Empty);
    }

    public static TouchRuntimeLayout Materialize(
        TouchLayoutProfile profile,
        double width,
        double height,
        SafeAreaMetrics safeArea)
    {
        TouchRect canvas = FullCanvas(width, height);
        TouchRect movement = Denormalize(profile.Movement, canvas);
        TouchRect jump = Denormalize(profile.Jump, canvas);
        TouchRect dash = Denormalize(profile.Dash, canvas);
        TouchRect grab = Denormalize(profile.Grab, canvas);
        return new TouchRuntimeLayout(
            width,
            height,
            safeArea,
            profile,
            Circle(movement, MovementVisualRatio),
            Denormalize(profile.FloatingRegion, canvas),
            Circle(jump, JumpVisualRatio),
            Circle(dash, DashVisualRatio),
            Denormalize(profile.SplitRegion, canvas),
            Circle(grab, GrabVisualRatio),
            grab,
            Denormalize(profile.Pause, canvas),
            Denormalize(profile.Journal, canvas),
            MaterializeExtra(profile.Extra0, canvas),
            MaterializeExtra(profile.Extra1, canvas),
            MaterializeExtra(profile.Extra2, canvas),
            MaterializeExtra(profile.Extra3, canvas));
    }

    private static TouchRuntimeExtraControl MaterializeExtra(TouchExtraControl extra, TouchRect canvas)
    {
        TouchRect rect = Denormalize(extra.Rect, canvas);
        double ratio = VisualRatio(extra.Kind);
        return new TouchRuntimeExtraControl(
            extra.Enabled, extra.Kind, extra.Shape, extra.OpacityPercent, rect, Circle(rect, ratio));
    }

    // The editor and live overlay both consume this one visual-size policy.
    // Keeping the ratio out of either renderer prevents the preview disk from
    // drifting away from the control players actually see during gameplay.
    public static double VisualRatio(TouchLayoutProfile profile, TouchLayoutSelection selection)
    {
        if (selection.IsExtra) return VisualRatio(profile.Extra(selection.ExtraIndex).Kind);
        return selection.Primary switch
        {
            TouchLayoutControl.Movement => MovementVisualRatio,
            TouchLayoutControl.Jump => JumpVisualRatio,
            TouchLayoutControl.Dash => DashVisualRatio,
            TouchLayoutControl.Grab => GrabVisualRatio,
            _ => 1.0,
        };
    }

    private static double VisualRatio(TouchExtraControlKind kind) => kind switch
    {
        TouchExtraControlKind.Jump => JumpVisualRatio,
        TouchExtraControlKind.Dash or TouchExtraControlKind.CrouchDash => DashVisualRatio,
        TouchExtraControlKind.Grab => GrabVisualRatio,
        _ => 1.0,
    };

    public static TouchLayoutValidation Validate(
        TouchLayoutProfile profile,
        double canvasWidth = 956,
        double canvasHeight = 440)
    {
        if (profile.SchemaVersion != TouchLayoutProfile.CurrentSchemaVersion)
            return new(false, "Unsupported layout version");
        if (profile.MovementMode is not (TouchMovementMode.Fixed or TouchMovementMode.Floating) ||
            profile.ActionLayout is not (TouchActionLayout.SeparateButtons or TouchActionLayout.SplitRegion) ||
            profile.Sliding is not (TouchSlideMode.Off or TouchSlideMode.JumpDash) ||
            profile.SplitOrientation is not (TouchSplitOrientation.TopLeftToBottomRight or TouchSplitOrientation.TopRightToBottomLeft or TouchSplitOrientation.Vertical or TouchSplitOrientation.Horizontal) ||
            profile.GrabShape is not (TouchGrabShape.Circle or TouchGrabShape.Rectangle))
            return new(false, "Invalid layout option");
        canvasWidth = Math.Max(1, canvasWidth);
        canvasHeight = Math.Max(1, canvasHeight);

        foreach (TouchLayoutControl control in Enum.GetValues<TouchLayoutControl>())
        {
            TouchRect rect = profile.Rect(control);
            bool circle = IsCircle(profile, control);
            if (!Finite(rect) || !StaysInsideScreen(
                    rect, circle, VisualRatio(profile, new TouchLayoutSelection(control)), canvasWidth, canvasHeight))
                return new(false, $"{Name(control)} must stay inside the screen");
            (double minimumWidth, double minimumHeight) = Minimum(control);
            if (rect.Width * canvasWidth < minimumWidth || rect.Height * canvasHeight < minimumHeight)
                return new(false, $"{Name(control)} is too small");
            if (!ValidOpacity(profile.OpacityFor(control)))
                return new(false, $"{Name(control)} opacity is invalid");
        }

        List<(string Name, TouchRect Rect, bool Circle)> active = new();
        TouchLayoutControl movementControl = profile.MovementMode == TouchMovementMode.Fixed
            ? TouchLayoutControl.Movement : TouchLayoutControl.FloatingRegion;
        active.Add((Name(movementControl), profile.Rect(movementControl), IsCircle(profile, movementControl)));
        if (profile.ActionLayout == TouchActionLayout.SeparateButtons)
        {
            active.Add((Name(TouchLayoutControl.Jump), profile.Jump, true));
            active.Add((Name(TouchLayoutControl.Dash), profile.Dash, true));
        }
        else
        {
            active.Add((Name(TouchLayoutControl.SplitRegion), profile.SplitRegion, false));
        }
        active.Add((Name(TouchLayoutControl.Grab), profile.Grab, profile.GrabShape == TouchGrabShape.Circle));
        active.Add((Name(TouchLayoutControl.Pause), profile.Pause, false));
        if (profile.JournalEnabled) active.Add((Name(TouchLayoutControl.Journal), profile.Journal, false));
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            TouchExtraControl extra = profile.Extra(index);
            if (!extra.Enabled) continue;
            if (!Enum.IsDefined(extra.Kind) || !Enum.IsDefined(extra.Shape) || !ValidOpacity(extra.OpacityPercent))
                return new(false, $"Extra control {index + 1} is invalid");
            if (!Finite(extra.Rect) || !StaysInsideScreen(
                    extra.Rect, ExtraIsCircle(extra), VisualRatio(extra.Kind), canvasWidth, canvasHeight))
                return new(false, $"{ExtraName(extra.Kind)} must stay inside the screen");
            (double minimumWidth, double minimumHeight) = Minimum(extra.Kind);
            if (extra.Rect.Width * canvasWidth < minimumWidth || extra.Rect.Height * canvasHeight < minimumHeight)
                return new(false, $"{ExtraName(extra.Kind)} is too small");
            active.Add(($"{ExtraName(extra.Kind)} {index + 2}", extra.Rect, ExtraIsCircle(extra)));
        }
        for (int first = 0; first < active.Count; first++)
        for (int second = first + 1; second < active.Count; second++)
        {
            if (!HitRegionsOverlap(active[first].Circle, active[first].Rect,
                    active[second].Circle, active[second].Rect, canvasWidth, canvasHeight)) continue;
            return new(false, $"{active[first].Name} overlaps {active[second].Name}");
        }
        return TouchLayoutValidation.Valid;
    }

    public static bool IsStructurallyValid(TouchLayoutProfile profile)
    {
        if (profile.SchemaVersion != TouchLayoutProfile.CurrentSchemaVersion ||
            profile.MovementMode is not (TouchMovementMode.Fixed or TouchMovementMode.Floating) ||
            profile.ActionLayout is not (TouchActionLayout.SeparateButtons or TouchActionLayout.SplitRegion) ||
            profile.Sliding is not (TouchSlideMode.Off or TouchSlideMode.JumpDash) ||
            profile.SplitOrientation is not (TouchSplitOrientation.TopLeftToBottomRight or TouchSplitOrientation.TopRightToBottomLeft or TouchSplitOrientation.Vertical or TouchSplitOrientation.Horizontal) ||
            profile.GrabShape is not (TouchGrabShape.Circle or TouchGrabShape.Rectangle)) return false;
        foreach (TouchLayoutControl control in Enum.GetValues<TouchLayoutControl>())
        {
            TouchRect rect = profile.Rect(control);
            if (!Finite(rect) || !StructurallyBounded(rect, IsCircle(profile, control)) ||
                !ValidOpacity(profile.OpacityFor(control))) return false;
        }
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            TouchExtraControl extra = profile.Extra(index);
            if (!extra.Enabled) continue;
            if (!Enum.IsDefined(extra.Kind) || !Enum.IsDefined(extra.Shape) || !ValidOpacity(extra.OpacityPercent) ||
                !Finite(extra.Rect) || !StructurallyBounded(extra.Rect, ExtraIsCircle(extra))) return false;
        }
        return true;
    }

    public static TouchLayoutProfile Mirror(TouchLayoutProfile profile)
    {
        static TouchRect M(TouchRect value) => value with { X = 1.0 - value.X - value.Width };
        TouchSplitOrientation orientation = profile.SplitOrientation switch
        {
            TouchSplitOrientation.TopLeftToBottomRight => TouchSplitOrientation.TopRightToBottomLeft,
            TouchSplitOrientation.TopRightToBottomLeft => TouchSplitOrientation.TopLeftToBottomRight,
            _ => profile.SplitOrientation,
        };
        bool jumpFirst = profile.SplitOrientation == TouchSplitOrientation.Vertical
            ? !profile.JumpOnFirstHalf
            : profile.JumpOnFirstHalf;
        return profile with
        {
            SplitOrientation = orientation,
            JumpOnFirstHalf = jumpFirst,
            Movement = M(profile.Movement),
            FloatingRegion = M(profile.FloatingRegion),
            Jump = M(profile.Jump),
            Dash = M(profile.Dash),
            SplitRegion = M(profile.SplitRegion),
            Grab = M(profile.Grab),
            Pause = M(profile.Pause),
            Journal = M(profile.Journal),
            Extra0 = MirrorExtra(profile.Extra0),
            Extra1 = MirrorExtra(profile.Extra1),
            Extra2 = MirrorExtra(profile.Extra2),
            Extra3 = MirrorExtra(profile.Extra3),
        };
    }

    private static TouchExtraControl MirrorExtra(TouchExtraControl extra) =>
        extra with { Rect = extra.Rect with { X = 1.0 - extra.Rect.X - extra.Rect.Width } };

    public static TouchOwnedControl SplitControl(
        TouchRect rect,
        TouchSplitOrientation orientation,
        bool jumpOnFirstHalf,
        TouchPoint point,
        TouchOwnedControl previous = TouchOwnedControl.None,
        double hysteresisPoints = 0)
    {
        if (!rect.Contains(point)) return TouchOwnedControl.None;
        double x = (point.X - rect.X) / rect.Width;
        double y = (point.Y - rect.Y) / rect.Height;
        double signed = orientation switch
        {
            TouchSplitOrientation.TopLeftToBottomRight => x - y,
            TouchSplitOrientation.TopRightToBottomLeft => 1.0 - x - y,
            TouchSplitOrientation.Vertical => 0.5 - x,
            _ => 0.5 - y,
        };
        double normalizedHysteresis = hysteresisPoints <= 0
            ? 0
            : hysteresisPoints / Math.Max(1.0, Math.Min(rect.Width, rect.Height));
        bool first = signed >= 0;
        if (Math.Abs(signed) <= normalizedHysteresis && previous is TouchOwnedControl.Jump or TouchOwnedControl.Dash)
            return previous;
        bool jump = first == jumpOnFirstHalf;
        return jump ? TouchOwnedControl.Jump : TouchOwnedControl.Dash;
    }

    public static TouchRect Usable(double width, double height, SafeAreaMetrics safeArea) => new(
        safeArea.Left,
        safeArea.Top,
        Math.Max(1, width - safeArea.Left - safeArea.Right),
        Math.Max(1, height - safeArea.Top - safeArea.Bottom));

    public static TouchRect FullCanvas(double width, double height) =>
        new(0, 0, Math.Max(1, width), Math.Max(1, height));

    public static TouchLayoutProfile RebaseFromSafeArea(
        TouchLayoutProfile profile,
        double width,
        double height,
        SafeAreaMetrics safeArea)
    {
        TouchRect safe = Usable(width, height, safeArea);
        TouchRect canvas = FullCanvas(width, height);
        TouchRect Convert(TouchRect value) => Normalize(Denormalize(value, safe), canvas);
        return profile with
        {
            SchemaVersion = TouchLayoutProfile.CurrentSchemaVersion,
            Movement = Convert(profile.Movement),
            FloatingRegion = Convert(profile.FloatingRegion),
            Jump = Convert(profile.Jump),
            Dash = Convert(profile.Dash),
            SplitRegion = Convert(profile.SplitRegion),
            Grab = Convert(profile.Grab),
            Pause = Convert(profile.Pause),
            Journal = Convert(profile.Journal),
        };
    }

    public static TouchRect Normalize(TouchRect rect, TouchRect usable) => new(
        (rect.X - usable.X) / usable.Width,
        (rect.Y - usable.Y) / usable.Height,
        rect.Width / usable.Width,
        rect.Height / usable.Height);

    public static TouchRect Denormalize(TouchRect rect, TouchRect usable) => new(
        usable.X + rect.X * usable.Width,
        usable.Y + rect.Y * usable.Height,
        rect.Width * usable.Width,
        rect.Height * usable.Height);

    public static TouchRect FitInside(TouchRect rect, TouchRect usable)
    {
        double width = Math.Min(rect.Width, usable.Width);
        double height = Math.Min(rect.Height, usable.Height);
        return new TouchRect(
            Math.Clamp(rect.X, usable.X, usable.Right - width),
            Math.Clamp(rect.Y, usable.Y, usable.Bottom - height),
            width,
            height);
    }

    // Circular controls retain a larger invisible hit radius than the disk the
    // player sees. Only that visible disk is constrained to the display: the
    // hit circle may be clipped by the physical edge, allowing truly flush
    // corner/edge placement without sacrificing the interior hit margin.
    public static TouchRect ConstrainToScreen(
        TouchRect rect,
        bool circle,
        double visualRatio,
        double canvasWidth,
        double canvasHeight)
    {
        canvasWidth = Math.Max(1, canvasWidth);
        canvasHeight = Math.Max(1, canvasHeight);
        if (!circle)
        {
            double width = Math.Min(rect.Width, 1);
            double height = Math.Min(rect.Height, 1);
            return rect with
            {
                X = Math.Clamp(rect.X, 0, 1 - width),
                Y = Math.Clamp(rect.Y, 0, 1 - height),
                Width = width,
                Height = height,
            };
        }

        TouchRect pixels = Scale(rect, canvasWidth, canvasHeight);
        double hitRadius = Math.Min(pixels.Width, pixels.Height) * 0.5;
        double visibleRadius = hitRadius * Math.Clamp(visualRatio, 0.01, 1.0);
        visibleRadius = Math.Min(visibleRadius, Math.Min(canvasWidth, canvasHeight) * 0.5);
        double centerX = Math.Clamp(pixels.X + pixels.Width * 0.5, visibleRadius, canvasWidth - visibleRadius);
        double centerY = Math.Clamp(pixels.Y + pixels.Height * 0.5, visibleRadius, canvasHeight - visibleRadius);
        return rect with
        {
            X = (centerX - pixels.Width * 0.5) / canvasWidth,
            Y = (centerY - pixels.Height * 0.5) / canvasHeight,
        };
    }

    private static TouchRect CircleRect(TouchCircle circle) => new(
        circle.Center.X - circle.HitRadius,
        circle.Center.Y - circle.HitRadius,
        circle.HitRadius * 2,
        circle.HitRadius * 2);

    private static TouchCircle Circle(TouchRect rect, double visualRatio)
    {
        double hit = Math.Min(rect.Width, rect.Height) * 0.5;
        return new TouchCircle(
            new TouchPoint(rect.X + rect.Width * 0.5, rect.Y + rect.Height * 0.5),
            hit * visualRatio,
            hit);
    }

    private static bool Finite(TouchRect value) =>
        double.IsFinite(value.X) && double.IsFinite(value.Y) &&
        double.IsFinite(value.Width) && double.IsFinite(value.Height) &&
        value.Width > 0 && value.Height > 0;

    private static bool StaysInsideScreen(
        TouchRect rect,
        bool circle,
        double visualRatio,
        double width,
        double height)
    {
        if (!circle) return rect.X >= 0 && rect.Y >= 0 && rect.Right <= 1 && rect.Bottom <= 1;
        TouchCircle visible = Circle(Scale(rect, width, height), visualRatio);
        const double epsilon = 0.001;
        return visible.Center.X - visible.Radius >= -epsilon &&
            visible.Center.Y - visible.Radius >= -epsilon &&
            visible.Center.X + visible.Radius <= width + epsilon &&
            visible.Center.Y + visible.Radius <= height + epsilon;
    }

    private static bool StructurallyBounded(TouchRect rect, bool circle)
    {
        if (!circle) return rect.X >= 0 && rect.Y >= 0 && rect.Right <= 1 && rect.Bottom <= 1;
        double centerX = rect.X + rect.Width * 0.5;
        double centerY = rect.Y + rect.Height * 0.5;
        return rect.Width <= 2 && rect.Height <= 2 &&
            centerX is >= 0 and <= 1 && centerY is >= 0 and <= 1;
    }

    private static bool HitRegionsOverlap(
        bool aCircle,
        TouchRect first,
        bool bCircle,
        TouchRect second,
        double width,
        double height)
    {
        TouchRect a = Scale(first, width, height);
        TouchRect b = Scale(second, width, height);
        if (!aCircle && !bCircle) return RectsOverlap(a, b);
        if (aCircle && bCircle)
        {
            (TouchPoint Center, double Radius) ca = CircleGeometry(a);
            (TouchPoint Center, double Radius) cb = CircleGeometry(b);
            TouchPoint delta = ca.Center - cb.Center;
            double radius = ca.Radius + cb.Radius;
            return delta.X * delta.X + delta.Y * delta.Y < radius * radius;
        }
        TouchRect rect = aCircle ? b : a;
        (TouchPoint Center, double Radius) circle = CircleGeometry(aCircle ? a : b);
        double closestX = Math.Clamp(circle.Center.X, rect.X, rect.Right);
        double closestY = Math.Clamp(circle.Center.Y, rect.Y, rect.Bottom);
        double dx = circle.Center.X - closestX;
        double dy = circle.Center.Y - closestY;
        return dx * dx + dy * dy < circle.Radius * circle.Radius;
    }

    private static bool IsCircle(TouchLayoutProfile profile, TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.Movement or TouchLayoutControl.Jump or TouchLayoutControl.Dash => true,
        TouchLayoutControl.Grab => profile.GrabShape == TouchGrabShape.Circle,
        _ => false,
    };

    // An extra control's shape is a user-facing geometry choice; its logical
    // action remains the same whether the hit region is circular or rectangular.
    public static bool ExtraIsCircle(TouchExtraControl extra) => extra.Shape == TouchGrabShape.Circle;

    private static bool ValidOpacity(int value) => value is >= 0 and <= 100 && value % 10 == 0;

    private static TouchRect Scale(TouchRect value, double width, double height) =>
        new(value.X * width, value.Y * height, value.Width * width, value.Height * height);
    private static (TouchPoint Center, double Radius) CircleGeometry(TouchRect value) =>
        (new TouchPoint(value.X + value.Width * 0.5, value.Y + value.Height * 0.5), Math.Min(value.Width, value.Height) * 0.5);
    private static bool RectsOverlap(TouchRect a, TouchRect b) =>
        a.X < b.Right && a.Right > b.X && a.Y < b.Bottom && a.Bottom > b.Y;

    private static (double Width, double Height) Minimum(TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.Movement => (96, 96),
        TouchLayoutControl.FloatingRegion => (140, 120),
        TouchLayoutControl.SplitRegion => (100, 70),
        TouchLayoutControl.Grab => (48, 42),
        TouchLayoutControl.Pause or TouchLayoutControl.Journal => (38, 32),
        _ => (52, 52),
    };

    private static (double Width, double Height) Minimum(TouchExtraControlKind kind) => kind switch
    {
        TouchExtraControlKind.Pause or TouchExtraControlKind.Journal => (38, 32),
        TouchExtraControlKind.CrouchDash or TouchExtraControlKind.QuickRestart => (70, 42),
        TouchExtraControlKind.Grab => (48, 42),
        _ => (52, 52),
    };

    public static string Name(TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.FloatingRegion => "Floating movement region",
        TouchLayoutControl.SplitRegion => "Jump / Dash region",
        _ => control.ToString(),
    };

    public static string ExtraName(TouchExtraControlKind kind) => kind switch
    {
        TouchExtraControlKind.CrouchDash => "Crouch Dash",
        TouchExtraControlKind.QuickRestart => "Quick Restart",
        _ => kind.ToString(),
    };

    public static bool TryAddExtra(
        TouchLayoutProfile profile,
        TouchExtraControlKind kind,
        TouchGrabShape shape,
        TouchRect source,
        double canvasWidth,
        double canvasHeight,
        out TouchLayoutProfile updated,
        out int extraIndex)
    {
        updated = profile;
        extraIndex = -1;
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            if (profile.Extra(index).Enabled) continue;
            extraIndex = index;
            break;
        }
        if (extraIndex < 0) return false;

        TouchRect sized = source;
        if (kind is TouchExtraControlKind.CrouchDash or TouchExtraControlKind.QuickRestart)
            sized = source with { Width = Math.Max(source.Width, 0.12), Height = Math.Max(source.Height * 0.72, 0.08) };
        TouchExtraControl extra = new(true, kind, shape, 100, sized);
        (double X, double Y)[] offsets =
        {
            (0.05, 0), (-0.05, 0), (0, 0.08), (0, -0.08),
            (0.10, 0), (-0.10, 0), (0.05, 0.10), (-0.05, 0.10),
            (0.15, 0), (-0.15, 0), (0.10, -0.12), (-0.10, -0.12),
        };
        foreach ((double x, double y) in offsets)
        {
            TouchRect candidate = ConstrainToScreen(sized with
            {
                X = sized.X + x,
                Y = sized.Y + y,
            }, shape == TouchGrabShape.Circle, VisualRatio(kind), canvasWidth, canvasHeight);
            TouchLayoutProfile attempt = profile.WithExtra(extraIndex, extra with { Rect = candidate });
            if (!Validate(attempt, canvasWidth, canvasHeight).IsValid) continue;
            updated = attempt;
            return true;
        }

        for (int row = 0; row < 6; row++)
        for (int column = 0; column < 10; column++)
        {
            TouchRect candidate = ConstrainToScreen(sized with
            {
                X = 0.02 + column * 0.095,
                Y = 0.02 + row * 0.16,
            }, shape == TouchGrabShape.Circle, VisualRatio(kind), canvasWidth, canvasHeight);
            TouchLayoutProfile attempt = profile.WithExtra(extraIndex, extra with { Rect = candidate });
            if (!Validate(attempt, canvasWidth, canvasHeight).IsValid) continue;
            updated = attempt;
            return true;
        }
        extraIndex = -1;
        return false;
    }
}

public static class TouchLayoutCodec
{
    private const string Prefix = "D3|2|";
    private const string LegacyPrefix = "D3|1|";

    public static string Encode(TouchLayoutProfile profile)
    {
        string[] values =
        {
            ((int)profile.MovementMode).ToString(CultureInfo.InvariantCulture),
            ((int)profile.ActionLayout).ToString(CultureInfo.InvariantCulture),
            ((int)profile.Sliding).ToString(CultureInfo.InvariantCulture),
            ((int)profile.SplitOrientation).ToString(CultureInfo.InvariantCulture),
            profile.JumpOnFirstHalf ? "1" : "0",
            ((int)profile.GrabShape).ToString(CultureInfo.InvariantCulture),
            R(profile.Movement), R(profile.FloatingRegion), R(profile.Jump), R(profile.Dash),
            R(profile.SplitRegion), R(profile.Grab), R(profile.Pause), R(profile.Journal),
            O(profile.Opacity),
            profile.JournalEnabled ? "1" : "0",
            E(profile.Extra0), E(profile.Extra1), E(profile.Extra2), E(profile.Extra3),
        };
        return Prefix + string.Join("|", values);
    }

    public static bool TryDecode(string? value, out TouchLayoutProfile profile) =>
        TryDecode(value, out profile, out _);

    public static bool TryDecode(string? value, out TouchLayoutProfile profile, out bool legacySafeAreaCoordinates)
    {
        profile = default;
        legacySafeAreaCoordinates = false;
        if (value == null || value.Length is < 20 or > 4096)
            return false;
        bool legacy = value.StartsWith(LegacyPrefix, StringComparison.Ordinal);
        if (!legacy && !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        int prefixLength = legacy ? LegacyPrefix.Length : Prefix.Length;
        string[] parts = value[prefixLength..].Split('|');
        if (parts.Length != (legacy ? 14 : 20) ||
            !I(parts[0], out int movement) || !I(parts[1], out int action) ||
            !I(parts[2], out int sliding) || !I(parts[3], out int orientation) ||
            !I(parts[4], out int assignment) || !I(parts[5], out int shape) ||
            !R(parts[6], out TouchRect movementRect) || !R(parts[7], out TouchRect floatingRect) ||
            !R(parts[8], out TouchRect jump) || !R(parts[9], out TouchRect dash) ||
            !R(parts[10], out TouchRect split) || !R(parts[11], out TouchRect grab) ||
            !R(parts[12], out TouchRect pause) || !R(parts[13], out TouchRect journal))
            return false;
        TouchControlOpacityProfile opacity = TouchControlOpacityProfile.Default;
        bool journalEnabled = true;
        TouchExtraControl extra0 = TouchExtraControl.Empty;
        TouchExtraControl extra1 = TouchExtraControl.Empty;
        TouchExtraControl extra2 = TouchExtraControl.Empty;
        TouchExtraControl extra3 = TouchExtraControl.Empty;
        if (!legacy &&
            (!O(parts[14], out opacity) || !I(parts[15], out int enabled) || enabled is not (0 or 1) ||
             !E(parts[16], out extra0) || !E(parts[17], out extra1) ||
             !E(parts[18], out extra2) || !E(parts[19], out extra3))) return false;
        if (!legacy) journalEnabled = parts[15] == "1";
        profile = new TouchLayoutProfile(
            TouchLayoutProfile.CurrentSchemaVersion,
            (TouchMovementMode)movement,
            (TouchActionLayout)action,
            (TouchSlideMode)sliding,
            (TouchSplitOrientation)orientation,
            assignment == 1,
            (TouchGrabShape)shape,
            movementRect, floatingRect, jump, dash, split, grab, pause, journal,
            opacity, journalEnabled, extra0, extra1, extra2, extra3);
        legacySafeAreaCoordinates = legacy;
        // Decoding is form-factor independent. The host performs point-size
        // and overlap validation against the current full-screen canvas.
        return assignment is 0 or 1 && TouchLayoutPolicy.IsStructurallyValid(profile);
    }

    private static string R(TouchRect value) => string.Join(",", new[]
    {
        F(value.X), F(value.Y), F(value.Width), F(value.Height),
    });

    private static bool R(string value, out TouchRect rect)
    {
        rect = default;
        string[] parts = value.Split(',');
        if (parts.Length != 4 || !D(parts[0], out double x) || !D(parts[1], out double y) ||
            !D(parts[2], out double width) || !D(parts[3], out double height)) return false;
        rect = new TouchRect(x, y, width, height);
        return true;
    }

    private static string O(TouchControlOpacityProfile value) => string.Join(",", new[]
    {
        value.Movement, value.FloatingRegion, value.Jump, value.Dash,
        value.SplitRegion, value.Grab, value.Pause, value.Journal,
    });

    private static bool O(string value, out TouchControlOpacityProfile opacity)
    {
        opacity = default;
        string[] parts = value.Split(',');
        if (parts.Length != 8) return false;
        int[] parsed = new int[8];
        for (int index = 0; index < parts.Length; index++) if (!I(parts[index], out parsed[index])) return false;
        opacity = new(parsed[0], parsed[1], parsed[2], parsed[3], parsed[4], parsed[5], parsed[6], parsed[7]);
        return true;
    }

    private static string E(TouchExtraControl value) => string.Join(",", new[]
    {
        value.Enabled ? "1" : "0",
        ((int)value.Kind).ToString(CultureInfo.InvariantCulture),
        ((int)value.Shape).ToString(CultureInfo.InvariantCulture),
        value.OpacityPercent.ToString(CultureInfo.InvariantCulture),
        F(value.Rect.X), F(value.Rect.Y), F(value.Rect.Width), F(value.Rect.Height),
    });

    private static bool E(string value, out TouchExtraControl extra)
    {
        extra = default;
        string[] parts = value.Split(',');
        if (parts.Length != 8 || !I(parts[0], out int enabled) || enabled is not (0 or 1) ||
            !I(parts[1], out int kind) || !I(parts[2], out int shape) || !I(parts[3], out int opacity) ||
            !D(parts[4], out double x) || !D(parts[5], out double y) ||
            !D(parts[6], out double width) || !D(parts[7], out double height)) return false;
        extra = new(enabled == 1, (TouchExtraControlKind)kind, (TouchGrabShape)shape,
            opacity, new TouchRect(x, y, width, height));
        return true;
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static bool D(string value, out double result) =>
        double.TryParse(value, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out result) && double.IsFinite(result);
    private static bool I(string value, out int result) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
}

public sealed class TouchLayoutEditorSession
{
    private const int MaximumUndo = 32;
    private readonly TouchLayoutProfile factory;
    private readonly List<TouchLayoutProfile> undo = new();
    private TouchLayoutProfile gestureStart;
    private bool gesture;

    public TouchLayoutEditorSession(TouchLayoutProfile current, TouchLayoutProfile factoryProfile)
    {
        Original = current;
        Working = current;
        factory = factoryProfile;
    }

    public TouchLayoutProfile Original { get; }
    public TouchLayoutProfile Working { get; private set; }
    public bool CanUndo => undo.Count > 0;
    public int UndoCount => undo.Count;

    public void BeginGesture()
    {
        if (gesture) return;
        gestureStart = Working;
        gesture = true;
    }

    public void PreviewRect(TouchLayoutControl control, TouchRect value)
    {
        if (!gesture) BeginGesture();
        Working = Working.WithRect(control, value);
    }

    public void PreviewRect(TouchLayoutSelection selection, TouchRect value)
    {
        if (!selection.IsExtra)
        {
            PreviewRect(selection.Primary, value);
            return;
        }
        if (!gesture) BeginGesture();
        Working = Working.WithExtra(selection.ExtraIndex,
            Working.Extra(selection.ExtraIndex) with { Rect = value });
    }

    public void EndGesture()
    {
        if (!gesture) return;
        gesture = false;
        if (gestureStart != Working) Push(gestureStart);
    }

    public void Change(TouchLayoutProfile value)
    {
        EndGesture();
        if (value == Working) return;
        Push(Working);
        Working = value;
    }

    public void ResetSelected(TouchLayoutControl control) => Change(Working.WithRect(control, factory.Rect(control)));
    public void ResetSelected(TouchLayoutSelection selection)
    {
        if (!selection.IsExtra)
        {
            TouchLayoutProfile reset = Working.WithRect(selection.Primary, factory.Rect(selection.Primary))
                .WithOpacity(selection.Primary, factory.OpacityFor(selection.Primary));
            Change(selection.Primary == TouchLayoutControl.Journal ? reset with { JournalEnabled = true } : reset);
            return;
        }
        TouchExtraControl extra = Working.Extra(selection.ExtraIndex);
        Change(Working.WithExtra(selection.ExtraIndex, extra with { OpacityPercent = 100 }));
    }

    public void SetOpacity(TouchLayoutSelection selection, int value)
    {
        value = Math.Clamp(value, 0, 100) / 10 * 10;
        if (!selection.IsExtra) Change(Working.WithOpacity(selection.Primary, value));
        else Change(Working.WithExtra(selection.ExtraIndex,
            Working.Extra(selection.ExtraIndex) with { OpacityPercent = value }));
    }

    public void SetShape(TouchLayoutSelection selection, TouchGrabShape shape)
    {
        if (!selection.IsExtra)
        {
            if (selection.Primary == TouchLayoutControl.Grab) Change(Working with { GrabShape = shape });
            return;
        }
        TouchExtraControl extra = Working.Extra(selection.ExtraIndex);
        Change(Working.WithExtra(selection.ExtraIndex, extra with { Shape = shape }));
    }

    public bool Delete(TouchLayoutSelection selection)
    {
        if (selection.IsExtra)
        {
            Change(Working.WithExtra(selection.ExtraIndex, TouchExtraControl.Empty));
            return true;
        }
        if (selection.Primary != TouchLayoutControl.Journal || !Working.JournalEnabled) return false;
        Change(Working with { JournalEnabled = false });
        return true;
    }
    public void ResetLayout() => Change(factory with
    {
        // Factory means factory geometry. Keep the user's surrounding Options
        // selections so a Split/Floating layout cannot silently become
        // Separate/Fixed while the Options rows still display the old mode.
        MovementMode = Working.MovementMode,
        ActionLayout = Working.ActionLayout,
        Sliding = Working.Sliding,
    });
    public void Mirror() => Change(TouchLayoutPolicy.Mirror(Working));

    public bool Undo()
    {
        EndGesture();
        if (undo.Count == 0) return false;
        Working = undo[^1];
        undo.RemoveAt(undo.Count - 1);
        return true;
    }

    public bool TryCommit(double usableWidth, double usableHeight, out TouchLayoutProfile value)
    {
        EndGesture();
        TouchLayoutValidation validation = TouchLayoutPolicy.Validate(Working, usableWidth, usableHeight);
        value = validation.IsValid ? Working : Original;
        return validation.IsValid;
    }

    private void Push(TouchLayoutProfile value)
    {
        if (undo.Count == MaximumUndo) undo.RemoveAt(0);
        undo.Add(value);
    }
}

public static class TouchLayoutEditorGeometry
{
    public static TouchRect Move(
        TouchRect start,
        TouchPoint deltaPoints,
        double usableWidth,
        double usableHeight,
        bool circle,
        double visualRatio) => TouchLayoutPolicy.ConstrainToScreen(
            start with
            {
                X = start.X + deltaPoints.X / Math.Max(1, usableWidth),
                Y = start.Y + deltaPoints.Y / Math.Max(1, usableHeight),
            }, circle, visualRatio, usableWidth, usableHeight);

    public static TouchRect Resize(
        TouchRect start,
        TouchPoint deltaPoints,
        double usableWidth,
        double usableHeight,
        bool circle,
        double visualRatio = 1.0)
    {
        usableWidth = Math.Max(1, usableWidth);
        usableHeight = Math.Max(1, usableHeight);
        if (!circle)
        {
            return start with
            {
                Width = Math.Clamp(start.Width + deltaPoints.X / usableWidth, 0.02, 1.0 - start.X),
                Height = Math.Clamp(start.Height + deltaPoints.Y / usableHeight, 0.02, 1.0 - start.Y),
            };
        }

        double startDiameter = Math.Min(start.Width * usableWidth, start.Height * usableHeight);
        double dominantDelta = Math.Abs(deltaPoints.X) >= Math.Abs(deltaPoints.Y)
            ? deltaPoints.X
            : deltaPoints.Y;
        double diameter = Math.Max(24, startDiameter + dominantDelta);
        diameter = Math.Min(diameter,
            Math.Min(usableWidth, usableHeight) / Math.Clamp(visualRatio, 0.01, 1.0));
        return TouchLayoutPolicy.ConstrainToScreen(start with
        {
            Width = diameter / usableWidth,
            Height = diameter / usableHeight,
        }, true, visualRatio, usableWidth, usableHeight);
    }
}

public sealed class GrabSourceArbiter
{
    private readonly bool[] raw = new bool[3];
    private readonly bool[] toggle = new bool[3];
    private GrabModeProfiles profiles;

    public GrabSourceArbiter(GrabModeProfiles modes, AppleInputSource initial)
    {
        profiles = modes.Validated(AppleGrabMode.Hold);
        ActiveSource = initial;
    }

    public AppleInputSource ActiveSource { get; private set; }
    public AppleGrabMode ActiveMode => profiles.For(ActiveSource);
    public bool Effective => EffectiveFor(ActiveSource);
    public GrabModeProfiles Profiles => profiles;

    public void Observe(AppleInputSource source, bool rawDown, bool pressed, bool meaningful)
    {
        raw[(int)source] = rawDown;
        if (meaningful && source != ActiveSource)
        {
            Array.Clear(toggle);
            ActiveSource = source;
        }
        if (source == ActiveSource && pressed && profiles.For(source) == AppleGrabMode.Toggle)
            toggle[(int)source] = !toggle[(int)source];
    }

    public void SetMode(AppleInputSource source, AppleGrabMode mode)
    {
        if (mode is not (AppleGrabMode.Hold or AppleGrabMode.Invert or AppleGrabMode.Toggle)) mode = AppleGrabMode.Hold;
        if (profiles.For(source) == mode) return;
        profiles = profiles.With(source, mode);
        toggle[(int)source] = false;
    }

    public void ForceSource(AppleInputSource source)
    {
        if (source == ActiveSource) return;
        Array.Clear(toggle);
        ActiveSource = source;
    }

    public void ResetTransient()
    {
        Array.Clear(raw);
        Array.Clear(toggle);
    }

    public bool EffectiveFor(AppleInputSource source) => profiles.For(source) switch
    {
        AppleGrabMode.Invert => !raw[(int)source],
        AppleGrabMode.Toggle => toggle[(int)source],
        _ => raw[(int)source],
    };
}

public sealed class CustomTouchInteractionState
{
    public const int MaximumTouches = 8;
    public const double DeadzoneRatio = 0.18;
    public const double HysteresisDegrees = 8.0;
    public const double SplitTransferHysteresisPoints = 4.0;

    private readonly FingerOwner[] owners = new FingerOwner[MaximumTouches];
    private TouchRuntimeLayout layout;
    private bool jump;
    private bool dash;
    private bool pause;
    private bool journal;
    private bool grab;
    private bool crouchDash;
    private bool quickRestart;
    private bool grabActionPressed;
    private bool meaningfulPressed;
    private bool previousJump;
    private bool previousDash;
    private bool previousPause;
    private bool previousJournal;
    private bool previousGrab;
    private bool previousCrouchDash;
    private bool previousQuickRestart;
    private TouchDirection direction;
    private TouchDirection previousDirection;
    private TouchPoint movementCenter;

    public CustomTouchInteractionState(TouchRuntimeLayout value)
    {
        layout = value;
        movementCenter = value.Movement.Center;
    }

    public AppleGrabMode GrabMode { get; set; } = AppleGrabMode.Toggle;
    public TouchDirection Direction => direction;
    public TouchPoint MovementCenter => movementCenter;
    public int MoveX => TouchDirectionPolicy.Axis(direction).X;
    public int MoveY => TouchDirectionPolicy.Axis(direction).Y;
    public bool Jump => jump;
    public bool Dash => dash;
    public bool Pause => pause;
    public bool Journal => journal;
    public bool Grab => grab;
    public bool CrouchDash => crouchDash;
    public bool QuickRestart => quickRestart;
    public bool GrabActionPressed => grabActionPressed;
    public bool MeaningfulPressed => meaningfulPressed;
    public bool JumpPressed => jump && !previousJump;
    public bool DashPressed => dash && !previousDash;
    public bool PausePressed => pause && !previousPause;
    public bool JournalPressed => journal && !previousJournal;
    public bool GrabPressed => grab && !previousGrab;
    public bool CrouchDashPressed => crouchDash && !previousCrouchDash;
    public bool QuickRestartPressed => quickRestart && !previousQuickRestart;
    public bool JumpReleased => !jump && previousJump;
    public bool DashReleased => !dash && previousDash;
    public bool PauseReleased => !pause && previousPause;
    public bool JournalReleased => !journal && previousJournal;
    public bool GrabReleased => !grab && previousGrab;
    public bool CrouchDashReleased => !crouchDash && previousCrouchDash;
    public bool QuickRestartReleased => !quickRestart && previousQuickRestart;
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
        meaningfulPressed = false;
        previousJump = jump;
        previousDash = dash;
        previousPause = pause;
        previousJournal = journal;
        previousGrab = grab;
        previousCrouchDash = crouchDash;
        previousQuickRestart = quickRestart;
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
            bool logicalAlreadyHeld = owners.Any(owner => owner.Active &&
                SameLogicalAction(owner.Control, control));
            owners[slot] = new FingerOwner(fingerId, control, point, true);
            meaningfulPressed |= control != TouchOwnedControl.None;
            bool logicalGrabEdge = (control is TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab) &&
                !logicalAlreadyHeld;
            grabActionPressed |= logicalGrabEdge;
            if (control == TouchOwnedControl.Movement)
            {
                movementCenter = layout.MovementMode == TouchMovementMode.Floating ? point : layout.Movement.Center;
                UpdateDirection(point);
            }
            RecomputeHeld();
            if (!hapticsEnabled) return TouchHapticAction.None;
            return control switch
            {
                TouchOwnedControl.Jump when !logicalAlreadyHeld => TouchHapticAction.Jump,
                TouchOwnedControl.Dash or TouchOwnedControl.CrouchDash when !logicalAlreadyHeld => TouchHapticAction.Dash,
                TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab when
                    GrabMode == AppleGrabMode.Toggle && logicalGrabEdge => TouchHapticAction.GrabToggle,
                _ => TouchHapticAction.None,
            };
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
        if (layout.SlideMode == TouchSlideMode.JumpDash && owner.Control is TouchOwnedControl.Jump or TouchOwnedControl.Dash)
        {
            TouchOwnedControl replacement = ActionAt(point, owner.Control);
            if (replacement is TouchOwnedControl.Jump or TouchOwnedControl.Dash)
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
                if (activeFingerIds[index] == owners[slot].FingerId) { found = true; break; }
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

    public void UpdateLayout(TouchRuntimeLayout value)
    {
        layout = value;
        Reset();
    }

    public void Reset()
    {
        Array.Clear(owners);
        direction = TouchDirection.Neutral;
        movementCenter = layout.Movement.Center;
        jump = dash = pause = journal = grab = crouchDash = quickRestart = false;
        previousJump = previousDash = previousPause = previousJournal = previousGrab =
            previousCrouchDash = previousQuickRestart = false;
        grabActionPressed = meaningfulPressed = false;
        previousDirection = TouchDirection.Neutral;
    }

    private TouchOwnedControl HitTest(TouchPoint point)
    {
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            TouchRuntimeExtraControl extra = layout.Extra(index);
            if (!extra.Enabled || !ExtraContains(extra, point)) continue;
            return ExtraOwnedControl(extra.Kind);
        }
        if (layout.Pause.Contains(point)) return TouchOwnedControl.Pause;
        if (layout.Profile.JournalEnabled && layout.Journal.Contains(point)) return TouchOwnedControl.Journal;
        bool grabHit = layout.GrabShape == TouchGrabShape.Rectangle
            ? layout.GrabRect.Contains(point)
            : layout.GrabCircle.Contains(point);
        if (grabHit) return TouchOwnedControl.Grab;
        if (layout.ActionLayout == TouchActionLayout.SplitRegion)
        {
            TouchOwnedControl split = TouchLayoutPolicy.SplitControl(
                layout.SplitRegion, layout.Profile.SplitOrientation, layout.Profile.JumpOnFirstHalf, point);
            if (split != TouchOwnedControl.None) return split;
        }
        else
        {
            if (layout.Jump.Contains(point)) return TouchOwnedControl.Jump;
            if (layout.Dash.Contains(point)) return TouchOwnedControl.Dash;
        }
        if (HasMovementOwner()) return TouchOwnedControl.None;
        if (layout.MovementMode == TouchMovementMode.Fixed)
            return layout.Movement.Contains(point) ? TouchOwnedControl.Movement : TouchOwnedControl.None;
        return layout.FloatingRegion.Contains(point) ? TouchOwnedControl.Movement : TouchOwnedControl.None;
    }

    private TouchOwnedControl ActionAt(TouchPoint point, TouchOwnedControl previous)
    {
        if (layout.ActionLayout == TouchActionLayout.SplitRegion)
        {
            TouchOwnedControl split = TouchLayoutPolicy.SplitControl(
                layout.SplitRegion, layout.Profile.SplitOrientation, layout.Profile.JumpOnFirstHalf,
                point, previous, SplitTransferHysteresisPoints);
            if (split is TouchOwnedControl.Jump or TouchOwnedControl.Dash) return split;
        }
        else
        {
            if (layout.Jump.Contains(point)) return TouchOwnedControl.Jump;
            if (layout.Dash.Contains(point)) return TouchOwnedControl.Dash;
        }
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            TouchRuntimeExtraControl extra = layout.Extra(index);
            if (!extra.Enabled || extra.Kind is not (TouchExtraControlKind.Jump or TouchExtraControlKind.Dash) ||
                !ExtraContains(extra, point)) continue;
            return ExtraOwnedControl(extra.Kind);
        }
        return previous;
    }

    private static bool ExtraContains(TouchRuntimeExtraControl extra, TouchPoint point) =>
        TouchLayoutPolicy.ExtraIsCircle(new TouchExtraControl(
            extra.Enabled, extra.Kind, extra.Shape, extra.OpacityPercent, extra.Rect))
            ? extra.Circle.Contains(point)
            : extra.Rect.Contains(point);

    private static TouchOwnedControl ExtraOwnedControl(TouchExtraControlKind kind) => kind switch
    {
        TouchExtraControlKind.Jump => TouchOwnedControl.Jump,
        TouchExtraControlKind.Dash => TouchOwnedControl.Dash,
        TouchExtraControlKind.Grab => TouchOwnedControl.Grab,
        TouchExtraControlKind.Pause => TouchOwnedControl.Pause,
        TouchExtraControlKind.Journal => TouchOwnedControl.Journal,
        TouchExtraControlKind.CrouchDash => TouchOwnedControl.CrouchDash,
        _ => TouchOwnedControl.QuickRestart,
    };

    private static bool SameLogicalAction(TouchOwnedControl first, TouchOwnedControl second)
    {
        if (first == second) return first != TouchOwnedControl.None;
        return first is TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab &&
            second is TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab;
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
        grab = owners.Any(owner => owner.Active && owner.Control is TouchOwnedControl.Grab or TouchOwnedControl.ShoulderGrab);
        crouchDash = owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.CrouchDash);
        quickRestart = owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.QuickRestart);
    }

    private void UpdateDirection(TouchPoint point) => direction = TouchDirectionPolicy.Classify(
        point - movementCenter, layout.Movement.Radius, DeadzoneRatio, HysteresisDegrees, direction);
    private bool HasMovementOwner() => owners.Any(owner => owner.Active && owner.Control == TouchOwnedControl.Movement);
    private int FindOwner(int fingerId)
    {
        for (int index = 0; index < owners.Length; index++)
            if (owners[index].Active && owners[index].FingerId == fingerId) return index;
        return -1;
    }
    private int FindFreeOwner()
    {
        for (int index = 0; index < owners.Length; index++) if (!owners[index].Active) return index;
        return -1;
    }

    private readonly record struct FingerOwner(int FingerId, TouchOwnedControl Control, TouchPoint Point, bool Active);
}
