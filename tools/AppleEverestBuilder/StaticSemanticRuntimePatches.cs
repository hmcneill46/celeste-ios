namespace AppleEverestBuilder;

// Exact build-time source edits for opted-in, hash-locked semantic plans.
// Nothing here is a target-side hook registry or a runtime installation path.
internal static class StaticSemanticRuntimePatches
{
    internal static string ContractSha256
    {
        get
        {
            using Stream source = typeof(StaticSemanticRuntimePatches).Assembly
                .GetManifestResourceStream("AppleEverestBuilder.StaticSemanticRuntimePatches.cs")!;
            using MemoryStream bytes = new();
            source.CopyTo(bytes);
            return Hashing.BytesSha256(bytes.ToArray());
        }
    }
    internal static void Apply(string managedRoot)
    {
        string target = Path.Combine(managedRoot, "Celeste", "Mod", "AppleEverestStatic");
        string level = Path.Combine(managedRoot, "Celeste", "Level.cs");
        if (File.Exists(Path.Combine(target, "AppleEverestBubbleReturnBerry.cs")))
        {
            // In this closure the callback installed by Player's constructor
            // is OnSquish. Preserve the return berry's state-21 suppression.
            Change(Path.Combine(managedRoot, "Celeste", "Player.cs"),
                "\tprivate void AppleEverestOriginal_OnSquish(global::Celeste.CollisionData data)\n\t{",
                "\tprivate void AppleEverestOriginal_OnSquish(global::Celeste.CollisionData data)\n\t{\n" +
                "\t\tif (StateMachine.State == 21) return;");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestVariantSemantics.cs")))
        {
            Change(level, "\t\tSpeedRing.DrawToBuffer(this);\n\t\tbase.BeforeRender();",
                "\t\tSpeedRing.DrawToBuffer(this);\n\t\tbase.BeforeRender();\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestBackgroundBrightnessState.BeforeRender();");
            Change(level, "\t\tBackground.Render(this);",
                "\t\tBackground.Render(this);\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestBackgroundBrightnessState.RenderAfterBackground();");
            Change(Path.Combine(managedRoot, "Celeste", "GameplayBuffers.cs"),
                "\t\tall.Clear();", "\t\tall.Clear();\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestBackgroundBrightnessState.UnloadBuffer();");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestStrawberryJamRendering.cs")))
        {
            Change(Path.Combine(managedRoot, "Celeste", "SoundSource.cs"),
                "\tprivate EventInstance instance;", "\tinternal EventInstance instance;");
            string playback = Path.Combine(managedRoot, "Celeste", "PlayerPlayback.cs");
            Change(playback, "\t\tstartDelay = 1f;",
                "\t\tstartDelay = 1f;\n\t\tappleEverestSJToggleable = true;");
            Change(playback, "\tprivate void Restart()",
                "\tprivate bool appleEverestSJToggleable;\n" +
                "\tinternal void AppleEverestSJPreUpdate()\n\t{\n" +
                "\t\tif (!appleEverestSJToggleable || !global::Celeste.Mod.AppleEverestStrawberryJamModule.Instance.Settings.TogglePlaybacks.Released) return;\n" +
                "\t\tif (Active) { Active = false; Visible = false; }\n" +
                "\t\telse { Active = true; Restart(); }\n\t}\n\n\tprivate void Restart()");
            // Everest's Entity.PreUpdate runs even for inactive entities. A
            // closed type test preserves that ordering without a new event ABI.
            Change(Path.Combine(managedRoot, "Monocle", "EntityList.cs"),
                "\t\t\tif (entity.Active)\n\t\t\t{\n\t\t\t\tentity.Update();",
                "\t\t\tif (entity is global::Celeste.PlayerPlayback playback) playback.AppleEverestSJPreUpdate();\n" +
                "\t\t\tif (entity.Active)\n\t\t\t{\n\t\t\t\tentity.Update();");
            Change(level, "\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.OnLevelLoaded(this);",
                "\t\tglobal::Celeste.Mod.AppleEverestCollabRuntime.OnLevelLoaded(this);\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.OnLevelLoaded(this);");
            Change(level, "\t\tDistort.Render((RenderTarget2D)GameplayBuffers.Gameplay,",
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.Render(this, false);\n" +
                "\t\tDistort.Render((RenderTarget2D)GameplayBuffers.Gameplay,");
            Change(level, "\t\tForeground.Render(this);",
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.Render(this, true);\n\t\tForeground.Render(this);");
            Change(Path.Combine(managedRoot, "Celeste", "GameplayBuffers.cs"),
                "\t\tall.Clear();", "\t\tall.Clear();\n\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.UnloadBuffers();");
            // GlowController's process-wide orphan cleanup is unconditional
            // even when all four authored whitelist/blacklist fields are empty.
            Change(Path.Combine(managedRoot, "Celeste", "LightingRenderer.cs"),
                "lights[i] != null && lights[i].Entity.Scene != scene",
                "lights[i] != null && (lights[i].Entity == null || lights[i].Entity.Scene != scene)");
            Change(Path.Combine(managedRoot, "Celeste", "Player.cs"),
                "\tprivate void AppleEverestOriginal_OnSquish(global::Celeste.CollisionData data)\n\t{",
                "\tprivate void AppleEverestOriginal_OnSquish(global::Celeste.CollisionData data)\n\t{\n" +
                "\t\tif (StateMachine.State == 21 && global::Celeste.Mod.AppleEverestStrawberryJamModule.IsSliceMap(SceneAs<Level>())) return;");
            string bloom = Path.Combine(managedRoot, "Celeste", "BloomRenderer.cs");
            Change(bloom, "\t\tif (!(Strength > 0f))",
                "\t\tfloat appleEverestMaskStrength = global::Celeste.Mod.AppleEverestSJMaskRendering.BeginBloom(this, scene);\n" +
                "\t\tif (!(Strength > 0f))");
            Change(bloom, "\t\tDraw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n\t\tDraw.Rect(-10f, -10f, 340f, 200f, Color.White * Base);",
                "\t\tvar appleEverestMaskRects = global::Celeste.Mod.AppleEverestSJMaskRendering.RenderBloom(this, target, scene, texture, appleEverestMaskStrength);\n" +
                "\t\tDraw.SpriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);\n\t\tDraw.Rect(-10f, -10f, 340f, 200f, Color.White * Base);");
            Change(bloom, "\t\tEngine.Instance.GraphicsDevice.SetRenderTarget(target);\n\t\tDraw.SpriteBatch.Begin(SpriteSortMode.Deferred, AdditiveMaskToScreen);",
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.ClearBloom(this, scene, appleEverestMaskStrength, appleEverestMaskRects);\n" +
                "\t\tEngine.Instance.GraphicsDevice.SetRenderTarget(target);\n\t\tDraw.SpriteBatch.Begin(SpriteSortMode.Deferred, AdditiveMaskToScreen);");
        }
    }

    internal static void Change(string path, string before, string after)
    {
        string text = File.ReadAllText(path);
        int at = text.IndexOf(before, StringComparison.Ordinal);
        if (at < 0 || text.IndexOf(before, at + before.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException($"semantic target changed: {Path.GetFileName(path)}");
        File.WriteAllText(path, text[..at] + after + text[(at + before.Length)..]);
    }
}
