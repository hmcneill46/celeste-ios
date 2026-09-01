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
        string DllPath, string DllSha256, StaticSemanticFactory[] Factories);

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
                new("entity", "LunaticHelper/StrawberryWithReturn", "strawberry-with-return"),
                new("entity", "LunaticHelper/StrawberryGate", "strawberry-gate")
            ]),
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
            [new("entity", "FancyTileEntities/FancyFakeWall", "fancy-fake-wall")])
    ];

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
            registered.DllPath, registered.DllSha256, registered.Factories);
    }

    internal static bool IncludeContent(string path) =>
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
