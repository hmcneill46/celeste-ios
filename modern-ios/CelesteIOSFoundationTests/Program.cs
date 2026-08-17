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

Console.WriteLine($"PASS: modern iOS foundation deterministic tests {passed}");
