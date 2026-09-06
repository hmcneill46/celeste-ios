using System.Text;

namespace AppleEverestBuilder;

internal static class CollabStaticTests
{
    internal static int Run(string repository, string temporary)
    {
        int passed = 0;
        void Pass(bool value, string name)
        {
            if (!value) throw new InvalidOperationException("FAIL: " + name);
            passed++;
        }
        void Reject(Action action, string text, string name)
        {
            try { action(); }
            catch (Exception exception) when (exception.Message.Contains(text, StringComparison.OrdinalIgnoreCase))
            { passed++; return; }
            throw new InvalidOperationException("FAIL: " + name);
        }

        Pass(StaticSemanticLowering.IncludeContent("Graphics/thing.png"),
            "static semantic lowering retains ordinary helper content");
        Pass(!StaticSemanticLowering.IncludeContent("bin/Helper.dll") &&
             !StaticSemanticLowering.IncludeContent("Code/Helper.cs") &&
             !StaticSemanticLowering.IncludeContent("Audio/Helper.bank") &&
             !StaticSemanticLowering.IncludeContent("Audio/Helper.guids.txt") &&
             !StaticSemanticLowering.IncludeContent("everest.yaml"),
            "static semantic lowering excludes executable/audio/metadata payloads");
        Pass(StaticSemanticLowering.CountsAsStrawberry(
                 new StaticSemanticFactory("entity", "Fixture/ReturningBerry", "strawberry-with-return")) &&
             !StaticSemanticLowering.CountsAsStrawberry(
                 new StaticSemanticFactory("entity", "Fixture/Block", "dream-move-block")) &&
             !StaticSemanticLowering.CountsAsStrawberry(
                 new StaticSemanticFactory("trigger", "Fixture/Trigger", "strawberry-with-return")),
            "progression collectible classification follows the resolved static runtime semantic");

        string root = Path.Combine(temporary, "collab-static");
        string staged = Path.Combine(root, "staged");
        string content = Path.Combine(root, "content");
        Directory.CreateDirectory(staged);
        Directory.CreateDirectory(content);
        File.WriteAllText(Path.Combine(staged, "CollabUtils2CollabID.txt"), "FixtureCollab\n", new UTF8Encoding(false));
        Directory.CreateDirectory(Path.Combine(staged, "Dialog"));
        File.WriteAllText(Path.Combine(staged, "Dialog", "English.txt"),
            "modname_FixtureCollab= Fixture Collab\n" +
            "FixtureCollab_0_Lobbies_lobby= Fixture Lobby\n" +
            "fixturecollab_1_lobby_a=\n    Map A\nFixtureCollab_1_Lobby_a_author= Author A\n" +
            "FixtureCollab_1_Lobby_b= Map B\nFixtureCollab_1_Lobby_b_author= Author B\n", new UTF8Encoding(false));
        string lobbyXml = Path.Combine(root, "lobby.xml");
        File.WriteAllText(lobbyXml,
            "<Map><levels><level name=\"room\" x=\"0\" y=\"0\" width=\"320\" height=\"180\">" +
            "<entities><player id=\"1\" x=\"160\" y=\"96\" />" +
            "<appleEverestEntity name=\"CollabUtils2/MiniHeartDoor\" id=\"6\" x=\"128\" y=\"64\" width=\"40\" height=\"56\" requires=\"2\" levelSet=\"FixtureCollab/1-Lobby\" color=\"advanced\" />" +
            "<appleEverestEntity name=\"CollabUtils2/RainbowBerry\" id=\"7\" x=\"160\" y=\"48\" levelSet=\"FixtureCollab/1-Lobby\" /></entities><triggers>" +
            "<appleEverestTrigger name=\"CollabUtils2/ChapterPanelTrigger\" id=\"2\" x=\"16\" y=\"64\" width=\"24\" height=\"32\" map=\"FixtureCollab/1-Lobby/a\" allowSaving=\"true\" returnToLobbyMode=\"SetReturnToHere\" />" +
            "<appleEverestTrigger name=\"CollabUtils2/ChapterPanelTrigger\" id=\"3\" x=\"280\" y=\"64\" width=\"24\" height=\"32\" map=\"FixtureCollab/1-Lobby/b\" allowSaving=\"false\" returnToLobbyMode=\"SetReturnToHere\" />" +
            "<appleEverestTrigger name=\"CollabUtils2/JournalTrigger\" id=\"4\" x=\"148\" y=\"128\" width=\"24\" height=\"32\" levelset=\"FixtureCollab/1-Lobby\" vanillaJournal=\"false\" showOnlyDiscovered=\"false\" />" +
            "<appleEverestTrigger name=\"CollabUtils2/JournalTrigger\" id=\"5\" x=\"148\" y=\"16\" width=\"24\" height=\"32\" levelset=\"FixtureCollab/1-Lobby\" vanillaJournal=\"false\" showOnlyDiscovered=\"false\" />" +
            "</triggers><solids /><bg /></level></levels><Filler /><Style><Backgrounds /><Foregrounds /></Style>" +
            "<meta Icon=\"areas/temple\" TitleBaseColor=\"6C7C81\" TitleAccentColor=\"2F344B\" " +
            "TitleTextColor=\"FFFFFF\" IntroType=\"WakeUp\" Dreaming=\"false\" ColorGrade=\"none\" " +
            "Wipe=\"Celeste.DropWipe\" DarknessAlpha=\"0.15\" BloomBase=\"0\" BloomStrength=\"1\" " +
            "Jumpthru=\"wood\" CoreMode=\"None\"><mode Inventory=\"Farewell\" StartLevel=\"room\" " +
            "HeartIsEnd=\"true\" IgnoreLevelAudioLayerData=\"false\"><audiostate " +
            "Music=\"event:/music/remix/05_mirror_temple\" Ambience=\"event:/env/amb/05_interior_main\" />" +
            "</mode></meta></Map>",
            new UTF8Encoding(false));
        string lobbyLogical = ContentCompiler.Stage(lobbyXml, "Content/Maps/FixtureCollab/0-Lobbies/lobby.xml", content);
        string lobbyPath = "Maps/FixtureCollab/0-Lobbies/lobby.bin";
        string mapAPath = "Maps/FixtureCollab/1-Lobby/a.bin";
        string mapBPath = "Maps/FixtureCollab/1-Lobby/b.bin";
        string mapAXml = Path.Combine(root, "map-a.xml");
        string mapBXml = Path.Combine(root, "map-b.xml");
        File.WriteAllText(mapAXml,
            "<Map><levels><level name=\"a-room\" x=\"0\" y=\"0\" width=\"320\" height=\"180\"><entities>" +
            "<player id=\"1\" x=\"16\" y=\"16\" /><appleEverestEntity name=\"CollabUtils2/MiniHeart\" id=\"2\" x=\"280\" y=\"80\" />" +
            "<appleEverestEntity name=\"CollabUtils2/SilverBerry\" id=\"3\" x=\"40\" y=\"40\" />" +
            "<appleEverestEntity name=\"CollabUtils2/SpeedBerry\" id=\"4\" x=\"64\" y=\"40\" goldTime=\"5\" silverTime=\"10\" bronzeTime=\"15\" />" +
            "</entities><triggers /><solids /><bg /></level></levels><Filler /><Style><Backgrounds /><Foregrounds /></Style></Map>",
            new UTF8Encoding(false));
        File.WriteAllText(mapBXml,
            "<Map><levels><level name=\"b-room\" x=\"0\" y=\"0\" width=\"320\" height=\"180\"><entities>" +
            "<player id=\"1\" x=\"16\" y=\"16\" /><appleEverestEntity name=\"CollabUtils2/MiniHeart\" id=\"2\" x=\"280\" y=\"80\" />" +
            "</entities><triggers /><solids /><bg /></level></levels><Filler /><Style><Backgrounds /><Foregrounds /></Style></Map>",
            new UTF8Encoding(false));
        _ = ContentCompiler.Stage(mapAXml, "Content/" + mapAPath[..^4] + ".xml", content);
        _ = ContentCompiler.Stage(mapBXml, "Content/" + mapBPath[..^4] + ".xml", content);
        ContentMountRecord[] mounts =
        [
            new("FixturePackage", 0, lobbyPath, lobbyPath, new string('1', 64), new string('2', 64)),
            new("FixturePackage", 0, mapAPath, mapAPath, new string('3', 64), new string('4', 64)),
            new("FixturePackage", 0, mapBPath, mapBPath, new string('5', 64), new string('6', 64))
        ];
        string compiledLobby = Path.Combine(content, lobbyLogical.Replace('/', Path.DirectorySeparatorChar));
        string expectedLobby = Path.Combine(content, lobbyPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(expectedLobby)!);
        if (!Path.GetFullPath(compiledLobby).Equals(Path.GetFullPath(expectedLobby), StringComparison.Ordinal))
            File.Copy(compiledLobby, expectedLobby, overwrite: true);
        MapProgressionRecord inspectedLobby = ContentCompiler.InspectProgression(expectedLobby, lobbyPath,
            new string('2', 64));
        string berryXml = Path.Combine(root, "berry.xml");
        File.WriteAllText(berryXml,
            "<Map><levels><level name=\"berry-room\" x=\"0\" y=\"0\" width=\"320\" height=\"180\">" +
            "<entities><player id=\"1\" x=\"16\" y=\"16\" />" +
            "<checkpoint id=\"3\" x=\"32\" y=\"16\" checkpointID=\"4\" />" +
            "<appleEverestEntity name=\"Fixture/ReturningBerry\" id=\"2\" x=\"160\" y=\"90\" /></entities>" +
            "<triggers /><solids /><bg /></level></levels><Filler /><Style><Backgrounds /><Foregrounds /></Style>" +
            "<meta Icon=\"areas/temple\" IntroType=\"WakeUp\" Wipe=\"Celeste.DropWipe\" /></Map>",
            new UTF8Encoding(false));
        string berryLogical = ContentCompiler.Stage(berryXml,
            "Content/Maps/FixtureCollab/1-Lobby/berry.xml", content);
        string berryPath = Path.Combine(content, berryLogical.Replace('/', Path.DirectorySeparatorChar));
        File.WriteAllText(Path.ChangeExtension(berryPath, ".meta.yaml"),
            "Icon: areas/farewell\nIntroType: WalkInRight\nWipe: Celeste.Starfield\n",
            new UTF8Encoding(false));
        MapProgressionRecord unresolvedBerry = ContentCompiler.InspectProgression(berryPath,
            "Maps/FixtureCollab/1-Lobby/berry.bin", new string('7', 64));
        MapProgressionRecord resolvedBerry = ContentCompiler.InspectProgression(berryPath,
            "Maps/FixtureCollab/1-Lobby/berry.bin", new string('7', 64),
            new HashSet<string>(["Fixture/ReturningBerry"], StringComparer.Ordinal));
        Pass(unresolvedBerry.Strawberries == 0 && resolvedBerry.Strawberries == 1,
            "only an exact resolved static strawberry lowering contributes to authored map totals");
        Pass(resolvedBerry.Checkpoints.SequenceEqual(new[] { "berry-room" }),
            "authored checkpoint entities become deterministic chapter checkpoint levels");
        Pass(resolvedBerry.Presentation?.Icon == "areas/temple" &&
             resolvedBerry.Presentation.IntroType == "WakeUp" &&
             resolvedBerry.Presentation.Wipe == "Celeste.DropWipe",
            "embedded map metadata overrides the earlier sidecar just as it does during Everest MapData loading");
        MapPresentationRecord presentation = inspectedLobby.Presentation!;
        Pass(presentation.Icon == "areas/temple" && presentation.TitleBaseColor == "6c7c81" &&
             presentation.TitleAccentColor == "2f344b" && presentation.TitleTextColor == "ffffff" &&
             presentation.IntroType == "WakeUp" && !presentation.Dreaming && presentation.ColorGrade == "none" &&
             presentation.Wipe == "Celeste.DropWipe" && presentation.DarknessAlpha == 0.15f &&
             presentation.BloomBase == 0f && presentation.BloomStrength == 1f &&
             presentation.Inventory == "Farewell" && presentation.StartLevel == "room" &&
             presentation.HeartIsEnd && !presentation.IgnoreLevelAudioLayerData &&
             presentation.Music == "event:/music/remix/05_mirror_temple" &&
             presentation.Ambience == "event:/env/amb/05_interior_main",
            "map-authored presentation, inventory, and audio metadata are frozen exactly");
        MapProgressionRecord[] maps =
        [
            Map("FixtureCollab/0-Lobbies/lobby", "FixtureCollab/0-Lobbies", "room", false),
            Map("FixtureCollab/1-Lobby/a", "FixtureCollab/1-Lobby", "a-room", true),
            Map("FixtureCollab/1-Lobby/b", "FixtureCollab/1-Lobby", "b-room", true)
        ];
        FileRecord[] records =
        [
            Record(staged, "CollabUtils2CollabID.txt"),
            Record(staged, "Dialog/English.txt")
        ];
        ResolvedMod mod = new()
        {
            Metadata = new EverestYamlEntry { Name = "FixturePackage", Version = "1.0.0" },
            Input = new ModInput { SourcePath = Path.Combine(root, "directory-input"), StagingRoot = staged,
                SourceSha256 = new string('a', 64), Files = records,
                Metadata = [new EverestYamlEntry { Name = "FixturePackage", Version = "1.0.0" }] },
            Classification = CompatibilityClass.CONTENT_ONLY,
            Mechanisms = new(StringComparer.Ordinal), ManagedFiles = [], ContentFiles = [],
            ManagedDetourTargets = new(StringComparer.Ordinal), DirectManagedHooks = [],
            ModInteropRegistrations = [], FrozenIlTransforms = []
        };

        CollabGeneration generation = CollabManifestGenerator.Generate([mod], mounts, content, maps);
        Pass(generation.Collabs.Count == 1 && generation.Collabs[0].Id == "FixtureCollab",
            "one exact static collab generated");
        CollabDescriptorRecord collab = generation.Collabs[0];
        Pass(collab.LobbySid == "FixtureCollab/0-Lobbies/lobby" && collab.Maps.Count == 2,
            "one lobby owns exactly two subordinate maps");
        Pass(collab.Maps.Select(value => value.Sid).SequenceEqual(new[] {
            "FixtureCollab/1-Lobby/a", "FixtureCollab/1-Lobby/b" }),
            "chapter-panel X order deterministically orders maps");
        Pass(collab.Maps[0].DisplayName == "Map A" && collab.Maps[0].Author == "Author A" &&
             collab.Maps[1].DisplayName == "Map B" && collab.Maps[1].Author == "Author B",
            "dialog-backed map titles and authors are frozen");
        Pass(collab.Maps[0].AllowSaving && !collab.Maps[1].AllowSaving &&
             collab.Maps.All(value => value.ReturnMode == "SetReturnToHere" &&
                 value.ReturnRoom == "room" && value.ReturnX == 160f && value.ReturnY == 96f),
            "package-defined save/return authority preserves explicit saving policies");
        Pass(collab.JournalLevelSet == "FixtureCollab/1-Lobby" && !collab.JournalVanilla &&
             !collab.JournalShowOnlyDiscovered, "multiple ordinary journal stations with identical semantics are frozen once");
        Pass(collab.MiniHeartDoors.Count == 1 && collab.MiniHeartDoors[0].Requires == 2 &&
             collab.MiniHeartDoors[0].ContributingMapSids.SequenceEqual(new[] {
                 "FixtureCollab/1-Lobby/a", "FixtureCollab/1-Lobby/b" }),
            "mini-heart door threshold and every contributing map are frozen from authored binaries");
        Pass(collab.LobbySpecialBerries.Count == 1 &&
             collab.LobbySpecialBerries[0].EntityType == "CollabUtils2/RainbowBerry" &&
             collab.LobbySpecialBerries[0].SemanticClass == "REQUIRED_BY_GRAPH" &&
             collab.Maps[0].MiniHeartCount == 1 && collab.Maps[1].MiniHeartCount == 1 &&
             collab.Maps[0].SpecialBerries.Select(value => value.EntityType).SequenceEqual(new[] {
                 "CollabUtils2/SilverBerry", "CollabUtils2/SpeedBerry" }),
            "typed special-berry graph and durability inputs are frozen without device discovery");
        Pass(generation.ManifestText.StartsWith("APPLE_EVEREST_STATIC_COLLAB_V2\n", StringComparison.Ordinal) &&
             generation.ManifestText.Contains("map\tFixtureCollab\t0\tFixtureCollab/1-Lobby/a\tMap A\tAuthor A", StringComparison.Ordinal),
            "versioned deterministic collab manifest emitted");
        Pass(generation.Source.Contains("GeneratedAppleEverestCollabManifest", StringComparison.Ordinal) &&
             generation.Source.Contains(generation.Sha256, StringComparison.Ordinal),
            "typed device manifest embeds its exact identity");

        string runtime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestCollabRuntime.cs"));
        string factories = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestSemanticFactories.cs"));
        string closureGenerator = File.ReadAllText(Path.Combine(repository,
            "tools/AppleEverestBuilder/ClosureGenerator.cs"));
        Pass(runtime.Contains("CollabUtils2_MapCompleted_", StringComparison.Ordinal) &&
             runtime.Contains("HeartGem == true", StringComparison.Ordinal),
            "completion flags derive from durable per-map heart state");
        Pass(runtime.Contains("!IsHeartSide(map.Sid)", StringComparison.Ordinal) &&
             runtime.Contains("OrderBy(map => map.Sid, StringComparer.Ordinal)", StringComparison.Ordinal) &&
             runtime.Contains("EndsWith(\"/ZZ-HeartSide\", StringComparison.Ordinal)", StringComparison.Ordinal),
            "ordinary collab journals exclude separately gated heart sides and use authored SID order");
        Pass(runtime.Contains("collabutils2_returntolobby", StringComparison.Ordinal) &&
             runtime.Contains("Engine.Scene = new ReturnScene(lobby, room, spawn)", StringComparison.Ordinal),
            "subordinate pause route uses generated return authority");
        Pass(runtime.Contains("OpenReturnToLobbyConfirmMenu", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_returntolobby_confirm_save", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_returntolobby_confirm_donotsave", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_returntolobby_confirm_cancel", StringComparison.Ordinal) &&
             runtime.Contains("menu_return_continue", StringComparison.Ordinal) &&
             runtime.Contains("menu_return_cancel", StringComparison.Ordinal) &&
             runtime.Contains("ReturnToLobby(level, menu, save: true)", StringComparison.Ordinal) &&
             runtime.Contains("ReturnToLobby(level, menu, save: false)", StringComparison.Ordinal),
            "return-to-lobby exposes both save-enabled and compact no-save confirmation choices");
        Pass(runtime.Contains("if (allowSaving)", StringComparison.Ordinal) &&
             runtime.Contains("OpenReturnToLobbyConfirmMenu(level, returnIndex, saving)", StringComparison.Ordinal) &&
             !runtime.Contains("else\n            {\n                level.Paused = true;\n                level.PauseLock = true;\n                level.Add(new AppleEverestCollabTransition(() => ReturnNow(level)));", StringComparison.Ordinal),
            "every chapter panel confirms return while its authored saving policy selects the menu shape");
        Pass(runtime.Contains("level.Pause(returnIndex, minimal: false)", StringComparison.Ordinal) &&
             runtime.Contains("SaveData.Instance?.AddDeath(level.Session.Area)", StringComparison.Ordinal),
            "return-to-lobby cancel restores pause selection and save follows vanilla death semantics");
        Pass(runtime.Contains("Tag = (int)Tags.PauseUpdate | (int)Tags.FrozenUpdate", StringComparison.Ordinal) &&
             runtime.Contains("while (UserIO.Saving) yield return null", StringComparison.Ordinal),
            "return-to-lobby save barrier continues while the Level is paused");
        Pass(runtime.Contains("level.Add(new AppleEverestCollabTransition(StartSelectedMap", StringComparison.Ordinal) &&
             !runtime.Contains("panel.Add(new Coroutine(StartSelectedMap", StringComparison.Ordinal) &&
             runtime.Contains("internal AppleEverestCollabTransition(IEnumerator routine)", StringComparison.Ordinal) &&
             runtime.Contains("Add(new Coroutine(Run(routine)))", StringComparison.Ordinal),
            "chapter map launch survives temporary chapter-panel teardown and asynchronous saves");
        Pass(runtime.Contains("wrapper.WrappedScene.GetUI<OuiChapterPanel>()", StringComparison.Ordinal) &&
             runtime.Contains("chapterPanel?.RemoveSelf()", StringComparison.Ordinal) &&
             runtime.Contains("wrapper.WrappedScene.Entities.UpdateLists()", StringComparison.Ordinal),
            "wrapped chapter panel is removed and flushed before its Metal-backed HUD target is released");
        Pass(runtime.Contains("string postcardKey = area?.Name + \"_postcard\"", StringComparison.Ordinal) &&
             runtime.Contains("Dialog.Has(postcardKey)", StringComparison.Ordinal) &&
             runtime.Contains("!continueSession && session.StartedFromBeginning", StringComparison.Ordinal) &&
             runtime.Contains("new AppleEverestCollabLevelEnter(session, Dialog.Get(postcardKey))", StringComparison.Ordinal) &&
             runtime.Contains("new Postcard(message,", StringComparison.Ordinal),
            "fresh collab starts preserve Everest's content-driven custom-map postcards while Continue skips them");
        Pass(runtime.Contains("SuspendCurrentSession", StringComparison.Ordinal) &&
             runtime.Contains("HasSuspendedSession", StringComparison.Ordinal) &&
             runtime.Contains("TryTakeSuspendedSession", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_chapterpanel_start", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_chapterpanel_continue", StringComparison.Ordinal) &&
             runtime.Contains("CheckpointLevelName = ContinueCheckpoint", StringComparison.Ordinal) &&
             runtime.Contains("panel.option = 1", StringComparison.Ordinal) &&
             runtime.Contains("ShouldDrawVanillaCheckpoint", StringComparison.Ordinal),
            "save-and-return exposes authentic Start Over and Continue bookmarks without routing synthetic options through vanilla checkpoint indexing");
        Pass(runtime.Contains("UsesSyntheticBookmarks", StringComparison.Ordinal) &&
             runtime.Contains("descriptor.Checkpoints.Length == 0", StringComparison.Ordinal) &&
             runtime.Contains("CheckpointPreviewName", StringComparison.Ordinal) &&
             runtime.Contains("MTN.Checkpoints.Has(key)", StringComparison.Ordinal) &&
             !runtime.Contains("session.Level = descriptor.Presentation.StartLevel", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestProgressionRuntime.StartLevel(Area)", StringComparison.Ordinal) &&
             closureGenerator.Contains("return GetAt(Vector2.Zero) ?? Levels.FirstOrDefault()", StringComparison.Ordinal),
            "authored checkpoint maps retain their full-height named photo list and resolve metadata start rooms before Session initialization");
        Pass(runtime.Contains("ResolveSessionCheckpoint(checkpoint)", StringComparison.Ordinal) &&
             runtime.Contains("return string.IsNullOrEmpty(checkpoint) ? null : checkpoint", StringComparison.Ordinal) &&
             !runtime.Contains("descriptor?.Presentation?.StartLevel", StringComparison.Ordinal) &&
             runtime.Contains("new Session(new AreaKey(ResolveArea(sid).ID), sessionCheckpoint)", StringComparison.Ordinal) &&
             runtime.Contains("checkpoint={session.StartCheckpoint ?? \"<none>\"} beginning={session.StartedFromBeginning}", StringComparison.Ordinal),
            "collab Start preserves beginning semantics and the effective authored intro while named photos remain explicit checkpoints");
        Pass(runtime.Contains("CompleteMapAndReturn", StringComparison.Ordinal) &&
             runtime.Contains("RegisterHeartGem", StringComparison.Ordinal) &&
             runtime.Contains("level.TimerStopped = true", StringComparison.Ordinal) &&
             runtime.Contains("level.DoScreenWipe(false, () => ReturnNow(level), false)", StringComparison.Ordinal) &&
             !runtime.Contains("level.Paused = true;\n        level.PauseLock = true;\n        UserIO.SaveHandler(file: true, settings: false);\n        level.Add(new AppleEverestCollabTransition(() => ReturnNow(level)))", StringComparison.Ordinal),
            "mini-heart completion persists and runs the map-authored wipe before lobby return");
        Pass(runtime.Contains("CollabUtils2/miniheart/\" + spriteName + \"/", StringComparison.Ordinal) &&
             runtime.Contains("AnimationFrames", StringComparison.Ordinal) &&
             runtime.Contains("case \"intermediate\"", StringComparison.Ordinal) &&
             runtime.Contains("case \"expert\"", StringComparison.Ordinal) &&
             runtime.Contains("Color.Orange", StringComparison.Ordinal),
            "mini-hearts use the package-authored tier sprites, animation, and palette instead of a generic blue heart");
        Pass(runtime.Contains("level.FormationBackdrop.Display = true", StringComparison.Ordinal) &&
             runtime.Contains("for (float time = 0f; time < 2f; time += Engine.RawDeltaTime)", StringComparison.Ordinal) &&
             runtime.Contains("Engine.TimeRate = Calc.Approach", StringComparison.Ordinal) &&
             runtime.Contains("level.Frozen = true", StringComparison.Ordinal) &&
             runtime.Contains("CompleteMapAndReturn(level)", StringComparison.Ordinal),
            "mini-heart collection preserves the bounded CollabUtils formation and closing sequence before lobby return");
        Pass(factories.Contains("AppleEverestDreamMoveBlockController", StringComparison.Ordinal) &&
             factories.Contains("AppleEverestStationBlock", StringComparison.Ordinal) &&
             factories.Contains("OnDashCollide = OnDashed", StringComparison.Ordinal),
            "selected Communal helper behavior is repository-owned and dash-driven");
        Pass(factories.Contains("objects/CommunalHelper/stationBlock/", StringComparison.Ordinal) &&
             factories.Contains("moon_block", StringComparison.Ordinal) &&
             factories.Contains("moonTrack/", StringComparison.Ordinal) &&
             factories.Contains("moonArrow/", StringComparison.Ordinal) &&
             !factories.Contains("Draw.HollowRect(Collider.Bounds, Color.LightBlue)", StringComparison.Ordinal),
            "StationBlock and track use the exact mounted CommunalHelper moon atlases rather than placeholders");
        Pass(factories.Contains("class AppleEverestFancyFakeWall", StringComparison.Ordinal) &&
             factories.Contains("data.Attr(\"tileData\"", StringComparison.Ordinal) &&
             factories.Contains("padded[x + 1, y + 1] = tileMap[x, y]", StringComparison.Ordinal) &&
             !factories.Contains("FancyFakeWall\" => new FakeWall", StringComparison.Ordinal),
            "FancyFakeWall preserves its authored mixed tile map and irregular collision mask");
        Pass(factories.Contains("class AppleEverestCrumbleBlockOnTouch", StringComparison.Ordinal) &&
             factories.Contains("GenerateOverlay(tileType", StringComparison.Ordinal) &&
             factories.Contains("PlayerBreakCheck()", StringComparison.Ordinal) &&
             factories.Contains("Session.DoNotLoad.Add(entityId)", StringComparison.Ordinal) &&
             !factories.Contains("CrumbleBlockOnTouch\" => new CrumblePlatform", StringComparison.Ordinal),
            "Shroom touch-crumble blocks preserve authored tiles, shape, touch behavior, and persistence");
        Pass(factories.Contains("class AppleEverestNoDashArea", StringComparison.Ordinal) &&
             factories.Contains("new DisplacementRenderHook(RenderDisplacement)", StringComparison.Ordinal) &&
             factories.Contains("Draw.Rect(Collider, Color.Red * 0.25f)", StringComparison.Ordinal) &&
             factories.Contains("ParticleSpeeds[index % ParticleSpeeds.Length]", StringComparison.Ordinal) &&
             factories.Contains("player.dashCooldownTimer = Engine.DeltaTime + 0.000001f", StringComparison.Ordinal) &&
             factories.Contains("Tween.TweenMode.YoyoLooping", StringComparison.Ordinal) &&
             !factories.Contains("if (player != null && CollideCheck(player)) player.Dashes = 0", StringComparison.Ordinal),
            "Frost no-dash areas retain moving red-particle visuals, displacement, flash, motion, and non-destructive dash suppression");
        Pass(factories.Contains("Vector2 trackOffset = center - selectedNode", StringComparison.Ordinal) &&
             factories.Contains("track.OffsetBy(trackOffset)", StringComparison.Ordinal) &&
             factories.Contains("FindConnectedTracks(scene, selectedTrack)", StringComparison.Ordinal) &&
             !factories.Contains("Position -= nodeOffset", StringComparison.Ordinal),
            "StationBlock preserves authored Solid positions so spikes and other StaticMovers attach before travel");
        Pass(factories.Contains("MoveToX(next.X, liftSpeed.X)", StringComparison.Ordinal) &&
             factories.Contains("MoveToY(next.Y, liftSpeed.Y)", StringComparison.Ordinal) &&
             factories.Contains("return DashCollisionResults.NormalCollision", StringComparison.Ordinal) &&
             !factories.Contains("return DashCollisionResults.Rebound", StringComparison.Ordinal),
            "StationBlock transports riders and attached geometry with canonical lift speed without camera-whipping rebound");
        Pass(factories.Contains("movementDelay = 0.2f", StringComparison.Ordinal) &&
             factories.Contains("ReactToDashImpact(direction)", StringComparison.Ordinal) &&
             factories.IndexOf("ReactToDashImpact(direction)", StringComparison.Ordinal) <
                 factories.IndexOf("if (selected == null)", StringComparison.Ordinal) &&
             factories.Contains("hitOffset = direction * 5f", StringComparison.Ordinal) &&
             factories.Contains("StartShaking(0.2f)", StringComparison.Ordinal) &&
             factories.Contains("Math.Abs(direction.Y) * 0.35f", StringComparison.Ordinal) &&
             factories.Contains("Calc.Approach(impactScale.X, 1f, 4f * Engine.DeltaTime)", StringComparison.Ordinal) &&
             factories.Contains("DrawCentered(visualCenter, Color.White, impactScale)", StringComparison.Ordinal),
            "StationBlock dash impact preserves CommunalHelper's squash, offset, shake, hold, and recovery animation even when the requested track direction is blocked");
        Pass(factories.Contains("class AppleEverestAttachedIceWall", StringComparison.Ordinal) &&
             factories.Contains("new ClimbBlocker(edge: false)", StringComparison.Ordinal) &&
             factories.Contains("climbBlocker.Blocking = true", StringComparison.Ordinal) &&
             factories.Contains("public override void Added(Scene scene)", StringComparison.Ordinal) &&
             factories.Contains("SolidChecker = solid => CollideCheck", StringComparison.Ordinal) &&
             factories.Contains("AppleEverestStaticRuntime.CreateStaticModSprite(spriteId)", StringComparison.Ordinal) &&
             factories.Contains("foreach (Sprite tile in tiles) tile.Play(\"ice\")", StringComparison.Ordinal) &&
             !factories.Contains("AttachedIceWall\" => new IceBlock", StringComparison.Ordinal),
            "Shroom attached ice walls activate after room attachment as visible two-pixel climb blockers carried and shaken by their neighbouring solids");
        Pass(factories.Contains("class AppleEverestCameraCatchupRuntime", StringComparison.Ordinal) &&
             factories.Contains("ResolveDivisor(float original, Player player)", StringComparison.Ordinal) &&
             factories.Contains("SessionStates.GetValue(session, static _ => new SessionState()).Speed = speed", StringComparison.Ordinal) &&
             factories.Contains("player.CollideCheck(trigger)", StringComparison.Ordinal) &&
             !factories.Contains("level.Camera.Position = Vector2.Lerp", StringComparison.Ordinal),
            "camera catch-up triggers replace the canonical interpolation divisor without directly jumping the camera");
        Pass(factories.Contains("private static DreamBlock CreateDreamMoveBlock", StringComparison.Ordinal) &&
             factories.Contains("DreamBlock block = new", StringComparison.Ordinal) &&
             factories.Contains("block.Add(new AppleEverestDreamMoveBlockController(data))", StringComparison.Ordinal) &&
             !factories.Contains("class AppleEverestDreamMoveBlock : DreamBlock", StringComparison.Ordinal),
            "DreamMoveBlock remains an exact canonical tracked entity with movement composed as a component");
        Pass(factories.Contains("AppleEverestGroupedTriggerSpikesUp", StringComparison.Ordinal) &&
             factories.Contains("triggerIfSameDirection", StringComparison.Ordinal) &&
             factories.Contains("killIfSameDirection", StringComparison.Ordinal) &&
             factories.Contains("danger/spikes/\" + spikeType + \"_up", StringComparison.Ordinal) &&
             factories.Contains("delayTimer = DelayTime", StringComparison.Ordinal) &&
             !factories.Contains("GroupedTriggerSpikesUp\" => new TriggerSpikes", StringComparison.Ordinal),
            "pinned grouped spikes retain whole-group delay, collision policy, and authored spike style");
        Pass(factories.Contains("block.CollideCheck<DreamBlock>(block.Position + amount)", StringComparison.Ordinal),
            "DreamMoveBlock movement collision uses the canonical tracked base without requesting a derived tracker key");
        Pass(factories.Contains("base(active: true, visible: true)", StringComparison.Ordinal) &&
             factories.Contains("objects/CommunalHelper/dreamMoveBlock/arrow", StringComparison.Ordinal) &&
             factories.Contains("class AppleEverestDreamMoveBlockArrow : Entity", StringComparison.Ordinal) &&
             factories.Contains("arrow = new AppleEverestDreamMoveBlockArrow(block, arrows[arrowIndex])", StringComparison.Ordinal) &&
             factories.Contains("texture.DrawCentered(block.Center, Color.White)", StringComparison.Ordinal) &&
             !factories.Contains("public override void Render()\n    {\n        if (Entity is DreamBlock block)", StringComparison.Ordinal),
            "DreamMoveBlock preserves its authored arrow through a companion entity because canonical Render bypasses components");
        Pass(factories.Contains("ResolveSpinnerColor(EntityData data, CrystalColor fallback)", StringComparison.Ordinal) &&
             factories.Contains("authored.Equals(\"Red\", StringComparison.OrdinalIgnoreCase)", StringComparison.Ordinal) &&
             factories.Contains("return (CrystalColor)(-1)", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestProgressionRuntime.IsCustom(Session.Area)", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestSemanticFactories.ResolveSpinnerColor(entity3, color)", StringComparison.Ordinal) &&
             closureGenerator.Contains("if ((int)color == -1)", StringComparison.Ordinal),
            "custom maps retain bounded authored spinner palettes including Everest core-mode semantics");
        Pass(closureGenerator.Contains("PatchAuthoredSpinnerVariants(Path.Combine(managedRoot, \"Celeste\", \"Level.cs\"))", StringComparison.Ordinal) &&
             closureGenerator.Split("Session.Area.ID == 10 || entity3.Bool(\\\"star\\\")", StringSplitOptions.None).Length == 3 &&
             closureGenerator.Split("Session.Level.StartsWith(\\\"d-\\\")) || entity3.Bool(\\\"dust\\\")", StringSplitOptions.None).Length == 3 &&
             closureGenerator.Contains("Add(new DustRotateSpinner(entity3, vector))", StringComparison.Ordinal) &&
             closureGenerator.Contains("Add(new DustTrackSpinner(entity3, vector))", StringComparison.Ordinal),
            "moving and circular vanilla spinner loaders retain Everest-authored star/dust variants while false values fall through to canonical blades");
        string progressionRuntime = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestProgressionRuntime.cs"));
        string secondCollabRuntime = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestSecondCollabRuntime.cs"));
        string staticRuntime = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestStaticRuntime.cs"));
        Pass(closureGenerator.Contains("IsSpriteBankXml(contentRoot, mount)", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestSpriteBankDescriptor", StringComparison.Ordinal) &&
             staticRuntime.Contains("MountStaticSpriteBanks();", StringComparison.Ordinal) &&
             staticRuntime.Contains("CreateStaticModSpriteOn", StringComparison.Ordinal) &&
             secondCollabRuntime.Contains("CreateStaticModSpriteOn(sprite, spriteId)", StringComparison.Ordinal) &&
             secondCollabRuntime.Contains("CreateStaticModSprite(\"CollabUtils2_holoRainbowBerry\")", StringComparison.Ordinal),
            "all build-time identified mod SpriteBank XMLs feed deterministic static custom-sprite lookup");
        string sceneWrapper = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestSceneWrappingEntity.cs"));
        Pass(runtime.Contains("new TalkComponent", StringComparison.Ordinal) &&
             runtime.Contains("PlayerMustBeFacing = false", StringComparison.Ordinal) &&
             runtime.Contains("new AppleEverestSceneWrappingEntity<Overworld>", StringComparison.Ordinal) &&
             runtime.Contains("new OuiJournalCover", StringComparison.Ordinal) &&
             runtime.Contains("AppleEverestCollabJournalProgress", StringComparison.Ordinal) &&
             sceneWrapper.Contains("WrappedScene.Begin()", StringComparison.Ordinal),
            "collab stations require talk and host canonical chapter-panel/journal UI in the active Level");
        Pass(closureGenerator.Contains("TryStartChapterPanel(this, checkpoint)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ConfigureJournalPages(this)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ChapterSubtitle(Area, chapter)", StringComparison.Ordinal) &&
             closureGenerator.Contains("NeedsChapterCheckpointPage(this)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ConfigureChapterCheckpoints(this)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ShouldDrawVanillaCheckpoint(this)", StringComparison.Ordinal) &&
             closureGenerator.Contains("public class Option", StringComparison.Ordinal) &&
             closureGenerator.Contains("new Oui[12]", StringComparison.Ordinal) &&
             closureGenerator.Contains("new global::Celeste.Mod.AppleEverestOuiEnterChapterPanel()", StringComparison.Ordinal) &&
             closureGenerator.Contains("new global::Celeste.Mod.AppleEverestOuiEnterJournal()", StringComparison.Ordinal),
            "canonical overworld UI has the bounded routing hooks and explicit static helper-Oui registry");
        Pass(closureGenerator.Contains("ConfigureChapterPanel(this)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ChapterSwapHeight(this, toHeight)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ShouldShowChapterDeaths(this)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ChapterAuthorOffset(this, -2f)", StringComparison.Ordinal) &&
             closureGenerator.Contains("ChapterTitleOffset(this, -18f)", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestIcon(int area)", StringComparison.Ordinal) &&
             runtime.Contains("icon.Position = panel.Position + panel.IconOffset", StringComparison.Ordinal) &&
             runtime.Contains("panel.selectingMode && UsesSyntheticBookmarks(forcedMapSid) ? 300 : fallback",
                 StringComparison.Ordinal),
            "forced collab chapter panels preserve the selected map icon, title-author order, authored height or compact synthetic-bookmark height, and normal-mode death count");
        Pass(closureGenerator.Contains("Graphics/Atlases/Checkpoints/", StringComparison.Ordinal) &&
             closureGenerator.Contains("atlas = \"Checkpoints\"", StringComparison.Ordinal) &&
             closureGenerator.Contains("CheckpointPreviewName(area, level)", StringComparison.Ordinal) &&
             staticRuntime.Contains("atlas = MTN.Checkpoints", StringComparison.Ordinal),
            "authored checkpoint polaroids are mounted and resolved by custom SID, mode, and checkpoint level");
        Pass(runtime.Contains("class IconCellFromGui", StringComparison.Ordinal) &&
             runtime.Contains("new IconCellFromGui(area.Icon, 60f, 50f)", StringComparison.Ordinal) &&
             runtime.Contains("CollabUtils2MinDeaths/\" + levelSet", StringComparison.Ordinal) &&
             runtime.Contains("MTN.Journal.Has(\"CollabUtils2MinDeaths/SpringCollab2020/1-Beginner\")", StringComparison.Ordinal) &&
             runtime.Contains("string speedBerryTexture = MTN.Journal.Has", StringComparison.Ordinal) &&
             runtime.Contains("CollabUtils2/speed_berry_pbs_heading", StringComparison.Ordinal) &&
             runtime.Contains("Dialog.Clean(\"journal_totals\")", StringComparison.Ordinal) &&
             runtime.Contains("mode.BestTime", StringComparison.Ordinal),
            "collab journal preserves per-map badges, minimum-death check, flag/best-time column, and totals row");
        Pass(progressionRuntime.Contains("presentation.DarknessAlpha", StringComparison.Ordinal) &&
             progressionRuntime.Contains("new AudioState(presentation.Music, presentation.Ambience)", StringComparison.Ordinal) &&
             progressionRuntime.Contains("Celeste.DropWipe", StringComparison.Ordinal) &&
             progressionRuntime.Contains("Inventory(presentation.Inventory)", StringComparison.Ordinal),
            "device AreaData uses the frozen map presentation rather than Prologue presentation defaults");
        Pass(progressionRuntime.Contains("Name = DialogKey(descriptor.Sid)", StringComparison.Ordinal) &&
             progressionRuntime.Contains("sid.Replace('/', '_').Replace('-', '_')", StringComparison.Ordinal),
            "custom AreaData names use Everest's generic SID-to-dialog-key normalisation");
        Pass(progressionRuntime.Contains("new CheckpointData(value, DialogKey(descriptor.Sid) + \"_\" + value)",
                StringComparison.Ordinal),
            "authored checkpoint labels resolve through Everest's canonical SID-plus-room dialog key");
        Pass(progressionRuntime.Contains("presentation.Icon == \"areas/null\" ? source.Icon : presentation.Icon",
                StringComparison.Ordinal),
            "canonical chapter select receives a valid vanilla fallback for Everest's no-custom-icon sentinel");
        Pass(factories.Contains("CollabUtils2/MiniHeartDoor", StringComparison.Ordinal) &&
             factories.Contains("CollabUtils2/RainbowBerry", StringComparison.Ordinal) &&
             factories.Contains("CollabUtils2/SilverBerry", StringComparison.Ordinal) &&
             factories.Contains("CollabUtils2/SpeedBerry", StringComparison.Ordinal) &&
             factories.Contains("MaxHelpingHand/SecretBerry", StringComparison.Ordinal) &&
             factories.Contains("EeveeHelper/FlagToggleModifier", StringComparison.Ordinal) &&
             factories.Contains("LunaticHelper/StrawberryGate", StringComparison.Ordinal),
            "second-collab entities are dispatched through the exact static semantic registry");
        Pass(factories.Contains("CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger", StringComparison.Ordinal) &&
             factories.Contains("CollabUtils2/RainbowBerryUnlockCutsceneTrigger", StringComparison.Ordinal) &&
             factories.Contains("CollabUtils2/SpeedBerryCollectTrigger", StringComparison.Ordinal) &&
             factories.Contains("everest/coreModeTrigger", StringComparison.Ordinal) &&
             factories.Contains("everest/crystalShatterTrigger", StringComparison.Ordinal),
            "second-collab and ordinary Everest triggers are dispatched without helper discovery");
        Pass(secondCollabRuntime.Contains("class AppleEverestMiniHeartDoor : HeartGemDoor", StringComparison.Ordinal) &&
             secondCollabRuntime.Contains("class AppleEverestRainbowBerry : Strawberry", StringComparison.Ordinal) &&
             secondCollabRuntime.Contains("class AppleEverestSpeedBerry : Strawberry", StringComparison.Ordinal) &&
             secondCollabRuntime.Contains("class AppleEverestFlagToggleModifier : Entity", StringComparison.Ordinal) &&
             secondCollabRuntime.Contains("class AppleEverestStrawberryGate : Solid", StringComparison.Ordinal) &&
             !secondCollabRuntime.Contains("Assembly.Load", StringComparison.Ordinal) &&
             !secondCollabRuntime.Contains("System.Reflection", StringComparison.Ordinal),
            "second-collab gameplay is repository-owned, typed, and contains no device-side code discovery");
        Pass(staticRuntime.Contains("EVEREST / PORT OPTIONS", StringComparison.Ordinal) &&
             staticRuntime.Contains("private static void OpenOptions(TextMenu parent)", StringComparison.Ordinal) &&
             staticRuntime.Contains("private static void PopulateOptions(TextMenu menu)", StringComparison.Ordinal) &&
             staticRuntime.Contains("CFBundleShortVersionString", StringComparison.Ordinal) &&
             staticRuntime.Contains("parent.Focused = true", StringComparison.Ordinal),
            "Everest diagnostics, modules, launches, and port identity live in one reversible Options submenu");
        Pass(closureGenerator.Contains("PatchSecondCollabSemantics", StringComparison.Ordinal) &&
             closureGenerator.Contains("SecondRealCollab:source-lowered-heart-door-and-special-berries:v1", StringComparison.Ordinal) &&
             closureGenerator.Contains("ConfigureHeartDoor(this, TopSolid, BotSolid", StringComparison.Ordinal) &&
             closureGenerator.Contains("ConfigureStrawberry(this, sprite, bloom, light)", StringComparison.Ordinal) &&
             progressionRuntime.Contains("CountsAsOrdinaryStrawberry", StringComparison.Ordinal) &&
             progressionRuntime.Contains("CollabUtils2/SilverBerry", StringComparison.Ordinal),
            "vanilla hosts are deterministically source-lowered for heart doors and special-berry progression");
        Pass(!runtime.Contains("Assembly.Load", StringComparison.Ordinal) &&
             !runtime.Contains("Everest.Content.Mods", StringComparison.Ordinal) &&
             !runtime.Contains("Directory.", StringComparison.Ordinal),
            "device collab runtime performs no archive/assembly/content discovery");

        File.WriteAllText(Path.Combine(staged, "CollabUtils2CollabID.txt"), "bad/id\n", new UTF8Encoding(false));
        Reject(() => CollabManifestGenerator.Generate([mod], mounts, content, maps), "invalid CollabUtils2 collab ID",
            "malformed collab ID fails closed");
        return passed;
    }

    private static FileRecord Record(string root, string relative)
    {
        string full = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        return new(relative, new FileInfo(full).Length, Hashing.FileSha256(full));
    }

    private static MapProgressionRecord Map(string sid, string levelSet, string room, bool completion) =>
        new("Maps/" + sid + ".bin", sid, levelSet, new string('b', 64), new string('c', 64), [room],
            0, completion, false, [], [], [], ["A"], completion);
}
