using CelesteIOSFoundation;

namespace Celeste;

public sealed class IOSSourceGrabModeOption : TextMenu.Slider
{
    private readonly string baseLabel;

    public IOSSourceGrabModeOption(string label)
        : base(label, ValueName, 0, 2, IOSTouchControls.ActiveGrabModeIndex)
    {
        baseLabel = label;
        Change(IOSTouchControls.SetActiveGrabMode);
        Refresh();
    }

    public override void Update()
    {
        Refresh();
        base.Update();
    }

    private void Refresh()
    {
        Label = baseLabel + " [" + IOSTouchControls.ActiveGrabSourceName + "]";
        int value = IOSTouchControls.ActiveGrabModeIndex;
        if (Index == value) return;
        PreviousIndex = value;
        Index = value;
    }

    private static string ValueName(int value) => (AppleGrabMode)value switch
    {
        AppleGrabMode.Invert => Dialog.Clean("OPTIONS_BUTTON_INVERT"),
        AppleGrabMode.Toggle => Dialog.Clean("OPTIONS_BUTTON_TOGGLE"),
        _ => Dialog.Clean("OPTIONS_BUTTON_HOLD"),
    };
}
