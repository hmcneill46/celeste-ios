using System;
using System.Collections.Generic;
using System.Linq;
using CelesteIOSFoundation;
using Foundation;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Monocle;

namespace Celeste;

public static partial class IOSTouchControls
{
    private const string PhoneLayoutKey = "CelesteIOS.TouchControls.Layout.Phone.v2";
    private const string TabletLayoutKey = "CelesteIOS.TouchControls.Layout.Tablet.v2";
    private const string LegacyPhoneLayoutKey = "CelesteIOS.TouchControls.Layout.Phone.v1";
    private const string LegacyTabletLayoutKey = "CelesteIOS.TouchControls.Layout.Tablet.v1";
    private const string D3MigrationKey = "CelesteIOS.TouchControls.D3Migrated.v1";
    private const string TouchGrabModeKey = "CelesteIOS.GrabMode.Touch.v1";
    private const string ControllerGrabModeKey = "CelesteIOS.GrabMode.Controller.v1";
    private const string KeyboardGrabModeKey = "CelesteIOS.GrabMode.Keyboard.v1";
    private const string DirectionalHapticsKey = "CelesteIOS.TouchControls.DirectionalHaptics.v1";
    private static TouchLayoutProfile layoutProfile;
    private static TouchLayoutProfile factoryProfile;
    private static GrabSourceArbiter grabArbiter;
    private static bool directionalHaptics;
    private static TouchLayoutEditorSession layoutEditor;
    private static Action layoutEditorClosed;
    private static TouchLayoutControl selectedControl;
    private static int selectedExtraIndex = -1;
    private static int editorFinger = -1;
    private static TouchPoint editorStartPoint;
    private static TouchRect editorStartRect;
    private static bool editorResize;
    private static int editorCommandOwner = -1;
    private static bool editorToolsVisible = true;
    private static bool editorToolsAtBottom;
    private static bool editorAddPalette;
    private static Texture2D splitHalfTexture;

    internal static bool D3EditorActive => layoutEditor != null;
    internal static bool D3EffectiveGrab => grabArbiter?.Effective ?? false;
    internal static bool D3GameplayGrab => grabArbiter != null &&
        GrabSourceVisibilityPolicy.GameplayValue(
            grabArbiter.ActiveSource, logicalVisible, grabArbiter.Effective);
    internal static AppleGrabMode ActiveGrabMode => grabArbiter?.ActiveMode ?? AppleGrabMode.Hold;
    internal static AppleInputSource ActiveGrabSource => grabArbiter?.ActiveSource ?? AppleInputSource.Touch;
    internal static int ActiveGrabModeIndex => (int)ActiveGrabMode;
    internal static string ActiveGrabSourceName => ActiveGrabSource switch
    {
        AppleInputSource.Controller => "Controller",
        AppleInputSource.Keyboard => "Keyboard",
        _ => "Touch",
    };

    public static int ActionLayoutIndex { get { EnsureInitialized(); return (int)layoutProfile.ActionLayout; } }
    public static bool D3SlidingEnabled { get { EnsureInitialized(); return layoutProfile.Sliding == TouchSlideMode.JumpDash; } }
    public static bool DirectionalHapticsEnabled { get { EnsureInitialized(); return directionalHaptics; } }

    public static string ActionLayoutName(int value) => (TouchActionLayout)value == TouchActionLayout.SplitRegion
        ? "Split Region" : "Separate Buttons";

    public static void SetActionLayout(int value)
    {
        EnsureInitialized();
        TouchActionLayout mode = value == 1 ? TouchActionLayout.SplitRegion : TouchActionLayout.SeparateButtons;
        SetProfileOption(layoutProfile with { ActionLayout = mode }, mode == TouchActionLayout.SplitRegion
            ? TouchLayoutControl.SplitRegion : TouchLayoutControl.Jump);
    }

    public static void SetDirectionalHaptics(bool enabled)
    {
        EnsureInitialized();
        if (enabled == directionalHaptics) return;
        directionalHaptics = enabled;
        Defaults.SetString(enabled ? "On" : "Off", DirectionalHapticsKey);
        Defaults.Synchronize();
    }

    public static void BeginLayoutEditor(Action closed)
    {
        EnsureInitialized();
        if (layoutEditor != null) return;
        BeginLayoutEditor(layoutProfile, closed);
    }

    public static byte[] ExportLayoutDocument()
    {
        EnsureInitialized();
        SaveLayout();
        Defaults.Synchronize();
        string phone = presentation.IsPad ? Defaults.StringForKey(PhoneLayoutKey) : TouchLayoutCodec.Encode(layoutProfile);
        string tablet = presentation.IsPad ? TouchLayoutCodec.Encode(layoutProfile) : Defaults.StringForKey(TabletLayoutKey);
        if (!TouchLayoutCodec.TryDecode(phone, out _)) phone = null;
        if (!TouchLayoutCodec.TryDecode(tablet, out _)) tablet = null;
        return new TouchLayoutShareDocument(phone, tablet).Encode();
    }

    public static bool BeginImportedLayoutPreview(byte[] payload, Action closed, out string error)
    {
        EnsureInitialized();
        error = "This touch layout isn't valid.";
        if (layoutEditor != null || !TouchLayoutShareDocument.TryDecode(payload, out TouchLayoutShareDocument document))
            return false;
        string encoded = presentation.IsPad ? document.TabletProfile : document.PhoneProfile;
        if (encoded is null)
        {
            error = presentation.IsPad
                ? "This file doesn't contain a Tablet layout."
                : "This file doesn't contain a Phone layout.";
            return false;
        }
        if (!TouchLayoutCodec.TryDecode(encoded, out TouchLayoutProfile imported) ||
            !TouchLayoutPolicy.Validate(imported, UsableWidth(), UsableHeight()).IsValid)
            return false;
        BeginLayoutEditor(imported, closed);
        error = null;
        return true;
    }

    private static void BeginLayoutEditor(TouchLayoutProfile working, Action closed)
    {
        state?.Reset();
        grabArbiter?.ResetTransient();
        layoutEditor = new TouchLayoutEditorSession(layoutProfile, factoryProfile);
        if (working != layoutProfile) layoutEditor.Change(working);
        layoutEditorClosed = closed;
        selectedControl = working.MovementMode == TouchMovementMode.Fixed
            ? TouchLayoutControl.Movement : TouchLayoutControl.FloatingRegion;
        selectedExtraIndex = -1;
        editorFinger = editorCommandOwner = -1;
        editorToolsVisible = true;
        editorToolsAtBottom = false;
        editorAddPalette = false;
    }

    public static void ResetD3Preferences()
    {
        EnsureInitialized();
        TouchPreferences defaults = TouchPreferences.Default;
        preferences = preferences with
        {
            Visibility = defaults.Visibility,
            OpacityPercent = defaults.OpacityPercent,
            Haptics = defaults.Haptics,
        };
        directionalHaptics = false;
        layoutProfile = factoryProfile;
        Defaults.SetString(TouchPreferencePolicy.Store(defaults.Visibility), TouchPreferencePolicy.VisibilityKey);
        Defaults.SetString(TouchPreferencePolicy.StorePercent(defaults.OpacityPercent), TouchPreferencePolicy.OpacityKey);
        Defaults.SetString(TouchPreferencePolicy.StoreHaptics(defaults.Haptics), TouchPreferencePolicy.HapticsKey);
        Defaults.SetString("Off", DirectionalHapticsKey);
        SaveLayout();
        Defaults.Synchronize();
        ApplyPreferences();
    }

    public static void SetActiveGrabMode(int value)
    {
        EnsureInitialized();
        AppleGrabMode mode = value is >= 0 and <= 2 ? (AppleGrabMode)value : AppleGrabMode.Hold;
        AppleInputSource source = ActiveGrabSource;
        if (grabArbiter.Profiles.For(source) == mode) return;
        grabArbiter.SetMode(source, mode);
        Defaults.SetString(StoreGrabMode(mode), GrabModeKey(source));
        Defaults.Synchronize();
        state.GrabMode = grabArbiter.ActiveMode;
    }

    internal static void UpdateHardwareSources()
    {
        if (!initialized || grabArbiter == null || state == null) return;
        bool keyboardRaw = false;
        bool keyboardPressed = false;
        foreach (Keys key in Input.Grab.Binding.Keyboard)
        {
            keyboardRaw |= MInput.Keyboard.Check(key);
            keyboardPressed |= MInput.Keyboard.Pressed(key);
        }
        bool keyboardMeaningful = KeyboardHasNewPress();

        MInput.GamePadData pad = MInput.GamePads[Input.Gamepad];
        bool controllerRaw = false;
        bool controllerPressed = false;
        foreach (Buttons button in Input.Grab.Binding.Controller)
        {
            controllerRaw |= pad.Check(button, 0.2f);
            controllerPressed |= pad.Pressed(button, 0.2f);
        }
        GamePadState current = pad.CurrentState;
        GamePadState previous = pad.PreviousState;
        bool controllerMeaningful = current.IsConnected &&
            (current.Buttons != previous.Buttons || current.DPad != previous.DPad ||
             Crossed(current.Triggers.Left, previous.Triggers.Left, 0.2f) ||
             Crossed(current.Triggers.Right, previous.Triggers.Right, 0.2f) ||
             Crossed(current.ThumbSticks.Left.Length(), previous.ThumbSticks.Left.Length(), 0.25f) ||
             Crossed(current.ThumbSticks.Right.Length(), previous.ThumbSticks.Right.Length(), 0.25f));

        grabArbiter.Observe(AppleInputSource.Keyboard, keyboardRaw, keyboardPressed, keyboardMeaningful);
        grabArbiter.Observe(AppleInputSource.Controller, controllerRaw, controllerPressed, controllerMeaningful);
        grabArbiter.Observe(AppleInputSource.Touch, state.Grab, state.GrabActionPressed, state.MeaningfulPressed);
        state.GrabMode = grabArbiter.ActiveMode;
    }

    internal static void ResetGrabArbiter()
    {
        grabArbiter?.ResetTransient();
        if (state != null) state.GrabMode = grabArbiter?.ActiveMode ?? AppleGrabMode.Hold;
    }

    private static bool Crossed(float current, float previous, float threshold) =>
        (current >= threshold) != (previous >= threshold);

    private static bool KeyboardHasNewPress()
    {
        // KeyboardState.GetPressedKeys allocates whenever any key is held.
        // The FNA state is a fixed 256-bit set, so a bounded edge scan keeps
        // the per-update source arbiter allocation-free.
        for (int value = 1; value < 256; value++)
        {
            Keys key = (Keys)value;
            if (MInput.Keyboard.CurrentState.IsKeyDown(key) &&
                MInput.Keyboard.PreviousState.IsKeyUp(key)) return true;
        }
        return false;
    }

    private static void D3LoadAndMigrate()
    {
        factoryProfile = TouchLayoutPolicy.Factory(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea,
            presentation.IsPad, 1.0, TouchGrabStyle.Toggle);
        GrabModes canonical = Settings.Instance?.GrabMode ?? GrabModes.Hold;
        AppleGrabMode canonicalMode = (AppleGrabMode)(int)canonical;
        bool migrated = Defaults.StringForKey(D3MigrationKey) == "Complete";
        if (!migrated)
        {
            TouchD2MigrationResult migration = TouchD2MigrationPolicy.Migrate(
                presentation.PointWidth, presentation.PointHeight, presentation.SafeArea,
                presentation.IsPad, preferences, canonicalMode);
            layoutProfile = migration.Layout;
            grabArbiter = new GrabSourceArbiter(migration.GrabModes,
                controllerConnected ? AppleInputSource.Controller : AppleInputSource.Touch);
            SaveLayout();
            SaveGrabProfiles();
            Defaults.SetString("Complete", D3MigrationKey);
            Defaults.Synchronize();
        }
        else
        {
            string stored = Defaults.StringForKey(CurrentLayoutKey());
            bool decoded = TouchLayoutCodec.TryDecode(stored, out layoutProfile, out bool legacyCoordinates);
            if (!decoded)
            {
                stored = Defaults.StringForKey(LegacyLayoutKey());
                decoded = TouchLayoutCodec.TryDecode(stored, out layoutProfile, out legacyCoordinates);
            }
            if (decoded && legacyCoordinates)
            {
                layoutProfile = TouchLayoutPolicy.RebaseFromSafeArea(
                    layoutProfile, presentation.PointWidth, presentation.PointHeight, presentation.SafeArea);
                SaveLayout();
                Defaults.Synchronize();
            }
            if (!decoded || !TouchLayoutPolicy.Validate(layoutProfile, UsableWidth(), UsableHeight()).IsValid)
                layoutProfile = factoryProfile;
            GrabModeProfiles profiles = new(
                ParseGrabMode(Defaults.StringForKey(TouchGrabModeKey), AppleGrabMode.Toggle),
                ParseGrabMode(Defaults.StringForKey(ControllerGrabModeKey), canonicalMode),
                ParseGrabMode(Defaults.StringForKey(KeyboardGrabModeKey), canonicalMode));
            grabArbiter = new GrabSourceArbiter(profiles, controllerConnected
                ? AppleInputSource.Controller : AppleInputSource.Touch);
        }
        directionalHaptics = Defaults.StringForKey(DirectionalHapticsKey) == "On";
    }

    private static TouchRuntimeLayout D3CreateRuntimeLayout() => TouchLayoutPolicy.Materialize(
        layoutProfile, presentation.PointWidth, presentation.PointHeight, presentation.SafeArea);

    private static void D3ControllerConnectionChanged(bool connected)
    {
        grabArbiter?.ForceSource(connected ? AppleInputSource.Controller : AppleInputSource.Touch);
        if (state != null) state.GrabMode = grabArbiter?.ActiveMode ?? AppleGrabMode.Hold;
    }

    private static void D3ApplyLayout()
    {
        TouchLayoutValidation validation = TouchLayoutPolicy.Validate(layoutProfile, UsableWidth(), UsableHeight());
        if (!validation.IsValid) layoutProfile = factoryProfile;
        layout = D3CreateRuntimeLayout();
        state?.UpdateLayout(layout);
        if (state != null) state.GrabMode = grabArbiter?.ActiveMode ?? AppleGrabMode.Hold;
    }

    private static void D3SetMovement(int value)
    {
        TouchMovementMode mode = value == 1 ? TouchMovementMode.Floating : TouchMovementMode.Fixed;
        SetProfileOption(layoutProfile with { MovementMode = mode }, mode == TouchMovementMode.Floating
            ? TouchLayoutControl.FloatingRegion : TouchLayoutControl.Movement);
    }

    private static void D3SetSliding(bool enabled) => SetProfileOption(layoutProfile with
    {
        Sliding = enabled ? TouchSlideMode.JumpDash : TouchSlideMode.Off,
    }, layoutProfile.ActionLayout == TouchActionLayout.SplitRegion
        ? TouchLayoutControl.SplitRegion : TouchLayoutControl.Jump);

    private static void SetProfileOption(TouchLayoutProfile candidate, TouchLayoutControl target)
    {
        EnsureInitialized();
        if (candidate == layoutProfile) return;
        if (!TouchLayoutPolicy.Validate(candidate, UsableWidth(), UsableHeight()).IsValid)
        {
            candidate = candidate.WithRect(target, factoryProfile.Rect(target));
            if (!TouchLayoutPolicy.Validate(candidate, UsableWidth(), UsableHeight()).IsValid) return;
        }
        layoutProfile = candidate;
        SaveLayout();
        Defaults.Synchronize();
        D3ApplyLayout();
    }

    private static void SaveLayout() => Defaults.SetString(TouchLayoutCodec.Encode(layoutProfile), CurrentLayoutKey());
    private static string CurrentLayoutKey() => presentation.IsPad ? TabletLayoutKey : PhoneLayoutKey;
    private static string LegacyLayoutKey() => presentation.IsPad ? LegacyTabletLayoutKey : LegacyPhoneLayoutKey;
    private static double UsableWidth() => Math.Max(1, presentation.PointWidth);
    private static double UsableHeight() => Math.Max(1, presentation.PointHeight);

    private static void SaveGrabProfiles()
    {
        Defaults.SetString(StoreGrabMode(grabArbiter.Profiles.Touch), TouchGrabModeKey);
        Defaults.SetString(StoreGrabMode(grabArbiter.Profiles.Controller), ControllerGrabModeKey);
        Defaults.SetString(StoreGrabMode(grabArbiter.Profiles.Keyboard), KeyboardGrabModeKey);
    }

    private static string GrabModeKey(AppleInputSource source) => source switch
    {
        AppleInputSource.Controller => ControllerGrabModeKey,
        AppleInputSource.Keyboard => KeyboardGrabModeKey,
        _ => TouchGrabModeKey,
    };
    private static string StoreGrabMode(AppleGrabMode mode) => mode switch
    {
        AppleGrabMode.Invert => "Invert",
        AppleGrabMode.Toggle => "Toggle",
        _ => "Hold",
    };
    private static AppleGrabMode ParseGrabMode(string value, AppleGrabMode fallback) => value switch
    {
        "Hold" => AppleGrabMode.Hold,
        "Invert" => AppleGrabMode.Invert,
        "Toggle" => AppleGrabMode.Toggle,
        _ => fallback,
    };

    private static TouchHapticAction D3ApplyTouch(int finger, TouchPhase phase, TouchPoint point)
    {
        TouchDirection before = state.Direction;
        TouchHapticAction result = state.Apply(finger, phase, point, preferences.Haptics);
        if (DirectionalHapticPolicy.ShouldPulse(before, state.Direction, directionalHaptics && preferences.Haptics))
        {
            feedback?.ImpactOccurred();
            feedback?.Prepare();
        }
        return result;
    }

    private static bool D3UpdateEditorIfActive()
    {
        if (layoutEditor == null) return false;
        state.Reset();
        TouchCollection touches = TouchPanel.GetState();
        bool ownerPresent = false;
        for (int index = 0; index < touches.Count; index++)
        {
            TouchLocation touch = touches[index];
            TouchPhase phase = touch.State switch
            {
                TouchLocationState.Pressed => TouchPhase.Pressed,
                TouchLocationState.Released => TouchPhase.Released,
                _ => TouchPhase.Moved,
            };
            TouchPoint point = new(touch.Position.X / presentation.NativeScale, touch.Position.Y / presentation.NativeScale);
            UpdateEditorTouch(touch.Id, phase, point);
            // A Pressed touch can become the editor owner inside
            // UpdateEditorTouch. Test ownership afterwards so that first frame
            // is not immediately mistaken for a cancelled drag.
            if (phase != TouchPhase.Released && touch.Id == editorFinger) ownerPresent = true;
        }
        if (editorFinger >= 0 && !ownerPresent)
        {
            layoutEditor.EndGesture();
            editorFinger = -1;
        }
        if (Input.MenuCancel?.Pressed == true) CloseLayoutEditor(false);
        return true;
    }

    private static void UpdateEditorTouch(int finger, TouchPhase phase, TouchPoint point)
    {
        if (phase == TouchPhase.Pressed)
        {
            EditorCommand command = CommandAt(point);
            if (command != EditorCommand.None)
            {
                editorCommandOwner = finger;
                RunCommand(command);
                return;
            }
            if (editorFinger >= 0) return;
            TouchLayoutSelection? selected = ControlAt(point);
            if (!selected.HasValue) return;
            selectedControl = selected.Value.Primary;
            selectedExtraIndex = selected.Value.ExtraIndex;
            editorAddPalette = false;
            editorFinger = finger;
            editorStartPoint = point;
            editorStartRect = SelectedRect();
            TouchRect runtimeRect = RuntimeRect(layoutEditor.Working, SelectedSelection());
            editorResize = ResizeHandle(layoutEditor.Working, SelectedSelection(), runtimeRect).Contains(point);
            layoutEditor.BeginGesture();
            return;
        }
        if (phase == TouchPhase.Released)
        {
            if (editorCommandOwner == finger) editorCommandOwner = -1;
            if (editorFinger != finger) return;
            layoutEditor.EndGesture();
            editorFinger = -1;
            return;
        }
        if (editorFinger != finger) return;
        TouchPoint delta = point - editorStartPoint;
        TouchRect next;
        bool circle = EditorControlIsCircle(layoutEditor.Working, SelectedSelection());
        double visualRatio = TouchLayoutPolicy.VisualRatio(layoutEditor.Working, SelectedSelection());
        if (editorResize)
        {
            next = TouchLayoutEditorGeometry.Resize(
                editorStartRect, delta, UsableWidth(), UsableHeight(),
                circle, visualRatio);
        }
        else
        {
            next = TouchLayoutEditorGeometry.Move(
                editorStartRect, delta, UsableWidth(), UsableHeight(), circle, visualRatio);
        }
        layoutEditor.PreviewRect(SelectedSelection(), next);
    }

    private static void RunCommand(EditorCommand command)
    {
        switch (command)
        {
            case EditorCommand.Done:
                CloseLayoutEditor(true);
                break;
            case EditorCommand.Cancel:
                CloseLayoutEditor(false);
                break;
            case EditorCommand.Undo:
                layoutEditor.Undo();
                EnsureEditorSelection();
                break;
            case EditorCommand.ResetSelected:
                layoutEditor.ResetSelected(SelectedSelection());
                break;
            case EditorCommand.ResetLayout:
                layoutEditor.ResetLayout();
                EnsureEditorSelection();
                break;
            case EditorCommand.Mirror:
                layoutEditor.Mirror();
                break;
            case EditorCommand.Shape:
                if (selectedExtraIndex >= 0)
                {
                    TouchExtraControl extra = layoutEditor.Working.Extra(selectedExtraIndex);
                    layoutEditor.SetShape(SelectedSelection(), extra.Shape == TouchGrabShape.Circle
                        ? TouchGrabShape.Rectangle : TouchGrabShape.Circle);
                }
                else if (selectedControl == TouchLayoutControl.Grab)
                {
                    layoutEditor.SetShape(SelectedSelection(), layoutEditor.Working.GrabShape == TouchGrabShape.Circle
                        ? TouchGrabShape.Rectangle : TouchGrabShape.Circle);
                }
                break;
            case EditorCommand.Shoulder:
                if (selectedControl == TouchLayoutControl.Grab)
                    layoutEditor.Change(layoutEditor.Working with { GrabShape = TouchGrabShape.Rectangle, Grab = factoryProfile.GrabShape == TouchGrabShape.Rectangle ? factoryProfile.Grab : ShoulderPreset() });
                break;
            case EditorCommand.Split:
                if (selectedExtraIndex < 0 && selectedControl == TouchLayoutControl.SplitRegion)
                    layoutEditor.Change(layoutEditor.Working with { SplitOrientation = (TouchSplitOrientation)(((int)layoutEditor.Working.SplitOrientation + 1) % 4) });
                break;
            case EditorCommand.Swap:
                if (selectedExtraIndex < 0 && selectedControl == TouchLayoutControl.SplitRegion)
                    layoutEditor.Change(layoutEditor.Working with { JumpOnFirstHalf = !layoutEditor.Working.JumpOnFirstHalf });
                break;
            case EditorCommand.ToggleTools:
                editorToolsVisible = !editorToolsVisible;
                break;
            case EditorCommand.MoveTools:
                editorToolsAtBottom = !editorToolsAtBottom;
                break;
            case EditorCommand.OpacityDown:
                layoutEditor.SetOpacity(SelectedSelection(), SelectedOpacity() - 10);
                break;
            case EditorCommand.OpacityUp:
                layoutEditor.SetOpacity(SelectedSelection(), SelectedOpacity() + 10);
                break;
            case EditorCommand.Duplicate:
                TouchExtraControlKind? duplicateKind = SelectedExtraKind();
                if (duplicateKind.HasValue) TryAddControl(duplicateKind.Value, true);
                break;
            case EditorCommand.Delete:
                if (layoutEditor.Delete(SelectedSelection()))
                {
                    selectedExtraIndex = -1;
                    selectedControl = TouchLayoutControl.Movement;
                }
                break;
            case EditorCommand.AddControl:
                editorAddPalette = true;
                break;
            case EditorCommand.AddBack:
                editorAddPalette = false;
                break;
            case EditorCommand.AddJump:
                TryAddControl(TouchExtraControlKind.Jump, false);
                break;
            case EditorCommand.AddDash:
                TryAddControl(TouchExtraControlKind.Dash, false);
                break;
            case EditorCommand.AddGrab:
                TryAddControl(TouchExtraControlKind.Grab, false);
                break;
            case EditorCommand.AddPause:
                TryAddControl(TouchExtraControlKind.Pause, false);
                break;
            case EditorCommand.AddJournal:
                TryAddControl(TouchExtraControlKind.Journal, false);
                break;
            case EditorCommand.AddCrouchDash:
                TryAddControl(TouchExtraControlKind.CrouchDash, false);
                break;
            case EditorCommand.AddQuickRestart:
                TryAddControl(TouchExtraControlKind.QuickRestart, false);
                break;
        }
    }

    private static void TryAddControl(TouchExtraControlKind kind, bool duplicate)
    {
        if (kind == TouchExtraControlKind.Journal && !layoutEditor.Working.JournalEnabled)
        {
            layoutEditor.Change(layoutEditor.Working with { JournalEnabled = true });
            selectedControl = TouchLayoutControl.Journal;
            selectedExtraIndex = -1;
            editorAddPalette = false;
            return;
        }

        TouchRect source;
        TouchGrabShape shape;
        if (duplicate)
        {
            source = SelectedRect();
            shape = EditorControlIsCircle(layoutEditor.Working, SelectedSelection())
                ? TouchGrabShape.Circle : TouchGrabShape.Rectangle;
        }
        else
        {
            TouchLayoutControl primary = PrimaryForExtra(kind) ?? TouchLayoutControl.Dash;
            source = layoutEditor.Working.Rect(primary);
            shape = kind is TouchExtraControlKind.Pause or TouchExtraControlKind.Journal or
                TouchExtraControlKind.CrouchDash or TouchExtraControlKind.QuickRestart
                ? TouchGrabShape.Rectangle
                : kind == TouchExtraControlKind.Grab ? layoutEditor.Working.GrabShape : TouchGrabShape.Circle;
        }
        if (!TouchLayoutPolicy.TryAddExtra(
                layoutEditor.Working, kind, shape, source, UsableWidth(), UsableHeight(),
                out TouchLayoutProfile updated, out int index)) return;
        layoutEditor.Change(updated);
        selectedExtraIndex = index;
        selectedControl = default;
        editorAddPalette = false;
    }

    private static void EnsureEditorSelection()
    {
        if (selectedExtraIndex >= 0)
        {
            if (selectedExtraIndex < TouchLayoutProfile.MaximumExtraControls &&
                layoutEditor.Working.Extra(selectedExtraIndex).Enabled) return;
            selectedExtraIndex = -1;
            selectedControl = TouchLayoutControl.Movement;
        }
        if (!IsEditorControlActive(layoutEditor.Working, selectedControl))
            selectedControl = layoutEditor.Working.MovementMode == TouchMovementMode.Fixed
                ? TouchLayoutControl.Movement : TouchLayoutControl.FloatingRegion;
    }

    private static TouchRect ShoulderPreset()
    {
        TouchControlLayout legacy = TouchControlLayout.Create(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea, presentation.IsPad, 1.0);
        return TouchLayoutPolicy.Normalize(legacy.Shoulder,
            TouchLayoutPolicy.FullCanvas(presentation.PointWidth, presentation.PointHeight));
    }

    private static void CloseLayoutEditor(bool save)
    {
        if (layoutEditor == null) return;
        if (save)
        {
            if (!layoutEditor.TryCommit(UsableWidth(), UsableHeight(), out TouchLayoutProfile committed)) return;
            layoutProfile = committed;
            SaveLayout();
            Defaults.Synchronize();
            D3ApplyLayout();
        }
        layoutEditor = null;
        editorFinger = editorCommandOwner = -1;
        Action callback = layoutEditorClosed;
        layoutEditorClosed = null;
        callback?.Invoke();
    }

    private static TouchLayoutSelection SelectedSelection() => new(selectedControl, selectedExtraIndex);

    private static TouchRect SelectedRect() => selectedExtraIndex >= 0
        ? layoutEditor.Working.Extra(selectedExtraIndex).Rect
        : layoutEditor.Working.Rect(selectedControl);

    private static int SelectedOpacity() => selectedExtraIndex >= 0
        ? layoutEditor.Working.Extra(selectedExtraIndex).OpacityPercent
        : layoutEditor.Working.OpacityFor(selectedControl);

    private static string SelectedName()
    {
        TouchExtraControlKind? kind = SelectedExtraKind();
        if (!kind.HasValue) return TouchLayoutPolicy.Name(selectedControl);

        string name = TouchLayoutPolicy.ExtraName(kind.Value);
        int count = 0;
        int ordinal = 0;
        TouchLayoutControl? primary = PrimaryForExtra(kind.Value);
        if (primary.HasValue && IsEditorControlActive(layoutEditor.Working, primary.Value))
        {
            count++;
            if (selectedExtraIndex < 0) ordinal = count;
        }
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            TouchExtraControl extra = layoutEditor.Working.Extra(index);
            if (!extra.Enabled || extra.Kind != kind.Value) continue;
            count++;
            if (selectedExtraIndex == index) ordinal = count;
        }

        // Numbers distinguish actual duplicates of the same action. Never leak
        // the unrelated internal Extra0..Extra3 storage slot into the UI.
        return count > 1 ? name + " " + ordinal : name;
    }

    private static TouchLayoutControl? PrimaryForExtra(TouchExtraControlKind kind) => kind switch
    {
        TouchExtraControlKind.Jump => TouchLayoutControl.Jump,
        TouchExtraControlKind.Dash => TouchLayoutControl.Dash,
        TouchExtraControlKind.Grab => TouchLayoutControl.Grab,
        TouchExtraControlKind.Pause => TouchLayoutControl.Pause,
        TouchExtraControlKind.Journal => TouchLayoutControl.Journal,
        _ => null,
    };

    private static TouchExtraControlKind? SelectedExtraKind() => selectedExtraIndex >= 0
        ? layoutEditor.Working.Extra(selectedExtraIndex).Kind
        : selectedControl switch
        {
            TouchLayoutControl.Jump => TouchExtraControlKind.Jump,
            TouchLayoutControl.Dash => TouchExtraControlKind.Dash,
            TouchLayoutControl.Grab => TouchExtraControlKind.Grab,
            TouchLayoutControl.Pause => TouchExtraControlKind.Pause,
            TouchLayoutControl.Journal => TouchExtraControlKind.Journal,
            _ => null,
        };

    private static TouchLayoutSelection? ControlAt(TouchPoint point)
    {
        TouchLayoutProfile profile = layoutEditor.Working;
        for (int index = TouchLayoutProfile.MaximumExtraControls - 1; index >= 0; index--)
        {
            TouchExtraControl extra = profile.Extra(index);
            if (!extra.Enabled) continue;
            TouchLayoutSelection selection = new(default, index);
            TouchRect rect = RuntimeRect(profile, selection);
            if (selectedExtraIndex == index && ResizeHandle(profile, selection, rect).Contains(point)) return selection;
            if (EditorControlContains(profile, selection, rect, point)) return selection;
        }
        TouchLayoutControl[] order =
        {
            TouchLayoutControl.Pause, TouchLayoutControl.Journal, TouchLayoutControl.Grab,
            profile.ActionLayout == TouchActionLayout.SplitRegion ? TouchLayoutControl.SplitRegion : TouchLayoutControl.Jump,
            profile.ActionLayout == TouchActionLayout.SplitRegion ? TouchLayoutControl.SplitRegion : TouchLayoutControl.Dash,
            TouchLayoutControl.Movement,
            TouchLayoutControl.FloatingRegion,
        };
        foreach (TouchLayoutControl control in order.Distinct())
        {
            if (!IsEditorControlActive(profile, control)) continue;
            TouchRect rect = RuntimeRect(profile, control);
            TouchLayoutSelection selection = new(control);
            if (selectedExtraIndex < 0 && control == selectedControl &&
                ResizeHandle(profile, selection, rect).Contains(point)) return selection;
            if (EditorControlContains(profile, selection, rect, point)) return selection;
        }
        return null;
    }

    private static bool IsEditorControlActive(TouchLayoutProfile profile, TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.Movement => true,
        TouchLayoutControl.FloatingRegion => profile.MovementMode == TouchMovementMode.Floating,
        TouchLayoutControl.Jump or TouchLayoutControl.Dash => profile.ActionLayout == TouchActionLayout.SeparateButtons,
        TouchLayoutControl.SplitRegion => profile.ActionLayout == TouchActionLayout.SplitRegion,
        TouchLayoutControl.Journal => profile.JournalEnabled,
        _ => true,
    };

    private static bool EditorControlIsCircle(TouchLayoutProfile profile, TouchLayoutSelection selection)
    {
        if (selection.IsExtra) return TouchLayoutPolicy.ExtraIsCircle(profile.Extra(selection.ExtraIndex));
        return selection.Primary switch
        {
            TouchLayoutControl.Movement or TouchLayoutControl.Jump or TouchLayoutControl.Dash => true,
            TouchLayoutControl.Grab => profile.GrabShape == TouchGrabShape.Circle,
            _ => false,
        };
    }

    private static bool EditorControlIsCircle(TouchLayoutProfile profile, TouchLayoutControl control) => control switch
    {
        TouchLayoutControl.Movement or TouchLayoutControl.Jump or TouchLayoutControl.Dash => true,
        TouchLayoutControl.Grab => profile.GrabShape == TouchGrabShape.Circle,
        _ => false,
    };

    private static bool SelectedCanChangeShape() => selectedExtraIndex >= 0 ||
        selectedControl == TouchLayoutControl.Grab;

    private static bool HasFreeExtraSlot() =>
        Enumerable.Range(0, TouchLayoutProfile.MaximumExtraControls)
            .Any(index => !layoutEditor.Working.Extra(index).Enabled);

    private static bool SelectedCanDuplicate() => SelectedExtraKind().HasValue && HasFreeExtraSlot();

    private static bool SelectedCanDelete() => selectedExtraIndex >= 0 ||
        selectedControl == TouchLayoutControl.Journal && layoutEditor.Working.JournalEnabled;

    private static bool EditorControlContains(
        TouchLayoutProfile profile,
        TouchLayoutSelection selection,
        TouchRect rect,
        TouchPoint point)
    {
        if (!EditorControlIsCircle(profile, selection)) return rect.Contains(point);
        TouchPoint center = new(rect.X + rect.Width * 0.5, rect.Y + rect.Height * 0.5);
        TouchPoint delta = point - center;
        double radius = Math.Min(rect.Width, rect.Height) * 0.5;
        return delta.X * delta.X + delta.Y * delta.Y <= radius * radius;
    }

    private static TouchRect RuntimeRect(TouchLayoutProfile profile, TouchLayoutControl control) =>
        TouchLayoutPolicy.Denormalize(profile.Rect(control),
            TouchLayoutPolicy.FullCanvas(presentation.PointWidth, presentation.PointHeight));

    private static TouchRect RuntimeRect(TouchLayoutProfile profile, TouchLayoutSelection selection) =>
        TouchLayoutPolicy.Denormalize(selection.IsExtra
                ? profile.Extra(selection.ExtraIndex).Rect
                : profile.Rect(selection.Primary),
            TouchLayoutPolicy.FullCanvas(presentation.PointWidth, presentation.PointHeight));

    private static TouchRect ResizeHandle(
        TouchLayoutProfile profile,
        TouchLayoutSelection selection,
        TouchRect rect)
    {
        const double size = 24;
        if (EditorControlIsCircle(profile, selection))
        {
            double visibleRadius = Math.Min(rect.Width, rect.Height) * 0.5 *
                TouchLayoutPolicy.VisualRatio(profile, selection);
            return new TouchRect(
                rect.X + rect.Width * 0.5 + visibleRadius - size,
                rect.Y + rect.Height * 0.5 + visibleRadius - size,
                size,
                size);
        }
        return new TouchRect(rect.Right - size, rect.Bottom - size, size, size);
    }

    private static void D3RenderEditor()
    {
        TouchLayoutProfile profile = layoutEditor.Working;
        TouchLayoutValidation validation = TouchLayoutPolicy.Validate(profile, UsableWidth(), UsableHeight());
        TouchRect safe = TouchLayoutPolicy.Usable(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea);
        Draw.Rect(new Rectangle(0, 0, presentation.PixelWidth, presentation.PixelHeight), new Color(4, 8, 18) * 0.88f);
        TouchRect full = TouchLayoutPolicy.FullCanvas(presentation.PointWidth, presentation.PointHeight);
        Draw.HollowRect(PixelRect(full), Color.White * 0.42f);
        // Safe area is guidance, not a placement boundary. Controls may use
        // letterboxed/pillarboxed edges and every physical corner of the display.
        Draw.HollowRect(PixelRect(safe), Color.White * 0.14f);
        Draw.Line(PixelPoint(new TouchPoint(full.Width * 0.5, 0)),
            PixelPoint(new TouchPoint(full.Width * 0.5, full.Height)), Color.White * 0.12f, Math.Max(1, Pixels(0.75)));
        Draw.Line(PixelPoint(new TouchPoint(0, full.Height * 0.5)),
            PixelPoint(new TouchPoint(full.Width, full.Height * 0.5)), Color.White * 0.12f, Math.Max(1, Pixels(0.75)));
        foreach (TouchLayoutControl control in Enum.GetValues<TouchLayoutControl>())
        {
            if (!IsEditorControlActive(profile, control)) continue;
            DrawEditorControl(profile, new TouchLayoutSelection(control), validation);
        }
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
            if (profile.Extra(index).Enabled)
                DrawEditorControl(profile, new TouchLayoutSelection(default, index), validation);
        DrawEditorToolbar();
    }

    private static void DrawEditorControl(
        TouchLayoutProfile profile,
        TouchLayoutSelection selection,
        TouchLayoutValidation validation)
    {
        TouchRect rect = RuntimeRect(profile, selection);
        Rectangle pixel = PixelRect(rect);
        bool selected = selection == SelectedSelection();
        bool circle = EditorControlIsCircle(profile, selection);
        Color fill = selected ? new Color(55, 143, 225) : new Color(28, 42, 70);
        Color outline = !validation.IsValid ? Color.OrangeRed : selected ? Color.White : Color.LightGray;
        if (circle)
        {
            float hitRadius = Math.Min(pixel.Width, pixel.Height) * 0.5f;
            float visualRadius = hitRadius * (float)TouchLayoutPolicy.VisualRatio(profile, selection);
            // The filled disk is the exact runtime visual size. The faint outer
            // ring exposes the larger ergonomic hit target without misleading
            // the user about what gameplay will render.
            Draw.SpriteBatch.Draw(circleTexture,
                new Rectangle(pixel.Center.X - (int)visualRadius, pixel.Center.Y - (int)visualRadius,
                    (int)(visualRadius * 2), (int)(visualRadius * 2)), fill * 0.64f);
            Draw.Circle(new Vector2(pixel.Center.X, pixel.Center.Y), visualRadius, outline,
                Math.Max(24, (int)(visualRadius * 0.7f)));
            Draw.Circle(new Vector2(pixel.Center.X, pixel.Center.Y), hitRadius,
                Color.White * (selected ? 0.36f : 0.18f), Math.Max(24, (int)(hitRadius * 0.7f)));
        }
        else
        {
            Draw.Rect(pixel, fill * 0.64f);
            Draw.HollowRect(pixel, outline);
        }
        if (selected)
        {
            Rectangle handle = PixelRect(ResizeHandle(profile, selection, rect));
            Draw.Rect(handle, Color.White * 0.92f);
            Draw.HollowRect(handle, Color.Black);
        }
        DrawEditorControlContent(profile, selection, rect);
    }

    private static void DrawEditorControlContent(
        TouchLayoutProfile profile,
        TouchLayoutSelection selection,
        TouchRect rect)
    {
        if (!selection.IsExtra)
        {
            if (selection.Primary == TouchLayoutControl.SplitRegion)
            {
                DrawSplitLine(rect, profile.SplitOrientation, Color.White * 0.85f);
                DrawEditorSplitIcons(profile, rect);
                return;
            }
            if (selection.Primary == TouchLayoutControl.Grab)
            {
                DrawEditorGrabIcon(rect, profile.GrabShape);
                return;
            }
            Texture2D primaryIcon = selection.Primary switch
            {
                TouchLayoutControl.Jump => jumpTexture,
                TouchLayoutControl.Dash => dashTexture,
                TouchLayoutControl.Pause => pauseTexture,
                TouchLayoutControl.Journal => journalTexture,
                _ => null,
            };
            if (primaryIcon != null)
            {
                DrawEditorIcon(rect, primaryIcon);
                return;
            }
            DrawEditorLabel(rect, TouchLayoutPolicy.Name(selection.Primary).ToUpperInvariant());
            return;
        }

        TouchExtraControl extra = profile.Extra(selection.ExtraIndex);
        if (extra.Kind == TouchExtraControlKind.Grab)
        {
            DrawEditorGrabIcon(rect, extra.Shape);
            return;
        }
        Texture2D icon = extra.Kind switch
        {
            TouchExtraControlKind.Jump => jumpTexture,
            TouchExtraControlKind.Dash => dashTexture,
            TouchExtraControlKind.CrouchDash => crouchDashTexture,
            TouchExtraControlKind.Pause => pauseTexture,
            TouchExtraControlKind.Journal => journalTexture,
            TouchExtraControlKind.QuickRestart => restartTexture,
            _ => null,
        };
        if (icon != null) DrawEditorIcon(rect, icon);
    }

    private static void DrawEditorIcon(TouchRect rect, Texture2D texture)
    {
        Rectangle pixel = PixelRect(rect);
        int side = (int)(Math.Min(pixel.Width, pixel.Height) * 0.48f);
        Draw.SpriteBatch.Draw(texture,
            new Rectangle(pixel.Center.X - side / 2, pixel.Center.Y - side / 2, side, side), Color.White);
    }

    private static void DrawEditorLabel(TouchRect rect, string label, double vertical = 0.5)
    {
        Rectangle pixel = PixelRect(rect);
        DrawEditorTextCentered(label,
            new Vector2(pixel.Center.X, (float)(pixel.Y + pixel.Height * vertical)),
            Color.White, EditorTextScale(label, pixel, 0.70));
    }

    private static void DrawEditorGrabIcon(TouchRect rect, TouchGrabShape shape)
    {
        Rectangle pixel = PixelRect(rect);
        int side = (int)(Math.Min(pixel.Width, pixel.Height) * 0.48f);
        Texture2D icon = D3EffectiveGrab ? grabGrabbedTexture : grabUngrabbedTexture;
        Draw.SpriteBatch.Draw(icon, new Rectangle(pixel.Center.X - side / 2, pixel.Center.Y - side / 2, side, side), Color.White);
    }

    private static void DrawEditorSplitIcons(TouchLayoutProfile profile, TouchRect rect)
    {
        SplitCenters(profile, rect, out TouchPoint first, out TouchPoint second);
        bool jumpFirst = profile.JumpOnFirstHalf;
        DrawEditorSplitIcon(first, jumpFirst ? jumpTexture : dashTexture, rect);
        DrawEditorSplitIcon(second, jumpFirst ? dashTexture : jumpTexture, rect);
    }

    private static void DrawEditorSplitIcon(TouchPoint center, Texture2D texture, TouchRect rect)
    {
        int side = (int)Math.Max(Pixels(18), Pixels(Math.Min(rect.Width, rect.Height) * 0.28));
        Vector2 pixel = PixelPoint(center);
        Draw.SpriteBatch.Draw(texture,
            new Rectangle((int)pixel.X - side / 2, (int)pixel.Y - side / 2, side, side), Color.White);
    }

    private static void DrawEditorToolbar()
    {
        TouchLayoutValidation validation = TouchLayoutPolicy.Validate(layoutEditor.Working, UsableWidth(), UsableHeight());
        Draw.Rect(PixelRect(EditorDockBounds()), new Color(4, 8, 18) * 0.90f);
        foreach ((EditorCommand Command, TouchRect Rect, string Label) item in CommandButtons())
        {
            bool enabled = IsCommandEnabled(item.Command, validation);
            Rectangle pixel = PixelRect(item.Rect);
            Draw.Rect(pixel, (enabled ? new Color(20, 31, 52) : Color.DarkSlateGray) * 0.94f);
            Draw.HollowRect(pixel, enabled ? Color.White : Color.Gray);
            DrawEditorTextCentered(item.Label, new Vector2(pixel.Center.X, pixel.Center.Y),
                enabled ? Color.White : Color.Gray, EditorTextScale(item.Label, pixel, 0.66));
        }
        if (!editorToolsVisible) return;

        // Celeste's fallback SpriteFont has a deliberately small glyph set.
        // Keep editor-only host copy ASCII-only so MeasureString cannot fail.
        double statusTop = EditorDockBounds().Y + (editorAddPalette ? 124 : 176);
        string selected = "SELECTED: " + SelectedName().ToUpperInvariant() +
            "  -  OPACITY " + SelectedOpacity() + "%";
        Rectangle statusBounds = PixelRect(new TouchRect(16, statusTop - 10,
            presentation.PointWidth - 32, 22));
        DrawEditorTextCentered(selected,
            new Vector2(presentation.PixelWidth * 0.5f, Pixels(statusTop)),
            Color.White, EditorTextScale(selected, statusBounds, 0.76));
        if (validation.IsValid)
        {
            const string guidance = "DRAG CONTROL TO MOVE  -  DRAG WHITE CORNER TO RESIZE";
            Rectangle guidanceBounds = PixelRect(new TouchRect(16, statusTop + 11,
                presentation.PointWidth - 32, 24));
            DrawEditorTextCentered(guidance,
                new Vector2(presentation.PixelWidth * 0.5f, Pixels(statusTop + 22)),
                Color.White, EditorTextScale(guidance, guidanceBounds, 0.82));
        }
        else
        {
            string failure = validation.Message.ToUpperInvariant();
            Rectangle failureBounds = PixelRect(new TouchRect(16, statusTop + 11,
                presentation.PointWidth - 32, 24));
            DrawEditorTextCentered(failure,
                new Vector2(presentation.PixelWidth * 0.5f, Pixels(statusTop + 22)),
                Color.OrangeRed, EditorTextScale(failure, failureBounds, 0.82));
        }
    }

    private static void DrawEditorTextCentered(string text, Vector2 position, Color color, float scale) =>
        ActiveFont.DrawOutline(text, position, new Vector2(0.5f, 0.5f), Vector2.One * scale,
            color, 2f, Color.Black);

    private static float EditorTextScale(string text, Rectangle bounds, double preferred)
    {
        Vector2 measured = ActiveFont.Measure(text);
        float desired = (float)preferred;
        if (measured.X <= 0) return desired;
        return Math.Min(desired, Math.Max(0.1f, (bounds.Width - Pixels(10)) / measured.X));
    }

    private static void DrawSplitLine(TouchRect rect, TouchSplitOrientation orientation, Color color)
    {
        Vector2 first;
        Vector2 second;
        switch (orientation)
        {
            case TouchSplitOrientation.TopRightToBottomLeft:
                first = PixelPoint(new TouchPoint(rect.Right, rect.Y));
                second = PixelPoint(new TouchPoint(rect.X, rect.Bottom));
                break;
            case TouchSplitOrientation.Vertical:
                first = PixelPoint(new TouchPoint(rect.X + rect.Width * 0.5, rect.Y));
                second = PixelPoint(new TouchPoint(rect.X + rect.Width * 0.5, rect.Bottom));
                break;
            case TouchSplitOrientation.Horizontal:
                first = PixelPoint(new TouchPoint(rect.X, rect.Y + rect.Height * 0.5));
                second = PixelPoint(new TouchPoint(rect.Right, rect.Y + rect.Height * 0.5));
                break;
            default:
                first = PixelPoint(new TouchPoint(rect.X, rect.Y));
                second = PixelPoint(new TouchPoint(rect.Right, rect.Bottom));
                break;
        }
        Draw.Line(first, second, color, Math.Max(2, Pixels(1.5)));
    }

    private static void D3DrawSplit(float alpha)
    {
        Rectangle rect = PixelRect(layout.SplitRegion);
        Draw.Rect(rect, new Color(18, 24, 42) * (0.58f * alpha));
        bool jumpFirst = layout.Profile.JumpOnFirstHalf;
        bool firstPressed = jumpFirst ? state.Jump : state.Dash;
        bool secondPressed = jumpFirst ? state.Dash : state.Jump;
        if (firstPressed)
            DrawSplitHalf(rect, layout.Profile.SplitOrientation, true,
                (jumpFirst ? new Color(124, 219, 255) : new Color(255, 104, 146)) * (0.72f * alpha));
        if (secondPressed)
            DrawSplitHalf(rect, layout.Profile.SplitOrientation, false,
                (jumpFirst ? new Color(255, 104, 146) : new Color(124, 219, 255)) * (0.72f * alpha));
        DrawSplitLine(layout.SplitRegion, layout.Profile.SplitOrientation, Color.White * (0.62f * alpha));
        SplitCenters(layout.Profile, layout.SplitRegion, out TouchPoint firstCenter, out TouchPoint secondCenter);
        DrawSplitIcon(firstCenter, jumpFirst ? jumpTexture : dashTexture, firstPressed, alpha);
        DrawSplitIcon(secondCenter, jumpFirst ? dashTexture : jumpTexture, secondPressed, alpha);
    }

    private static void DrawSplitHalf(
        Rectangle rect,
        TouchSplitOrientation orientation,
        bool first,
        Color color)
    {
        if (orientation == TouchSplitOrientation.Vertical)
        {
            int half = rect.Width / 2;
            Draw.Rect(first
                ? new Rectangle(rect.X, rect.Y, half, rect.Height)
                : new Rectangle(rect.X + half, rect.Y, rect.Width - half, rect.Height), color);
            return;
        }
        if (orientation == TouchSplitOrientation.Horizontal)
        {
            int half = rect.Height / 2;
            Draw.Rect(first
                ? new Rectangle(rect.X, rect.Y, rect.Width, half)
                : new Rectangle(rect.X, rect.Y + half, rect.Width, rect.Height - half), color);
            return;
        }

        if (splitHalfTexture == null)
        {
            const int size = 128;
            splitHalfTexture = new Texture2D(Celeste.Instance.GraphicsDevice, size, size, false, SurfaceFormat.Color);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                pixels[y * size + x] = x >= y ? Color.White : Color.Transparent;
            splitHalfTexture.SetData(pixels);
        }

        SpriteEffects effects = orientation == TouchSplitOrientation.TopLeftToBottomRight
            ? (first ? SpriteEffects.None : SpriteEffects.FlipHorizontally | SpriteEffects.FlipVertically)
            : (first ? SpriteEffects.FlipHorizontally : SpriteEffects.FlipVertically);
        Draw.SpriteBatch.Draw(splitHalfTexture, rect, null, color, 0f, Vector2.Zero, effects, 0f);
    }

    private static void SplitCenters(
        TouchLayoutProfile profile,
        TouchRect rect,
        out TouchPoint firstCenter,
        out TouchPoint secondCenter)
    {
        if (profile.SplitOrientation == TouchSplitOrientation.Vertical)
        {
            firstCenter = new(rect.X + rect.Width * 0.25, rect.Y + rect.Height * 0.5);
            secondCenter = new(rect.X + rect.Width * 0.75, rect.Y + rect.Height * 0.5);
        }
        else if (profile.SplitOrientation == TouchSplitOrientation.Horizontal)
        {
            firstCenter = new(rect.X + rect.Width * 0.5, rect.Y + rect.Height * 0.25);
            secondCenter = new(rect.X + rect.Width * 0.5, rect.Y + rect.Height * 0.75);
        }
        else
        {
            firstCenter = profile.SplitOrientation == TouchSplitOrientation.TopLeftToBottomRight
                ? new(rect.X + rect.Width * 0.72, rect.Y + rect.Height * 0.28)
                : new(rect.X + rect.Width * 0.28, rect.Y + rect.Height * 0.28);
            secondCenter = new(rect.X + rect.Width - (firstCenter.X - rect.X),
                rect.Y + rect.Height - (firstCenter.Y - rect.Y));
        }
    }

    private static void DrawSplitIcon(TouchPoint center, Texture2D texture, bool pressed, float alpha)
    {
        float side = Pixels(Math.Min(layout.SplitRegion.Width, layout.SplitRegion.Height) * 0.32);
        Vector2 pixel = PixelPoint(center);
        Draw.SpriteBatch.Draw(texture,
            new Rectangle((int)(pixel.X - side * 0.5f), (int)(pixel.Y - side * 0.5f), (int)side, (int)side),
            Color.White * ((pressed ? 1f : 0.84f) * alpha));
    }

    private static void D3DrawGrab(float alpha)
    {
        bool active = D3EffectiveGrab;
        Texture2D icon = active ? grabGrabbedTexture : grabUngrabbedTexture;
        Color color = active ? new Color(255, 188, 91) : new Color(18, 24, 42);
        if (layout.GrabShape == TouchGrabShape.Circle)
        {
            float radius = Pixels(layout.GrabCircle.Radius);
            Draw.SpriteBatch.Draw(circleTexture, CircleDestination(layout.GrabCircle.Center, radius), color * ((active ? 0.82f : 0.58f) * alpha));
            Draw.SpriteBatch.Draw(icon, CircleDestination(layout.GrabCircle.Center, radius * 0.58f), Color.White * (0.90f * alpha));
            return;
        }
        Rectangle rect = PixelRect(layout.GrabRect);
        Draw.Rect(rect, color * ((active ? 0.72f : 0.52f) * alpha));
        int side = (int)(Math.Min(rect.Width, rect.Height) * 0.58f);
        Draw.SpriteBatch.Draw(icon, new Rectangle(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side),
            Color.White * (0.90f * alpha));
    }

    private static float D3ControlAlpha(TouchLayoutControl control, float alpha) =>
        alpha * layout.Profile.OpacityFor(control) / 100f;

    private static void D3DrawExtras(float alpha)
    {
        for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
        {
            TouchRuntimeExtraControl extra = layout.Extra(index);
            if (!extra.Enabled) continue;
            float localAlpha = alpha * extra.OpacityPercent / 100f;
            bool active = extra.Kind switch
            {
                TouchExtraControlKind.Jump => state.Jump,
                TouchExtraControlKind.Dash => state.Dash,
                TouchExtraControlKind.Grab => D3EffectiveGrab,
                TouchExtraControlKind.Pause => state.Pause,
                TouchExtraControlKind.Journal => state.Journal,
                TouchExtraControlKind.CrouchDash => state.CrouchDash,
                _ => state.QuickRestart,
            };
            Texture2D icon = extra.Kind switch
            {
                TouchExtraControlKind.Jump => jumpTexture,
                TouchExtraControlKind.Dash => dashTexture,
                TouchExtraControlKind.CrouchDash => crouchDashTexture,
                TouchExtraControlKind.Grab => active ? grabGrabbedTexture : grabUngrabbedTexture,
                TouchExtraControlKind.Pause => pauseTexture,
                TouchExtraControlKind.Journal => journalTexture,
                TouchExtraControlKind.QuickRestart => restartTexture,
                _ => dashTexture,
            };
            Color accent = extra.Kind switch
            {
                TouchExtraControlKind.Jump => new Color(124, 219, 255),
                TouchExtraControlKind.Dash or TouchExtraControlKind.CrouchDash => new Color(255, 104, 146),
                TouchExtraControlKind.Grab => new Color(255, 188, 91),
                _ => Color.White,
            };
            if (TouchLayoutPolicy.ExtraIsCircle(new TouchExtraControl(
                    extra.Enabled, extra.Kind, extra.Shape, extra.OpacityPercent, extra.Rect)))
            {
                DrawAction(extra.Circle, icon, active, accent, localAlpha);
            }
            else
            {
                DrawExtraRectangle(extra.Rect, icon, active, accent, localAlpha);
            }
        }
    }

    private static void DrawExtraRectangle(
        TouchRect touchRect,
        Texture2D icon,
        bool active,
        Color accent,
        float alpha)
    {
        Rectangle rect = PixelRect(touchRect);
        Draw.Rect(rect, (active ? accent : new Color(18, 24, 42)) * ((active ? 0.72f : 0.54f) * alpha));
        int side = (int)(Math.Min(rect.Width, rect.Height) * 0.58f);
        Draw.SpriteBatch.Draw(icon,
            new Rectangle(rect.Center.X - side / 2, rect.Center.Y - side / 2, side, side),
            Color.White * (0.90f * alpha));
    }

    private static EditorCommand CommandAt(TouchPoint point)
    {
        TouchLayoutValidation validation = TouchLayoutPolicy.Validate(layoutEditor.Working, UsableWidth(), UsableHeight());
        foreach ((EditorCommand Command, TouchRect Rect, string Label) item in CommandButtons())
        {
            if (!item.Rect.Contains(point)) continue;
            if (!IsCommandEnabled(item.Command, validation)) return EditorCommand.None;
            return item.Command;
        }
        return EditorCommand.None;
    }

    private static bool IsCommandEnabled(EditorCommand command, TouchLayoutValidation validation) => command switch
    {
        EditorCommand.Done => validation.IsValid,
        EditorCommand.Undo => layoutEditor.CanUndo,
        EditorCommand.OpacityDown => SelectedOpacity() > 0,
        EditorCommand.OpacityUp => SelectedOpacity() < 100,
        EditorCommand.Duplicate => SelectedCanDuplicate(),
        EditorCommand.Delete => SelectedCanDelete(),
        EditorCommand.Shape => SelectedCanChangeShape(),
        EditorCommand.AddJump or EditorCommand.AddDash or EditorCommand.AddGrab or
        EditorCommand.AddPause or EditorCommand.AddCrouchDash or EditorCommand.AddQuickRestart => HasFreeExtraSlot(),
        EditorCommand.AddJournal => !layoutEditor.Working.JournalEnabled || HasFreeExtraSlot(),
        _ => true,
    };

    private static IEnumerable<(EditorCommand Command, TouchRect Rect, string Label)> CommandButtons()
    {
        TouchRect dock = EditorDockBounds();
        double top = dock.Y + 4;
        double gap = 6;
        if (!editorToolsVisible)
        {
            const double compactWidth = 126;
            double compactStart = presentation.PointWidth * 0.5 - compactWidth - gap * 0.5;
            yield return (EditorCommand.ToggleTools, new TouchRect(compactStart, top, compactWidth, 48), "SHOW TOOLS");
            yield return (EditorCommand.MoveTools, new TouchRect(compactStart + compactWidth + gap, top, compactWidth, 48),
                editorToolsAtBottom ? "MOVE UP" : "MOVE DOWN");
            yield break;
        }

        EditorCommand[] primary =
        {
            EditorCommand.Done, EditorCommand.Cancel, EditorCommand.Undo, EditorCommand.ResetLayout,
            EditorCommand.Mirror, EditorCommand.ToggleTools, EditorCommand.MoveTools,
        };
        string[] labels =
        {
            "DONE", "CANCEL", "UNDO", "FACTORY", "MIRROR", "HIDE TOOLS",
            editorToolsAtBottom ? "MOVE UP" : "MOVE DOWN",
        };
        double width = Math.Min(92, (dock.Width - 12 - (primary.Length - 1) * gap) / primary.Length);
        double start = presentation.PointWidth * 0.5 - (primary.Length * width + (primary.Length - 1) * gap) * 0.5;
        for (int index = 0; index < primary.Length; index++)
            yield return (primary[index], new TouchRect(start + index * (width + gap), top, width, 48), labels[index]);

        double contextTop = top + 54;
        if (editorAddPalette)
        {
            EditorCommand[] addCommands =
            {
                EditorCommand.AddBack, EditorCommand.AddJump, EditorCommand.AddDash, EditorCommand.AddGrab,
                EditorCommand.AddPause, EditorCommand.AddJournal, EditorCommand.AddCrouchDash,
                EditorCommand.AddQuickRestart,
            };
            string[] addLabels =
                { "BACK", "JUMP", "DASH", "GRAB", "PAUSE", "JOURNAL", "CROUCH DASH", "QUICK RESTART" };
            double addWidth = Math.Min(92,
                (dock.Width - 12 - (addCommands.Length - 1) * gap) / addCommands.Length);
            double addStart = presentation.PointWidth * 0.5 -
                (addCommands.Length * addWidth + (addCommands.Length - 1) * gap) * 0.5;
            for (int index = 0; index < addCommands.Length; index++)
                yield return (addCommands[index],
                    new TouchRect(addStart + index * (addWidth + gap), contextTop, addWidth, 48), addLabels[index]);
            yield break;
        }

        EditorCommand[] context =
        {
            EditorCommand.ResetSelected, EditorCommand.OpacityDown, EditorCommand.OpacityUp,
            EditorCommand.Duplicate, EditorCommand.Delete, EditorCommand.AddControl,
        };
        string[] contextLabels = { "RESET", "OPACITY -", "OPACITY +", "DUPLICATE", "DELETE", "ADD CONTROL" };
        double contextWidth = Math.Min(112, (dock.Width - 12 - (context.Length - 1) * gap) / context.Length);
        double contextStart = presentation.PointWidth * 0.5 -
            (context.Length * contextWidth + (context.Length - 1) * gap) * 0.5;
        for (int index = 0; index < context.Length; index++)
            yield return (context[index],
                new TouchRect(contextStart + index * (contextWidth + gap), contextTop, contextWidth, 48), contextLabels[index]);

        double detailTop = contextTop + 54;
        double detailStart = presentation.PointWidth * 0.5 - 185;
        if (SelectedCanChangeShape())
        {
            bool circle = EditorControlIsCircle(layoutEditor.Working, SelectedSelection());
            yield return (EditorCommand.Shape, new TouchRect(detailStart, detailTop, 118, 48),
                circle ? "RECTANGLE" : "CIRCLE");
            if (selectedExtraIndex < 0 && selectedControl == TouchLayoutControl.Grab)
                yield return (EditorCommand.Shoulder, new TouchRect(detailStart + 124, detailTop, 118, 48), "SHOULDER");
        }
        if (selectedExtraIndex < 0 && selectedControl == TouchLayoutControl.SplitRegion)
        {
            yield return (EditorCommand.Split, new TouchRect(detailStart, detailTop, 118, 48), "NEXT SPLIT");
            yield return (EditorCommand.Swap, new TouchRect(detailStart + 124, detailTop, 118, 48), "SWAP");
        }
    }

    private static TouchRect EditorDockBounds()
    {
        TouchRect safe = TouchLayoutPolicy.Usable(
            presentation.PointWidth, presentation.PointHeight, presentation.SafeArea);
        double height = editorToolsVisible ? (editorAddPalette ? 164 : 212) : 56;
        double top = editorToolsAtBottom ? safe.Bottom - height : safe.Y;
        return new TouchRect(safe.X, top, safe.Width, height);
    }

    private enum EditorCommand
    {
        None, Done, Cancel, Undo, ResetSelected, ResetLayout, Mirror, Shape, Shoulder, Split, Swap,
        ToggleTools, MoveTools, OpacityDown, OpacityUp, Duplicate, Delete, AddControl, AddBack,
        AddJump, AddDash, AddGrab, AddPause, AddJournal, AddCrouchDash,
        AddQuickRestart,
    }
}
