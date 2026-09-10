using System.Text;
using System.Text.Json;

namespace AppleEverestBuilder;

// This set decides which factories require strict data guards. Availability
// comes from the generated and compiled selector, never membership in this set.
internal static class SelectedFactoryProfiles
{
    private static readonly HashSet<string> Selected = Load();
    private static HashSet<string> Load()
    {
        using Stream stream = typeof(SelectedFactoryProfiles).Assembly.GetManifestResourceStream("AppleEverest.SelectedFactoryProfiles")
            ?? throw new InvalidDataException("missing reviewed selected factory profiles");
        using JsonDocument document = JsonDocument.Parse(stream);
        HashSet<string> selected = document.RootElement.GetProperty("factories").EnumerateArray()
            .Select(factory => factory.GetProperty("kind").GetString() + ":" + factory.GetProperty("customId").GetString())
            .ToHashSet(StringComparer.Ordinal);
        selected.UnionWith(new[] { "entity:CommunalHelper/PlayerBubbleRegion", "trigger:ContortHelper/RandomSoundTrigger",
            "entity:MaxHelpingHand/FlagTouchSwitch", "entity:MaxHelpingHand/FlagSwitchGate" });
        return selected;
    }
    internal static bool Contains(string kind, string id) => Selected.Contains(kind + ":" + id);
    internal static string EntryMethod(string kind, string id) => "Create_" + kind + "_" +
        Hashing.BytesSha256(Encoding.UTF8.GetBytes(kind + ":" + id))[..16];
    internal static string? ExplicitConstructor(string kind, string id) => (kind, id) switch
    {
        ("entity", "everest/coreMessage") => "new global::Celeste.Mod.Entities.CustomCoreMessage(data, offset)",
        ("trigger", "everest/changeInventoryTrigger") => "new AppleEverestChangeInventoryTrigger(data, offset)",
        ("trigger", "everest/coreModeTrigger") => "new AppleEverestCoreModeTrigger(data, offset)",
        ("trigger", "everest/crystalShatterTrigger") => "new AppleEverestCrystalShatterTrigger(data, offset)",
        ("trigger", "everest/flagTrigger") => "new AppleEverestFlagTrigger(data, offset)",
        ("trigger", "everest/smoothCameraOffsetTrigger") => "new AppleEverestSmoothCameraOffsetTrigger(data, offset)",
        ("entity", "CollabUtils2/SilverBerry") => "new AppleEverestSilverBerry(data, offset, entityId)",
        ("trigger", "CollabUtils2/ChapterPanelTrigger") => "new AppleEverestChapterPanelTrigger(data, offset)",
        ("trigger", "CollabUtils2/JournalTrigger") => "new AppleEverestJournalTrigger(data, offset)",
        ("entity", "MaxHelpingHand/GroupedTriggerSpikesUp") => "new AppleEverestGroupedTriggerSpikesUp(data, offset)",
        ("trigger", "MaxHelpingHand/CameraCatchupSpeedTrigger") => "new AppleEverestCameraCatchupTrigger(data, offset)",
        _ => null
    };
}
