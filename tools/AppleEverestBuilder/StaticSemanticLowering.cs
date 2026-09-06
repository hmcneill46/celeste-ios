namespace AppleEverestBuilder;

// Exact helper releases used by Stage 25K-A contain broad desktop-Everest
// surfaces (runtime hooks, Lua, dynamic data, custom banks) which are neither
// needed by nor safe for this pinned collab.  The host therefore accepts only
// these complete archive/DLL identities and lowers only the map factories
// named below to repository-owned static implementations.  No helper DLL is
// shipped and no device-side discovery occurs.
internal static class StaticSemanticLowering
{
    private sealed record Registered(
        string Id, string Owner, string Version, string SourceSha256,
        string DllPath, string DllSha256, StaticSemanticFactory[] Factories,
        StaticSemanticDependency[]? EffectiveDependencies = null,
        string[]? ContentPrefixes = null,
        StaticSemanticModulePlan? Module = null,
        string[]? RuntimeFiles = null,
        StaticSemanticTracking[]? Tracking = null,
        StaticSemanticDependency[]? LinkRequirements = null,
        string[]? PooledEntityTypes = null);

    private static readonly Registered[] Registry =
    [
        new("brokemiahelper-1.8.5-selected-kj-v1", "BrokemiaHelper", "1.8.5",
            "8ca14d6791d481178e2383c965f8f810f6cd442a211ddafbb9cc7186db86602c",
            "bin/BrokemiaHelper.dll", "89408eb1fb4ab1f11312f5dcbb579d3747b7b5bf38f7bf2c62359ffe2c1df841",
            [new("entity", "BrokemiaHelper/caveWall", "cave-wall", "new AppleEverestCaveWall(data, offset)", true)],
            [], [], RuntimeFiles: ["AppleEverestCaveWall.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestCaveWall", [])]),
        new("cherryhelper-1.8.2-selected-kj-v1", "CherryHelper", "1.8.2",
            "3a157baabcc7b9a0157fdcb8404e0044f0fbf3926df7b7788b29923149d711dc",
            "Code/bin/CherryHelper.dll", "dd0951122a93ffce9cfd74e36415dff34473061eacb792cf758168080828a2fa",
            [
                new("entity", "CherryHelper/AssistRect", "assist-rectangle", "new AppleEverestAssistRectangle(data, offset)", true),
                new("entity", "CherryHelper/ItemCrystal", "item-crystal", "new AppleEverestItemCrystal(data, offset)", true),
                new("entity", "CherryHelper/ItemCrystalPedestal", "item-crystal-pedestal", "new AppleEverestItemCrystalPedestal(data, offset)", true)
            ], [], ["Graphics/Sprites.xml", "Graphics/Atlases/Gameplay/objects/itemCrystal/", "Graphics/Atlases/Gameplay/objects/itemCrystalPedestal/"],
            RuntimeFiles: ["AppleEverestAssistRectangle.cs", "AppleEverestItemCrystal.cs", "AppleEverestItemCrystalPedestal.cs", "AppleEverestItemCrystalCollider.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestItemCrystal", []), new("Celeste.Mod.AppleEverestItemCrystalCollider", [], true)]),
        new("femtohelper-1.15.22-selected-kj-v1", "FemtoHelper", "1.15.22",
            "19ff3a3b968c02082344ea11cb7b6df780a47d9118e1cae882d2e880d67257d2",
            "bin/FemtoHelper.dll", "f99b8ec46987b8dd5b0ce018f854d55f07519651d720d63ed7343eaf82963280",
            [
                new("entity", "FemtoHelper/CustomParallaxBigWaterfall", "femto-waterfall", "new AppleEverestFemtoWaterfall(data, offset)", true),
                new("entity", "FemtoHelper/ParticleEmitter", "particle-emitter", "new AppleEverestParticleEmitter(data, offset)", true),
                new("backdrop", "FemtoHelper/WindPetals", "wind-petals", "AppleEverestWindPetals.Create(data)", true)
            ], [], [], RuntimeFiles: ["AppleEverestFemtoWaterfall.cs", "AppleEverestParticleEmitter.cs", "AppleEverestWindPetals.cs", "AppleEverestSelectedProfileGuard.cs", "AppleEverestSelectedVisualHelpers.cs"], Tracking: [new("Celeste.Mod.AppleEverestParticleEmitter", [])]),
        new("flaglinesandsuch-1.6.80-selected-kj-v1", "FlaglinesAndSuch", "1.6.80",
            "897ade835828e742c64fefc2f85cc458ab6cbe3ae00c5a509000588b9aeccce2",
            "bin/FlaglinesAndSuch.dll", "42801778b18505cd4455e59e5c3865f3f0268580d73e256e886b531cd8025ede",
            [
                new("backdrop", "FlaglinesAndSuch/customDreamStars", "dream-stars", "AppleEverestDreamStars.Create(data)", true),
                new("backdrop", "FlaglinesAndSuch/customGodrays", "godrays", "AppleEverestGodrays.Create(data)", true)
            ], [], [], RuntimeFiles: ["AppleEverestDreamStars.cs", "AppleEverestGodrays.cs", "AppleEverestSelectedProfileGuard.cs"]),
        new("honlyhelper-1.7.5-selected-kj-v1", "HonlyHelper", "1.7.5",
            "cb8524306f28c0d04081dd63b5a3ccad4d7fecd5bc71970e06376606439298e3",
            "Code/bin/HonlyHelper.dll", "110a7d00881f4ad38b587ca51afdb0f03c4d3d0ac20cc46da3e665615932c367",
            [
                new("entity", "HonlyHelper/PettableCat", "pettable-cat", "new AppleEverestPettableCat(data, offset)", true),
                new("trigger", "HonlyHelper/CameraTargetCrossfadeTrigger", "camera-target-crossfade", "new AppleEverestCameraTargetCrossfadeTrigger(data, offset)", true)
            ], [], ["Graphics/Sprites.xml", "Graphics/Atlases/Gameplay/characters/HonlyHelper/pettableCat/"], RuntimeFiles: ["AppleEverestPettableCat.cs", "AppleEverestCameraTargetCrossfadeTrigger.cs", "AppleEverestSelectedProfileGuard.cs"], Tracking: [new("Celeste.Mod.AppleEverestCameraTargetCrossfadeTrigger", [])]),
        new("vivhelper-1.14.10-selected-kj-v1", "VivHelper", "1.14.10",
            "81468fa30222a69bb6c648ef6d8ddcace0d51a60172a1a0930bb823b3d3ff199",
            "dll/VivHelper.dll", "d843355cbce2be03611d46a200cc4b341888ec53deb29e940b18442bbdc4f55f",
            [
                new("entity", "VivHelper/CustomHangingLamp", "custom-hanging-lamp", "new AppleEverestCustomHangingLamp(data, offset)", true),
                new("entity", "VivHelper/CustomSpinner", "viv-spinner", "new AppleEverestVivSpinner(data, offset)", true)
            ], [], ["Graphics/Atlases/Gameplay/VivHelper/customHangingLamp/", "Graphics/Atlases/Gameplay/VivHelper/customSpinner/white/fg_white", "Graphics/Atlases/Gameplay/VivHelper/customSpinner/white/bg_white"],
            RuntimeFiles: ["AppleEverestCustomHangingLamp.cs", "AppleEverestVivSpinner.cs", "AppleEverestVivCrystalDebris.cs", "AppleEverestSelectedProfileGuard.cs"], Tracking: [new("Celeste.Mod.AppleEverestVivSpinner", [])],
            PooledEntityTypes: ["Celeste.Mod.AppleEverestVivCrystalDebris"]),
        new("pandorasbox-1.0.49-selected-kj-v1", "PandorasBox", "1.0.49",
            "7113d897271264cd6bd8089e06112c536970fb016138350a4eeaf98576651e04",
            "PandorasBox.dll", "d48168822a646892dc9850ebfcb75376cb80663cef7c2fceb9c36fe3d3af02e7",
            [
                new("entity", "pandorasBox/coloredWaterfall", "colored-waterfall", "new AppleEverestColoredWaterfall(data, offset)", true),
                new("entity", "pandorasBox/coloredBigWaterfall", "colored-big-waterfall", "new AppleEverestColoredBigWaterfall(data, offset)", true),
                new("entity", "pandorasBox/coloredWater", "colored-water", "new AppleEverestColoredWater(data, offset)", true),
                new("entity", "pandorasBox/entityActivator", "entity-activator", "new AppleEverestEntityActivator(data, offset)", true)
            ], [], [], RuntimeFiles: ["AppleEverestColoredWaterfall.cs", "AppleEverestColoredBigWaterfall.cs", "AppleEverestColoredWater.cs", "AppleEverestEntityActivator.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestColoredWater", ["Celeste.Water"]), new("Celeste.Mod.AppleEverestEntityActivator", [])],
            LinkRequirements: [new("XaphanHelper", "1.0.79"), new("FrostHelper", "1.80.1")]),
        new("collabutils2-1.13.4-henny-v1", "CollabUtils2", "1.13.4",
            "b987d25608874623453e2c75c441661b906d6fd907948699983c2f12ba14c88e",
            "bin/CollabUtils2.dll", "ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60",
            [
                new("entity", "CollabUtils2/LobbyMapController", "lobby-map-controller", "new AppleEverestLobbyMapController(data, offset)", true),
                new("entity", "CollabUtils2/LobbyMapMarker", "lobby-map-marker", "new AppleEverestLobbyMapMarker(data, offset)", true),
                new("entity", "CollabUtils2/LobbyMapWarp", "lobby-map-warp", "new AppleEverestLobbyMapWarp(data, offset)", true),
                new("trigger", "CollabUtils2/ChapterPanelTrigger", "chapter-panel"),
                new("trigger", "CollabUtils2/JournalTrigger", "journal"),
                new("entity", "CollabUtils2/MiniHeart", "mini-heart", "new AppleEverestCollabMiniHeart(data, offset, entityId)", true),
                new("entity", "CollabUtils2/GoldenBerryPlayerRespawnPoint", "golden-berry-respawn-point"),
                new("entity", "CollabUtils2/MiniHeartDoor", "mini-heart-door", "new AppleEverestCollabMiniHeartDoor(data, offset, entityId)", true),
                new("entity", "CollabUtils2/RainbowBerry", "rainbow-berry", "new AppleEverestCollabRainbowBerry(data, offset, entityId)", true),
                new("entity", "CollabUtils2/SilverBerry", "silver-berry"),
                new("entity", "CollabUtils2/SpeedBerry", "speed-berry"),
                new("trigger", "CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger", "mini-heart-door-unlock-trigger", "new AppleEverestCollabMiniHeartDoorUnlockTrigger(data, offset)", true),
                new("trigger", "CollabUtils2/RainbowBerryUnlockCutsceneTrigger", "rainbow-berry-unlock-trigger", "new AppleEverestCollabRainbowBerryUnlockTrigger(data, offset)", true),
                new("trigger", "CollabUtils2/SpeedBerryCollectTrigger", "speed-berry-collect-trigger")
            ], Module: CollabModule(), RuntimeFiles: ["AppleEverestFactoryCanaryStickers.cs", "AppleEverestCollabJournalStickers.cs", "AppleEverestCollabMiniHeart.cs", "AppleEverestCollabAbstractMiniHeart.cs", "AppleEverestCollabRainbowBerry.cs", "AppleEverestCollabHoloRainbowBerry.cs", "AppleEverestCollabRainbowBerryUnlockCutscene.cs", "AppleEverestCollabRainbowBerryUnlockTrigger.cs", "AppleEverestCollabRainbowBerryPerfectEffect.cs", "AppleEverestCollabMemorialText.cs", "AppleEverestCollabStrawberryHooks.cs", "AppleEverestCollabHeartDoor.cs", "AppleEverestCollabEngineAccess.cs", "AppleEverestCollabAssistSkipConfirmUI.cs", "AppleEverestLobbyMapController.cs", "AppleEverestLobbyMapMarker.cs", "AppleEverestLobbyMapWarp.cs", "AppleEverestLobbyMapUI.cs", "AppleEverestLobbyVisitManager.cs", "AppleEverestByteArray2D.cs", "AppleEverestButtonHelper.cs", "AppleEverestCollabState.cs", "AppleEverestCollabPresentation.cs", "AppleEverestCollabMapMetadata.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestCollabMiniHeartDoorUnlockTrigger", []), new("Celeste.Mod.AppleEverestLobbyMapController", []), new("Celeste.Mod.AppleEverestLobbyMapWarp", []), new("Celeste.Mod.AppleEverestLobbyMapUI", [])]),
        new("eeveehelper-1.12.5-kayonara-v1", "EeveeHelper", "1.12.5",
            "355db1937a979a577b96c93cf639e812a20551e5b3f531a9e1565fda31dcaa86",
            "Code/bin/EeveeHelper.dll", "1613b4f89a8dd119537052e6c85031e543ec65179e5c7ac7a431b283be9e8ad8",
            [new("entity", "EeveeHelper/FlagToggleModifier", "flag-toggle-modifier")]),
        new("xaphanhelper-1.0.79-kayonara-v1", "XaphanHelper", "1.0.79",
            "530ea4bbad7ac5cd43be692297d6d743e0a81e789d3408f121448759df7643be",
            "Code/bin/XaphanHelper.dll", "e408dc081cb79d89b3436724b74413a603189cb53e84af5844918df08717273c",
            [new("entity", "XaphanHelper/Slope", "xaphan-slope", "new AppleEverestXaphanSlope(data, offset)", true)],
            [], ["Graphics/Sprites.xml", "Graphics/Atlases/Gameplay/objects/XaphanHelper/Slope/cement.png", "Graphics/Atlases/Gameplay/characters/Xaphan/player/slopeSlide", "Graphics/Atlases/Gameplay/characters/Xaphan/player_no_backpack/slopeSlide"],
            RuntimeFiles: ["AppleEverestXaphanSlope.cs", "AppleEverestXaphanPlayerPlatform.cs", "AppleEverestXaphanFakePlayerPlatform.cs", "AppleEverestXaphanSlopeHooks.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestXaphanSlope", []), new("Celeste.Mod.AppleEverestXaphanPlayerPlatform", []), new("Celeste.Mod.AppleEverestXaphanFakePlayerPlatform", [])]),
        new("communalhelper-1.25.5-henny-v1", "CommunalHelper", "1.25.5",
            "c0637033de9d9d0e48883504cacb1006b3205a99f1310d2ecd04d2783f303205",
            "src/bin/Debug/net8.0/CommunalHelper.dll", "4011b959ed4e9cc4cb98bf43f6884ae81361fecc994787640553205029c94a8a",
            [
                new("entity", "CommunalHelper/DreamMoveBlock", "dream-move-block"),
                new("entity", "CommunalHelper/StationBlock", "station-block"),
                new("entity", "CommunalHelper/StationBlockTrack", "station-block-track")
            ]),
        new("maxhelpinghand-1.40.9-henny-v1", "MaxHelpingHand", "1.40.9",
            "5f0d558d6f2cbd02032677e70e5910084acbcd3ddaa7aaa005335d6a8fce4276",
            "bin/MaxHelpingHand.dll", "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6",
            [
                new("entity", "MaxHelpingHand/GroupedTriggerSpikesUp", "grouped-trigger-spikes-up"),
                new("entity", "MaxHelpingHand/CustomSummitCheckpoint", "custom-summit-checkpoint"),
                new("entity", "MaxHelpingHand/FlagSwitchGate", "flag-switch-gate"),
                new("entity", "MaxHelpingHand/FlagTouchSwitch", "flag-touch-switch"),
                new("entity", "MaxHelpingHand/SecretBerry", "secret-berry"),
                new("entity", "MaxHelpingHand/CustomTutorialWithNoBird", "custom-tutorial-with-no-bird",
                    "new AppleEverestCustomTutorialWithNoBird(data, offset, entityId)"),
                new("entity", "MaxHelpingHand/MoreCustomNPC", "more-custom-npc",
                    "new AppleEverestMoreCustomNpc(data, offset, entityId)"),
                new("trigger", "MaxHelpingHand/CameraCatchupSpeedTrigger", "camera-catchup-speed"),
                new("trigger", "MaxHelpingHand/FlagToggleCameraOffsetTrigger", "flag-toggle-camera-offset", "AppleEverestMaxFlagCamera.Offset(data, offset)", true),
                new("trigger", "MaxHelpingHand/FlagToggleCameraTargetTrigger", "flag-toggle-camera-target", "AppleEverestMaxFlagCamera.Target(data, offset)", true),
                new("trigger", "MaxHelpingHand/FlagToggleSmoothCameraOffsetTrigger", "flag-toggle-smooth-camera-offset", "AppleEverestMaxFlagCamera.Smooth(data, offset)", true),
                new("entity", "MaxHelpingHand/SidewaysJumpThru", "sideways-jump-thru", "new AppleEverestSidewaysJumpThru(data, offset)", true),
                new("entity", "MaxHelpingHand/SetFlagOnSpawnController", "set-flag-on-spawn", "new AppleEverestSetFlagOnSpawn(data, offset)", true),
                new("entity", "MaxHelpingHand/FlagExitBlock", "flag-exit-block", "new AppleEverestFlagExitBlock(data, offset)", true),
                new("backdrop", "MaxHelpingHand/HeatWaveNoColorGrade", "heatwave-no-grade", "new AppleEverestHeatWaveNoColorGrade()", true),
                new("trigger", "MaxHelpingHand/ColorGradeFadeTrigger", "color-grade-fade", "new AppleEverestColorGradeFadeTrigger(data, offset)", true),
                new("trigger", "MaxHelpingHand/CameraOffsetBorder", "camera-offset-border", "new AppleEverestCameraOffsetBorder(data, offset)", true),
                new("entity", "MaxHelpingHand/ParallaxFadeOutController", "parallax-fade-out", "new AppleEverestParallaxFadeOutController(data, offset)", true),
                new("entity", "MaxHelpingHand/StylegroundFadeController", "styleground-fade", "new AppleEverestStylegroundFadeController(data, offset)", true),
                new("entity", "MaxHelpingHand/RainbowSpinnerColorAreaController", "rainbow-spinner-color-area", "new AppleEverestRainbowSpinnerColorArea(data, offset)", true)
            ], ContentPrefixes:
            [
                "Graphics/Atlases/Gameplay/MaxHelpingHand/summitcheckpoints/",
                "Graphics/Atlases/Gameplay/objects/MaxHelpingHand/flagSwitchGate/",
                "Graphics/Atlases/Gameplay/objects/MaxHelpingHand/flagTouchSwitch/"
            ], RuntimeFiles: ["AppleEverestSidewaysJumpThru.cs", "AppleEverestEverestBaseEntitySemantics.cs", "AppleEverestMaxFlagSemantics.cs", "AppleEverestMaxColorSemantics.cs", "AppleEverestCameraOffsetBorder.cs", "AppleEverestStylegroundFadeController.cs", "AppleEverestParallaxFadeOutController.cs", "AppleEverestRainbowSpinnerColorArea.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestSidewaysJumpThru", []), new("Celeste.Mod.AppleEverestColorGradeFadeTrigger", []), new("Celeste.Mod.AppleEverestCameraOffsetBorder", []), new("Celeste.Mod.AppleEverestStylegroundFadeController", []), new("Celeste.Mod.AppleEverestParallaxFadeOutController", []), new("Celeste.Mod.AppleEverestRainbowSpinnerColorArea", []), new("Celeste.Mod.AppleEverestMoreCustomNpc", [])]),
        new("lunatichelper-1.1.1-henny-v1", "LunaticHelper", "1.1.1",
            "e7cef501937fc1bc07d1ff13e753fe920b4ccbbd4e4db4c0b2c4312de89fdd78",
            "LunaticHelper.dll", "fc08f00296551a6025c5e31422c6edd5e8136e6861459f44ffea909129c6a925",
            [
                new("entity", "LunaticHelper/StrawberryWithReturn", "strawberry-with-return", "new AppleEverestBubbleReturnBerry(data, offset, entityId)"),
                new("entity", "LunaticHelper/StrawberryGate", "strawberry-gate"),
                new("entity", "LunaticHelper/InvisibleLightSource", "invisible-light-source", "new AppleEverestInvisibleLightSource(data, offset)", true),
                new("backdrop", "LunaticHelper/CustomDust", "custom-dust", "AppleEverestCustomDust.Create(data)", true)
            ], RuntimeFiles: ["AppleEverestBubbleReturnBerry.cs", "AppleEverestCustomDust.cs", "AppleEverestInvisibleLightSource.cs", "AppleEverestSelectedProfileGuard.cs"]),
        new("shroomhelper-1.2.10-henny-v1", "ShroomHelper", "1.2.10",
            "76fa23d9dfabb8203dc2eee8ca406bcb8407766f6385ef7c28dc439be0e40034",
            "Code/bin/ShroomHelper.dll", "2428be4659522324b4b426452b17a095a78b857048d6340a0c4720151c08fa1d",
            [
                new("entity", "ShroomHelper/AttachedIceWall", "attached-ice-wall"),
                new("entity", "ShroomHelper/CrumbleBlockOnTouch", "crumble-block-on-touch")
            ]),
        // Explicitly reviewed advancement. The 1.79.1 profile remains separate;
        // this exact 1.80.1 entry accepts only its implemented selected factories.
        new("frosthelper-1.80.1-selected-kj-v1", "FrostHelper", "1.80.1",
            "9ff388dd81ac09d033c45d6e93a6c194f4a7714711e1cc5032bb30b5e2b89d69",
            "Code/FrostHelper/bin/FrostTempleHelper.dll", "723e204638703111c12746badd31b8d37cd41d4978d1a2d98315647406d880af",
            [
                new("entity", "FrostHelper/EntityMover", "frost-entity-mover", "new AppleEverestFrostEntityMover(data, offset)", true),
                new("entity", "FrostHelper/CustomFlutterBird", "frost-flutter-bird", "new AppleEverestFrostFlutterBird(data, offset)", true),
                new("entity", "FrostHelper/WireLamps", "frost-wire-lamps", "new AppleEverestFrostWireLamps(data, offset)", true),
                new("trigger", "FrostHelper/OnSpawnActivator", "frost-on-spawn", "new AppleEverestFrostOnSpawnActivator(data, offset)", true),
                new("entity", "FrostHelper/CustomFireBarrier", "frost-fire-barrier", "new AppleEverestFrostFireBarrier(data, offset)", true),
                new("entity", "FrostHelper/IceSpinner", "frost-ice-spinner", "new AppleEverestFrostSpinner(data, offset)", true),
                new("entity", "FrostHelper/DecalContainer", "frost-decal-container", "new AppleEverestFrostDecalContainerMaker(data, offset)", true)
            ], null, ["Graphics/Atlases/Gameplay/danger/FrostHelper/icecrystal/fg0", "Graphics/Atlases/Gameplay/danger/FrostHelper/icecrystal/bg.png"],
            RuntimeFiles: ["AppleEverestFrostMoverSemantics.cs", "AppleEverestFrostWireLamps.cs", "AppleEverestFrostFireBarrier.cs", "AppleEverestFrostLavaRect.cs", "AppleEverestSelectedVisualHelpers.cs", "AppleEverestSelectedProfileGuard.cs", "AppleEverestFrostSpinner.cs", "AppleEverestFrostSpinnerCollider.cs", "AppleEverestFrostSpinnerTextures.cs", "AppleEverestFrostCrystalDebris.cs", "AppleEverestFrostDebrisMotion.cs", "AppleEverestFrostOutlineImage.cs", "AppleEverestFrostDecalContainerMaker.cs", "AppleEverestFrostDecalContainerRenderer.cs", "AppleEverestFrostDecalContainer.cs", "AppleEverestFrostDecalContainerHelpers.cs", "AppleEverestFrostParallaxDecalRenderer.cs", "AppleEverestFrostDecalInfo.cs", "AppleEverestDecalSemantics.cs", "AppleEverestSelectedDecalRegistry.cs", "AppleEverestCoreState.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestFrostSpinner", []), new("Celeste.Mod.AppleEverestFrostSpinnerRenderer", []), new("Celeste.Mod.AppleEverestFrostOutlineImage", [], true)],
            LinkRequirements: [new("MaxHelpingHand", "1.40.9"), new("ExtendedVariantMode", "0.50.5")],
            PooledEntityTypes: ["Celeste.Mod.AppleEverestFrostCrystalDebris"]),
        new("frosthelper-1.79.1-henny-v1", "FrostHelper", "1.79.1",
            "72d289424dc289343b606f460f62b68c63dcab4f62488363be506bce609f1790",
            "Code/FrostHelper/bin/FrostTempleHelper.dll", "e1314eca75af6670569a58689c267746151d1ba428e8808607dde23499654971",
            [new("entity", "FrostHelper/NoDashArea", "no-dash-area")]),
        new("fancytileentities-1.6.2-henny-v1", "FancyTileEntities", "1.6.2",
            "e62e9cf62cdbe9f4fdeb27f1174aa6fed55ffc40f0485cd870c3f433ced79a29",
            "FancyTileEntities/bin/Debug/net452/FancyTileEntities.dll", "48c4ba952c602c8f40325392edf9975c86ea7a90cd25a3954b0efe7086b6f0a1",
            [
                new("entity", "FancyTileEntities/FancyFakeWall", "fancy-fake-wall"),
                new("entity", "FancyTileEntities/FancySolidTiles", "fancy-solid-tiles", "new AppleEverestFancySolidTiles(data, offset)", true)
            ], RuntimeFiles: ["AppleEverestFancySolidTiles.cs", "AppleEverestSelectedProfileGuard.cs"],
            Tracking: [new("Celeste.Mod.AppleEverestFancySolidTiles", ["Celeste.SolidTiles"])]),
        new("crystallinehelper-1.17.2-sj-beginner-v1", "CrystallineHelper", "1.17.2",
            "6a5fdd5a7b4ae95b77ebc351a5b663c872c67a5d09347752fbf423e7ae50deb2",
            "Code/bin/vitmod.dll", "456410258fbce4594c3e987d025bf651e1bd3f49d2d676a921d8ea9f27ba052a",
            [
                new("trigger", "vitellary/bloomstrengthtrigger", "bloom-strength",
                    "new AppleEverestBloomStrengthTrigger(data, offset)"),
                new("trigger", "vitellary/editdepthtrigger", "edit-depth",
                    "new AppleEverestEditDepthTrigger(data, offset)"),
                new("trigger", "vitellary/triggertrigger", "holdable-trigger-trigger",
                    "new AppleEverestTriggerTrigger(data, offset, entityId)"),
                new("entity", "appleEverest/stage25kfDepthTarget", "depth-target-canary",
                    "new AppleEverestStage25KFDepthTarget(data, offset)")
            ], [], [], RuntimeFiles: ["AppleEverestCrystallineSemantics.cs"], Tracking: [new("Celeste.Mod.AppleEverestTriggerTrigger", [])]),
        new("vortexhelper-1.2.19-sj-beginner-v1", "VortexHelper", "1.2.19",
            "c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b",
            "Code/bin/VortexHelper.dll", "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73",
            [new("entity", "VortexHelper/AttachedJumpThru", "attached-jump-thru",
                "new AppleEverestAttachedJumpThru(data, offset)")], [], [],
            RuntimeFiles: ["AppleEverestVortexSemantics.cs"], Tracking: [new("Celeste.Mod.AppleEverestAttachedJumpThru", [])]),
        new("contorthelper-1.5.5-sj-beginner-v1", "ContortHelper", "1.5.5",
            "d71860aa6259b612f63ffe5b3189f68a3a7130d6c5e7f49bd2f6edeb64fca45a",
            "ContortHelper.dll", "c3983e67e1b535fbb1e0f0a541e8c78ad8f4cd150f66636c783a83dc5ffb4488",
            [new("trigger", "ContortHelper/MadelineSpotlightModifierTrigger", "madeline-spotlight-modifier",
                "new AppleEverestMadelineSpotlightModifierTrigger(data, offset)")],
            [], [], RuntimeFiles: ["AppleEverestContortSemantics.cs"]),
        new("extendedvariantmode-0.50.5-sj-beginner-v1", "ExtendedVariantMode", "0.50.5",
            "0f07fdc4c3d177c90dd9c5fbc57e2cbe46872ce4f248a4259b1168081e768bef",
            "bin/ExtendedVariantMode.dll", "cb28846f7f7348ddb996f63498c97694616436e97ef34f67880b697ba024fe81",
            [
                new("trigger", "ExtendedVariantMode/FloatExtendedVariantFadeTrigger", "background-brightness-fade",
                    "new AppleEverestBackgroundBrightnessFadeTrigger(data, offset)"),
                new("trigger", "ExtendedVariantMode/ResetVariantsTrigger", "reset-slice-variants",
                    "new AppleEverestResetSliceVariantsTrigger(data, offset)")
            ], [], [], VariantModule(), ["AppleEverestVariantSemantics.cs"]),
        new("junglehelper-1.4.10-sj-beginner-v1", "JungleHelper", "1.4.10",
            "911457d12e30d0912eb2420a39dbde7dc892560cd8a512284dccb89d93ff6e49",
            "Code/bin/JungleHelper.dll", "fed840ade7250f05e38b70a81bcfe751ca87ff63274f4b568172868cacf56f8a",
            [new("entity", "JungleHelper/MossyWall", "mossy-wall", "new AppleEverestMossyWall(data, offset)")], [],
            ["Graphics/Atlases/Gameplay/JungleHelper/Moss/"], RuntimeFiles: ["AppleEverestJungleSemantics.cs"]),
        new("yetanotherhelper-1.2.5-sj-beginner-v1", "YetAnotherHelper", "1.2.5",
            "e48a8c6b1941ebef23853fdb65b4db487aa9aa3162935b975b5468675de93da6",
            "YetAnotherHelper/bin/Debug/net452/YetAnotherHelper.dll", "f48e16a568edf43913beac0684bd26b7dc12d45be4011a8b214e05db86b78aea",
            [new("entity", "YetAnotherHelper/BubbleField", "bubble-field", "new AppleEverestBubbleField(data, offset)")], [],
            ["Graphics/Atlases/Gameplay/particles/YetAnotherHelper/"], RuntimeFiles: ["AppleEverestBubbleSemantics.cs"]),
        new("strawberryjam2021-1.0.12-beginner-root-v1", "StrawberryJam2021", "1.0.12",
            "d5e68237d8371fa26ed5d804578ce5b1841673ba380d7438fb4fafcd73899063",
            "Code/StrawberryJam2021.dll", "8d5b9184204e7e7728965bcf95af5f150f6220dafbfe52fdd6c23e50d47e5258",
            [
                new("entity", "SJ2021/AllInOneMask", "sj-all-in-one-mask", "new AppleEverestSJMaskEntity(\"all-in-one\", data, offset)"),
                new("entity", "SJ2021/BloomMask", "sj-bloom-mask", "new AppleEverestSJMaskEntity(\"bloom\", data, offset)"),
                new("entity", "SJ2021/GlowController", "sj-glow-controller", "new AppleEverestSJGlowController(data, offset)"),
                new("entity", "SJ2021/StrawberryJamJar", "sj-jam-jar", "new AppleEverestSJJamJar(data, offset)"),
                new("entity", "SJ2021/StylegroundMask", "sj-styleground-mask", "new AppleEverestSJMaskEntity(\"styleground\", data, offset)"),
                new("entity", "appleEverest/stage25keRootState", "root-state-canary", "new AppleEverestStage25KERootCanary(data, offset)"),
                new("entity", "appleEverest/stage25kfAudio", "multi-bank-audio-canary", "new AppleEverestStage25KFAudioCanary(data, offset)")
            ],
            [new("CollabUtils2", "1.10.11")],
            ["Dialog/English.txt", "Graphics/Atlases/Gui/SJ2021/1-Beginner/beginnermap.png", "Graphics/Atlases/Gui/maps/SJ2021/",
                "Graphics/Atlases/Gameplay/decals/SJ2021/BeginnerLobby/wooden_bench.png",
                "Graphics/StrawberryJam2021/CustomEntitySprites.xml", "Graphics/Atlases/Gameplay/objects/StrawberryJam2021/jamJar/beginner/",
                "Graphics/SJ2021xmls/BeginnerLobby/Sprites.xml", "Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml",
                "Graphics/SJ2021xmls/BeginnerLobby/AnimatedTiles.xml", "Graphics/ColorGrading/SJ2021/Hanky/ForestNight.png",
                "Graphics/Atlases/Gameplay/decals/SJ2021/BeginnerLobby/objects/itemCrystal/",
                "Graphics/Atlases/Gameplay/decals/SJ2021/BeginnerLobby/cave/crabPedestal.png",
                "Graphics/Atlases/Gameplay/decals/SJ2021/BeginnerLobby/chimes_a",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/BeginnerLobby/deckdock.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/BeginnerLobby/lobbyCliffS.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/BeginnerLobby/lobbyReflection.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/BeginnerLobby/lobbyCliff.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/BeginnerLobby/avGrass.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/BeginnerLobby/invisible.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/awheyaway/Blanker.png",
                "Graphics/Atlases/Gameplay/debris/SJ2021/awheyaway/debrisTexture.png",
                "Graphics/Atlases/Gameplay/tilesets/SJ2021/snas/girdernosnow.png",
                "Graphics/Atlases/Gameplay/animatedTiles/SJ2021/BeginnerLobby/grass/top_a"],
            StrawberryJamModule(),
            ["AppleEverestStrawberryJamState.cs", "AppleEverestStrawberryJamEntities.cs",
                "AppleEverestStrawberryJamRendering.cs", "AppleEverestRootStateCanary.cs", "AppleEverestSelectedCanaryAssets.cs"])
    ];

    private static StaticSemanticModulePlan CollabModule() => new(
        new AppleStaticDeclaration
        {
            SchemaVersion = 1,
            ModuleType = "Celeste.Mod.AppleEverestCollabModule",
            SettingsType = "Celeste.Mod.AppleEverestCollabSettings",
            SaveDataType = "Celeste.Mod.AppleEverestCollabSaveData",
            SessionType = "Celeste.Mod.AppleEverestCollabSession",
            ButtonBindingProperties = ["DisplayLobbyMap", "HoldToPan", "PanLobbyMapUp", "PanLobbyMapDown", "PanLobbyMapLeft", "PanLobbyMapRight"],
            Durability = new AppleModuleDurabilityCompatibility
            {
                SaveDataClass = "TYPED_YAML", SessionClass = "TYPED_YAML", AsyncClass = "DEFAULT_ASYNC"
            }
        }, "collabutils2-selected-save-session-v2:original-named-collections-flags-and-return-route",
        "global::Celeste.Mod.AppleEverestCollabDurability.Adapter");

    private static StaticSemanticModulePlan VariantModule() => new(
        new AppleStaticDeclaration
        {
            SchemaVersion = 1,
            ModuleType = "Celeste.Mod.AppleEverestVariantModule",
            SessionType = "Celeste.Mod.AppleEverestVariantSession",
            Durability = new AppleModuleDurabilityCompatibility
            {
                SaveDataClass = "NONE", SessionClass = "TYPED_YAML", AsyncClass = "DEFAULT_ASYNC"
            }
        },
        "evm-beginner-state-v1:session=BackgroundBrightness:Single=1",
        "global::Celeste.Mod.AppleEverestVariantDurability.Adapter");

    private static StaticSemanticModulePlan StrawberryJamModule() => new(
        new AppleStaticDeclaration
        {
            SchemaVersion = 1,
            ModuleType = "Celeste.Mod.AppleEverestStrawberryJamModule",
            SettingsType = "Celeste.Mod.AppleEverestStrawberryJamSettings",
            SaveDataType = "Celeste.Mod.AppleEverestStrawberryJamSaveData",
            SessionType = "Celeste.Mod.AppleEverestStrawberryJamSession",
            ButtonBindingProperties = ["TogglePlaybacks"],
            SettingsProperties =
            [
                new AppleSettingProperty
                {
                    Name = "DisplayDashSequence", Label = "Display Dash Sequence",
                    Kind = "bool", Type = "System.Boolean"
                }
            ],
            Durability = new AppleModuleDurabilityCompatibility
            {
                SaveDataClass = "TYPED_YAML",
                SessionClass = "TYPED_YAML",
                AsyncClass = "DEFAULT_ASYNC"
            }
        },
        "sj2021-root-state-v1:save=ModifiedThemeMaps,FilledJamJarSIDs;session=MusicWonkyBeatIndex,CassetteWonkyBeatIndex,MusicBeatTimer,CassetteBeatTimer,CassetteBlocksDisabled,CassetteBlocksLastParameter,OshiroBSideMode,SkateboardEnabled,ZeroG,ExpiringDashRemainingTime,ExpiringDashFlashThreshold,RainDensity",
        "global::Celeste.Mod.AppleEverestStrawberryJamModuleDurability.Adapter");

    internal static StaticSemanticLoweringPlan? Resolve(ModInput input, EverestYamlEntry metadata)
    {
        Registered? registered = Registry.SingleOrDefault(item =>
            item.Owner == metadata.Name && item.Version == metadata.Version &&
            item.SourceSha256 == input.SourceSha256);
        if (registered == null)
        {
            Registered[] sameOwner = Registry.Where(item => item.Owner == metadata.Name).ToArray();
            if (sameOwner.Length != 0)
                throw new InvalidDataException($"unregistered semantic helper identity for {metadata.Name}: version={metadata.Version}; source={input.SourceSha256}");
            return null;
        }
        if (!string.Equals(metadata.DLL?.Replace('\\', '/'), registered.DllPath, StringComparison.Ordinal))
            throw new InvalidDataException($"semantic lowering DLL path mismatch for {metadata.Name}");
        string full = Path.Combine(input.StagingRoot, registered.DllPath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(full) || Hashing.FileSha256(full) != registered.DllSha256)
            throw new InvalidDataException($"semantic lowering DLL identity mismatch for {metadata.Name}");
        return new(registered.Id, registered.Owner, registered.Version, registered.SourceSha256,
            registered.DllPath, registered.DllSha256, registered.Factories,
            registered.EffectiveDependencies, registered.ContentPrefixes, registered.Module, registered.RuntimeFiles, registered.Tracking, registered.LinkRequirements, registered.PooledEntityTypes);
    }

    internal static bool IncludeContent(string path) => IncludeOrdinaryContent(path);

    internal static string StageContent(StaticSemanticLoweringPlan? plan, string source, string relative,
        string stagedPath, string content)
    {
        string[]? names = (plan?.Owner, relative) switch
        {
            ("CherryHelper", "Graphics/Sprites.xml") => ["itemCrystal", "itemCrystalPedestal"],
            ("XaphanHelper", "Graphics/Sprites.xml") => ["XaphanHelper_Extend_player", "XaphanHelper_Extend_player_no_backpack"],
            ("StrawberryJam2021", "Graphics/StrawberryJam2021/CustomEntitySprites.xml") => ["jamJar_beginner"],
            ("StrawberryJam2021", "Graphics/SJ2021xmls/BeginnerLobby/ForegroundTiles.xml") => ["9", "z", "7", "-", "`", "~", "%", "E", "t", "/"],
            ("StrawberryJam2021", "Graphics/SJ2021xmls/BeginnerLobby/AnimatedTiles.xml") => ["SJ2021_BeginnerLobby_grass_top_a"],
            _ => null
        };
        if (names == null)
            return ContentCompiler.Stage(source, stagedPath, content);
        // Project exact original definitions; retain their document ordering,
        // attributes and children, including tile copy and ignore semantics.
        System.Xml.Linq.XDocument original = System.Xml.Linq.XDocument.Load(source);
        System.Xml.Linq.XElement[] selected = original.Root!.Elements().Where(element =>
            names.Contains((string?)element.Attribute("id") ?? (string?)element.Attribute("name") ?? element.Name.LocalName,
                StringComparer.Ordinal)).ToArray();
        if (selected.Length != names.Length) throw new InvalidDataException("selected content projection changed: " + relative);
        System.Xml.Linq.XDocument projected = new(new System.Xml.Linq.XElement(original.Root.Name,
            selected.Select(element => new System.Xml.Linq.XElement(element))));
        if (plan?.Owner == "XaphanHelper" && relative == "Graphics/Sprites.xml")
        {
            // Retain the selected animation and its exact frame metadata. Its
            // source sprite bank also starts unrelated turn-around animations;
            // use the selected loop as the projected bank's construction start.
            foreach (var sprite in projected.Root!.Elements())
            {
                foreach (var child in sprite.Elements().ToArray())
                    if (child.Name.LocalName == "Metadata")
                    {
                        foreach (var frames in child.Elements().ToArray())
                            if (!((string?)frames.Attribute("path"))!.EndsWith("/slopeSlide", StringComparison.Ordinal)) frames.Remove();
                    }
                    else if ((string?)child.Attribute("id") != "XaphanHelper_slopeSlide") child.Remove();
                if (sprite.Elements("Loop").Count() != 1 || sprite.Element("Metadata")?.Elements("Frames").Count() != 1)
                    throw new InvalidDataException("selected Xaphan slope animation projection changed");
                sprite.SetAttributeValue("start", "XaphanHelper_slopeSlide");
            }
        }
        string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-selected-asset-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            projected.Save(temporary);
            return ContentCompiler.Stage(temporary, stagedPath, content);
        }
        finally { File.Delete(temporary); }
    }

    internal static bool IncludeContent(StaticSemanticLoweringPlan plan, string path) =>
        IncludeOrdinaryContent(path) &&
        ((plan.Id is "junglehelper-1.4.10-sj-beginner-v1" or "strawberryjam2021-1.0.12-beginner-root-v1" &&
            SelectedDecalContent.Includes(plan.Owner, path)) || plan.ContentPrefixes == null || plan.ContentPrefixes.Any(prefix =>
            path.StartsWith(prefix, StringComparison.Ordinal)));

    private static bool IncludeOrdinaryContent(string path) =>
        !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".bank", StringComparison.OrdinalIgnoreCase) &&
        !path.EndsWith(".guids.txt", StringComparison.OrdinalIgnoreCase) &&
        !path.Equals("everest.yaml", StringComparison.OrdinalIgnoreCase) &&
        !path.Equals("everest.yml", StringComparison.OrdinalIgnoreCase);

    // Progression metadata must describe the statically lowered runtime entity,
    // not merely the custom ID stored in the source map. Keep this classification
    // semantic so future exact/hash-approved strawberry lowerings participate
    // without adding map- or helper-specific progression exceptions.
    internal static bool CountsAsStrawberry(StaticSemanticFactory factory) =>
        factory.Kind == "entity" && factory.RuntimeFactory is "strawberry" or "strawberry-with-return";
}
