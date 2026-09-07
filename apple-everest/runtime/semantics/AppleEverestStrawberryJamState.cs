#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod;

internal sealed class AppleEverestStrawberryJamSettings : EverestModuleSettings
{
    public bool DisplayDashSequence { get; set; }
    public ButtonBinding TogglePlaybacks { get; set; } = new(Buttons.Back, Keys.Tab);
}

internal sealed class AppleEverestStrawberryJamSaveData : EverestModuleSaveData
{
    public HashSet<string> ModifiedThemeMaps = new(StringComparer.Ordinal);
    public HashSet<string> FilledJamJarSIDs { get; set; } = new(StringComparer.Ordinal);
}

internal sealed class AppleEverestStrawberryJamRainDensity
{
    public float Density { get; set; } = 1f;
    public float StartDensity { get; set; } = 1f;
    public float EndDensity { get; set; } = 1f;
    public float Duration { get; set; }
}

internal sealed class AppleEverestStrawberryJamSession : EverestModuleSession
{
    public int MusicWonkyBeatIndex;
    public int CassetteWonkyBeatIndex;
    public float MusicBeatTimer;
    public float CassetteBeatTimer;
    public bool CassetteBlocksDisabled = true;
    public string CassetteBlocksLastParameter = "";
    public bool OshiroBSideMode;
    public bool SkateboardEnabled;
    public bool ZeroG;
    public double ExpiringDashRemainingTime;
    public float ExpiringDashFlashThreshold;
    public AppleEverestStrawberryJamRainDensity RainDensityData = new();
}

internal sealed class AppleEverestStrawberryJamModule : EverestModule
{
    internal static AppleEverestStrawberryJamModule Instance { get; private set; }
    internal int LifecycleState { get; private set; }
    internal SpriteBank SpriteBank { get; private set; }

    public override Type SettingsType => typeof(AppleEverestStrawberryJamSettings);
    public override Type SaveDataType => typeof(AppleEverestStrawberryJamSaveData);
    public override Type SessionType => typeof(AppleEverestStrawberryJamSession);

    internal AppleEverestStrawberryJamSettings Settings => (AppleEverestStrawberryJamSettings)_Settings;
    internal AppleEverestStrawberryJamSaveData Save => (AppleEverestStrawberryJamSaveData)_SaveData;
    internal AppleEverestStrawberryJamSession Session => (AppleEverestStrawberryJamSession)_Session;

    public AppleEverestStrawberryJamModule() => Instance = this;
    internal static bool IsSliceMap(Level level)
    {
        string sid = AppleEverestMapBinding.ForSession(level?.Session)?.Sid;
        return sid is "StrawberryJam2021/0-Lobbies/1-Beginner" or "StrawberryJam2021/1-Beginner/Bing_Over_Google";
    }
    public override void Load()
    {
        AppleEverestStrawberryJamLobbyLoading.Load();
        LifecycleState = Math.Max(LifecycleState, 1);
    }
    public override void Initialize() => LifecycleState = Math.Max(LifecycleState, 2);
    public override void LoadContent(bool firstLoad)
    {
        SpriteBank = new SpriteBank(GFX.Game,
            "AppleEverest/Mods/StrawberryJam2021/Graphics/StrawberryJam2021/CustomEntitySprites.xml");
        LifecycleState = Math.Max(LifecycleState, 3);
    }
    public override void Unload()
    {
        AppleEverestStrawberryJamLobbyLoading.Unload();
        LifecycleState = 0;
    }
}

internal static class AppleEverestStrawberryJamModuleDurability
{
    private const string Schema = "58f135298191f68e6cf6ec27204d82b50f93e0642b5305a3415fe48b898d6d70";
    internal static readonly AppleEverestModuleDurabilityAdapter Adapter = new(
        Schema, SerializeSave, DeserializeSave, SerializeSession, DeserializeSession);

    private static byte[] SerializeSave(EverestModuleSaveData value)
    {
        AppleEverestStrawberryJamSaveData save = (AppleEverestStrawberryJamSaveData)value;
        return AppleEverestModuleYaml.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            WriteStrings(writer, "ModifiedThemeMaps", save.ModifiedThemeMaps);
            WriteStrings(writer, "FilledJamJarSIDs", save.FilledJamJarSIDs);
            writer.WriteEndObject();
        });
    }

    private static EverestModuleSaveData DeserializeSave(byte[] payload, int slot)
    {
        using JsonDocument document = AppleEverestModuleYaml.Parse(payload);
        JsonElement root = document.RootElement;
        AppleEverestModuleYaml.RequireObject(root);
        RequireVersion(root);
        AppleEverestStrawberryJamSaveData result = new() { Index = slot };
        if (AppleEverestModuleYaml.TryProperty(root, "ModifiedThemeMaps", out JsonElement themes))
            result.ModifiedThemeMaps = ReadStrings(themes);
        if (AppleEverestModuleYaml.TryProperty(root, "FilledJamJarSIDs", out JsonElement jars))
            result.FilledJamJarSIDs = ReadStrings(jars);
        return result;
    }

    private static byte[] SerializeSession(EverestModuleSession value)
    {
        AppleEverestStrawberryJamSession session = (AppleEverestStrawberryJamSession)value;
        AppleEverestStrawberryJamRainDensity rain = session.RainDensityData ?? new();
        return AppleEverestModuleYaml.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", 1);
            writer.WriteNumber("MusicWonkyBeatIndex", session.MusicWonkyBeatIndex);
            writer.WriteNumber("CassetteWonkyBeatIndex", session.CassetteWonkyBeatIndex);
            writer.WriteNumber("MusicBeatTimer", session.MusicBeatTimer);
            writer.WriteNumber("CassetteBeatTimer", session.CassetteBeatTimer);
            writer.WriteBoolean("CassetteBlocksDisabled", session.CassetteBlocksDisabled);
            writer.WriteString("CassetteBlocksLastParameter", session.CassetteBlocksLastParameter ?? "");
            writer.WriteBoolean("OshiroBSideMode", session.OshiroBSideMode);
            writer.WriteBoolean("SkateboardEnabled", session.SkateboardEnabled);
            writer.WriteBoolean("ZeroG", session.ZeroG);
            writer.WriteNumber("ExpiringDashRemainingTime", session.ExpiringDashRemainingTime);
            writer.WriteNumber("ExpiringDashFlashThreshold", session.ExpiringDashFlashThreshold);
            writer.WritePropertyName("RainDensityData");
            writer.WriteStartObject();
            writer.WriteNumber("Density", rain.Density);
            writer.WriteNumber("StartDensity", rain.StartDensity);
            writer.WriteNumber("EndDensity", rain.EndDensity);
            writer.WriteNumber("Duration", rain.Duration);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
    }

    private static EverestModuleSession DeserializeSession(byte[] payload, int slot)
    {
        using JsonDocument document = AppleEverestModuleYaml.Parse(payload);
        JsonElement root = document.RootElement;
        AppleEverestModuleYaml.RequireObject(root);
        RequireVersion(root);
        AppleEverestStrawberryJamSession result = new() { Index = slot };
        result.MusicWonkyBeatIndex = Int(root, "MusicWonkyBeatIndex", 0);
        result.CassetteWonkyBeatIndex = Int(root, "CassetteWonkyBeatIndex", 0);
        result.MusicBeatTimer = Single(root, "MusicBeatTimer", 0f);
        result.CassetteBeatTimer = Single(root, "CassetteBeatTimer", 0f);
        result.CassetteBlocksDisabled = Boolean(root, "CassetteBlocksDisabled", true);
        result.CassetteBlocksLastParameter = String(root, "CassetteBlocksLastParameter", "");
        result.OshiroBSideMode = Boolean(root, "OshiroBSideMode", false);
        result.SkateboardEnabled = Boolean(root, "SkateboardEnabled", false);
        result.ZeroG = Boolean(root, "ZeroG", false);
        result.ExpiringDashRemainingTime = Double(root, "ExpiringDashRemainingTime", 0d);
        result.ExpiringDashFlashThreshold = Single(root, "ExpiringDashFlashThreshold", 0f);
        if (AppleEverestModuleYaml.TryProperty(root, "RainDensityData", out JsonElement rain))
        {
            AppleEverestModuleYaml.RequireObject(rain);
            result.RainDensityData = new AppleEverestStrawberryJamRainDensity
            {
                Density = Single(rain, "Density", 1f),
                StartDensity = Single(rain, "StartDensity", 1f),
                EndDensity = Single(rain, "EndDensity", 1f),
                Duration = Single(rain, "Duration", 0f)
            };
        }
        return result;
    }

    private static void RequireVersion(JsonElement root)
    {
        if (AppleEverestModuleYaml.TryProperty(root, "schemaVersion", out JsonElement version) &&
            AppleEverestModuleYaml.Int32(version) != 1)
            throw new InvalidOperationException("unsupported Strawberry Jam root state schema");
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WritePropertyName(name);
        writer.WriteStartArray();
        foreach (string item in (values ?? Array.Empty<string>()).Where(item => item != null)
                     .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
            writer.WriteStringValue(item);
        writer.WriteEndArray();
    }

    private static HashSet<string> ReadStrings(JsonElement value)
    {
        AppleEverestModuleYaml.RequireArray(value);
        HashSet<string> result = new(StringComparer.Ordinal);
        foreach (JsonElement item in value.EnumerateArray())
        {
            if (result.Count >= 4096) throw new InvalidOperationException("Strawberry Jam state set exceeds bound");
            string text = AppleEverestModuleYaml.String(item) ?? "";
            if (text.Length > 512) throw new InvalidOperationException("Strawberry Jam state key exceeds bound");
            result.Add(text);
        }
        return result;
    }

    private static int Int(JsonElement root, string name, int fallback) =>
        AppleEverestModuleYaml.TryProperty(root, name, out JsonElement value) ? AppleEverestModuleYaml.Int32(value) : fallback;
    private static float Single(JsonElement root, string name, float fallback) =>
        AppleEverestModuleYaml.TryProperty(root, name, out JsonElement value) ? AppleEverestModuleYaml.Single(value) : fallback;
    private static double Double(JsonElement root, string name, double fallback) =>
        AppleEverestModuleYaml.TryProperty(root, name, out JsonElement value) ? AppleEverestModuleYaml.Double(value) : fallback;
    private static bool Boolean(JsonElement root, string name, bool fallback) =>
        AppleEverestModuleYaml.TryProperty(root, name, out JsonElement value) ? AppleEverestModuleYaml.Boolean(value) : fallback;
    private static string String(JsonElement root, string name, string fallback) =>
        AppleEverestModuleYaml.TryProperty(root, name, out JsonElement value) ? AppleEverestModuleYaml.String(value) ?? fallback : fallback;
}
