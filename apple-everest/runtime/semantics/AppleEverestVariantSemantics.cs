#nullable disable
using System;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod;

// STATIC_EVM_SLICE_SEMANTICS: one known float variant. No runtime variant
// registry, DetourContext, or install/uninstall lifecycle is represented.
internal sealed class AppleEverestVariantSession : EverestModuleSession
{
    public float BackgroundBrightness = 1f;
}

internal sealed class AppleEverestVariantModule : EverestModule
{
    internal static AppleEverestVariantModule Instance { get; private set; }
    public override Type SessionType => typeof(AppleEverestVariantSession);
    internal AppleEverestVariantSession Session => (AppleEverestVariantSession)_Session;
    public AppleEverestVariantModule() => Instance = this;
    public override void Load() { }
    public override void Unload() => AppleEverestBackgroundBrightnessState.UnloadBuffer();
}

internal static class AppleEverestVariantDurability
{
    internal static readonly AppleEverestModuleDurabilityAdapter Adapter = new(
        "1bc8952683c1730e82f97c86a28d4a74ffb1c53345574e6706e4c5c95cb22a8d", null, null, Serialize, Deserialize);
    private static byte[] Serialize(EverestModuleSession value) => AppleEverestModuleYaml.Write(writer =>
    {
        writer.WriteStartObject();
        writer.WriteNumber("schemaVersion", 1);
        writer.WriteNumber("BackgroundBrightness", ((AppleEverestVariantSession)value).BackgroundBrightness);
        writer.WriteEndObject();
    });
    private static EverestModuleSession Deserialize(byte[] bytes, int slot)
    {
        using JsonDocument document = AppleEverestModuleYaml.Parse(bytes);
        JsonElement root = document.RootElement;
        AppleEverestModuleYaml.RequireObject(root);
        if (root.TryGetProperty("schemaVersion", out JsonElement version) && version.GetInt32() != 1)
            throw new InvalidOperationException("unsupported bounded variant session schema");
        AppleEverestVariantSession session = new() { Index = slot };
        if (root.TryGetProperty("BackgroundBrightness", out JsonElement brightness))
            session.BackgroundBrightness = AppleEverestModuleYaml.Single(brightness);
        return session;
    }
}

internal static class AppleEverestBackgroundBrightnessState
{
    private static VirtualRenderTarget blackMask;
    internal static float Value
    {
        get => AppleEverestVariantModule.Instance?.Session?.BackgroundBrightness ?? 1f;
        set => AppleEverestVariantModule.Instance.Session.BackgroundBrightness = value;
    }
    internal static void BeforeRender()
    {
        if (blackMask == null) blackMask = VirtualContent.CreateRenderTarget("static-background-brightness", 320, 180);
        Engine.Graphics.GraphicsDevice.SetRenderTarget(blackMask);
        Engine.Graphics.GraphicsDevice.Clear(Color.Black);
    }
    internal static void RenderAfterBackground()
    {
        if (Value >= 1f) return;
        Draw.SpriteBatch.Begin(SpriteSortMode.Deferred, GFX.DestinationTransparencySubtract,
            SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, GFX.FxDither, Matrix.Identity);
        Draw.SpriteBatch.Draw((RenderTarget2D)blackMask, Vector2.Zero,
            Color.White * MathHelper.Clamp(1f - Value, 0f, 1f));
        Draw.SpriteBatch.End();
    }
    internal static void UnloadBuffer()
    {
        blackMask?.Dispose();
        blackMask = null;
    }
}

internal sealed class AppleEverestBackgroundBrightnessFadeTrigger : Trigger
{
    private readonly float from;
    private readonly float to;
    private readonly PositionModes mode;
    internal AppleEverestBackgroundBrightnessFadeTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        if (data.Attr("variantChange", "Gravity") != "BackgroundBrightness" || data.Bool("revertOnDeath", true))
            throw new InvalidOperationException("brightness trigger is outside the frozen Beginner semantics");
        from = data.Float("valueA", 1f);
        to = data.Float("valueB", 1f);
        mode = data.Attr("positionMode", "NoEffect") switch
        {
            "LeftToRight" => PositionModes.LeftToRight,
            "BottomToTop" => PositionModes.BottomToTop,
            "NoEffect" => PositionModes.NoEffect,
            _ => throw new InvalidOperationException("brightness fade direction is outside the frozen slice")
        };
    }
    public override void OnStay(Player player)
    {
        base.OnStay(player);
        AppleEverestBackgroundBrightnessState.Value = Calc.ClampedMap(GetPositionLerp(player, mode), 0f, 1f, from, to);
    }
}

internal sealed class AppleEverestResetSliceVariantsTrigger : Trigger
{
    private readonly bool extended;
    internal AppleEverestResetSliceVariantsTrigger(EntityData data, Vector2 offset) : base(data, offset)
    {
        extended = data.Bool("extended", false);
        // vanilla=true clears EVM's map override lists. Those lists are empty
        // in this product; saved player Assist Mode settings are untouched.
    }
    public override void OnEnter(Player player)
    {
        if (extended) AppleEverestBackgroundBrightnessState.Value = 1f;
    }
}
