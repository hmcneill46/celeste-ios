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
        string[]? RuntimeFiles = null);

    private static readonly Registered[] Registry =
    [
        new("collabutils2-1.13.4-henny-v1", "CollabUtils2", "1.13.4",
            "b987d25608874623453e2c75c441661b906d6fd907948699983c2f12ba14c88e",
            "bin/CollabUtils2.dll", "ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60",
            [
                new("trigger", "CollabUtils2/ChapterPanelTrigger", "chapter-panel"),
                new("trigger", "CollabUtils2/JournalTrigger", "journal"),
                new("entity", "CollabUtils2/MiniHeart", "mini-heart"),
                new("entity", "CollabUtils2/GoldenBerryPlayerRespawnPoint", "golden-berry-respawn-point"),
                new("entity", "CollabUtils2/MiniHeartDoor", "mini-heart-door"),
                new("entity", "CollabUtils2/RainbowBerry", "rainbow-berry"),
                new("entity", "CollabUtils2/SilverBerry", "silver-berry"),
                new("entity", "CollabUtils2/SpeedBerry", "speed-berry"),
                new("trigger", "CollabUtils2/MiniHeartDoorUnlockCutsceneTrigger", "mini-heart-door-unlock-trigger"),
                new("trigger", "CollabUtils2/RainbowBerryUnlockCutsceneTrigger", "rainbow-berry-unlock-trigger"),
                new("trigger", "CollabUtils2/SpeedBerryCollectTrigger", "speed-berry-collect-trigger")
            ]),
        new("eeveehelper-1.12.5-kayonara-v1", "EeveeHelper", "1.12.5",
            "355db1937a979a577b96c93cf639e812a20551e5b3f531a9e1565fda31dcaa86",
            "Code/bin/EeveeHelper.dll", "1613b4f89a8dd119537052e6c85031e543ec65179e5c7ac7a431b283be9e8ad8",
            [new("entity", "EeveeHelper/FlagToggleModifier", "flag-toggle-modifier")]),
        new("xaphanhelper-1.0.79-kayonara-v1", "XaphanHelper", "1.0.79",
            "530ea4bbad7ac5cd43be692297d6d743e0a81e789d3408f121448759df7643be",
            "Code/bin/XaphanHelper.dll", "e408dc081cb79d89b3436724b74413a603189cb53e84af5844918df08717273c",
            []),
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
                new("trigger", "MaxHelpingHand/CameraCatchupSpeedTrigger", "camera-catchup-speed")
            ]),
        new("lunatichelper-1.1.1-henny-v1", "LunaticHelper", "1.1.1",
            "e7cef501937fc1bc07d1ff13e753fe920b4ccbbd4e4db4c0b2c4312de89fdd78",
            "LunaticHelper.dll", "fc08f00296551a6025c5e31422c6edd5e8136e6861459f44ffea909129c6a925",
            [
                new("entity", "LunaticHelper/StrawberryWithReturn", "strawberry-with-return", "new AppleEverestBubbleReturnBerry(data, offset, entityId)"),
                new("entity", "LunaticHelper/StrawberryGate", "strawberry-gate")
            ], RuntimeFiles: ["AppleEverestBubbleReturnBerry.cs"]),
        new("shroomhelper-1.2.10-henny-v1", "ShroomHelper", "1.2.10",
            "76fa23d9dfabb8203dc2eee8ca406bcb8407766f6385ef7c28dc439be0e40034",
            "Code/bin/ShroomHelper.dll", "2428be4659522324b4b426452b17a095a78b857048d6340a0c4720151c08fa1d",
            [
                new("entity", "ShroomHelper/AttachedIceWall", "attached-ice-wall"),
                new("entity", "ShroomHelper/CrumbleBlockOnTouch", "crumble-block-on-touch")
            ]),
        new("frosthelper-1.79.1-henny-v1", "FrostHelper", "1.79.1",
            "72d289424dc289343b606f460f62b68c63dcab4f62488363be506bce609f1790",
            "Code/FrostHelper/bin/FrostTempleHelper.dll", "e1314eca75af6670569a58689c267746151d1ba428e8808607dde23499654971",
            [new("entity", "FrostHelper/NoDashArea", "no-dash-area")]),
        new("fancytileentities-1.6.2-henny-v1", "FancyTileEntities", "1.6.2",
            "e62e9cf62cdbe9f4fdeb27f1174aa6fed55ffc40f0485cd870c3f433ced79a29",
            "FancyTileEntities/bin/Debug/net452/FancyTileEntities.dll", "48c4ba952c602c8f40325392edf9975c86ea7a90cd25a3954b0efe7086b6f0a1",
            [new("entity", "FancyTileEntities/FancyFakeWall", "fancy-fake-wall")]),
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
            ], [], [], RuntimeFiles: ["AppleEverestCrystallineSemantics.cs"]),
        new("vortexhelper-1.2.19-sj-beginner-v1", "VortexHelper", "1.2.19",
            "c071d33bb1cc4f0387ea204834e212bd020f3143aea68c9f4e56b9bfa35def1b",
            "Code/bin/VortexHelper.dll", "f5a32f02c2699dcdf41bb1af808ead631491571c3d259429761953693bd37f73",
            [new("entity", "VortexHelper/AttachedJumpThru", "attached-jump-thru",
                "new AppleEverestAttachedJumpThru(data, offset)")], [], [],
            RuntimeFiles: ["AppleEverestVortexSemantics.cs"]),
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
            ["Graphics/StrawberryJam2021/CustomEntitySprites.xml", "Graphics/Atlases/Gameplay/objects/StrawberryJam2021/jamJar/beginner/"],
            StrawberryJamModule(),
            ["AppleEverestStrawberryJamState.cs", "AppleEverestStrawberryJamEntities.cs",
                "AppleEverestStrawberryJamRendering.cs", "AppleEverestRootStateCanary.cs"])
    ];

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
            registered.EffectiveDependencies, registered.ContentPrefixes, registered.Module, registered.RuntimeFiles);
    }

    internal static bool IncludeContent(string path) => IncludeOrdinaryContent(path);

    internal static string StageContent(StaticSemanticLoweringPlan? plan, string source, string relative,
        string stagedPath, string content)
    {
        if (plan?.Id != "strawberryjam2021-1.0.12-beginner-root-v1" ||
            relative != "Graphics/StrawberryJam2021/CustomEntitySprites.xml")
            return ContentCompiler.Stage(source, stagedPath, content);
        // Project the one audited SpriteBank definition from the original
        // distributed asset. Other entity textures are not in this product.
        System.Xml.Linq.XDocument original = System.Xml.Linq.XDocument.Load(source);
        System.Xml.Linq.XElement jar = original.Root!.Elements("jamJar_beginner").Single();
        System.Xml.Linq.XDocument projected = new(new System.Xml.Linq.XElement(original.Root.Name,
            new System.Xml.Linq.XElement(jar)));
        string temporary = Path.Combine(Path.GetTempPath(), "apple-everest-jar-" + Guid.NewGuid().ToString("N") + ".xml");
        try
        {
            projected.Save(temporary);
            return ContentCompiler.Stage(temporary, stagedPath, content);
        }
        finally { File.Delete(temporary); }
    }

    internal static bool IncludeContent(StaticSemanticLoweringPlan plan, string path) =>
        IncludeOrdinaryContent(path) &&
        (plan.ContentPrefixes == null || plan.ContentPrefixes.Any(prefix =>
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
