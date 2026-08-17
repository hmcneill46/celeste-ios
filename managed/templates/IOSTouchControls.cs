using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using CelesteAppleInput;
using CelesteIOSFoundation;
using Foundation;
using GameController;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;
using UIKit;

namespace Celeste;

public static class IOSTouchControls
{
    private const int TextureSize = 128;
    // Celeste's ordinary keyboard/controller prompt frames are 80 x 80. Keep
    // the larger mask for the touch overlay, but expose a normal-size prompt.
    private const int PromptSize = 80;
    private const float FadeSeconds = 0.15f;
    private const string PromptPreferenceKey = "CelesteIOS.ControllerPrompts.v1";
    private static readonly Stopwatch LatencyClock = Stopwatch.StartNew();
    private static readonly NSUserDefaults Defaults = NSUserDefaults.StandardUserDefaults;
    private static TouchPreferences preferences;
    private static ControllerPromptMode promptMode;
    private static TouchInteractionState state;
    private static TouchControlLayout layout;
    private static IOSPresentationCoordinator.IOSPresentation presentation;
    private static UIImpactFeedbackGenerator feedback;
    private static Texture2D circleTexture;
    private static Texture2D jumpTexture;
    private static Texture2D dashTexture;
    private static Texture2D grabUngrabbedTexture;
    private static Texture2D grabGrabbedTexture;
    private static Texture2D pauseTexture;
    private static Texture2D journalTexture;
    private static Texture2D neutralTouchTexture;
    private static MTexture jumpPrompt;
    private static MTexture dashPrompt;
    private static MTexture grabPrompt;
    private static MTexture pausePrompt;
    private static MTexture journalPrompt;
    private static MTexture neutralTouchPrompt;
    private static bool initialized;
    private static bool editing;
    private static bool logicalVisible;
    private static bool controllerConnected;
    private static float visualVisibility;
    private static int recoveryOwner = -1;
    private static long pendingTouchTick;
    private static long latencySamples;
    private static double logicalLatencyTotal;
    private static double updateLatencyTotal;
    private static double logicalLatencyMaximum;
    private static double updateLatencyMaximum;

    internal static bool Grab => state != null && logicalVisible && state.Grab;
    internal static bool GrabActionPressed => state != null && logicalVisible && state.GrabActionPressed;
    internal static bool LogicalVisible => initialized && logicalVisible;
    internal static bool PhysicalControllerConnected => initialized && controllerConnected;
    internal static int MoveX => state != null && logicalVisible ? state.MoveX : 0;
    internal static int MoveY => state != null && logicalVisible ? state.MoveY : 0;
    internal static bool TouchPromptActive => initialized && (logicalVisible || NeedsRecovery);
    internal static bool NeedsRecovery => initialized &&
        TouchVisibilityPolicy.NeedsRecovery(preferences.Visibility, controllerConnected, editing);

    public static void Bind()
    {
        BindButton(Input.Jump, TouchAction.Jump);
        BindButton(Input.Dash, TouchAction.Dash);
        BindButton(Input.Talk, TouchAction.Jump);
        BindButton(Input.Pause, TouchAction.Pause);
        BindButton(Input.MenuConfirm, TouchAction.Jump);
        BindButton(Input.MenuCancel, TouchAction.Dash);
        BindButton(Input.MenuLeft, TouchAction.Left);
        BindButton(Input.MenuRight, TouchAction.Right);
        BindButton(Input.MenuUp, TouchAction.Up);
        BindButton(Input.MenuDown, TouchAction.Down);
        BindButton(Input.MenuJournal, TouchAction.Journal);
        BindAxis(Input.MoveX, true);
        BindAxis(Input.MoveY, false);
        BindAxis(Input.GliderMoveY, false);
        BindJoystick(Input.Aim);
        BindJoystick(Input.Feather);
        BindJoystick(Input.MountainAim);
    }

    public static void Update()
    {
        EnsureInitialized();
        if (!initialized || !IOSPresentationCoordinator.Ensure()) return;
        IOSPresentationCoordinator.IOSPresentation current = IOSPresentationCoordinator.Current;
        if (current != presentation)
        {
            presentation = current;
            ApplyLayout();
        }

        bool connected = GamePad.GetState(PlayerIndex.One).IsConnected;
        if (connected != controllerConnected)
        {
            controllerConnected = connected;
            Reset(connected ? "controller-connected" : "controller-disconnected");
        }

        bool nextLogicalVisible = TouchVisibilityPolicy.IsVisible(preferences.Visibility, controllerConnected) ||
            (editing && preferences.Visibility == TouchControlVisibility.Off && !controllerConnected);
        if (nextLogicalVisible != logicalVisible)
        {
            state.Reset();
            recoveryOwner = -1;
            logicalVisible = nextLogicalVisible;
        }
        float target = logicalVisible ? 1f : 0f;
        visualVisibility = Calc.Approach(visualVisibility, target, Engine.RawDeltaTime / FadeSeconds);

        state.BeginFrame();
        TouchCollection touches = TouchPanel.GetState();
        Span<int> activeIds = stackalloc int[TouchInteractionState.MaximumTouches];
        int activeCount = 0;
        for (int index = 0; index < touches.Count; index++)
        {
            TouchLocation touch = touches[index];
            TouchPhase phase = touch.State switch
            {
                TouchLocationState.Pressed => TouchPhase.Pressed,
                TouchLocationState.Released => TouchPhase.Released,
                _ => TouchPhase.Moved,
            };
            if (phase != TouchPhase.Released && activeCount < activeIds.Length)
                activeIds[activeCount++] = touch.Id;
            TouchPoint point = new(
                touch.Position.X / presentation.NativeScale,
                touch.Position.Y / presentation.NativeScale);

            if (NeedsRecovery)
            {
                if (phase == TouchPhase.Pressed && RecoveryRect().Contains(point))
                {
                    recoveryOwner = touch.Id;
                    SetVisibility((int)TouchControlVisibility.Automatic);
                }
                if (phase == TouchPhase.Released && recoveryOwner == touch.Id) recoveryOwner = -1;
                continue;
            }
            if (!logicalVisible) continue;
            if (phase == TouchPhase.Pressed) pendingTouchTick = Stopwatch.GetTimestamp();
            TouchHapticAction haptic = state.Apply(touch.Id, phase, point, preferences.Haptics);
            if (haptic != TouchHapticAction.None)
            {
                feedback?.ImpactOccurred();
                feedback?.Prepare();
            }
        }
        state.ReleaseMissingOwners(activeIds[..activeCount]);
        if (recoveryOwner >= 0 && !activeIds[..activeCount].Contains(recoveryOwner)) recoveryOwner = -1;
        if (pendingTouchTick != 0)
        {
            double elapsed = ElapsedMilliseconds(pendingTouchTick);
            logicalLatencyTotal += elapsed;
            logicalLatencyMaximum = Math.Max(logicalLatencyMaximum, elapsed);
        }
    }

    public static void MarkGameUpdate()
    {
        if (pendingTouchTick == 0) return;
        double elapsed = ElapsedMilliseconds(pendingTouchTick);
        updateLatencyTotal += elapsed;
        updateLatencyMaximum = Math.Max(updateLatencyMaximum, elapsed);
        latencySamples++;
        pendingTouchTick = 0;
        if (latencySamples % 100 == 0)
        {
            Console.WriteLine(
                $"iOS touch latency samples={latencySamples}; logical-mean-ms={logicalLatencyTotal / latencySamples:F3}; " +
                $"logical-max-ms={logicalLatencyMaximum:F3}; update-mean-ms={updateLatencyTotal / latencySamples:F3}; " +
                $"update-max-ms={updateLatencyMaximum:F3}; buffered-frames=0");
        }
    }

    public static void Render()
    {
        EnsureInitialized();
        if (!initialized || !IOSPresentationCoordinator.Ensure() || Celeste.Instance?.GraphicsDevice == null || Draw.SpriteBatch == null)
            return;
        EnsureTextures();
        GraphicsDevice device = Celeste.Instance.GraphicsDevice;
        Viewport previousViewport = device.Viewport;
        device.Viewport = new Viewport(0, 0, presentation.PixelWidth, presentation.PixelHeight);
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone);

        if (NeedsRecovery)
            DrawRecovery();
        if (visualVisibility > 0f)
        {
            float configured = preferences.OpacityPercent / 100f;
            if (editing) configured = Math.Max(configured, 0.25f);
            float alpha = configured * visualVisibility;
            DrawMovement(alpha);
            DrawAction(layout.Jump, jumpTexture, state.Jump, new Color(124, 219, 255), alpha);
            DrawAction(layout.Dash, dashTexture, state.Dash, new Color(255, 104, 146), alpha);
            if (state.GrabStyle == TouchGrabStyle.ShoulderHold) DrawShoulder(alpha);
            else DrawGrab(alpha);
            DrawPause(alpha);
            DrawJournal(alpha);
        }
        Draw.SpriteBatch.End();
        device.Viewport = previousViewport;
    }

    public static void Reset(string reason)
    {
        state?.Reset();
        recoveryOwner = -1;
        pendingTouchTick = 0;
    }

    public static void Dispose()
    {
        Reset("dispose");
        feedback?.Dispose();
        feedback = null;
        initialized = false;
    }

    public static void SetOptionsEditing(bool value)
    {
        editing = value;
        if (!value && preferences.Visibility == TouchControlVisibility.Off)
        {
            state?.Reset();
            logicalVisible = false;
        }
    }

    public static int VisibilityIndex { get { EnsureInitialized(); return (int)preferences.Visibility; } }
    public static int MovementIndex { get { EnsureInitialized(); return (int)preferences.Movement; } }
    public static int GrabIndex { get { EnsureInitialized(); return (int)preferences.Grab; } }
    public static bool SlidingEnabled { get { EnsureInitialized(); return preferences.Sliding == TouchSlideMode.JumpDash; } }
    public static int OpacityIndex { get { EnsureInitialized(); return preferences.OpacityPercent / 10; } }
    public static int SizeIndex { get { EnsureInitialized(); return (preferences.SizePercent - 70) / 10; } }
    public static bool HapticsEnabled { get { EnsureInitialized(); return preferences.Haptics; } }
    public static int PromptModeIndex { get { EnsureInitialized(); return (int)promptMode; } }

    public static string VisibilityName(int value) => (TouchControlVisibility)value switch
    {
        TouchControlVisibility.Always => "Always",
        TouchControlVisibility.Off => "Off",
        _ => "Automatic",
    };
    public static string MovementName(int value) => (TouchMovementMode)value == TouchMovementMode.Floating ? "Floating" : "Fixed";
    public static string GrabName(int value) => (TouchGrabStyle)value switch
    {
        TouchGrabStyle.HoldButton => "Hold Button",
        TouchGrabStyle.ShoulderHold => "Shoulder Hold",
        _ => "Toggle",
    };
    public static string PercentFromZero(int value) => $"{value * 10}%";
    public static string SizeName(int value) => $"{70 + value * 10}%";
    public static string PromptModeName(int value) => AppleControllerPromptPolicy.DisplayName((ControllerPromptMode)value);

    public static void SetVisibility(int value)
    {
        TouchControlVisibility mode = value is >= 0 and <= 2
            ? (TouchControlVisibility)value : TouchPreferences.Default.Visibility;
        UpdatePreferences(preferences with { Visibility = mode }, TouchPreferencePolicy.VisibilityKey, TouchPreferencePolicy.Store(mode));
    }
    public static void SetMovement(int value)
    {
        TouchMovementMode mode = value is >= 0 and <= 1
            ? (TouchMovementMode)value : TouchPreferences.Default.Movement;
        UpdatePreferences(preferences with { Movement = mode }, TouchPreferencePolicy.MovementKey, TouchPreferencePolicy.Store(mode));
    }
    public static void SetGrab(int value)
    {
        TouchGrabStyle mode = value is >= 0 and <= 2
            ? (TouchGrabStyle)value : TouchPreferences.Default.Grab;
        UpdatePreferences(preferences with { Grab = mode }, TouchPreferencePolicy.GrabKey, TouchPreferencePolicy.Store(mode));
    }
    public static void SetSliding(bool enabled) => UpdatePreferences(
        preferences with { Sliding = enabled ? TouchSlideMode.JumpDash : TouchSlideMode.Off },
        TouchPreferencePolicy.SlidingKey,
        TouchPreferencePolicy.Store(enabled ? TouchSlideMode.JumpDash : TouchSlideMode.Off));
    public static void SetOpacity(int index)
    {
        int percent = Math.Clamp(index, 0, 10) * 10;
        UpdatePreferences(preferences with { OpacityPercent = percent }, TouchPreferencePolicy.OpacityKey, TouchPreferencePolicy.StorePercent(percent));
    }
    public static void SetSize(int index)
    {
        int percent = 70 + Math.Clamp(index, 0, 6) * 10;
        UpdatePreferences(preferences with { SizePercent = percent }, TouchPreferencePolicy.SizeKey, TouchPreferencePolicy.StorePercent(percent));
    }
    public static void SetHaptics(bool enabled) => UpdatePreferences(
        preferences with { Haptics = enabled }, TouchPreferencePolicy.HapticsKey, TouchPreferencePolicy.StoreHaptics(enabled));

    public static void SetPromptMode(int value)
    {
        ControllerPromptMode next = value is >= 0 and <= 4
            ? (ControllerPromptMode)value : ControllerPromptMode.Automatic;
        if (next == promptMode) return;
        promptMode = next;
        Defaults.SetString(AppleControllerPromptPolicy.StoredValue(next), PromptPreferenceKey);
        Defaults.Synchronize();
    }

    public static void ResetPreferences()
    {
        TouchPreferences defaults = TouchPreferences.Default;
        preferences = defaults;
        Defaults.SetString(TouchPreferencePolicy.Store(defaults.Visibility), TouchPreferencePolicy.VisibilityKey);
        Defaults.SetString(TouchPreferencePolicy.Store(defaults.Movement), TouchPreferencePolicy.MovementKey);
        Defaults.SetString(TouchPreferencePolicy.Store(defaults.Grab), TouchPreferencePolicy.GrabKey);
        Defaults.SetString(TouchPreferencePolicy.Store(defaults.Sliding), TouchPreferencePolicy.SlidingKey);
        Defaults.SetString(TouchPreferencePolicy.StorePercent(defaults.OpacityPercent), TouchPreferencePolicy.OpacityKey);
        Defaults.SetString(TouchPreferencePolicy.StorePercent(defaults.SizePercent), TouchPreferencePolicy.SizeKey);
        Defaults.SetString(TouchPreferencePolicy.StoreHaptics(defaults.Haptics), TouchPreferencePolicy.HapticsKey);
        Defaults.Synchronize();
        ApplyPreferences();
    }

    internal static string ResolvePromptPrefix(string automaticPrefix)
    {
        if (TouchPromptActive) return "keyboard";
        return AppleControllerPromptPolicy.ResolvePrefix(promptMode, automaticPrefix, CurrentControllerFamily());
    }

    internal static MTexture TouchPrompt(VirtualButton button)
    {
        if (!TouchPromptActive || Celeste.Instance?.GraphicsDevice == null) return null;
        EnsureTextures();
        if (ReferenceEquals(button, Input.MenuConfirm) || ReferenceEquals(button, Input.Jump) ||
            ReferenceEquals(button, Input.Talk)) return jumpPrompt;
        if (ReferenceEquals(button, Input.MenuCancel) || ReferenceEquals(button, Input.Dash) ||
            ReferenceEquals(button, Input.CrouchDash)) return dashPrompt;
        if (ReferenceEquals(button, Input.Grab)) return grabPrompt;
        if (ReferenceEquals(button, Input.Pause)) return pausePrompt;
        if (ReferenceEquals(button, Input.MenuJournal)) return journalPrompt;
        return neutralTouchPrompt;
    }

    internal static MTexture TouchPrompt(Buttons button)
    {
        if (!TouchPromptActive || Celeste.Instance?.GraphicsDevice == null) return null;
        EnsureTextures();
        return neutralTouchPrompt;
    }

    private static void EnsureInitialized()
    {
        if (initialized) return;
        if (!IOSPresentationCoordinator.Ensure()) return;
        presentation = IOSPresentationCoordinator.Current;
        preferences = LoadPreferences();
        promptMode = AppleControllerPromptPolicy.ParseStored(Defaults.StringForKey(PromptPreferenceKey));
        layout = TouchControlLayout.Create(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea,
            presentation.IsPad, preferences.SizePercent / 100.0);
        state = new TouchInteractionState(layout);
        ApplyPreferences();
        controllerConnected = GamePad.GetState(PlayerIndex.One).IsConnected;
        logicalVisible = TouchVisibilityPolicy.IsVisible(preferences.Visibility, controllerConnected);
        visualVisibility = logicalVisible ? 1f : 0f;
        feedback = new UIImpactFeedbackGenerator(UIImpactFeedbackStyle.Light);
        feedback.Prepare();
        initialized = true;
    }

    private static TouchPreferences LoadPreferences() => new TouchPreferences(
        TouchPreferencePolicy.ParseVisibility(Defaults.StringForKey(TouchPreferencePolicy.VisibilityKey)),
        TouchPreferencePolicy.ParseMovement(Defaults.StringForKey(TouchPreferencePolicy.MovementKey)),
        TouchPreferencePolicy.ParseGrab(Defaults.StringForKey(TouchPreferencePolicy.GrabKey)),
        TouchPreferencePolicy.ParseSliding(Defaults.StringForKey(TouchPreferencePolicy.SlidingKey)),
        TouchPreferencePolicy.ParseOpacity(Defaults.StringForKey(TouchPreferencePolicy.OpacityKey)),
        TouchPreferencePolicy.ParseSize(Defaults.StringForKey(TouchPreferencePolicy.SizeKey)),
        TouchPreferencePolicy.ParseHaptics(Defaults.StringForKey(TouchPreferencePolicy.HapticsKey))).Validated();

    private static void UpdatePreferences(TouchPreferences next, string key, string stored)
    {
        next = next.Validated();
        if (next == preferences) return;
        preferences = next;
        Defaults.SetString(stored, key);
        Defaults.Synchronize();
        ApplyPreferences();
    }

    private static void ApplyPreferences()
    {
        if (state == null) return;
        state.MovementMode = preferences.Movement;
        state.GrabStyle = preferences.Grab;
        state.SlideMode = preferences.Sliding;
        if (presentation.IsValid) ApplyLayout();
        else state.Reset();
    }

    private static void ApplyLayout()
    {
        layout = TouchControlLayout.Create(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea,
            presentation.IsPad, preferences.SizePercent / 100.0);
        state?.UpdateLayout(layout);
        if (state != null)
        {
            state.MovementMode = preferences.Movement;
            state.GrabStyle = preferences.Grab;
            state.SlideMode = preferences.Sliding;
        }
    }

    private static AppleControllerFamily CurrentControllerFamily()
    {
        GCController? current = GCController.Current;
        GCController[] connected = GCController.Controllers ?? Array.Empty<GCController>();
        List<ControllerCandidate> candidates = new(connected.Length + 1);
        for (int index = 0; index < connected.Length; index++)
        {
            GCController controller = connected[index];
            candidates.Add(new ControllerCandidate(
                current != null && controller.Handle == current.Handle,
                controller.ExtendedGamepad != null,
                Classify(controller.ProductCategory),
                index));
        }
        if (current != null && !Array.Exists(connected, controller => controller.Handle == current.Handle))
        {
            candidates.Add(new ControllerCandidate(
                true,
                current.ExtendedGamepad != null,
                Classify(current.ProductCategory),
                connected.Length));
        }
        return AppleControllerPromptPolicy.SelectAppleFamily(candidates);
    }

    private static AppleControllerFamily Classify(string? category)
    {
        if (EqualsCategory(category, GCProductCategory.DualSense) || EqualsCategory(category, GCProductCategory.DualShock4))
            return AppleControllerFamily.PlayStation;
        if (EqualsCategory(category, GCProductCategory.XboxOne)) return AppleControllerFamily.Xbox;
        if (EqualsCategory(category, GCProductCategory.SiriRemote1stGen) ||
            EqualsCategory(category, GCProductCategory.SiriRemote2ndGen) ||
            EqualsCategory(category, GCProductCategory.ControlCenterRemote) ||
            EqualsCategory(category, GCProductCategory.UniversalElectronicsRemote) ||
            EqualsCategory(category, GCProductCategory.CoalescedRemote))
            return AppleControllerFamily.Remote;
        return AppleControllerFamily.Unknown;
    }

    private static bool EqualsCategory(string? value, NSString category) =>
        string.Equals(value, category.ToString(), StringComparison.Ordinal);

    private static void BindButton(VirtualButton button, TouchAction action)
    {
        button.AdditionalCheck = () => Check(action);
        button.AdditionalPressed = () => Pressed(action);
        button.AdditionalReleased = () => Released(action);
    }
    private static void BindAxis(VirtualIntegerAxis axis, bool horizontal) =>
        axis.AdditionalValue = () => horizontal ? MoveX : MoveY;
    private static void BindJoystick(VirtualJoystick joystick) =>
        joystick.AdditionalValue = () => new Vector2(MoveX, MoveY);

    private static bool Check(TouchAction action)
    {
        if (!logicalVisible || state == null) return false;
        return action switch
        {
            TouchAction.Jump => state.Jump,
            TouchAction.Dash => state.Dash,
            TouchAction.Pause => state.Pause,
            TouchAction.Journal => state.Journal,
            TouchAction.Left => state.MoveX < 0,
            TouchAction.Right => state.MoveX > 0,
            TouchAction.Up => state.MoveY < 0,
            TouchAction.Down => state.MoveY > 0,
            _ => false,
        };
    }
    private static bool Pressed(TouchAction action)
    {
        if (!logicalVisible || state == null) return false;
        return action switch
        {
            TouchAction.Jump => state.JumpPressed,
            TouchAction.Dash => state.DashPressed,
            TouchAction.Pause => state.PausePressed,
            TouchAction.Journal => state.JournalPressed,
            TouchAction.Left => state.LeftPressed,
            TouchAction.Right => state.RightPressed,
            TouchAction.Up => state.UpPressed,
            TouchAction.Down => state.DownPressed,
            _ => false,
        };
    }
    private static bool Released(TouchAction action)
    {
        if (state == null) return false;
        return action switch
        {
            TouchAction.Jump => state.JumpReleased,
            TouchAction.Dash => state.DashReleased,
            TouchAction.Pause => state.PauseReleased,
            TouchAction.Journal => state.JournalReleased,
            TouchAction.Left => state.LeftReleased,
            TouchAction.Right => state.RightReleased,
            TouchAction.Up => state.UpReleased,
            TouchAction.Down => state.DownReleased,
            _ => false,
        };
    }

    private static void EnsureTextures()
    {
        if (circleTexture != null) return;
        GraphicsDevice device = Celeste.Instance.GraphicsDevice;
        circleTexture = new Texture2D(device, TextureSize, TextureSize, false, SurfaceFormat.Color);
        Color[] circle = new Color[TextureSize * TextureSize];
        double center = (TextureSize - 1) * 0.5;
        double radius = center - 1;
        for (int y = 0; y < TextureSize; y++)
        for (int x = 0; x < TextureSize; x++)
        {
            double distance = Math.Sqrt((x - center) * (x - center) + (y - center) * (y - center));
            byte alpha = (byte)Math.Clamp((radius + 1 - distance) * 255, 0, 255);
            circle[y * TextureSize + x] = new Color(alpha, alpha, alpha, alpha);
        }
        circleTexture.SetData(circle);
        (jumpTexture, jumpPrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.jump.a8", "iOS Touch Jump");
        (dashTexture, dashPrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.dash.a8", "iOS Touch Dash");
        (grabUngrabbedTexture, grabPrompt) = LoadAlphaTexture(
            "Celeste.IOSTouchControls.grab-ungrabbed.a8", "iOS Touch Grab");
        (grabGrabbedTexture, _) = LoadAlphaTexture(
            "Celeste.IOSTouchControls.grab-grabbed.a8", "iOS Touch Grab Active");
        (pauseTexture, pausePrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.pause.a8", "iOS Touch Pause");
        (journalTexture, journalPrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.journal.a8", "iOS Touch Journal");
        (neutralTouchTexture, neutralTouchPrompt) = LoadAlphaTexture("Celeste.IOSTouchControls.touch.a8", "iOS Touch Prompt");
    }

    private static (Texture2D Texture, MTexture Prompt) LoadAlphaTexture(string resource, string name)
    {
        byte[] alpha = new byte[TextureSize * TextureSize];
        using Stream stream = typeof(IOSTouchControls).Assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidDataException($"Touch alpha resource is missing: {resource}");
        int offset = 0;
        while (offset < alpha.Length)
        {
            int read = stream.Read(alpha, offset, alpha.Length - offset);
            if (read <= 0) throw new InvalidDataException($"Touch alpha resource is truncated: {resource}");
            offset += read;
        }
        if (stream.ReadByte() >= 0) throw new InvalidDataException($"Touch alpha resource is oversized: {resource}");
        Color[] overlayColors = new Color[alpha.Length];
        for (int index = 0; index < alpha.Length; index++)
            overlayColors[index] = new Color(alpha[index], alpha[index], alpha[index], alpha[index]);
        VirtualTexture overlayTexture = VirtualContent.CreateTexture(name, TextureSize, TextureSize, Color.Transparent);
        overlayTexture.Texture.SetData(overlayColors);

        Color[] promptColors = new Color[PromptSize * PromptSize];
        for (int y = 0; y < PromptSize; y++)
        for (int x = 0; x < PromptSize; x++)
        {
            int sourceX = Math.Min(TextureSize - 1, ((x * 2 + 1) * TextureSize) / (PromptSize * 2));
            int sourceY = Math.Min(TextureSize - 1, ((y * 2 + 1) * TextureSize) / (PromptSize * 2));
            byte value = alpha[sourceY * TextureSize + sourceX];
            promptColors[y * PromptSize + x] = new Color(value, value, value, value);
        }
        VirtualTexture promptTexture = VirtualContent.CreateTexture(name + " Prompt", PromptSize, PromptSize, Color.Transparent);
        promptTexture.Texture.SetData(promptColors);
        return (overlayTexture.Texture, new MTexture(promptTexture));
    }

    private static void DrawMovement(float alpha)
    {
        TouchPoint center = state.MovementMode == TouchMovementMode.Floating && state.Direction != TouchDirection.Neutral
            ? state.MovementCenter : layout.Movement.Center;
        float radius = Pixels(layout.Movement.Radius);
        Color background = state.Direction == TouchDirection.Neutral
            ? new Color(20, 28, 48) * (0.44f * alpha)
            : new Color(91, 186, 255) * (0.58f * alpha);
        Draw.SpriteBatch.Draw(circleTexture, CircleDestination(center, radius), background);
        for (int index = 0; index < 8; index++)
        {
            double angle = index * Math.PI / 4.0;
            Vector2 start = PixelPoint(center);
            Vector2 end = start + new Vector2((float)Math.Cos(angle), (float)-Math.Sin(angle)) * radius * 0.72f;
            Draw.Line(start, end, Color.White * (0.42f * alpha), Math.Max(2, Pixels(1.5)));
        }
        Draw.SpriteBatch.Draw(circleTexture, CircleDestination(center, radius * 0.20f), Color.White * (0.65f * alpha));
    }

    private static void DrawAction(TouchCircle circle, Texture2D icon, bool pressed, Color color, float alpha)
    {
        float radius = Pixels(circle.Radius);
        Draw.SpriteBatch.Draw(circleTexture, CircleDestination(circle.Center, radius),
            (pressed ? color : new Color(18, 24, 42)) * ((pressed ? 0.82f : 0.58f) * alpha));
        Draw.SpriteBatch.Draw(icon, CircleDestination(circle.Center, radius * 0.58f), Color.White * (0.90f * alpha));
    }

    private static void DrawGrab(float alpha)
    {
        float radius = Pixels(layout.Grab.Radius);
        Draw.SpriteBatch.Draw(circleTexture, CircleDestination(layout.Grab.Center, radius),
            (state.Grab ? new Color(255, 188, 91) : new Color(18, 24, 42)) * ((state.Grab ? 0.82f : 0.58f) * alpha));
        Texture2D icon = state.Grab ? grabGrabbedTexture : grabUngrabbedTexture;
        Draw.SpriteBatch.Draw(icon, CircleDestination(layout.Grab.Center, radius * 0.58f), Color.White * (0.90f * alpha));
    }

    private static void DrawShoulder(float alpha)
    {
        Rectangle rect = PixelRect(layout.Shoulder);
        Draw.Rect(rect, (state.Grab ? new Color(255, 188, 91) : new Color(18, 24, 42)) * (0.52f * alpha));
        Draw.TextCentered(Draw.DefaultFont, "GRAB", new Vector2(rect.Center.X, rect.Center.Y),
            Color.White * (0.80f * alpha), (float)(0.45 * presentation.NativeScale));
    }

    private static void DrawPause(float alpha)
    {
        Rectangle rect = PixelRect(layout.Pause);
        Draw.Rect(rect, new Color(18, 24, 42) * (0.58f * alpha));
        int side = (int)(Math.Min(rect.Width, rect.Height) * 0.72f);
        Draw.SpriteBatch.Draw(pauseTexture,
            new Rectangle(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side),
            Color.White * alpha);
    }

    private static void DrawJournal(float alpha)
    {
        Rectangle rect = PixelRect(layout.Journal);
        Draw.Rect(rect, new Color(18, 24, 42) * (0.52f * alpha));
        int side = (int)(Math.Min(rect.Width, rect.Height) * 0.70f);
        Draw.SpriteBatch.Draw(journalTexture,
            new Rectangle(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side),
            Color.White * (0.92f * alpha));
    }

    private static void DrawRecovery()
    {
        Rectangle rect = PixelRect(RecoveryRect());
        Draw.Rect(rect, new Color(10, 18, 34) * 0.88f);
        Draw.HollowRect(rect, Color.White * 0.78f);
        Draw.TextCentered(Draw.DefaultFont, "ENABLE TOUCH CONTROLS", new Vector2(rect.Center.X, rect.Center.Y),
            Color.White, (float)(0.42 * presentation.NativeScale));
    }

    private static TouchRect RecoveryRect()
    {
        double width = Math.Min(300, presentation.PointWidth - presentation.SafeArea.Left - presentation.SafeArea.Right - 32);
        double height = 54;
        return new TouchRect(
            presentation.PointWidth * 0.5 - width * 0.5,
            presentation.PointHeight - presentation.SafeArea.Bottom - height - 18,
            width,
            height);
    }

    private static Rectangle CircleDestination(TouchPoint center, float radius)
    {
        Vector2 pixel = PixelPoint(center);
        int diameter = (int)Math.Round(radius * 2);
        return new Rectangle((int)Math.Round(pixel.X - radius), (int)Math.Round(pixel.Y - radius), diameter, diameter);
    }
    private static Rectangle PixelRect(TouchRect rect) => new(
        (int)Math.Round(rect.X * presentation.NativeScale),
        (int)Math.Round(rect.Y * presentation.NativeScale),
        (int)Math.Round(rect.Width * presentation.NativeScale),
        (int)Math.Round(rect.Height * presentation.NativeScale));
    private static Vector2 PixelPoint(TouchPoint point) => new(
        (float)(point.X * presentation.NativeScale),
        (float)(point.Y * presentation.NativeScale));
    private static float Pixels(double points) => (float)(points * presentation.NativeScale);
    private static double ElapsedMilliseconds(long start) =>
        (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;

    private enum TouchAction { Jump, Dash, Pause, Journal, Left, Right, Up, Down }
}
