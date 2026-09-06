#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;
namespace Celeste.Mod;

internal sealed class AppleEverestCollabSettings : EverestModuleSettings
{
    public ButtonBinding DisplayLobbyMap { get; set; } = new(Buttons.RightStick, Keys.Tab);
    public ButtonBinding HoldToPan { get; set; } = new(0, Keys.LeftShift);
    public ButtonBinding PanLobbyMapUp { get; set; } = new(Buttons.RightThumbstickUp);
    public ButtonBinding PanLobbyMapDown { get; set; } = new(Buttons.RightThumbstickDown);
    public ButtonBinding PanLobbyMapLeft { get; set; } = new(Buttons.RightThumbstickLeft);
    public ButtonBinding PanLobbyMapRight { get; set; } = new(Buttons.RightThumbstickRight);
}

internal sealed class AppleEverestCollabSaveData : EverestModuleSaveData
{
    public Dictionary<string, string> SessionsPerLevel = new();
    public Dictionary<string, Dictionary<string, string>> ModSessionsPerLevel = new();
    public Dictionary<string, Dictionary<string, string>> ModSessionsPerLevelBinary = new();
    public Dictionary<string, string> VisitedLobbyPositions = new();
    public Dictionary<string, HashSet<string>> LearnedTech = new();
    public HashSet<string> OpenedMiniHeartDoors { get; set; } = new();
    public HashSet<string> CombinedRainbowBerries { get; set; } = new();
    public Dictionary<string, long> SpeedBerryPBs { get; set; } = new();
    public bool SpeedberryOptionMessageShown { get; set; }
    public HashSet<string> CompletedWarpPedestalSIDs { get; set; } = new();
    public bool RevealMap { get; set; }
    public bool PauseVisitingPoints { get; set; }
    public bool ShowVisitedPoints { get; set; }
}

internal sealed class AppleEverestCollabModule : EverestModule
{
    internal static AppleEverestCollabModule Instance { get; private set; }
    public override Type SettingsType => typeof(AppleEverestCollabSettings);
    public override Type SaveDataType => typeof(AppleEverestCollabSaveData);
    public override Type SessionType => typeof(AppleEverestCollabSession);
    internal AppleEverestCollabSettings Settings => (AppleEverestCollabSettings)_Settings;
    internal AppleEverestCollabSaveData SaveData => (AppleEverestCollabSaveData)_SaveData;
    internal AppleEverestCollabSession Session => (AppleEverestCollabSession)_Session;
    public AppleEverestCollabModule() { Instance = this; }
    public override void Load() { }
    public override void Unload() { }
}

// Same paired save-slot module snapshots as other accepted semantic modules.
// The original names, defaults and nested collections survive round trips.
internal static class AppleEverestCollabDurability
{
    private const string Schema = "222c4e13f375b546fe21d6c6295999a77eb39d03466f956069188d94b396b834";
    internal static readonly AppleEverestModuleDurabilityAdapter Adapter = new(Schema, Serialize, Deserialize, SerializeSession, DeserializeSession);
    private static byte[] SerializeSession(EverestModuleSession value)
    {
        var session = (AppleEverestCollabSession)value;
        return AppleEverestModuleYaml.Write(w =>
        {
            w.WriteStartObject();
            w.WriteString("LobbySID", session.LobbySID);
            w.WriteString("LobbyRoom", session.LobbyRoom);
            w.WriteNumber("LobbySpawnPointX", session.LobbySpawnPointX);
            w.WriteNumber("LobbySpawnPointY", session.LobbySpawnPointY);
            w.WriteString("GymExitMapSID", session.GymExitMapSID);
            w.WriteBoolean("GymExitSaveAllowed", session.GymExitSaveAllowed);
            w.WriteBoolean("SaveAndReturnToLobbyAllowed", session.SaveAndReturnToLobbyAllowed);
            w.WriteEndObject();
        });
    }
    private static EverestModuleSession DeserializeSession(byte[] bytes, int slot)
    {
        using var document = AppleEverestModuleYaml.Parse(bytes);
        var root = document.RootElement;
        AppleEverestModuleYaml.RequireObject(root);
        var session = new AppleEverestCollabSession();
        if (root.TryGetProperty("LobbySID", out var sid)) session.LobbySID = ReadString(sid);
        if (root.TryGetProperty("LobbyRoom", out var room)) session.LobbyRoom = ReadString(room);
        if (root.TryGetProperty("LobbySpawnPointX", out var x)) session.LobbySpawnPointX = x.GetSingle();
        if (root.TryGetProperty("LobbySpawnPointY", out var y)) session.LobbySpawnPointY = y.GetSingle();
        if (root.TryGetProperty("GymExitMapSID", out var gym)) session.GymExitMapSID = ReadString(gym);
        if (root.TryGetProperty("GymExitSaveAllowed", out var gymSave)) session.GymExitSaveAllowed = gymSave.GetBoolean();
        if (root.TryGetProperty("SaveAndReturnToLobbyAllowed", out var save)) session.SaveAndReturnToLobbyAllowed = save.GetBoolean();
        return session;
    }
    private static byte[] Serialize(EverestModuleSaveData value)
    {
        var save = (AppleEverestCollabSaveData)value;
        return AppleEverestModuleYaml.Write(w =>
        {
            w.WriteStartObject();
            w.WritePropertyName("SessionsPerLevel"); Map(w, save.SessionsPerLevel, String);
            w.WritePropertyName("ModSessionsPerLevel"); Map(w, save.ModSessionsPerLevel, (j, v) => Map(j, v, String));
            w.WritePropertyName("ModSessionsPerLevelBinary"); Map(w, save.ModSessionsPerLevelBinary, (j, v) => Map(j, v, String));
            w.WritePropertyName("VisitedLobbyPositions"); Map(w, save.VisitedLobbyPositions, String);
            w.WritePropertyName("LearnedTech"); Map(w, save.LearnedTech, Strings);
            w.WritePropertyName("OpenedMiniHeartDoors"); Strings(w, save.OpenedMiniHeartDoors);
            w.WritePropertyName("CombinedRainbowBerries"); Strings(w, save.CombinedRainbowBerries);
            w.WritePropertyName("SpeedBerryPBs"); Map(w, save.SpeedBerryPBs, (j, v) => j.WriteNumberValue(v));
            w.WriteBoolean("SpeedberryOptionMessageShown", save.SpeedberryOptionMessageShown);
            w.WritePropertyName("CompletedWarpPedestalSIDs"); Strings(w, save.CompletedWarpPedestalSIDs);
            w.WriteBoolean("RevealMap", save.RevealMap);
            w.WriteBoolean("PauseVisitingPoints", save.PauseVisitingPoints);
            w.WriteBoolean("ShowVisitedPoints", save.ShowVisitedPoints);
            w.WriteEndObject();
        });
    }
    private static EverestModuleSaveData Deserialize(byte[] bytes, int slot)
    {
        using var document = AppleEverestModuleYaml.Parse(bytes);
        var root = document.RootElement;
        AppleEverestModuleYaml.RequireObject(root);
        var save = new AppleEverestCollabSaveData { Index = slot };
        if (root.TryGetProperty("SessionsPerLevel", out var sessions)) save.SessionsPerLevel = ReadMap(sessions, ReadString);
        if (root.TryGetProperty("ModSessionsPerLevel", out var mods)) save.ModSessionsPerLevel = ReadMap(mods, v => ReadMap(v, ReadString));
        if (root.TryGetProperty("ModSessionsPerLevelBinary", out var binaries)) save.ModSessionsPerLevelBinary = ReadMap(binaries, v => ReadMap(v, ReadString));
        if (root.TryGetProperty("VisitedLobbyPositions", out var visits)) save.VisitedLobbyPositions = ReadMap(visits, ReadString);
        if (root.TryGetProperty("LearnedTech", out var tech)) save.LearnedTech = ReadMap(tech, ReadStrings);
        if (root.TryGetProperty("OpenedMiniHeartDoors", out var doors)) save.OpenedMiniHeartDoors = ReadStrings(doors);
        if (root.TryGetProperty("CombinedRainbowBerries", out var berries)) save.CombinedRainbowBerries = ReadStrings(berries);
        if (root.TryGetProperty("SpeedBerryPBs", out var pbs)) save.SpeedBerryPBs = ReadMap(pbs, v => v.GetInt64());
        if (root.TryGetProperty("SpeedberryOptionMessageShown", out var shown)) save.SpeedberryOptionMessageShown = shown.GetBoolean();
        if (root.TryGetProperty("CompletedWarpPedestalSIDs", out var pedestals)) save.CompletedWarpPedestalSIDs = ReadStrings(pedestals);
        if (root.TryGetProperty("RevealMap", out var reveal)) save.RevealMap = reveal.GetBoolean();
        if (root.TryGetProperty("PauseVisitingPoints", out var pause)) save.PauseVisitingPoints = pause.GetBoolean();
        if (root.TryGetProperty("ShowVisitedPoints", out var show)) save.ShowVisitedPoints = show.GetBoolean();
        return save;
    }
    private static void String(Utf8JsonWriter writer, string value) => writer.WriteStringValue(value);
    private static string ReadString(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : AppleEverestModuleYaml.String(value);
    private static void Map<T>(Utf8JsonWriter writer, Dictionary<string, T> values, Action<Utf8JsonWriter, T> write)
    {
        if (values == null) { writer.WriteNullValue(); return; }
        writer.WriteStartObject();
        foreach (var pair in values.OrderBy(v => v.Key, StringComparer.Ordinal))
        { writer.WritePropertyName(pair.Key); write(writer, pair.Value); }
        writer.WriteEndObject();
    }
    private static Dictionary<string, T> ReadMap<T>(JsonElement element, Func<JsonElement, T> read)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;
        AppleEverestModuleYaml.RequireObject(element);
        var result = new Dictionary<string, T>();
        foreach (var entry in element.EnumerateObject())
            if (!result.TryAdd(entry.Name, read(entry.Value))) throw new InvalidOperationException("duplicate Collab save dictionary key");
        return result;
    }
    private static void Strings(Utf8JsonWriter writer, HashSet<string> values)
    {
        if (values == null) { writer.WriteNullValue(); return; }
        writer.WriteStartArray();
        foreach (string value in values.OrderBy(v => v, StringComparer.Ordinal)) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }
    private static HashSet<string> ReadStrings(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null) return null;
        AppleEverestModuleYaml.RequireArray(element);
        return element.EnumerateArray().Select(ReadString).ToHashSet();
    }
}
