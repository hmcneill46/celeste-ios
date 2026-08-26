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
            "FixtureCollab_1_Lobby_a= Map A\nFixtureCollab_1_Lobby_a_author= Author A\n" +
            "FixtureCollab_1_Lobby_b= Map B\nFixtureCollab_1_Lobby_b_author= Author B\n", new UTF8Encoding(false));
        string lobbyXml = Path.Combine(root, "lobby.xml");
        File.WriteAllText(lobbyXml,
            "<Map><levels><level name=\"room\" x=\"0\" y=\"0\" width=\"320\" height=\"180\">" +
            "<entities><player id=\"1\" x=\"160\" y=\"96\" /></entities><triggers>" +
            "<appleEverestTrigger name=\"CollabUtils2/ChapterPanelTrigger\" id=\"2\" x=\"16\" y=\"64\" width=\"24\" height=\"32\" map=\"FixtureCollab/1-Lobby/a\" allowSaving=\"true\" returnToLobbyMode=\"SetReturnToHere\" />" +
            "<appleEverestTrigger name=\"CollabUtils2/ChapterPanelTrigger\" id=\"3\" x=\"280\" y=\"64\" width=\"24\" height=\"32\" map=\"FixtureCollab/1-Lobby/b\" allowSaving=\"true\" returnToLobbyMode=\"SetReturnToHere\" />" +
            "<appleEverestTrigger name=\"CollabUtils2/JournalTrigger\" id=\"4\" x=\"148\" y=\"128\" width=\"24\" height=\"32\" levelset=\"FixtureCollab/1-Lobby\" vanillaJournal=\"false\" showOnlyDiscovered=\"false\" />" +
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
        Pass(collab.Maps.All(value => value.AllowSaving && value.ReturnMode == "SetReturnToHere" &&
             value.ReturnRoom == "room" && value.ReturnX == 160f && value.ReturnY == 96f),
            "package-defined save/return authority is frozen");
        Pass(collab.JournalLevelSet == "FixtureCollab/1-Lobby" && !collab.JournalVanilla &&
             !collab.JournalShowOnlyDiscovered, "exact journal semantics frozen");
        Pass(generation.ManifestText.StartsWith("APPLE_EVEREST_STATIC_COLLAB_V1\n", StringComparison.Ordinal) &&
             generation.ManifestText.Contains("map\tFixtureCollab\t0\tFixtureCollab/1-Lobby/a\tMap A\tAuthor A", StringComparison.Ordinal),
            "versioned deterministic collab manifest emitted");
        Pass(generation.Source.Contains("GeneratedAppleEverestCollabManifest", StringComparison.Ordinal) &&
             generation.Source.Contains(generation.Sha256, StringComparison.Ordinal),
            "typed device manifest embeds its exact identity");

        string runtime = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestCollabRuntime.cs"));
        string factories = File.ReadAllText(Path.Combine(repository, "apple-everest/runtime/AppleEverestSemanticFactories.cs"));
        Pass(runtime.Contains("CollabUtils2_MapCompleted_", StringComparison.Ordinal) &&
             runtime.Contains("HeartGem == true", StringComparison.Ordinal),
            "completion flags derive from durable per-map heart state");
        Pass(runtime.Contains("collabutils2_returntolobby", StringComparison.Ordinal) &&
             runtime.Contains("LaunchPersistentAt", StringComparison.Ordinal),
            "subordinate pause route uses generated return authority");
        Pass(runtime.Contains("OpenReturnToLobbyConfirmMenu", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_returntolobby_confirm_save", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_returntolobby_confirm_donotsave", StringComparison.Ordinal) &&
             runtime.Contains("collabutils2_returntolobby_confirm_cancel", StringComparison.Ordinal) &&
             runtime.Contains("ReturnToLobby(level, menu, save: true)", StringComparison.Ordinal) &&
             runtime.Contains("ReturnToLobby(level, menu, save: false)", StringComparison.Ordinal),
            "return-to-lobby exposes the package-authored save, do-not-save, and cancel choices");
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
        Pass(runtime.Contains("CompleteMapAndReturn", StringComparison.Ordinal) &&
             runtime.Contains("RegisterHeartGem", StringComparison.Ordinal),
            "mini-heart completion persists before lobby return");
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
        string closureGenerator = File.ReadAllText(Path.Combine(repository,
            "tools/AppleEverestBuilder/ClosureGenerator.cs"));
        Pass(factories.Contains("ResolveSpinnerColor(EntityData data, CrystalColor fallback)", StringComparison.Ordinal) &&
             factories.Contains("authored.Equals(\"Red\", StringComparison.OrdinalIgnoreCase)", StringComparison.Ordinal) &&
             factories.Contains("return (CrystalColor)(-1)", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestProgressionRuntime.IsCustom(Session.Area)", StringComparison.Ordinal) &&
             closureGenerator.Contains("AppleEverestSemanticFactories.ResolveSpinnerColor(entity3, color)", StringComparison.Ordinal) &&
             closureGenerator.Contains("if ((int)color == -1)", StringComparison.Ordinal),
            "custom maps retain bounded authored spinner palettes including Everest core-mode semantics");
        string progressionRuntime = File.ReadAllText(Path.Combine(repository,
            "apple-everest/runtime/AppleEverestProgressionRuntime.cs"));
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
        Pass(progressionRuntime.Contains("presentation.DarknessAlpha", StringComparison.Ordinal) &&
             progressionRuntime.Contains("new AudioState(presentation.Music, presentation.Ambience)", StringComparison.Ordinal) &&
             progressionRuntime.Contains("Celeste.DropWipe", StringComparison.Ordinal) &&
             progressionRuntime.Contains("Inventory(presentation.Inventory)", StringComparison.Ordinal),
            "device AreaData uses the frozen map presentation rather than Prologue presentation defaults");
        Pass(progressionRuntime.Contains("Name = DialogKey(descriptor.Sid)", StringComparison.Ordinal) &&
             progressionRuntime.Contains("sid.Replace('/', '_').Replace('-', '_')", StringComparison.Ordinal),
            "custom AreaData names use Everest's generic SID-to-dialog-key normalisation");
        Pass(progressionRuntime.Contains("presentation.Icon == \"areas/null\" ? source.Icon : presentation.Icon",
                StringComparison.Ordinal),
            "canonical chapter select receives a valid vanilla fallback for Everest's no-custom-icon sentinel");
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
