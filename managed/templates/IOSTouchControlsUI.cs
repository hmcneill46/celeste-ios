using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

// Uses the same standalone TextMenu pattern as KeyboardConfigUI and
// ButtonConfigUI. This is intentionally not another reflected Oui.
[Tracked(false)]
public sealed class IOSTouchControlsUI : TextMenu
{
    private bool closing;

    public IOSTouchControlsUI()
    {
        IOSTouchControls.SetOptionsEditing(true);
        Add(new Header("TOUCH CONTROLS"));
        Add(new SubHeader("DISPLAY"));
        Option<int> visibility = new Slider(
            "Touch Controls", IOSTouchControls.VisibilityName, 0, 2,
            IOSTouchControls.VisibilityIndex).Change(IOSTouchControls.SetVisibility);
        Option<int> movement = new Slider(
            "Movement Pad", IOSTouchControls.MovementName, 0, 1,
            IOSTouchControls.MovementIndex).Change(IOSTouchControls.SetMovement);
        Option<int> opacity = new Slider(
            "Opacity", IOSTouchControls.PercentFromZero, 0, 10,
            IOSTouchControls.OpacityIndex).Change(IOSTouchControls.SetOpacity);
        Option<int> size = new Slider(
            "Control Size", IOSTouchControls.SizeName, 0, 6,
            IOSTouchControls.SizeIndex).Change(IOSTouchControls.SetSize);
        Add(visibility);
        Add(movement);
        Add(opacity);
        Add(size);

        Add(new SubHeader("ACTIONS"));
        Option<int> grab = new Slider(
            "Grab Style", IOSTouchControls.GrabName, 0, 2,
            IOSTouchControls.GrabIndex).Change(IOSTouchControls.SetGrab);
        Option<bool> sliding = new OnOff(
            "Slide Jump / Dash", IOSTouchControls.SlidingEnabled).Change(IOSTouchControls.SetSliding);
        Option<bool> haptics = new OnOff(
            "Touch Haptics", IOSTouchControls.HapticsEnabled).Change(IOSTouchControls.SetHaptics);
        Add(grab);
        Add(sliding);
        Add(new SubHeader("ONE HELD FINGER MAY SLIDE BETWEEN JUMP AND DASH", false));
        Add(haptics);
        Add(new SubHeader(""));
        Add(new Button("Reset Touch Controls").Pressed(delegate
        {
            IOSTouchControls.ResetPreferences();
            visibility.Index = IOSTouchControls.VisibilityIndex;
            movement.Index = IOSTouchControls.MovementIndex;
            grab.Index = IOSTouchControls.GrabIndex;
            sliding.Index = IOSTouchControls.SlidingEnabled ? 1 : 0;
            opacity.Index = IOSTouchControls.OpacityIndex;
            size.Index = IOSTouchControls.SizeIndex;
            haptics.Index = IOSTouchControls.HapticsEnabled ? 1 : 0;
        }));
        Add(new SubHeader("OFF EXPECTS A CONTROLLER; A RECOVERY BUTTON REMAINS AVAILABLE", false));

        OnESC = OnCancel = delegate
        {
            Focused = false;
            closing = true;
        };
        MinWidth = 650f;
        Position.Y = ScrollTargetY;
        Alpha = 0f;
    }

    public override void Update()
    {
        base.Update();
        Alpha = Calc.Approach(Alpha, closing ? 0f : 1f, Engine.RawDeltaTime * 8f);
        if (closing && Alpha <= 0f) Close();
    }

    public override void Render()
    {
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * Ease.CubeOut(Alpha));
        base.Render();
    }
}
