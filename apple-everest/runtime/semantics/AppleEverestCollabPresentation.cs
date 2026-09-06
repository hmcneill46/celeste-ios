#nullable disable
using System;
using System.Linq;
namespace Celeste.Mod;

internal static class AppleEverestCollabPresentation
{
    internal static AreaData Area(string sid) => AppleEverestProgressionRuntime.TryDescriptor(sid, out var map)
        ? AreaData.Get(map.RuntimeAreaId) : null;
    internal static string Sid(AreaData area) => AppleEverestProgressionRuntime.Sid(new AreaKey(area.ID));
    internal static AreaStats Stats(AreaData area) => SaveData.Instance != null && area != null && area.ID < SaveData.Instance.Areas.Count
        ? SaveData.Instance.Areas[area.ID] : null;
    private static string[] CollabNames => GeneratedAppleEverestCollabManifest.Collabs.Select(v => v.Id)
        .Concat(new[] { "StrawberryJam2021" }).Distinct(StringComparer.Ordinal).ToArray();
    internal static string GetCollabNameForSID(string sid) => CollabNames.FirstOrDefault(name => sid.StartsWith(name + "/", StringComparison.Ordinal));
    internal static string GetLobbyLevelSet(string sid)
    {
        string name = CollabNames.FirstOrDefault(name => sid.StartsWith(name + "/0-Lobbies/", StringComparison.Ordinal) && sid != name + "/0-Lobbies/0-Prologue");
        return name == null ? null : name + "/" + sid.Substring((name + "/0-Lobbies/").Length);
    }
    internal static bool IsCollabGym(string sid) => CollabNames.Any(name => sid.StartsWith(name + "/0-Gyms/", StringComparison.Ordinal)) &&
        Area(sid.Replace("/0-Gyms/", "/0-Lobbies/")) != null;
    internal static bool IsHeartSide(string sid) => GetCollabNameForSID(sid) != null && sid.EndsWith("/ZZ-HeartSide", StringComparison.Ordinal);
    internal static (int Collected, int Total) SilverBerries(string levelSet, string[] maps = null)
    {
        if (levelSet != "StrawberryJam2021/1-Beginner") return AppleEverestProgressionRuntime.SilverBerries(levelSet, SaveData.Instance, maps);
        var selected = AppleEverestCollabMapMetadata.BeginnerSilverBerries.Where(v => maps == null || maps.Length == 0 || maps.Contains(v.Sid)).ToArray();
        return (selected.Count(v => Stats(Area(v.Sid))?.Modes[0]?.Strawberries.Contains(v.Id) == true), selected.Length);
    }
}
