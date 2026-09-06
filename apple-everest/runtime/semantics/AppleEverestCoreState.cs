#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
namespace Celeste.Mod;

internal sealed class AppleEverestCoreSession : EverestModuleSession
{
    public int WhateverElseCount { get; set; } = 1337;
    public HashSet<string> AttachedDecals { get; set; } = new();
}

// The selected CoreModule session participates in the existing paired module
// snapshot and new-Session reset paths. It has no independent save file.
internal sealed class AppleEverestCoreModule : EverestModule
{
    private static AppleEverestCoreModule instance;
    internal static AppleEverestCoreSession Session => (AppleEverestCoreSession)instance._Session;
    public override Type SessionType => typeof(AppleEverestCoreSession);
    public AppleEverestCoreModule() { instance = this; }
    public override void Load() { }
    public override void Unload() { }
}

internal static class AppleEverestCoreDurability
{
    internal static readonly AppleEverestModuleDurabilityAdapter Adapter = new(
        GeneratedAppleEverestModuleRegistry.CoreSessionSchema, null, null, Serialize, Deserialize);
    private static byte[] Serialize(EverestModuleSession value)
    {
        AppleEverestCoreSession session = (AppleEverestCoreSession)value;
        return AppleEverestModuleYaml.Write(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("WhateverElseCount", session.WhateverElseCount);
            writer.WritePropertyName("AttachedDecals");
            if (session.AttachedDecals == null) writer.WriteNullValue();
            else
            {
                writer.WriteStartArray();
                foreach (string decal in session.AttachedDecals.OrderBy(value => value, StringComparer.Ordinal))
                    writer.WriteStringValue(decal);
                writer.WriteEndArray();
            }
            writer.WriteEndObject();
        });
    }
    private static EverestModuleSession Deserialize(byte[] data, int slot)
    {
        using JsonDocument document = AppleEverestModuleYaml.Parse(data);
        JsonElement root = document.RootElement;
        AppleEverestModuleYaml.RequireObject(root);
        var session = new AppleEverestCoreSession { Index = slot };
        if (root.TryGetProperty("WhateverElseCount", out var count)) session.WhateverElseCount = AppleEverestModuleYaml.Int32(count);
        if (root.TryGetProperty("AttachedDecals", out var decals))
        {
            if (decals.ValueKind == JsonValueKind.Null) session.AttachedDecals = null;
            else
            {
                AppleEverestModuleYaml.RequireArray(decals);
                foreach (var decal in decals.EnumerateArray()) session.AttachedDecals.Add(AppleEverestModuleYaml.String(decal));
            }
        }
        return session;
    }
}
