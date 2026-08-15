using CelesteIOSFoundation;

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

Console.WriteLine($"PASS: modern iOS foundation deterministic tests {passed}");
