using CelesteIOSFoundation;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste;

[Tracked(false)]
public sealed class IOSTouchControlsUI : TextMenu
{
    private bool closing;
    private bool resetPending;

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
        Option<int> action = new Slider(
            "Action Layout", IOSTouchControls.ActionLayoutName, 0, 1,
            IOSTouchControls.ActionLayoutIndex).Change(IOSTouchControls.SetActionLayout);
        Option<bool> sliding = new OnOff(
            "Button Sliding", IOSTouchControls.D3SlidingEnabled).Change(IOSTouchControls.SetSliding);
        Option<int> opacity = new Slider(
            "Opacity", IOSTouchControls.PercentFromZero, 0, 10,
            IOSTouchControls.OpacityIndex).Change(IOSTouchControls.SetOpacity);
        Option<bool> haptics = new OnOff(
            "Touch Haptics", IOSTouchControls.HapticsEnabled).Change(IOSTouchControls.SetHaptics);
        Option<bool> directional = new OnOff(
            "Directional Haptics", IOSTouchControls.DirectionalHapticsEnabled).Change(IOSTouchControls.SetDirectionalHaptics);
        Add(visibility);
        Add(movement);
        Add(action);
        Add(sliding);
        Add(opacity);
        Add(haptics);
        Add(directional);
        Add(new SubHeader("DIRECTIONAL HAPTICS PULSE ONLY WHEN THE MOVEMENT DIRECTION CHANGES", false));
        Add(new SubHeader("LAYOUT"));
        SubHeader status = new("Imported layouts open in the editor before anything is saved", false);
        Add(new Button("Edit Layout").Pressed(delegate
        {
            Focused = false;
            IOSTouchControls.BeginLayoutEditor(delegate { Focused = true; });
        }));
        Add(new Button("Export Layout...").Pressed(delegate
        {
            Focused = false;
            byte[] document = IOSTouchControls.ExportLayoutDocument();
            IOSPortableDocument portable = new(
                "Celeste-Touch-Layout.celestetouch", IOSPortableDocumentKind.TouchLayout, document);
            if (!IOSFilePortabilityBridge.RequestExport(new[] { portable }, false, delegate(bool success, string error)
            {
                Focused = true;
                status.Title = error ?? (success ? "Touch layout exported." : "Export cancelled.");
                RecalculateSize();
            }))
            {
                Focused = true;
                status.Title = "Files is unavailable.";
            }
        }));
        Add(new Button("Share Layout...").Pressed(delegate
        {
            Focused = false;
            byte[] document = IOSTouchControls.ExportLayoutDocument();
            IOSPortableDocument portable = new(
                "Celeste-Touch-Layout.celestetouch", IOSPortableDocumentKind.TouchLayout, document);
            if (!IOSFilePortabilityBridge.RequestExport(new[] { portable }, true, delegate(bool success, string error)
            {
                Focused = true;
                status.Title = error ?? (success ? "Touch layout shared." : "Share cancelled.");
                RecalculateSize();
            }))
            {
                Focused = true;
                status.Title = "Sharing is unavailable.";
            }
        }));
        Add(new Button("Import Layout...").Pressed(delegate
        {
            Focused = false;
            if (!IOSFilePortabilityBridge.RequestImport(
                IOSPortableDocumentKind.TouchLayout, TouchLayoutShareDocument.MaximumBytes, delegate(IOSExternalReadResult result)
                {
                    if (result.Cancelled)
                    {
                        Focused = true;
                        status.Title = "Import cancelled.";
                    }
                    else if (result.Data == null)
                    {
                        Focused = true;
                        status.Title = result.ErrorMessage ?? "The selected layout could not be read.";
                    }
                    else if (!IOSTouchControls.BeginImportedLayoutPreview(result.Data, delegate
                    {
                        Focused = true;
                        status.Title = "Touch layout preview closed.";
                    }, out string error))
                    {
                        Focused = true;
                        status.Title = error;
                    }
                    RecalculateSize();
                }))
            {
                Focused = true;
                status.Title = "Files is unavailable.";
            }
        }));
        Add(status);
        Button reset = new("Reset Touch Controls");
        reset.Pressed(delegate
        {
            if (!resetPending)
            {
                resetPending = true;
                reset.Label = "Press Again to Restore Factory Touch Layout";
                return;
            }
            resetPending = false;
            IOSTouchControls.ResetD3Preferences();
            reset.Label = "Reset Touch Controls";
            visibility.Index = IOSTouchControls.VisibilityIndex;
            movement.Index = IOSTouchControls.MovementIndex;
            action.Index = IOSTouchControls.ActionLayoutIndex;
            sliding.Index = IOSTouchControls.D3SlidingEnabled ? 1 : 0;
            opacity.Index = IOSTouchControls.OpacityIndex;
            haptics.Index = IOSTouchControls.HapticsEnabled ? 1 : 0;
            directional.Index = IOSTouchControls.DirectionalHapticsEnabled ? 1 : 0;
        });
        Add(reset);
        Add(new SubHeader("GRAB BEHAVIOUR USES CELESTE'S SOURCE-AWARE GRAB MODE", false));
        Add(new SubHeader("OFF EXPECTS A CONTROLLER; A RECOVERY BUTTON REMAINS AVAILABLE", false));

        OnESC = OnCancel = delegate
        {
            if (resetPending)
            {
                resetPending = false;
                reset.Label = "Reset Touch Controls";
                return;
            }
            Focused = false;
            closing = true;
        };
        MinWidth = 720f;
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
