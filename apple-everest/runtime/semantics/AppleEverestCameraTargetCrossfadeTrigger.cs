#nullable disable
// Selected static implementation reviewed against HonlyHelper 1.7.5.
// Package identity, authored guard and linked target patches are independently bound.
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Monocle;


namespace Celeste.Mod;

internal sealed class AppleEverestCameraTargetCrossfadeTrigger : Trigger
{
    public Vector2 TargetA;

    public Vector2 TargetB;

    public float LerpStrengthA;

    public float LerpStrengthB;

    public PositionModes PositionMode;

    public bool XOnly;

    public bool YOnly;

    public string DeleteFlag;

    private readonly float LerpAToB;

    private Vector2 AToB;

    private float LerpPos;

    public AppleEverestCameraTargetCrossfadeTrigger(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        TargetA = data.Nodes[0] + offset - new Vector2(320f, 180f) * 0.5f;
        TargetB = data.Nodes[1] + offset - new Vector2(320f, 180f) * 0.5f;
        LerpStrengthA = data.Float("lerpStrengthA");
        LerpStrengthB = data.Float("lerpStrengthB");
        PositionMode = data.Enum("positionMode", PositionModes.NoEffect);
        XOnly = data.Bool("xOnly");
        YOnly = data.Bool("yOnly");
        DeleteFlag = data.Attr("deleteFlag");
        AToB = TargetB - TargetA;
        LerpAToB = LerpStrengthB - LerpStrengthA;
    }

    public override void OnStay(Player player)
    {
        if (string.IsNullOrEmpty(DeleteFlag) || !SceneAs<Level>().Session.GetFlag(DeleteFlag))
        {
            LerpPos = MathHelper.Clamp(GetPositionLerp(player, PositionMode), 0f, 1f);
            player.CameraAnchor = TargetA + AToB * LerpPos;
            player.CameraAnchorLerp = Vector2.One * (LerpStrengthA + LerpAToB * LerpPos);
            player.CameraAnchorIgnoreX = YOnly;
            player.CameraAnchorIgnoreY = XOnly;
        }
    }

    public override void OnLeave(Player player)
    {
        base.OnLeave(player);
        bool flag = false;
        foreach (Trigger item in player.AppleEverestTriggersInside)
        {
            if ((item is CameraTargetTrigger || item is CameraAdvanceTargetTrigger /* no selected Honly corner triggers */ || item is AppleEverestCameraTargetCrossfadeTrigger) ? true : false)
            {
                flag = true;
                break;
            }
        }
        if (!flag)
        {
            player.CameraAnchorIgnoreX = false;
            player.CameraAnchorIgnoreY = false;
            player.CameraAnchorLerp = Vector2.Zero;
        }
    }
}
