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
        Change(level, "\tpublic void LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false)\n\t{",
            "\tpublic void LoadLevel(Player.IntroTypes playerIntro, bool isFromLoader = false)\n\t{\n" +
            "\t\tglobal::Celeste.Mod.AppleEverestStaticRuntime.PrepareDialogForMap(Session);");
        string dialog = Path.Combine(managedRoot, "Celeste", "Dialog.cs");
        foreach (string lookup in new[] { "language.Dialog.ContainsKey(name)", "language.Dialog.TryGetValue(name, out value)",
                     "language.Cleaned.TryGetValue(name, out value)" })
            Change(dialog, lookup, lookup.Replace("(name", "(global::Celeste.Mod.AppleEverestStaticRuntime.ResolveDialogKey(name)"));
        foreach (string part in new[] { "cardtop", "card" })
            Change(Path.Combine(managedRoot, "Celeste", "OuiChapterPanel.cs"),
                "GFX.Gui[(!flag) ? \"areaselect/" + part + "\" : \"areaselect/" + part + "_golden\"]",
                "GFX.Gui[global::Celeste.Mod.AppleEverestCollabRuntime.ChapterCardTexture(this, (!flag) ? \"areaselect/" +
                part + "\" : \"areaselect/" + part + "_golden\")]");
        if (File.Exists(Path.Combine(target, "AppleEverestCollabState.cs")))
            Change(Path.Combine(target, "AppleEverestCollabRuntime.cs"),
                "internal static AppleEverestCollabSession Route => null;",
                "internal static AppleEverestCollabSession Route => AppleEverestCollabModule.Instance?.Session;");
        if (File.Exists(Path.Combine(target, "AppleEverestCollabJournalStickers.cs")))
        {
            Change(Path.Combine(target, "AppleEverestCollabRuntime.cs"),
                "journal.Pages.Add(new OuiJournalCover(journal));", "journal.Pages.Add(new AppleEverestCollabJournalCover(journal));");
            Change(Path.Combine(managedRoot, "Celeste", "LevelLoader.cs"),
                "\t\tglobal::On.Celeste.LevelLoader.Invoke_ctor(this, session, startPosition, (appleSelf, appleArg0, appleArg1) => appleSelf.AppleEverestOriginal_ctor(appleArg0, appleArg1));",
                "\t\tglobal::Celeste.Mod.AppleEverestCollabJournalCover.PrepareLevel(session);\n" +
                "\t\tglobal::On.Celeste.LevelLoader.Invoke_ctor(this, session, startPosition, (appleSelf, appleArg0, appleArg1) => appleSelf.AppleEverestOriginal_ctor(appleArg0, appleArg1));");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestCollabStrawberryHooks.cs")))
        {
            const string hooks = "global::Celeste.Mod.AppleEverestCollabStrawberryHooks.";
            string strawberry = Path.Combine(managedRoot, "Celeste", "Strawberry.cs");
            Change(strawberry, "\tprivate BloomPoint bloom;", "\tprivate BloomPoint bloom;\n\tinternal BloomPoint AppleEverestCollabBloom => bloom;");
            Change(strawberry, "\tprivate VertexLight light;", "\tprivate VertexLight light;\n\tinternal VertexLight AppleEverestCollabLight => light;");
            Change(strawberry, "\t\tAdd(sprite);", "\t\tsprite = " + hooks + "ReplaceSprite(sprite, this);\n\t\tAdd(sprite);");
            Change(Path.Combine(target, "AppleEverestSecondCollabRuntime.cs"),
                "            AppleEverestSilverBerry => SaveData.Instance.CheckStrawberry(berry.ID) ? \"CollabUtils2_ghostSilverBerry\" : \"CollabUtils2_silverBerry\",\n", "");
            Change(strawberry, "\"event:/game/general/strawberry_get\", Position,",
                hooks + "CollectionSound(\"event:/game/general/strawberry_get\", this), Position,");
            Change(strawberry, "\tprivate IEnumerator CollectRoutine(int collectIndex)\n\t{",
                "\tprivate IEnumerator CollectRoutine(int collectIndex) => " + hooks + "Collect(this, AppleEverestCollabOriginalCollect(collectIndex));\n\n" +
                "\tprivate IEnumerator AppleEverestCollabOriginalCollect(int collectIndex)\n\t{");
            Change(Path.Combine(managedRoot, "Monocle", "EntityList.cs"), "\tprivate List<Entity> toAdd;",
                "\tprivate List<Entity> toAdd;\n\tinternal void AppleEverestRemoveCollabPoints()\n\t{\n" +
                "\t\tEntity pending = toAdd.Find(e => e is global::Celeste.StrawberryPoints);\n\t\tif (pending != null) toAdd.Remove(pending);\n\t}");
            Change(Path.Combine(managedRoot, "Celeste", "Player.cs"),
                "\t\treturn global::On.Celeste.Player.Invoke_Die(this, direction, evenIfInvincible, registerDeathInStats, (appleSelf, appleArg0, appleArg1, appleArg2) => appleSelf.orig_Die(appleArg0, appleArg1, appleArg2));",
                "\t\treturn " + hooks + "Die(this, direction, evenIfInvincible, registerDeathInStats, (d, e, r) => global::On.Celeste.Player.Invoke_Die(this, d, e, r, (appleSelf, appleArg0, appleArg1, appleArg2) => appleSelf.orig_Die(appleArg0, appleArg1, appleArg2)));");
            Change(Path.Combine(managedRoot, "Celeste", "PlayerDeadBody.cs"), "HasGolden ? \"event:/new_content/char/madeline/death_golden\"",
                "HasGolden ? " + hooks + "GoldenDeathSound(\"event:/new_content/char/madeline/death_golden\")");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestCollabEngineAccess.cs")))
        {
            Change(Path.Combine(managedRoot, "Monocle", "EntityList.cs"),
                "\tprivate List<Entity> toAdd;",
                "\tprivate List<Entity> toAdd;\n\tinternal IEnumerable<Entity> AppleEverestCollabToAdd => toAdd;");
            Change(Path.Combine(managedRoot, "Celeste", "HeartGemDoor.cs"), "public class HeartGemDoor : Entity", "public partial class HeartGemDoor : Entity");
            Change(level, "public class Level : Scene, IOverlayHandler", "public partial class Level : Scene, IOverlayHandler");
            foreach (string mode in new[] { "AssistMode", "VariantMode" })
                Change(level, "\tprivate void " + mode + "(int returnIndex, bool minimal)\n\t{",
                    "\tprivate void " + mode + "(int returnIndex, bool minimal)\n\t{\n" +
                    "\t\tAppleEverestCollabOriginal" + mode + "(returnIndex, minimal);\n" +
                    "\t\tglobal::Celeste.Mod.AppleEverestCollabMiniHeartDoorUnlockTrigger.AddAssistOption(this);\n\t}\n\n" +
                    "\tprivate void AppleEverestCollabOriginal" + mode + "(int returnIndex, bool minimal)\n\t{");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestDecalSemantics.cs")))
        {
            string decal = Path.Combine(managedRoot, "Celeste", "Decal.cs");
            Change(decal, "public class Decal : Entity", "public partial class Decal : Entity");
            Change(decal, "\tpublic override void Added(Scene scene)\n\t{",
                "\tpublic override void Added(Scene scene)\n\t{\n\t\tAppleEverestOriginalAdded(scene);\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestSelectedDecalRegistry.Apply(this);\n\t}\n" +
                "\tprivate void AppleEverestOriginalAdded(Scene scene)\n\t{");
            Change(decal, "\t\tbase.Scene.Add(solid);",
                "\t\tappleEverestSolids.Add(solid);\n\t\tbase.Scene.Add(solid);");
            Change(decal, "(entity.Position - Position).Length() < 32f", "(entity.Position - Position).Length() < appleEverestHideRange");
            Change(decal, "(entity.Position - Position).Length() > 48f", "(entity.Position - Position).Length() > appleEverestShowRange");
            Change(decal, "\t\tparallax = true;", "\t\tparallax = amount != 0f;");
            Change(Path.Combine(managedRoot, "Monocle", "Entity.cs"),
                "\tpublic Scene Scene { get; private set; }",
                "\tpublic Scene Scene { get; private set; }\n\tinternal void AppleEverestBindScene(Scene scene) => Scene = scene;");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestSidewaysJumpThru.cs")))
        {
            const string sideways = "global::Celeste.Mod.AppleEverestSidewaysJumpThru.";
            string player = Path.Combine(managedRoot, "Celeste", "Player.cs");
            Change(player, "\tprivate int NormalUpdate()\n\t{",
                "\tprivate int NormalUpdate() => " + sideways + "AfterNormal(AppleEverestSidewaysOriginalNormalUpdate(), this);\n\n" +
                "\tprivate int AppleEverestSidewaysOriginalNormalUpdate()\n\t{");
            Change(player, "\tpublic bool DuckFreeAt(Vector2 at)\n\t{",
                "\tpublic bool DuckFreeAt(Vector2 at) => " + sideways + "AfterDuckFree(AppleEverestSidewaysOriginalDuckFreeAt(at), this, at);\n\n" +
                "\tprivate bool AppleEverestSidewaysOriginalDuckFreeAt(Vector2 at)\n\t{");
            Change(player, "\tprivate bool ClimbHopBlockedCheck()\n\t{",
                "\tprivate bool ClimbHopBlockedCheck() => " + sideways + "AfterClimbHop(AppleEverestSidewaysOriginalClimbHopBlockedCheck(), this);\n\n" +
                "\tprivate bool AppleEverestSidewaysOriginalClimbHopBlockedCheck()\n\t{");
            Change(Path.Combine(managedRoot, "Celeste", "LevelLoader.cs"),
                "\t\tglobal::On.Celeste.LevelLoader.Invoke_ctor(this, session, startPosition, (appleSelf, appleArg0, appleArg1) => appleSelf.AppleEverestOriginal_ctor(appleArg0, appleArg1));",
                "\t\tglobal::On.Celeste.LevelLoader.Invoke_ctor(this, session, startPosition, (appleSelf, appleArg0, appleArg1) => appleSelf.AppleEverestOriginal_ctor(appleArg0, appleArg1));\n" +
                "\t\t" + sideways + "PrepareLevel(session);");
            Change(Path.Combine(managedRoot, "Celeste", "OverworldLoader.cs"), "\t\tfadeIn = snow == null;",
                "\t\tfadeIn = snow == null;\n\t\t" + sideways + "EnterOverworld(startMode);");
        }
        string mapData = Path.Combine(managedRoot, "Celeste", "MapData.cs");
        string mapSource = File.ReadAllText(mapData);
        const string berryCensus = "if (entity.Name == \"strawberry\")";
        if (mapSource.Split(berryCensus, StringSplitOptions.None).Length != 3)
            throw new InvalidDataException("canonical MapData strawberry census changed");
        File.WriteAllText(mapData, mapSource.Replace(berryCensus,
            "if (global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.CountsAsMapStrawberry(entity.Name))", StringComparison.Ordinal));
        Change(Path.Combine(managedRoot, "Celeste", "LevelData.cs"),
            "child2.Name == \"strawberry\" || child2.Name == \"snowberry\"",
            "global::Celeste.Mod.GeneratedAppleEverestGameplayRegistry.CountsAsMapStrawberry(child2.Name) || child2.Name == \"snowberry\"");
        string rumble = Path.Combine(managedRoot, "Celeste", "RumbleTrigger.cs");
        Change(rumble, "\tprivate float right;",
            "\tprivate float right;\n\tprivate bool appleEverestConstrainHeight;\n\tprivate float appleEverestTop, appleEverestBottom;");
        Change(rumble, "\t\tmanualTrigger = data.Bool(\"manualTrigger\");",
            "\t\tmanualTrigger = data.Bool(\"manualTrigger\");\n\t\tappleEverestConstrainHeight = data.Bool(\"constrainHeight\");");
        Change(rumble, "\t\t\tright = Math.Max(array[0].X, array[1].X);",
            "\t\t\tright = Math.Max(array[0].X, array[1].X);\n" +
            "\t\t\tappleEverestTop = Math.Min(array[0].Y, array[1].Y);\n\t\t\tappleEverestBottom = Math.Max(array[0].Y, array[1].Y);");
        Change(rumble, "entity.X >= left && entity.X <= right",
            "(!appleEverestConstrainHeight || entity.Y >= appleEverestTop && entity.Y <= appleEverestBottom) && entity.X >= left && entity.X <= right");
        Change(rumble, "item.IsCrack && item.X >= left && item.X <= right",
            "item.IsCrack && (!appleEverestConstrainHeight || item.Y >= appleEverestTop && item.Y <= appleEverestBottom) && item.X >= left && item.X <= right");
        if (File.Exists(Path.Combine(target, "AppleEverestFrostSpinner.cs")))
        {
            Change(Path.Combine(managedRoot, "Monocle", "Component.cs"),
                "\tpublic Entity Entity { get; private set; }",
                "\tpublic Entity Entity { get; private set; }\n\tinternal void AppleEverestBindEntity(Entity entity) => Entity = entity;");
            Change(Path.Combine(managedRoot, "Celeste", "GFX.cs"), "\tpublic static void UnloadData()\n\t{",
                "\tpublic static void UnloadData()\n\t{\n\t\tglobal::Celeste.Mod.AppleEverestFrostSpinnerTextures.Unload();");
            Change(Path.Combine(target, "AppleEverestSemanticFactories.cs"),
                "        base.OnEnter(player);\n        if (Scene == null)\n            return;\n\n        List<Entity> spinners = Scene.Tracker.GetEntities<CrystalStaticSpinner>();",
                "        AppleEverestFrostSpinner.BeforeCrystalShatter(this, mode == Modes.All);\n" +
                "        base.OnEnter(player);\n        if (Scene == null)\n            return;\n\n        List<Entity> spinners = Scene.Tracker.GetEntities<CrystalStaticSpinner>();");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestVivSpinner.cs")))
        {
            string player = Path.Combine(managedRoot, "Celeste", "Player.cs");
            Change(player, "\t\treturn 10;\n\t}\n\n\tpublic void StopSummitLaunch()",
                "\t\tglobal::Celeste.Mod.AppleEverestVivSpinner.BeforePlayerStateReturn(this);\n\t\treturn 10;\n\t}\n\n\tpublic void StopSummitLaunch()");
            Change(player, "\t\treturn 18;\n\t}\n\n\tprivate IEnumerator ReflectionFallCoroutine()",
                "\t\tglobal::Celeste.Mod.AppleEverestVivSpinner.BeforePlayerStateReturn(this);\n\t\treturn 18;\n\t}\n\n\tprivate IEnumerator ReflectionFallCoroutine()");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestFrostSpinner.cs")))
            Change(Path.Combine(managedRoot, "Celeste", "Player.cs"),
                "\t\treturn 10;\n\t}\n\n\tpublic void StopSummitLaunch()",
                "\t\tglobal::Celeste.Mod.AppleEverestFrostSpinner.BeforeSummitReturn(this);\n\t\treturn 10;\n\t}\n\n\tpublic void StopSummitLaunch()");
        if (File.Exists(Path.Combine(target, "AppleEverestEntityActivator.cs")))
        {
            Change(Path.Combine(managedRoot, "Celeste", "PlaybackBillboard.cs"), "\tprivate class FG : Entity", "\tinternal class FG : Entity");
            Change(Path.Combine(managedRoot, "Celeste", "SwapBlock.cs"), "\tprivate class PathRenderer : Entity", "\tinternal class PathRenderer : Entity");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestXaphanSlopeHooks.cs")))
        {
            const string hooks = "global::Celeste.Mod.AppleEverestXaphanSlopeHooks.";
            Change(Path.Combine(managedRoot, "Monocle", "Sprite.cs"),
                "\tprivate Dictionary<string, Animation> animations;",
                "\tprivate Dictionary<string, Animation> animations;\n" +
                "\tinternal void AppleEverestCopyMissingAnimations(Sprite source)\n\t{\n" +
                "\t\tforeach (var animation in source.animations)\n" +
                "\t\t\tif (!animations.ContainsKey(animation.Key)) animations[animation.Key] = animation.Value;\n\t}");
            string actor = Path.Combine(managedRoot, "Celeste", "Actor.cs");
            Change(actor, "\tpublic bool MoveH(float moveH, Collision onCollide = null, Solid pusher = null)\n\t{",
                "\tpublic bool MoveH(float moveH, Collision onCollide = null, Solid pusher = null)\n\t{\n\t\tmoveH = " + hooks + "ActorMoveH(this, moveH);");
            Change(actor, "\tprotected bool TrySquishWiggle(CollisionData data, int wiggleX = 3, int wiggleY = 3)\n\t{",
                "\tprotected bool TrySquishWiggle(CollisionData data, int wiggleX = 3, int wiggleY = 3)\n\t{\n\t\tif (" + hooks + "TrySquish(this, data)) return true;");
            Change(Path.Combine(managedRoot, "Celeste", "Solid.cs"), "\tpublic override void Update()\n\t{",
                "\tpublic override void Update()\n\t{\n\t\t" + hooks + "BeforeSolidUpdate(this);");
            foreach (string type in new[] { "TheoCrystal", "Glider", "Puffer", "Seeker", "Debris", "MoveBlock" })
            {
                string after = type == "MoveBlock" ? "AfterMoveBlockUpdate(this)" :
                    "AfterActorUpdate(this" + (type is "TheoCrystal" or "Glider" ? ", Hold.IsHeld" : "") + ")";
                Change(Path.Combine(managedRoot, "Celeste", type + ".cs"), "\tpublic override void Update()\n\t{",
                    "\tpublic override void Update()\n\t{\n" +
                    "\t\tif (GetType() != typeof(" + type + ")) { AppleEverestXaphanOriginalUpdate(); return; }\n" +
                    "\t\tglobal::Celeste.Mod.AppleEverestXaphanSlope.SetCollisionBeforeUpdate(this);\n" +
                    "\t\tAppleEverestXaphanOriginalUpdate();\n\t\t" + hooks + after + ";\n\t}\n\n" +
                    "\tprivate void AppleEverestXaphanOriginalUpdate()\n\t{");
            }
            foreach ((string type, string handler) in new[] { ("TheoCrystal", "TheoCollideH"), ("Glider", "GliderCollideH") })
                Change(Path.Combine(managedRoot, "Celeste", type + ".cs"), "\tprivate void OnCollideH(CollisionData data)\n\t{",
                    "\tprivate void OnCollideH(CollisionData data)\n\t{\n\t\tif (" + hooks + handler + "(this, data)) return;");
            Change(Path.Combine(managedRoot, "Monocle", "Sprite.cs"),
                "\tpublic void Play(string id, bool restart = false, bool randomizeFrame = false)\n\t{",
                "\tpublic void Play(string id, bool restart = false, bool randomizeFrame = false)\n\t{\n" +
                "\t\tid = " + hooks + "PlayerSpriteAnimation(this, id);");
            Change(Path.Combine(target, "AppleEverestStaticRuntime.cs"),
                "        StaticSpriteBank = new SpriteBank(GFX.Game, composite);",
                "        StaticSpriteBank = new SpriteBank(GFX.Game, composite);\n        " + hooks + "InstallSpriteExtensions(StaticSpriteBank);");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestSelectedCanaryAssets.cs")))
            Change(Path.Combine(managedRoot, "Celeste", "LevelLoader.cs"), "\tprivate void LoadingThread()\n\t{",
                "\tprivate void LoadingThread()\n\t{\n\t\tglobal::Celeste.Mod.AppleEverestSelectedCanaryAssets.PrepareLevel(session);");
        if (File.Exists(Path.Combine(target, "AppleEverestFrostFireBarrier.cs")))
            Change(Path.Combine(managedRoot, "Celeste", "GameplayBuffers.cs"),
                "\t\tall.Clear();", "\t\tall.Clear();\n\t\tglobal::Celeste.Mod.AppleEverestFrostLavaGeometry.UnloadBuffer();");
        if (File.Exists(Path.Combine(target, "AppleEverestRainbowSpinnerColorArea.cs")))
            // This is the selected graph's sole CrystalStaticSpinner.GetHue
            // hook. Its int.MaxValue / MaddieHelpingHand_AfterAll precedence
            // is the outermost typed branch, before the original calculation.
            Change(Path.Combine(managedRoot, "Celeste", "CrystalStaticSpinner.cs"),
                "\tprivate Color GetHue(Vector2 position)\n\t{",
                "\tprivate Color GetHue(Vector2 position)\n\t{\n" +
                "\t\tif (global::Celeste.Mod.AppleEverestRainbowSpinnerColorArea.TryHue(this, position, out Color appleEverestHue)) return appleEverestHue;");
        if (File.Exists(Path.Combine(target, "AppleEverestSelectedProfileGuard.cs")))
        {
            Change(Path.Combine(managedRoot, "Celeste", "MapData.cs"),
                "\t\tstrawberry.Values[\"checkpointID\"] = y;",
                "\t\tglobal::Celeste.Mod.AppleEverestSelectedProfileGuard.RecordBerryNormalization(strawberry, y, x);\n" +
                "\t\tstrawberry.Values[\"checkpointID\"] = y;");
            string scene = Path.Combine(managedRoot, "Monocle", "Scene.cs");
            Change(scene, "return (int)((TimeActive - Engine.DeltaTime) / interval) < (int)(TimeActive / interval);",
                "return (int)(((double)TimeActive - Engine.DeltaTime) / interval) < (int)((double)TimeActive / interval);");
            Change(scene, "return Math.Floor((TimeActive - offset - Engine.DeltaTime) / interval) < Math.Floor((TimeActive - offset) / interval);",
                "return Math.Floor(((double)TimeActive - offset - Engine.DeltaTime) / interval) < Math.Floor(((double)TimeActive - offset) / interval);");
            Change(scene, "\t\tif (this.OnEndOfFrame != null)\n\t\t{\n\t\t\tthis.OnEndOfFrame();\n\t\t\tthis.OnEndOfFrame = null;\n\t\t}",
                "\t\tglobal::System.Threading.Interlocked.Exchange(ref OnEndOfFrame, null)?.Invoke();");
            Change(Path.Combine(managedRoot, "Monocle", "TileGrid.cs"),
                "\t\tRectangle clippedRenderTiles = GetClippedRenderTiles();",
                "\t\tif (ClipCamera == null && Scene is global::Celeste.Level appleEverestLevel) ClipCamera = appleEverestLevel.Camera;\n" +
                "\t\tRectangle clippedRenderTiles = GetClippedRenderTiles();");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestStylegroundFadeController.cs")))
        {
            string renderer = Path.Combine(managedRoot, "Celeste", "BackdropRenderer.cs");
            Change(Path.Combine(managedRoot, "Celeste", "Parallax.cs"), "\tprivate float fadeIn = 1f;",
                "\tprivate float fadeIn = 1f;\n\tinternal float AppleEverestFadeIn => fadeIn;");
            Change(Path.Combine(managedRoot, "Celeste", "Backdrop.cs"), "\tpublic bool IsVisible(Level level)\n\t{",
                "\tpublic bool IsVisible(Level level)\n\t{\n" +
                "\t\tif (global::Celeste.Mod.AppleEverestStylegroundFadeController.ForceVisible(this)) return true;");
            Change(renderer, "\tpublic override void Update(Scene scene)\n\t{",
                "\tpublic override void Update(Scene scene)\n\t{\n" +
                "\t\tAppleEverestOriginalUpdate(scene);\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestStylegroundFadeController.AfterRendererUpdate(scene);\n\t}\n\n" +
                "\tprivate void AppleEverestOriginalUpdate(Scene scene)\n\t{");
            // Pinned Everest distinguishes its looping batch from ordinary
            // backdrops. This also preserves the helper's render-target switch.
            Change(renderer, "\tprivate bool usingSpritebatch;",
                "\tprivate bool usingSpritebatch;\n\tprivate bool usingLoopingSpritebatch;");
            Change(renderer, "\tpublic void StartSpritebatchLooping(BlendState blendState)\n\t{",
                "\tpublic void StartSpritebatchLooping(BlendState blendState)\n\t{\n\t\tusingLoopingSpritebatch = true;");
            Change(renderer, "\t\tusingSpritebatch = false;", "\t\tusingSpritebatch = usingLoopingSpritebatch = false;");
            Change(renderer, "if (backdrop.Visible)",
                "if (global::Celeste.Mod.AppleEverestParallaxFadeOutController.IsVisible(backdrop))");
            Change(renderer, "if (backdrop is Parallax && (backdrop as Parallax).BlendState != blendState)",
                "if (backdrop is Parallax && (!usingLoopingSpritebatch || (backdrop as Parallax).BlendState != blendState))");
            Change(renderer, "\t\t\t\tif (backdrop.UseSpritebatch && !usingSpritebatch)",
                "\t\t\t\tif (backdrop is not Parallax && backdrop.UseSpritebatch && usingLoopingSpritebatch) EndSpritebatch();\n" +
                "\t\t\t\tif (backdrop.UseSpritebatch && !usingSpritebatch)");
            Change(renderer, "\t\t\t\t\tStartSpritebatch(blendState);",
                "\t\t\t\t\tif (backdrop is Parallax) StartSpritebatchLooping(blendState);\n\t\t\t\t\telse StartSpritebatch(blendState);");
            Change(renderer, "\t\t\t\tbackdrop.Render(scene);",
                "\t\t\t\tglobal::Celeste.Mod.AppleEverestStylegroundFadeController.RenderStart(this, backdrop);\n" +
                "\t\t\t\tbackdrop.Render(scene);\n" +
                "\t\t\t\tglobal::Celeste.Mod.AppleEverestStylegroundFadeController.RenderEnd(this, backdrop, blendState);");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestCaveWall.cs")))
        {
            string talk = Path.Combine(managedRoot, "Celeste", "TalkComponent.cs");
            // Everest's UI base methods precede Brokemia's post-hooks.
            Change(talk, "Handler.Entity == null || base.Scene.CollideCheck<FakeWall>(Handler.Entity.Position)",
                "Handler.Entity?.Collider != null ? Scene.CollideCheck<FakeWall>(Handler.Entity.Collider.Bounds) : " +
                "Handler.Entity == null || Scene.CollideCheck<FakeWall>(Handler.Entity.Position)");
            Change(talk, "\t\t\t\talpha = 0f;\n\t\t\t}\n\t\t}",
                "\t\t\t\talpha = 0f;\n\t\t\t}\n\t\t\tif (Highlighted) alpha = 1f;\n\t\t}");
            Change(talk, "alpha < 1f && Handler.Entity != null && !base.Scene.CollideCheck<FakeWall>(Handler.Entity.Position)",
                "alpha < 1f && Handler.Entity != null && !(Handler.Entity.Collider != null ? " +
                "Scene.CollideCheck<FakeWall>(Handler.Entity.Collider.Bounds) : Scene.CollideCheck<FakeWall>(Handler.Entity.Position))");
            Change(talk, "\t\t\tbase.Update();", "\t\t\tif (Highlighted) alpha = 1f;\n\t\t\tbase.Update();");
            Change(talk, "\t\tprivate float alpha = 1f;", "\t\tprivate float alpha = 1f;\n" +
                "\t\tinternal float AppleEverestCaveAlpha { get => alpha; set => alpha = value; }");
            Change(talk, "\t\tpublic override void Awake(Scene scene)\n\t\t{",
                "\t\tpublic override void Awake(Scene scene)\n\t\t{\n" +
                "\t\t\tAppleEverestOriginalAwake(scene);\n" +
                "\t\t\tglobal::Celeste.Mod.AppleEverestCaveWall.AfterTalkAwake(this);\n\t\t}\n\n" +
                "\t\tprivate void AppleEverestOriginalAwake(Scene scene)\n\t\t{");
            Change(talk, "\t\tpublic override void Update()\n\t\t{",
                "\t\tpublic override void Update()\n\t\t{\n" +
                "\t\t\tAppleEverestOriginalUpdate();\n" +
                "\t\t\tglobal::Celeste.Mod.AppleEverestCaveWall.AfterTalkUpdate(this);\n\t\t}\n\n" +
                "\t\tprivate void AppleEverestOriginalUpdate()\n\t\t{");
            Change(Path.Combine(managedRoot, "Celeste", "PlayerDeadBody.cs"),
                "\tpublic override void Awake(Scene scene)\n\t{",
                "\tpublic override void Awake(Scene scene)\n\t{\n" +
                "\t\tAppleEverestOriginalAwake(scene);\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestCaveWall.AfterDeadBodyAwake(this, scene);\n\t}\n\n" +
                "\tprivate void AppleEverestOriginalAwake(Scene scene)\n\t{");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestColoredWater.cs")))
        {
            string water = Path.Combine(managedRoot, "Celeste", "Water.cs");
            string player = Path.Combine(managedRoot, "Celeste", "Player.cs");
            Change(player, "if (Speed.Y < 0f && Speed.Y >= -60f)",
                "if (Speed.Y < 0f && Speed.Y >= -60f && AppleEverestIsOverWater())");
            Change(player, "\tprivate bool SwimCheck()", "\tprivate bool AppleEverestIsOverWater()\n\t{\n" +
                "\t\tRectangle bounds = Collider.Bounds;\n\t\tbounds.Height += 2;\n\t\treturn Scene.CollideCheck<Water>(bounds);\n\t}\n\n" +
                "\tprivate bool SwimCheck()");
            string interaction = Path.Combine(managedRoot, "Celeste", "WaterInteraction.cs");
            Change(interaction, "\tpublic WaterInteraction(Func<bool> isDashing)", """
                private Collider appleEverestWaterCollider;
                public WaterInteraction(Rectangle bounds, Func<bool> isDashing)
                    : this(new Hitbox(bounds.Width, bounds.Height, bounds.X, bounds.Y), isDashing) { }
                public WaterInteraction(Collider collider, Func<bool> isDashing) : this(isDashing)
                { appleEverestWaterCollider = collider; }
                public Vector2 AbsoluteCenter => Entity.Position + (appleEverestWaterCollider ?? Entity.Collider).Center;
                public Rectangle Bounds
                {
                    get
                    {
                        Collider original = Entity.Collider;
                        if (appleEverestWaterCollider != null) Entity.Collider = appleEverestWaterCollider;
                        Rectangle result = Entity.Collider.Bounds;
                        Entity.Collider = original;
                        return result;
                    }
                }
                public bool Check(Entity water)
                {
                    Collider original = Entity.Collider;
                    if (appleEverestWaterCollider != null) Entity.Collider = appleEverestWaterCollider;
                    bool result = water.CollideCheck(Entity);
                    Entity.Collider = original;
                    return result;
                }

                """ + "\tpublic WaterInteraction(Func<bool> isDashing)");
            Change(water, "\t\t\tEntity entity = component.Entity;",
                "\t\t\tVector2 appleEverestWaterCenter = component.AbsoluteCenter;\n\t\t\tEntity entity = component.Entity;");
            Change(water, "bool flag2 = CollideCheck(entity);", "bool flag2 = component.Check(this);");
            Change(water, "base.Scene.CollideCheck<Solid>(new Rectangle((int)entity.Center.X - 4, (int)entity.Center.Y, 8, 16))",
                "Scene.CollideCheck<Solid>(new Vector2(component.Bounds.Left, Top + 8f), new Vector2(component.Bounds.Right, Top + 8f))");
            string waterSource = File.ReadAllText(water);
            if (waterSource.Split("entity.Center", StringSplitOptions.None).Length - 1 != 9)
                throw new InvalidDataException("semantic WaterInteraction center target changed");
            File.WriteAllText(water, waterSource.Replace("entity.Center", "appleEverestWaterCenter", StringComparison.Ordinal));
            foreach (string color in new[] { "FillColor", "SurfaceColor" })
                Change(water, "public static readonly Color " + color, "public static Color " + color);
            Change(water, "\tprivate Rectangle fill;", "\tprivate Rectangle fill;\n" +
                "\tinternal Rectangle AppleEverestFill { get => fill; set => fill = value; }\n" +
                "\tinternal HashSet<WaterInteraction> AppleEverestContains => contains;");
            Change(water, "\t\tprivate float timer;", "\t\tprivate float timer;\n" +
                "\t\tinternal float appleEverestTimer { get => timer; set => timer = value; }\n" +
                "\t\tinternal VertexPositionColor[] appleEverestMesh => mesh;\n" +
                "\t\tinternal int appleEverestFillIndex => fillStartIndex;\n" +
                "\t\tinternal int appleEverestSurfaceIndex => surfaceStartIndex;\n" +
                "\t\tinternal int appleEverestRayIndex => rayStartIndex;");
            Change(water, "\t\tpublic void Update()\n\t\t{", "\t\tpublic void Update()\n\t\t{\n" +
                "\t\t\tif (global::Celeste.Mod.AppleEverestColoredWater.CurrentlyUpdating)\n\t\t\t{\n" +
                "\t\t\t\tglobal::Celeste.Mod.AppleEverestColoredWater.UpdateSurface(this);\n\t\t\t\treturn;\n\t\t\t}");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestItemCrystal.cs")))
            Change(Path.Combine(managedRoot, "Celeste", "Holdable.cs"), "\tprivate float cannotHoldDelay;",
                "\tprivate float cannotHoldDelay;\n\tinternal float AppleEverestCannotHoldDelay { set => cannotHoldDelay = value; }");
        if (File.Exists(Path.Combine(target, "AppleEverestFancySolidTiles.cs")))
        {
            string autotiler = Path.Combine(managedRoot, "Celeste", "Autotiler.cs");
            Change(autotiler, "\tprivate Tiles TileHandler(", """
                // FancyTileEntities' exact selected overlay algorithm, lowered
                // into its declaring type to remove the reflection dispatch.
                private VirtualMap<char> appleEverestFancyForceData;
                private Point appleEverestFancyStart;
                internal Generated AppleEverestFancyOverlay(VirtualMap<char> forceData,
                    int startX, int startY, VirtualMap<char> mapData)
                {
                    appleEverestFancyForceData = forceData;
                    appleEverestFancyStart = new Point(startX, startY);
                    TileGrid grid = new TileGrid(8, 8, forceData.Columns, forceData.Rows);
                    AnimatedTiles animated = new AnimatedTiles(forceData.Columns, forceData.Rows, GFX.AnimatedTilesBank);
                    Rectangle rectangle = new Rectangle(startX, startY, forceData.Columns, forceData.Rows);
                    try
                    {
                        for (int i = startX; i < startX + forceData.Columns; i += 50)
                            for (int j = startY; j < startY + forceData.Rows; j += 50)
                            {
                                if (!mapData.AnyInSegmentAtTile(i, j)) { j = j / 50 * 50; continue; }
                                for (int k = i; k < Math.Min(i + 50, startX + forceData.Columns); k++)
                                    for (int l = j; l < Math.Min(j + 50, startY + forceData.Rows); l++)
                                    {
                                        Tiles tiles = TileHandler(mapData, k, l, rectangle,
                                            forceData[k - startX, l - startY], default(Behaviour));
                                        if (tiles == null) continue;
                                        grid.Tiles[k - startX, l - startY] = Calc.Random.Choose(tiles.Textures);
                                        if (tiles.HasOverlays)
                                            animated.Set(k - startX, l - startY, Calc.Random.Choose(tiles.OverlapSprites));
                                    }
                            }
                    }
                    finally { appleEverestFancyForceData = null; }
                    return new Generated { TileGrid = grid, SpriteOverlay = animated };
                }

                """ + "\tprivate Tiles TileHandler(");
            Change(autotiler, "\tprivate bool CheckTile(TerrainType set, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour)\n\t{",
                "\tprivate bool CheckTile(TerrainType set, VirtualMap<char> mapData, int x, int y, Rectangle forceFill, Behaviour behaviour)\n\t{\n" +
                "\t\tif (appleEverestFancyForceData != null)\n\t\t{\n" +
                "\t\t\tint fx = x - appleEverestFancyStart.X, fy = y - appleEverestFancyStart.Y;\n" +
                "\t\t\tchar tile = fx < 0 || fy < 0 || fx >= appleEverestFancyForceData.Columns || fy >= appleEverestFancyForceData.Rows\n" +
                "\t\t\t\t? '\\0' : appleEverestFancyForceData[fx, fy];\n" +
                "\t\t\tif (tile == '0' || tile == '\\0') forceFill = Rectangle.Empty;\n" +
                "\t\t\telse if (set.Ignore(tile)) return false;\n\t\t}");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestFrostMoverSemantics.cs")))
        {
            Change(Path.Combine(managedRoot, "Celeste", "FlutterBird.cs"),
                "\t\t\tflyawaySfx.Play(\"event:/game/general/birdbaby_flyaway\");",
                "\t\t\tif (this is global::Celeste.Mod.AppleEverestFrostFlutterBird) Tag |= Tags.Persistent;\n" +
                "\t\t\tflyawaySfx.Play(\"event:/game/general/birdbaby_flyaway\");");
            // Wrap the whole method so the post-Awake callback also runs after
            // the no-additions early return. No per-entity Awake approximation.
            Change(Path.Combine(managedRoot, "Monocle", "EntityList.cs"),
                "\tpublic void UpdateLists()\n\t{",
                "\tpublic void UpdateLists()\n\t{\n" +
                "\t\tAppleEverestOriginalUpdateLists();\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestFrostOnSpawnActivator.AfterUpdateLists(Scene);\n\t}\n\n" +
                "\tprivate void AppleEverestOriginalUpdateLists()\n\t{");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestMaxColorSemantics.cs")))
        {
            Change(level, "\tprivate string lastColorGrade;", "\tinternal string lastColorGrade;");
            Change(level, "\tprivate float colorGradeEase;", "\tinternal float colorGradeEase;");
            Change(level, "\tprivate float colorGradeEaseSpeed = 1f;", "\tinternal float colorGradeEaseSpeed = 1f;");
            Change(level, "\t\tif (lastColorGrade != Session.ColorGrade)",
                "\t\tif (!global::Celeste.Mod.AppleEverestColorGradeFadeTrigger.SuppressGradeUpdate(this) && lastColorGrade != Session.ColorGrade)");
            string heat = Path.Combine(managedRoot, "Celeste", "HeatWave.cs");
            Change(heat, "\tprivate float heat;",
                "\tprivate float heat;\n\tinternal float AppleEverestHeat => heat;\n\tinternal float AppleEverestFade => fade;");
            Change(Path.Combine(managedRoot, "Celeste", "ExitBlock.cs"), "\tprivate EffectCutout cutout;",
                "\tprivate EffectCutout cutout;\n" +
                "\tinternal void AppleEverestSolidUpdate() { base.Update(); }\n" +
                "\tinternal float AppleEverestAlpha { get => tiles.Alpha; set { cutout.Alpha = tiles.Alpha = value; } }");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestCameraOffsetBorder.cs")))
        {
            Change(Path.Combine(managedRoot, "Celeste", "Player.cs"), "\tpublic Vector2 CameraTarget\n",
                "\tpublic Vector2 CameraTarget => global::Celeste.Mod.AppleEverestCameraOffsetBorder.CameraTarget(this, AppleEverestCameraTargetBase);\n" +
                "\tprivate Vector2 AppleEverestCameraTargetBase\n");
            // The accepted HookGen wrapper is not itself an iterator; running
            // this before its dispatch retains On.TransitionRoutine timing.
            Change(level, "\tprivate IEnumerator TransitionRoutine(LevelData next, Vector2 direction)\n\t{",
                "\tprivate IEnumerator TransitionRoutine(LevelData next, Vector2 direction)\n\t{\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestCameraOffsetBorder.BeginTransition(this);");
        }
        if (File.Exists(Path.Combine(target, "AppleEverestCameraTargetCrossfadeTrigger.cs")))
            Change(Path.Combine(managedRoot, "Celeste", "Player.cs"),
                "\tprivate HashSet<Trigger> triggersInside;",
                "\tprivate HashSet<Trigger> triggersInside;\n" +
                "\tinternal IEnumerable<Trigger> AppleEverestTriggersInside => triggersInside;");
        if (File.Exists(Path.Combine(target, "AppleEverestColoredBigWaterfall.cs")))
            Change(Path.Combine(managedRoot, "Celeste", "BigWaterfall.cs"),
                "\tprivate Color fillColor;",
                "\tprivate Color fillColor;\n" +
                "\tinternal void AppleEverestSetColors(Color surface, Color fill) { surfaceColor = surface; fillColor = fill; }");
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
            // Loading finishes before Level.Begin creates the gameplay buffers.
            // Consume masked backdrops only after that creation: its initial
            // Unload must not discard the newly loaded level's groups.
            Change(level, "\t\tGameplayBuffers.Create();",
                "\t\tGameplayBuffers.Create();\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.OnLevelLoaded(this);");
            Change(level, "\t\tDistort.Render((RenderTarget2D)GameplayBuffers.Gameplay,",
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.Render(this, false);\n" +
                "\t\tDistort.Render((RenderTarget2D)GameplayBuffers.Gameplay,");
            Change(level, "\t\tForeground.Render(this);",
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.Render(this, true, true);\n\t\tForeground.Render(this);\n" +
                "\t\tglobal::Celeste.Mod.AppleEverestSJMaskRendering.Render(this, true, false);");
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
