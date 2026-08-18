using CelesteIOSFoundation;
using CelesteAppleInput;
using Microsoft.Xna.Framework.Input.Touch;

int passed = 0;
void Check(bool value, string name)
{
    if (!value) throw new InvalidOperationException($"FAIL: {name}");
    passed++;
}

PixelRect phone = PresentationPolicy.AspectFit(1280, 720, 2556, 1179);
Check(phone.Width == 2096 && phone.Height == 1179, "wide-phone aspect fit");
Check(phone.X == 230 && phone.Y == 0, "wide-phone centered");
PixelRect ipad = PresentationPolicy.AspectFit(1280, 720, 2732, 2048);
Check(ipad.Width == 2732 && ipad.Height == 1537, "iPad aspect fit");
Check(ipad.X == 0 && ipad.Y > 0, "iPad letterbox centered");
Check(PresentationPolicy.AspectFit(0, 720, 100, 100) == default, "invalid geometry closed");
Check(PresentationPolicy.IsLandscape(100, 50), "landscape accepted");
Check(!PresentationPolicy.IsLandscape(50, 100), "portrait rejected");
Check(PlatformPolicies.SupportsOrientation(true, true, false), "orientation policy");
Check(!PlatformPolicies.SupportsOrientation(true, true, true), "portrait not advertised");
Check(PlatformPolicies.AudioLane(true) == FoundationAudioLane.SimulatorNoFmod, "simulator no-FMOD lane");
Check(PlatformPolicies.AudioLane(false) == FoundationAudioLane.DeviceFmod, "device FMOD lane");
Check(new SafeAreaMetrics(0, 62, 20, 62).IsValidFor(956, 440), "wide-phone safe area");
Check(!new SafeAreaMetrics(0, -1, 0, 0).IsValidFor(956, 440), "negative safe area rejected");

LifecyclePolicy lifecycle = new();
Check(lifecycle.Connect(), "first scene connects");
Check(!lifecycle.Connect(), "duplicate scene start suppressed");
Check(lifecycle.RuntimeStartCount == 1, "one runtime");
lifecycle.BecomeActive(); Check(lifecycle.State == AppleSceneState.Active, "active");
lifecycle.ResignActive(); Check(lifecycle.State == AppleSceneState.ConnectedInactive, "inactive");
lifecycle.EnterBackground(); Check(lifecycle.State == AppleSceneState.Background, "background");
lifecycle.BecomeActive(); Check(lifecycle.State == AppleSceneState.Active, "foreground active");

TouchControllerState touch = new();
touch.Update(4, -4, true, false, true);
Check(touch.Horizontal == 1 && touch.Vertical == -1, "touch axes clamped");
Check(touch.Jump && !touch.Dash && touch.Grab, "touch buttons represented");
touch.Reset(); Check(touch.Horizontal == 0 && !touch.Jump, "touch reset");

string root = Path.Combine(Path.GetTempPath(), "celeste-ios-foundation-tests", Guid.NewGuid().ToString("N"));
try
{
    AtomicFileStore store = new(root);
    Check(Directory.Exists(root), "storage directory created");
    store.Write("0.celeste", "A"u8); Check(File.ReadAllText(Path.Combine(root, "0.celeste")) == "A", "first atomic write");
    store.Write("0.celeste", "B"u8); Check(File.ReadAllText(Path.Combine(root, "0.celeste")) == "B", "atomic replacement");
    try { store.Write("0.celeste", "C"u8, AtomicWriteFault.BeforeCommit); } catch (IOException) { }
    Check(File.ReadAllText(Path.Combine(root, "0.celeste")) == "B", "pre-commit failure preserves old file");
    Check(!Directory.EnumerateFiles(root, "*.tmp").Any(), "temporary files cleaned");
    Check(AtomicFileStore.IsApprovedLogicalName("settings.celeste"), "settings approved");
    Check(!AtomicFileStore.IsApprovedLogicalName("../0.celeste"), "traversal rejected");
    try { store.Write("../0.celeste", "x"u8); } catch (ArgumentException) { passed++; }
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

// Stable SDL finger IDs survive compact-array reordering. A vacated slot is
// reserved for its Released edge until the following update.
void StableMap(int[] previous, int[] active, int[] expected, string name)
{
    int[] result = new int[previous.Length];
    StableTouchSlotPolicy.Map(previous, active, result);
    Check(result.SequenceEqual(expected), name);
}

StableMap(new[] { -1, -1, -1, -1 }, new[] { 11 }, new[] { 11, -1, -1, -1 }, "stable one finger");
StableMap(new[] { -1, -1, -1, -1 }, new[] { 11, 22 }, new[] { 11, 22, -1, -1 }, "stable two fingers");
StableMap(new[] { 11, 22, -1, -1 }, new[] { 22 }, new[] { -2, 22, -1, -1 }, "array compaction preserves second finger");
StableMap(new[] { 11, 22, 33, -1 }, new[] { 33, 11 }, new[] { 11, -2, 33, -1 }, "middle release preserves outer IDs");
StableMap(new[] { 11, 22, 33, 44 }, new[] { 44, 22, 11 }, new[] { 11, 22, -2, 44 }, "reverse order stable");
StableMap(new[] { 11, 22, -1, -1 }, new[] { 22, 33 }, new[] { -2, 22, 33, -1 }, "new finger uses free slot");
StableMap(new[] { 11, 22, 33, 44 }, new[] { 22, 33, 44, 55 }, new[] { -2, 22, 33, 44 }, "full turnover preserves release edge");
StableMap(new[] { -1, 22, 33, 44 }, new[] { 22, 33, 44, 55 }, new[] { 55, 22, 33, 44 }, "vacated slot accepts new finger next update");
StableMap(new[] { 11, 22, -1, -1 }, Array.Empty<int>(), new[] { -2, -2, -1, -1 }, "cancellation releases every active ID");
StableMap(new[] { -1, -1, -1, -1 }, Array.Empty<int>(), new[] { -1, -1, -1, -1 }, "empty snapshot has no ghosts");
int[] collisionResult = new int[4];
StableTouchSlotPolicy.Map(new[] { 11, 22, 33, 44 }, new[] { 44, 33, 22, 11 }, collisionResult);
Check(collisionResult.Where(value => value >= 0).Distinct().Count() == 4, "stable mapping has no slot collision");

TouchPreferences defaults = TouchPreferences.Default;
Check(defaults.Visibility == TouchControlVisibility.Automatic, "touch default Automatic");
Check(defaults.Movement == TouchMovementMode.Fixed, "touch default Fixed");
Check(defaults.Grab == TouchGrabStyle.Toggle, "touch default Toggle Grab");
Check(defaults.Sliding == TouchSlideMode.Off, "touch default sliding Off");
Check(defaults.OpacityPercent == 70 && defaults.SizePercent == 100 && defaults.Haptics, "touch visual/haptic defaults");
Check(TouchPreferencePolicy.ParseVisibility("bad") == TouchControlVisibility.Automatic, "invalid visibility safe");
Check(TouchPreferencePolicy.ParseMovement(null) == TouchMovementMode.Fixed, "missing movement safe");
Check(TouchPreferencePolicy.ParseGrab("HoldButton") == TouchGrabStyle.HoldButton, "Hold Button parse");
Check(TouchPreferencePolicy.ParseSliding("JumpDash") == TouchSlideMode.JumpDash, "sliding parse");
Check(TouchPreferencePolicy.ParseOpacity("0") == 0, "zero opacity valid");
Check(TouchPreferencePolicy.ParseOpacity("15") == 70, "opacity steps validated");
Check(TouchPreferencePolicy.ParseSize("130") == 130, "maximum size valid");
Check(TouchPreferencePolicy.ParseSize("140") == 100, "invalid size safe");
Check(TouchPreferencePolicy.ParseHaptics("broken"), "invalid haptics defaults On");

SafeAreaMetrics touchSafe = new(0, 62, 20, 62);
TouchControlLayout touchLayout = TouchControlLayout.Create(956, 440, touchSafe, false, 1.0);
Check(touchLayout.HasNonOverlappingActionHitAreas(), "phone action geometry non-overlap");
Check(Math.Abs(touchLayout.Movement.Center.X - 168) < 0.01, "fixed movement safe-area position");
Check(Math.Abs(touchLayout.Jump.Radius - 46) < 0.01, "Jump reference radius");
Check(Math.Abs(touchLayout.Dash.Radius - 40) < 0.01, "Dash reference radius");
Check(touchLayout.Journal.X >= touchSafe.Left && touchLayout.Journal.Y >= touchSafe.Top,
    "Journal stays inside the safe-area origin");
Check(touchLayout.Journal.Right <= touchLayout.Width - touchSafe.Right &&
      touchLayout.Journal.Bottom <= touchLayout.Height - touchSafe.Bottom,
    "Journal stays inside the safe-area bounds");
TouchControlLayout small = TouchControlLayout.Create(956, 440, touchSafe, false, 0.7);
TouchControlLayout large = TouchControlLayout.Create(956, 440, touchSafe, false, 1.3);
Check(small.Jump.Radius < touchLayout.Jump.Radius && large.Jump.Radius > touchLayout.Jump.Radius, "70-130 size scales controls");
TouchControlLayout padLayout = TouchControlLayout.Create(1024, 768, new SafeAreaMetrics(24, 0, 20, 0), true, 1.0);
Check(Math.Abs(padLayout.Jump.Radius - 46 * 1.18) < 0.01, "iPad semantic multiplier");
Check(padLayout.HasNonOverlappingActionHitAreas(), "iPad geometry non-overlap");
TouchControlLayout largePad = TouchControlLayout.Create(1366, 1024, new SafeAreaMetrics(24, 0, 20, 0), true, 1.3);
Check(largePad.HasNonOverlappingActionHitAreas(), "large iPad geometry non-overlap");

TouchDirection[] directions =
{
    TouchDirection.East, TouchDirection.NorthEast, TouchDirection.North, TouchDirection.NorthWest,
    TouchDirection.West, TouchDirection.SouthWest, TouchDirection.South, TouchDirection.SouthEast,
};
TouchPoint[] directionPoints =
{
    new(60, 0), new(60, -60), new(0, -60), new(-60, -60),
    new(-60, 0), new(-60, 60), new(0, 60), new(60, 60),
};
for (int index = 0; index < directions.Length; index++)
    Check(TouchDirectionPolicy.Classify(directionPoints[index], 72, 0.18, 8, TouchDirection.Neutral) == directions[index], $"eight-way direction {directions[index]}");
Check(TouchDirectionPolicy.Classify(new TouchPoint(12, 0), 72, 0.18, 8, TouchDirection.East) == TouchDirection.Neutral, "0.18 radial deadzone");
double nearBoundary = 27 * Math.PI / 180;
TouchPoint boundaryPoint = new(60 * Math.Cos(nearBoundary), -60 * Math.Sin(nearBoundary));
Check(TouchDirectionPolicy.Classify(boundaryPoint, 72, 0.18, 8, TouchDirection.East) == TouchDirection.East, "8-degree direction retention");

TouchInteractionState interaction = new(touchLayout);
interaction.BeginFrame();
interaction.Apply(1, TouchPhase.Pressed, new TouchPoint(touchLayout.Movement.Center.X + 60, touchLayout.Movement.Center.Y), true);
Check(interaction.MoveX == 1 && interaction.MoveY == 0 && interaction.ActiveOwnerCount == 1, "fixed movement ownership");
interaction.Apply(2, TouchPhase.Pressed, touchLayout.Jump.Center, true);
Check(interaction.Jump && interaction.JumpPressed, "Jump down edge");
interaction.BeginFrame();
Check(interaction.Jump && !interaction.JumpPressed, "Jump held duration");
interaction.Apply(1, TouchPhase.Released, touchLayout.Movement.Center, true);
Check(interaction.MoveX == 0 && interaction.Jump, "crossed release preserves Jump");
interaction.Apply(2, TouchPhase.Released, touchLayout.Jump.Center, true);
Check(!interaction.Jump && interaction.JumpReleased && interaction.ActiveOwnerCount == 0, "Jump release edge");

interaction.Reset();
interaction.BeginFrame();
TouchPoint journalCenter = new(
    touchLayout.Journal.X + touchLayout.Journal.Width * 0.5,
    touchLayout.Journal.Y + touchLayout.Journal.Height * 0.5);
Check(interaction.Apply(21, TouchPhase.Pressed, journalCenter, true) == TouchHapticAction.None,
    "Journal press adds no haptic noise");
Check(interaction.Journal && interaction.JournalPressed, "Journal/Special down edge");
interaction.BeginFrame();
Check(interaction.Journal && !interaction.JournalPressed, "Journal/Special held duration");
interaction.Apply(22, TouchPhase.Pressed, touchLayout.Jump.Center, true);
Check(interaction.Journal && interaction.Jump, "Journal and action touches remain independent");
interaction.Apply(21, TouchPhase.Released, journalCenter, true);
Check(!interaction.Journal && interaction.JournalReleased && interaction.Jump,
    "Journal/Special release preserves another action");
interaction.Apply(22, TouchPhase.Released, touchLayout.Jump.Center, true);

interaction.Reset();
interaction.BeginFrame();
interaction.Apply(3, TouchPhase.Pressed, touchLayout.Dash.Center, true);
Check(interaction.Dash && interaction.DashPressed, "Dash one pressed edge");
interaction.BeginFrame();
interaction.Apply(3, TouchPhase.Moved, touchLayout.Dash.Center, true);
Check(interaction.Dash && !interaction.DashPressed, "held Dash does not repeat");
interaction.Apply(3, TouchPhase.Released, touchLayout.Dash.Center, true);
Check(!interaction.Dash, "Dash release");

interaction.Reset();
interaction.GrabStyle = TouchGrabStyle.Toggle;
Check(interaction.Apply(4, TouchPhase.Pressed, touchLayout.Grab.Center, true) == TouchHapticAction.GrabToggle, "Toggle Grab haptic edge");
Check(interaction.GrabActionPressed, "Toggle Grab first tap exposes action edge");
interaction.Apply(4, TouchPhase.Released, touchLayout.Grab.Center, true);
Check(interaction.Grab, "Toggle Grab latched");
interaction.BeginFrame();
Check(!interaction.GrabActionPressed, "Grab action edge lasts one frame");
interaction.Apply(5, TouchPhase.Pressed, touchLayout.Grab.Center, true);
Check(!interaction.Grab && interaction.GrabActionPressed, "Toggle Grab second tap clears and remains an action");
interaction.Reset();
interaction.GrabStyle = TouchGrabStyle.HoldButton;
interaction.Apply(6, TouchPhase.Pressed, touchLayout.Grab.Center, true);
Check(interaction.Grab && interaction.GrabActionPressed, "Hold Grab down and action edge");
interaction.Apply(6, TouchPhase.Released, touchLayout.Grab.Center, true);
Check(!interaction.Grab, "Hold Grab up");
interaction.Reset();
interaction.GrabStyle = TouchGrabStyle.ShoulderHold;
interaction.Apply(7, TouchPhase.Pressed, new TouchPoint(touchLayout.Shoulder.X + 10, touchLayout.Shoulder.Y + 10), true);
Check(interaction.Grab && interaction.GrabActionPressed, "Shoulder Hold down and action edge");
interaction.Apply(7, TouchPhase.Released, touchLayout.Shoulder.Contains(new TouchPoint(0, 0)) ? new TouchPoint(0, 0) : touchLayout.Jump.Center, true);
Check(!interaction.Grab, "Shoulder Hold up");

interaction.Reset();
interaction.SlideMode = TouchSlideMode.Off;
interaction.Apply(8, TouchPhase.Pressed, touchLayout.Jump.Center, true);
interaction.BeginFrame();
interaction.Apply(8, TouchPhase.Moved, touchLayout.Dash.Center, true);
Check(interaction.Jump && !interaction.Dash, "button sliding Off retains owner");
interaction.Reset();
interaction.SlideMode = TouchSlideMode.JumpDash;
interaction.Apply(9, TouchPhase.Pressed, touchLayout.Jump.Center, true);
interaction.BeginFrame();
interaction.Apply(9, TouchPhase.Moved, touchLayout.Dash.Center, true);
Check(!interaction.Jump && interaction.Dash && interaction.DashPressed && interaction.JumpReleased, "button sliding transfers one edge");

interaction.Reset();
interaction.GrabStyle = TouchGrabStyle.HoldButton;
interaction.Apply(10, TouchPhase.Pressed, new TouchPoint(touchLayout.Movement.Center.X + 60, touchLayout.Movement.Center.Y), true);
interaction.Apply(11, TouchPhase.Pressed, touchLayout.Jump.Center, true);
interaction.Apply(12, TouchPhase.Pressed, touchLayout.Dash.Center, true);
interaction.Apply(13, TouchPhase.Pressed, touchLayout.Grab.Center, true);
Check(interaction.ActiveOwnerCount == 4 && interaction.MoveX == 1 && interaction.Jump && interaction.Dash && interaction.Grab, "four-finger multitouch");
interaction.ReleaseMissingOwners(new[] { 10, 12, 13 });
Check(!interaction.Jump && interaction.MoveX == 1 && interaction.Dash && interaction.Grab, "missing middle owner releases only itself");
interaction.ReleaseMissingOwners(Array.Empty<int>());
Check(interaction.ActiveOwnerCount == 0 && interaction.MoveX == 0 && !interaction.Dash && !interaction.Grab,
    "touch cancellation releases every remaining owner");
interaction.Reset();
Check(interaction.ActiveOwnerCount == 0 && !interaction.Jump && !interaction.Dash && !interaction.Grab, "lifecycle reset clears all state");
interaction.GrabStyle = TouchGrabStyle.Toggle;
interaction.Apply(14, TouchPhase.Pressed, touchLayout.Grab.Center, true);
Check(interaction.Grab, "toggle Grab can latch before lifecycle boundary");
interaction.UpdateLayout(touchLayout);
Check(!interaction.Grab, "layout or preference boundary clears Toggle Grab");
interaction.Apply(14, TouchPhase.Pressed, touchLayout.Grab.Center, true);
Check(interaction.Grab, "Toggle Grab can relatch after layout update");
interaction.Reset();
Check(!interaction.Grab, "controller or lifecycle reset clears Toggle Grab");

interaction = new TouchInteractionState(touchLayout) { MovementMode = TouchMovementMode.Floating };
TouchPoint floatingOrigin = new(touchLayout.MovementAcquisition.X + 100, touchLayout.MovementAcquisition.Y + 100);
interaction.Apply(20, TouchPhase.Pressed, floatingOrigin, true);
Check(interaction.MoveX == 0 && interaction.MoveY == 0, "floating touch begins neutral");
interaction.Apply(20, TouchPhase.Moved, new TouchPoint(floatingOrigin.X + 60, floatingOrigin.Y), true);
Check(interaction.MoveX == 1, "floating drag moves east");
interaction.Apply(20, TouchPhase.Released, floatingOrigin, true);
Check(interaction.MoveX == 0, "floating release clears movement");

Check(TouchVisibilityPolicy.IsVisible(TouchControlVisibility.Automatic, false), "Automatic without controller");
Check(!TouchVisibilityPolicy.IsVisible(TouchControlVisibility.Automatic, true), "Automatic hides for controller");
Check(TouchVisibilityPolicy.IsVisible(TouchControlVisibility.Always, true), "Always coexists");
Check(!TouchVisibilityPolicy.IsVisible(TouchControlVisibility.Off, false), "Off hidden");
Check(TouchVisibilityPolicy.NeedsRecovery(TouchControlVisibility.Off, false, false), "Off recovery affordance");
Check(!TouchVisibilityPolicy.NeedsRecovery(TouchControlVisibility.Off, false, true), "Off editing remains usable");
Check(TouchHapticsPolicy.ForPress(TouchOwnedControl.Jump, TouchGrabStyle.Toggle, true) == TouchHapticAction.Jump, "Jump haptic policy");
Check(TouchHapticsPolicy.ForPress(TouchOwnedControl.Movement, TouchGrabStyle.Toggle, true) == TouchHapticAction.None, "movement has no haptic spam");
Check(TouchHapticsPolicy.ForPress(TouchOwnedControl.Grab, TouchGrabStyle.HoldButton, true) == TouchHapticAction.None, "Hold Grab has no toggle haptic");
Check(!TouchGrabPolicy.Resolve(false, true, false, true), "touch-only Grab ignores controller Invert");
Check(TouchGrabPolicy.Resolve(true, true, false, false), "touch-only Grab supplies final held state");
Check(TouchGrabPolicy.Resolve(false, true, true, true), "controller Grab retained during coexistence");
Check(TouchGrabPolicy.Resolve(true, true, true, false), "touch Grab combines during coexistence");
Check(!TouchGrabPolicy.Resolve(false, false, true, false), "controller-only neutral Grab remains neutral");

Check(AppleControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.Automatic, "xb1", AppleControllerFamily.PlayStation) == "ps4", "iOS DualSense Automatic PlayStation");
Check(AppleControllerPromptPolicy.ResolvePrefix(ControllerPromptMode.NintendoSwitch, "xb1", AppleControllerFamily.PlayStation) == "ns", "manual prompt family shared");
Check(AppleControllerPromptPolicy.SelectAppleFamily(new[]
{
    new ControllerCandidate(false, true, AppleControllerFamily.Xbox, 0),
    new ControllerCandidate(true, true, AppleControllerFamily.PlayStation, 1),
}) == AppleControllerFamily.PlayStation, "current Apple controller family wins");

// Stage 24D3 keeps the D2 factory geometry but stores editable hit regions in
// normalized full-screen coordinates. Phone and tablet values are independent.
TouchLayoutProfile d3Phone = TouchLayoutPolicy.Factory(956, 440, touchSafe, false);
TouchLayoutProfile d3Tablet = TouchLayoutPolicy.Factory(1024, 768, new SafeAreaMetrics(24, 0, 20, 0), true);
Check(TouchLayoutPolicy.Validate(d3Phone, 956, 440).IsValid, "D3 factory phone validates");
Check(TouchLayoutPolicy.Validate(d3Tablet, 1024, 768).IsValid, "D3 factory tablet validates");
Check(TouchLayoutPolicy.Validate(d3Phone with { ActionLayout = TouchActionLayout.SplitRegion }, 956, 440).IsValid,
    "factory Split Region validates");
Check(TouchLayoutPolicy.Validate(d3Phone with { MovementMode = TouchMovementMode.Floating }, 956, 440).IsValid,
    "factory Floating region validates");
TouchRect fittedFactoryRegion = TouchLayoutPolicy.Denormalize(d3Phone.FloatingRegion,
    TouchLayoutPolicy.FullCanvas(956, 440));
Check(fittedFactoryRegion.X >= touchSafe.Left && fittedFactoryRegion.Right <= 956 - touchSafe.Right &&
      fittedFactoryRegion.Y >= touchSafe.Top && fittedFactoryRegion.Bottom <= 440 - touchSafe.Bottom,
    "factory inactive Floating region is inside real iPhone safe area");
TouchRuntimeLayout d3Runtime = TouchLayoutPolicy.Materialize(d3Phone, 956, 440, touchSafe);
Check(Math.Abs(d3Runtime.Jump.Radius - touchLayout.Jump.Radius) < 0.001, "D3 factory preserves D2 Jump size");
Check(Math.Abs(d3Runtime.Movement.Center.X - touchLayout.Movement.Center.X) < 0.001, "D3 factory preserves D2 movement position");
Check(Math.Abs(d3Runtime.Movement.Radius / d3Runtime.Movement.HitRadius -
      TouchLayoutPolicy.VisualRatio(d3Phone, new TouchLayoutSelection(TouchLayoutControl.Movement))) < 0.000001,
    "editor and gameplay share the exact phone movement visual ratio");
TouchRuntimeLayout d3TabletRuntime = TouchLayoutPolicy.Materialize(
    d3Tablet, 1024, 768, new SafeAreaMetrics(24, 0, 20, 0));
Check(Math.Abs(d3TabletRuntime.Movement.Radius / d3TabletRuntime.Movement.HitRadius -
      TouchLayoutPolicy.VisualRatio(d3Tablet, new TouchLayoutSelection(TouchLayoutControl.Movement))) < 0.000001,
    "editor and gameplay share the exact tablet movement visual ratio");
Check(TouchLayoutCodec.TryDecode(TouchLayoutCodec.Encode(d3Phone), out TouchLayoutProfile decodedPhone) && decodedPhone == d3Phone,
    "layout serialization round trip");
Check(!TouchLayoutCodec.TryDecode(null, out _), "missing layout rejected");
Check(!TouchLayoutCodec.TryDecode("D3|2|bad", out _), "unknown layout version rejected");
Check(!TouchLayoutCodec.TryDecode(TouchLayoutCodec.Encode(d3Phone).Replace("0.", "NaN", StringComparison.Ordinal), out _),
    "non-finite layout rejected");
Check(TouchLayoutCodec.Encode(d3Phone) != TouchLayoutCodec.Encode(d3Tablet), "phone/tablet profiles remain independent");

string encodedV2 = TouchLayoutCodec.Encode(d3Phone);
string legacyV1 = "D3|1|" + string.Join("|", encodedV2["D3|2|".Length..].Split('|').Take(14));
Check(TouchLayoutCodec.TryDecode(legacyV1, out TouchLayoutProfile legacyDecoded, out bool legacyCoordinates) && legacyCoordinates,
    "schema-v1 safe-area layout remains decodable for migration");
TouchLayoutProfile rebasedLegacy = TouchLayoutPolicy.RebaseFromSafeArea(legacyDecoded, 956, 440, touchSafe);
Check(rebasedLegacy.SchemaVersion == TouchLayoutProfile.CurrentSchemaVersion &&
      TouchLayoutPolicy.IsStructurallyValid(rebasedLegacy),
    "schema-v1 layout rebases into schema-v2 full-screen coordinates");

TouchLayoutProfile edgeLayout = d3Phone with
{
    JournalEnabled = false,
    Pause = d3Phone.Pause with { X = 0, Y = 0 },
};
Check(TouchLayoutPolicy.Validate(edgeLayout, 956, 440).IsValid && edgeLayout.Pause.X < touchSafe.Left / 956.0,
    "controls may use the physical screen outside the Celeste safe/content bounds");
TouchRect flushCircleRect = TouchLayoutEditorGeometry.Move(
    d3Phone.Jump, new TouchPoint(-4000, 0), 956, 440, true, TouchLayoutPolicy.JumpVisualRatio);
TouchRuntimeLayout flushCircleRuntime = TouchLayoutPolicy.Materialize(
    d3Phone with { Jump = flushCircleRect }, 956, 440, touchSafe);
Check(Math.Abs(flushCircleRuntime.Jump.Center.X - flushCircleRuntime.Jump.Radius) < 0.001 &&
      flushCircleRuntime.Jump.HitRadius > flushCircleRuntime.Jump.Center.X,
    "visible circular control can sit flush to the display while its larger hit margin is clipped");
Check(TouchLayoutPolicy.IsStructurallyValid(d3Phone with { Jump = flushCircleRect }),
    "bounded off-screen hit-circle coordinates remain persistable");

TouchLayoutProfile opacityLayout = d3Phone.WithOpacity(TouchLayoutControl.Movement, 30)
    .WithOpacity(TouchLayoutControl.Journal, 0);
Check(TouchLayoutCodec.TryDecode(TouchLayoutCodec.Encode(opacityLayout), out TouchLayoutProfile opacityRoundTrip) &&
      opacityRoundTrip.OpacityFor(TouchLayoutControl.Movement) == 30 &&
      opacityRoundTrip.OpacityFor(TouchLayoutControl.Journal) == 0,
    "individual primary-control opacity round trip includes fully transparent runtime controls");
TouchLayoutProfile invalidOpacity = d3Phone.WithOpacity(TouchLayoutControl.Dash, 35);
Check(!TouchLayoutPolicy.Validate(invalidOpacity, 956, 440).IsValid &&
      !TouchLayoutCodec.TryDecode(TouchLayoutCodec.Encode(invalidOpacity), out _),
    "individual opacity is bounded to deterministic ten-percent steps");

TouchLayoutEditorSession editor = new(d3Phone, d3Phone);
TouchRect originalJump = editor.Working.Jump;
editor.BeginGesture();
editor.PreviewRect(TouchLayoutControl.Jump, originalJump with { X = originalJump.X - 0.03 });
editor.EndGesture();
Check(editor.CanUndo && editor.UndoCount == 1, "editor move creates one Undo unit");
Check(editor.Undo() && editor.Working == d3Phone, "editor Undo restores prior layout");
editor.BeginGesture();
editor.PreviewRect(TouchLayoutControl.Jump, originalJump with { Width = originalJump.Width * 0.9, Height = originalJump.Height * 0.9 });
editor.EndGesture();
Check(editor.Working.Jump.Width < originalJump.Width, "editor resize is independent");
Check(editor.Original == d3Phone, "editor Cancel source remains immutable");
editor.ResetSelected(TouchLayoutControl.Jump);
Check(editor.Working.Jump == d3Phone.Jump, "Reset Selected restores factory geometry");
editor.Change(editor.Working with { GrabShape = TouchGrabShape.Rectangle });
Check(editor.Working.GrabShape == TouchGrabShape.Rectangle, "Grab rectangle editor state");
editor.ResetLayout();
Check(editor.Working == d3Phone, "Reset Layout restores full factory profile");
TouchLayoutProfile nonFactoryModes = d3Phone with
{
    MovementMode = TouchMovementMode.Floating,
    ActionLayout = TouchActionLayout.SplitRegion,
    Sliding = TouchSlideMode.JumpDash,
    SplitOrientation = TouchSplitOrientation.Horizontal,
};
TouchLayoutEditorSession preservingReset = new(nonFactoryModes, d3Phone);
preservingReset.ResetLayout();
Check(preservingReset.Working.MovementMode == TouchMovementMode.Floating &&
      preservingReset.Working.ActionLayout == TouchActionLayout.SplitRegion &&
      preservingReset.Working.Sliding == TouchSlideMode.JumpDash,
    "Factory geometry preserves Movement, Action Layout, and Sliding Options");
Check(preservingReset.Working.SplitRegion == d3Phone.SplitRegion,
    "Factory reset still restores split geometry");

TouchRect resizeStart = new(0.20, 0.20, 0.20, 0.20);
TouchRect resizedRectangle = TouchLayoutEditorGeometry.Resize(
    resizeStart, new TouchPoint(100, 20), 832, 420, false);
Check(resizedRectangle.Width > resizeStart.Width && resizedRectangle.Height > resizeStart.Height &&
      Math.Abs((resizedRectangle.Width - resizeStart.Width) - (resizedRectangle.Height - resizeStart.Height)) > 0.01,
    "rectangle corner independently changes width and height");
TouchRect resizedCircle = TouchLayoutEditorGeometry.Resize(
    resizeStart, new TouchPoint(100, 20), 832, 420, true);
Check(Math.Abs(resizedCircle.Width * 832 - resizedCircle.Height * 420) < 0.000001,
    "circle corner resize preserves a physical circle");
editor.Mirror();
TouchLayoutProfile mirrored = editor.Working;
Check(Math.Abs(mirrored.Movement.X - (1 - d3Phone.Movement.Right)) < 0.000001, "Mirror Layout reflects movement");
TouchLayoutProfile unmirrored = TouchLayoutPolicy.Mirror(mirrored);
Check(unmirrored.SplitOrientation == d3Phone.SplitOrientation &&
      Math.Abs(unmirrored.Movement.X - d3Phone.Movement.X) < 0.000000001 &&
      Math.Abs(unmirrored.Jump.X - d3Phone.Jump.X) < 0.000000001,
    "Mirror Layout is reversible");
Check(editor.TryCommit(956, 440, out TouchLayoutProfile committed) && committed == mirrored, "valid Done commits working profile");

TouchLayoutProfile offscreen = d3Phone with { Jump = d3Phone.Jump with { X = 0.99 } };
Check(!TouchLayoutPolicy.Validate(offscreen, 956, 440).IsValid, "off-screen control rejected");
TouchLayoutProfile tooSmall = d3Phone with { Jump = d3Phone.Jump with { Width = 0.001, Height = 0.001 } };
Check(!TouchLayoutPolicy.Validate(tooSmall, 956, 440).IsValid, "minimum action size enforced");
TouchLayoutProfile overlap = d3Phone with { Dash = d3Phone.Jump };
Check(!TouchLayoutPolicy.Validate(overlap, 956, 440).IsValid, "ambiguous action overlap rejected");
TouchLayoutEditorSession invalidEditor = new(d3Phone, d3Phone);
invalidEditor.Change(overlap);
Check(!invalidEditor.TryCommit(956, 440, out TouchLayoutProfile failedCommit) && failedCommit == d3Phone,
    "invalid Done fails closed to original");

Check(TouchLayoutPolicy.TryAddExtra(
        d3Phone, TouchExtraControlKind.Grab, TouchGrabShape.Rectangle, d3Phone.Grab,
        956, 440, out TouchLayoutProfile oneExtraGrab, out int firstGrabIndex),
    "editor can add one bounded duplicate Grab control");
Check(TouchLayoutPolicy.TryAddExtra(
        oneExtraGrab, TouchExtraControlKind.Grab, TouchGrabShape.Rectangle, d3Phone.Grab,
        956, 440, out TouchLayoutProfile twoExtraGrabs, out int secondGrabIndex),
    "editor can add a second bounded duplicate Grab control");
Check(firstGrabIndex != secondGrabIndex && TouchLayoutPolicy.Validate(twoExtraGrabs, 956, 440).IsValid,
    "duplicate controls remain distinct and non-overlapping");
TouchLayoutProfile transparentExtra = twoExtraGrabs.WithExtra(firstGrabIndex,
    twoExtraGrabs.Extra(firstGrabIndex) with { OpacityPercent = 20 });
Check(TouchLayoutCodec.TryDecode(TouchLayoutCodec.Encode(transparentExtra), out TouchLayoutProfile extraOpacityRoundTrip) &&
      extraOpacityRoundTrip.Extra(firstGrabIndex).OpacityPercent == 20,
    "individual duplicate-control opacity round trip");

TouchRuntimeLayout duplicateGrabRuntime = TouchLayoutPolicy.Materialize(twoExtraGrabs, 956, 440, touchSafe);
TouchRuntimeExtraControl firstGrab = duplicateGrabRuntime.Extra(firstGrabIndex);
TouchRuntimeExtraControl secondGrab = duplicateGrabRuntime.Extra(secondGrabIndex);
TouchPoint firstGrabCenter = new(firstGrab.Rect.X + firstGrab.Rect.Width * 0.5,
    firstGrab.Rect.Y + firstGrab.Rect.Height * 0.5);
TouchPoint secondGrabCenter = new(secondGrab.Rect.X + secondGrab.Rect.Width * 0.5,
    secondGrab.Rect.Y + secondGrab.Rect.Height * 0.5);
CustomTouchInteractionState duplicateGrabState = new(duplicateGrabRuntime) { GrabMode = AppleGrabMode.Toggle };
duplicateGrabState.BeginFrame();
duplicateGrabState.Apply(70, TouchPhase.Pressed, firstGrabCenter, true);
Check(duplicateGrabState.Grab && duplicateGrabState.GrabActionPressed,
    "first duplicate Grab owner emits one logical toggle edge");
duplicateGrabState.BeginFrame();
duplicateGrabState.Apply(71, TouchPhase.Pressed, secondGrabCenter, true);
Check(duplicateGrabState.Grab && !duplicateGrabState.GrabActionPressed,
    "second simultaneous Grab owner does not double-toggle");
duplicateGrabState.BeginFrame();
duplicateGrabState.Apply(70, TouchPhase.Released, firstGrabCenter, true);
Check(duplicateGrabState.Grab && !duplicateGrabState.GrabReleased,
    "releasing one duplicate Grab preserves the remaining owner");
duplicateGrabState.BeginFrame();
duplicateGrabState.Apply(71, TouchPhase.Released, secondGrabCenter, true);
Check(!duplicateGrabState.Grab && duplicateGrabState.GrabReleased,
    "logical Grab releases only when its final owner releases");

TouchLayoutEditorSession extraEditor = new(twoExtraGrabs, d3Phone);
extraEditor.SetOpacity(new TouchLayoutSelection(default, firstGrabIndex), 40);
Check(extraEditor.Working.Extra(firstGrabIndex).OpacityPercent == 40,
    "editor changes one duplicate opacity without affecting peers");
Check(extraEditor.Delete(new TouchLayoutSelection(default, firstGrabIndex)) &&
      !extraEditor.Working.Extra(firstGrabIndex).Enabled && extraEditor.Working.Extra(secondGrabIndex).Enabled,
    "editor deletes only the selected duplicate control");
Check(!extraEditor.Delete(new TouchLayoutSelection(TouchLayoutControl.Movement)) &&
      !extraEditor.Delete(new TouchLayoutSelection(TouchLayoutControl.Pause)),
    "editor cannot delete essential Movement or Pause controls");
Check(extraEditor.Delete(new TouchLayoutSelection(TouchLayoutControl.Journal)) &&
      !extraEditor.Working.JournalEnabled,
    "default Journal control is optional and deletable");

TouchLayoutProfile fourExtras = d3Phone;
for (int index = 0; index < TouchLayoutProfile.MaximumExtraControls; index++)
{
    Check(TouchLayoutPolicy.TryAddExtra(fourExtras, TouchExtraControlKind.CrouchDash,
            TouchGrabShape.Rectangle, d3Phone.Dash, 956, 440, out TouchLayoutProfile added, out _),
        $"bounded optional-control slot {index + 1} can be populated");
    fourExtras = added;
}
Check(!TouchLayoutPolicy.TryAddExtra(fourExtras, TouchExtraControlKind.Jump,
        TouchGrabShape.Circle, d3Phone.Jump, 956, 440, out _, out _),
    "fifth optional control is rejected by the bounded schema");
TouchRuntimeLayout crouchRuntime = TouchLayoutPolicy.Materialize(fourExtras, 956, 440, touchSafe);
TouchRect crouchRect = crouchRuntime.Extra(0).Rect;
TouchPoint crouchCenter = new(crouchRect.X + crouchRect.Width * 0.5,
    crouchRect.Y + crouchRect.Height * 0.5);
CustomTouchInteractionState crouchState = new(crouchRuntime);
crouchState.BeginFrame();
Check(crouchState.Apply(72, TouchPhase.Pressed, crouchCenter, true) == TouchHapticAction.Dash &&
      crouchState.CrouchDash && crouchState.CrouchDashPressed,
    "optional Crouch Dash emits its dedicated logical input and Dash haptic");
crouchState.BeginFrame();
crouchState.Apply(72, TouchPhase.Released, crouchCenter, true);
Check(!crouchState.CrouchDash && crouchState.CrouchDashReleased,
    "optional Crouch Dash releases normally");
Check(TouchLayoutPolicy.TryAddExtra(
        d3Phone, TouchExtraControlKind.QuickRestart, TouchGrabShape.Rectangle, d3Phone.Dash,
        956, 440, out TouchLayoutProfile restartProfile, out int restartIndex),
    "editor can add the distinct Quick Restart action");
TouchRuntimeLayout restartRuntime = TouchLayoutPolicy.Materialize(restartProfile, 956, 440, touchSafe);
TouchRect restartRect = restartRuntime.Extra(restartIndex).Rect;
TouchPoint restartCenter = new(restartRect.X + restartRect.Width * 0.5,
    restartRect.Y + restartRect.Height * 0.5);
CustomTouchInteractionState restartState = new(restartRuntime);
restartState.BeginFrame();
restartState.Apply(73, TouchPhase.Pressed, restartCenter, true);
Check(restartState.QuickRestart && restartState.QuickRestartPressed,
    "optional Quick Restart emits its dedicated logical input");
restartState.BeginFrame();
restartState.Apply(73, TouchPhase.Released, restartCenter, true);
Check(!restartState.QuickRestart && restartState.QuickRestartReleased,
    "optional Quick Restart releases normally");

TouchRect splitRect = new(100, 100, 200, 120);
TouchPoint exactCenter = new(200, 160);
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.TopLeftToBottomRight, true, exactCenter) == TouchOwnedControl.Jump,
    "TL-BR exact split line belongs to first half");
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.TopRightToBottomLeft, true, exactCenter) == TouchOwnedControl.Jump,
    "TR-BL exact split line belongs to first half");
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.Vertical, true, exactCenter) == TouchOwnedControl.Jump,
    "vertical exact split line belongs to first half");
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.Horizontal, true, exactCenter) == TouchOwnedControl.Jump,
    "horizontal exact split line belongs to first half");
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.Vertical, false, new TouchPoint(120, 160)) == TouchOwnedControl.Dash,
    "split Jump/Dash assignment swaps");
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.Horizontal, true, new TouchPoint(200, 210)) == TouchOwnedControl.Dash,
    "horizontal second half resolves Dash");
Check(TouchLayoutPolicy.SplitControl(splitRect, TouchSplitOrientation.Vertical, true, new TouchPoint(201, 160), TouchOwnedControl.Jump, 4) == TouchOwnedControl.Jump,
    "split transfer hysteresis preserves ownership near line");

TouchLayoutProfile splitProfile = d3Phone with
{
    ActionLayout = TouchActionLayout.SplitRegion,
    SplitRegion = TouchLayoutPolicy.Normalize(splitRect, new TouchRect(0, 0, 956, 440)),
};
TouchRuntimeLayout splitRuntime = TouchLayoutPolicy.Materialize(splitProfile, 956, 440, new SafeAreaMetrics(0, 0, 0, 0));
CustomTouchInteractionState splitInteraction = new(splitRuntime);
TouchPoint firstSplit = new(240, 125);
TouchPoint secondSplit = new(130, 205);
splitInteraction.Apply(50, TouchPhase.Pressed, firstSplit, true);
Check(splitInteraction.Jump && splitInteraction.JumpPressed, "Split Region first half presses Jump");
splitInteraction.BeginFrame();
splitInteraction.Apply(50, TouchPhase.Moved, secondSplit, true);
Check(splitInteraction.Jump && !splitInteraction.Dash, "Split sliding Off retains owner");
splitInteraction.Reset();
splitProfile = splitProfile with { Sliding = TouchSlideMode.JumpDash };
splitRuntime = TouchLayoutPolicy.Materialize(splitProfile, 956, 440, new SafeAreaMetrics(0, 0, 0, 0));
splitInteraction = new CustomTouchInteractionState(splitRuntime);
splitInteraction.Apply(51, TouchPhase.Pressed, firstSplit, true);
splitInteraction.BeginFrame();
splitInteraction.Apply(51, TouchPhase.Moved, secondSplit, true);
Check(!splitInteraction.Jump && splitInteraction.Dash && splitInteraction.DashPressed, "Split sliding On transfers edge");

TouchLayoutProfile rectangleGrab = d3Phone with { GrabShape = TouchGrabShape.Rectangle };
TouchRuntimeLayout rectangleRuntime = TouchLayoutPolicy.Materialize(rectangleGrab, 956, 440, touchSafe);
CustomTouchInteractionState customInteraction = new(rectangleRuntime) { GrabMode = AppleGrabMode.Hold };
TouchPoint rectangleCenter = new(rectangleRuntime.GrabRect.X + rectangleRuntime.GrabRect.Width * 0.5,
    rectangleRuntime.GrabRect.Y + rectangleRuntime.GrabRect.Height * 0.5);
customInteraction.Apply(52, TouchPhase.Pressed, rectangleCenter, true);
Check(customInteraction.Grab && customInteraction.GrabActionPressed, "rectangular Grab hit and icon state source");
customInteraction.Apply(52, TouchPhase.Released, rectangleCenter, true);
Check(!customInteraction.Grab, "rectangular Grab releases");

TouchPreferences migrateToggle = TouchPreferences.Default with { SizePercent = 130, Movement = TouchMovementMode.Floating };
TouchD2MigrationResult toggleMigration = TouchD2MigrationPolicy.Migrate(956, 440, touchSafe, false, migrateToggle, AppleGrabMode.Invert);
Check(toggleMigration.GrabModes.Touch == AppleGrabMode.Toggle && toggleMigration.GrabModes.Controller == AppleGrabMode.Invert,
    "D2 Toggle migration and controller preservation");
Check(toggleMigration.Layout.MovementMode == TouchMovementMode.Floating &&
      TouchLayoutPolicy.Materialize(toggleMigration.Layout, 956, 440, touchSafe).Jump.Radius > touchLayout.Jump.Radius,
    "D2 movement and global size bake into D3 layout");
TouchPreferences migrateHold = TouchPreferences.Default with { Grab = TouchGrabStyle.HoldButton };
Check(TouchD2MigrationPolicy.Migrate(956, 440, touchSafe, false, migrateHold, AppleGrabMode.Hold).GrabModes.Touch == AppleGrabMode.Hold,
    "D2 Hold Button migrates to Touch Hold");
TouchPreferences migrateShoulder = TouchPreferences.Default with { Grab = TouchGrabStyle.ShoulderHold };
TouchD2MigrationResult shoulderMigration = TouchD2MigrationPolicy.Migrate(956, 440, touchSafe, false, migrateShoulder, AppleGrabMode.Hold);
Check(shoulderMigration.GrabModes.Touch == AppleGrabMode.Hold && shoulderMigration.Layout.GrabShape == TouchGrabShape.Rectangle,
    "D2 Shoulder Hold migrates to Hold plus rectangle");

GrabSourceArbiter grab = new(new GrabModeProfiles(AppleGrabMode.Toggle, AppleGrabMode.Hold, AppleGrabMode.Invert), AppleInputSource.Touch);
grab.Observe(AppleInputSource.Touch, true, true, true);
Check(grab.Effective && grab.ActiveMode == AppleGrabMode.Toggle, "Touch Toggle latches");
grab.Observe(AppleInputSource.Touch, false, false, false);
Check(grab.Effective, "Touch Toggle survives release");
grab.Observe(AppleInputSource.Controller, true, true, true);
Check(grab.ActiveSource == AppleInputSource.Controller && grab.Effective, "meaningful controller switches to Hold");
grab.Observe(AppleInputSource.Controller, false, false, false);
Check(!grab.Effective, "Controller Hold follows raw release");
grab.Observe(AppleInputSource.Keyboard, false, false, true);
Check(grab.ActiveSource == AppleInputSource.Keyboard && grab.Effective, "Keyboard Invert active while idle");
grab.Observe(AppleInputSource.Keyboard, true, true, true);
Check(!grab.Effective, "Keyboard Invert releases while held");
grab.Observe(AppleInputSource.Touch, false, false, false);
Check(grab.ActiveSource == AppleInputSource.Keyboard, "idle touch does not steal active source");
grab.ForceSource(AppleInputSource.Touch);
Check(grab.ActiveSource == AppleInputSource.Touch && !grab.Effective, "source switch clears stale Toggle latch");
grab.SetMode(AppleInputSource.Touch, AppleGrabMode.Invert);
Check(grab.Effective, "Touch Invert visually active by default");
grab.Observe(AppleInputSource.Touch, true, true, true);
Check(!grab.Effective, "Touch Invert visually releases while pressed");
grab.SetMode(AppleInputSource.Touch, AppleGrabMode.Hold);
Check(grab.Effective && grab.Profiles.Controller == AppleGrabMode.Hold && grab.Profiles.Keyboard == AppleGrabMode.Invert,
    "per-source Grab profile edit is isolated");
grab.ResetTransient();
Check(!grab.Effective, "lifecycle clears Grab transients");
Check(GrabSourceVisibilityPolicy.GameplayValue(AppleInputSource.Controller, false, true),
    "controller Grab remains active while Automatic hides the touch overlay");
Check(GrabSourceVisibilityPolicy.GameplayValue(AppleInputSource.Keyboard, false, true),
    "keyboard Grab remains active while the touch overlay is hidden");
Check(!GrabSourceVisibilityPolicy.GameplayValue(AppleInputSource.Touch, false, true),
    "hidden touch cannot leave Invert Grab permanently active");
Check(GrabSourceVisibilityPolicy.GameplayValue(AppleInputSource.Touch, true, true),
    "visible touch still supplies effective Grab");

Check(DirectionalHapticPolicy.ShouldPulse(TouchDirection.Neutral, TouchDirection.West, true),
    "directional haptic pulses on first direction");
Check(DirectionalHapticPolicy.ShouldPulse(TouchDirection.West, TouchDirection.NorthWest, true),
    "directional haptic pulses on sector transition");
Check(!DirectionalHapticPolicy.ShouldPulse(TouchDirection.West, TouchDirection.West, true),
    "directional haptic does not repeat while held");
Check(!DirectionalHapticPolicy.ShouldPulse(TouchDirection.West, TouchDirection.Neutral, true),
    "directional haptic stays quiet on neutral release");
Check(!DirectionalHapticPolicy.ShouldPulse(TouchDirection.West, TouchDirection.NorthWest, false),
    "directional haptic preference Off preserves D2 feel");

string encodedPhone = TouchLayoutCodec.Encode(d3Phone);
string encodedTablet = TouchLayoutCodec.Encode(d3Tablet);
TouchLayoutShareDocument sharedLayouts = new(encodedPhone, encodedTablet);
byte[] sharedBytes = sharedLayouts.Encode();
Check(sharedBytes.Length < TouchLayoutShareDocument.MaximumBytes, "touch share document bounded");
Check(TouchLayoutShareDocument.TryDecode(sharedBytes, out TouchLayoutShareDocument decodedLayouts),
    "touch share document decodes");
Check(decodedLayouts.PhoneProfile == encodedPhone && decodedLayouts.TabletProfile == encodedTablet,
    "touch share document preserves exact D3 profiles");
Check(TouchLayoutCodec.TryDecode(decodedLayouts.PhoneProfile, out TouchLayoutProfile sharedPhone) &&
      sharedPhone == d3Phone, "touch share reuses exact D3 codec");
Check(!TouchLayoutShareDocument.TryDecode(Array.Empty<byte>(), out _), "empty touch share rejected");
Check(!TouchLayoutShareDocument.TryDecode(new byte[TouchLayoutShareDocument.MaximumBytes + 1], out _),
    "oversized touch share rejected before JSON parse");
Check(!TouchLayoutShareDocument.TryDecode("{\"format\":\"Celeste Touch Layout\",\"version\":2,\"phoneProfile\":\"x\"}"u8.ToArray(), out _),
    "unsupported touch share version rejected");
Check(!TouchLayoutShareDocument.TryDecode("{\"format\":\"Celeste Touch Layout\",\"version\":1}"u8.ToArray(), out _),
    "touch share missing profiles rejected");
Check(!TouchLayoutShareDocument.TryDecode("{\"format\":\"Celeste Touch Layout\",\"version\":1,\"phoneProfile\":\"D3|2|NaN\"}"u8.ToArray(), out _),
    "invalid D3 profile rejected");
byte[] unknownPropertyDocument = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new
{
    format = "Celeste Touch Layout",
    version = 1,
    phoneProfile = encodedPhone,
    extra = 1,
});
Check(!TouchLayoutShareDocument.TryDecode(unknownPropertyDocument, out _),
    "unknown touch share property rejected");
Check(TouchLayoutShareDocument.Extension == "celestetouch" &&
      TouchLayoutShareDocument.TypeIdentifier == "io.github.roootthefox.celeste.touch-layout",
    "touch share extension and UTI fixed");

IOSFilePortabilityBridge.Clear();
Check(!IOSFilePortabilityBridge.IsAvailable, "Files bridge unavailable until all host delegates register");
Check(!IOSFilePortabilityBridge.RequestImport(IOSPortableDocumentKind.CelesteLogicalFile, 10, _ => { }),
    "Files bridge fails closed without a host");
bool importCalled = false;
bool exportCalled = false;
IOSFilePortabilityBridge.ImportRequested = (kind, maximum, completed) =>
{
    importCalled = kind == IOSPortableDocumentKind.TouchLayout && maximum == 12;
    completed(IOSExternalReadResult.Success(new byte[] { 1 }));
};
IOSFilePortabilityBridge.ExportRequested = (documents, share, completed) =>
{
    exportCalled = documents.Count == 1 && share;
    completed(true, null);
};
Check(IOSFilePortabilityBridge.IsAvailable, "Files bridge available only with complete host surface");
Check(IOSFilePortabilityBridge.RequestImport(IOSPortableDocumentKind.TouchLayout, 12,
      result => importCalled &= result.Data is { Length: 1 }) && importCalled,
    "Files bridge forwards bounded import and copied bytes");
Check(IOSFilePortabilityBridge.RequestExport(
      new[] { new IOSPortableDocument("x.celestetouch", IOSPortableDocumentKind.TouchLayout, new byte[] { 1 }) },
      true, (_, _) => { }) && exportCalled,
    "Files bridge forwards explicit share request");
IOSFilePortabilityBridge.Clear();
Check(!IOSFilePortabilityBridge.IsAvailable, "Files bridge clears every process-owned delegate");

Console.WriteLine($"PASS: modern iOS foundation deterministic tests {passed}");
