using System.Text;

namespace AppleEverestBuilder;

internal static class LevelSetProgressionManifest
{
    internal static LevelSetProgressionRecord[] Create(IEnumerable<MapProgressionRecord> maps)
    {
        MapProgressionRecord[] ordered = (maps ?? Array.Empty<MapProgressionRecord>())
            .OrderBy(map => map.Sid, StringComparer.Ordinal).ToArray();
        if (ordered.Select(map => map.Sid).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
            throw new InvalidDataException("duplicate static map SID in LevelSet manifest");

        return ordered.GroupBy(map => map.LevelSet, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                MapProgressionRecord[] members = group.OrderBy(map => map.Sid, StringComparer.Ordinal).ToArray();
                string semantic = "apple-everest-progression-levelset-v1\n" + group.Key + "\n" +
                    string.Join("\n", members.Select(map => map.Sid + "\t" + map.CompatibilityId));
                return new LevelSetProgressionRecord(
                    group.Key,
                    Hashing.BytesSha256(Encoding.UTF8.GetBytes(semantic)),
                    members.Select(map => map.Sid).ToArray(),
                    members.Sum(map => map.Strawberries),
                    members.Count(map => map.Heart),
                    members.Count(map => map.Cassette),
                    members.Count(map => map.CompletionAvailable));
            }).ToArray();
    }

    internal static string Text(IEnumerable<LevelSetProgressionRecord> levelSets) =>
        "APPLE_EVEREST_LEVELSET_MANIFEST_V1\n" + string.Join("\n",
            (levelSets ?? Array.Empty<LevelSetProgressionRecord>()).Select(levelSet => string.Join("\t",
                levelSet.LevelSet, levelSet.Identity, string.Join(",", levelSet.MapSids),
                levelSet.MaximumStrawberries, levelSet.MaximumHearts,
                levelSet.MaximumCassettes, levelSet.MaximumCompletions))) + "\n";
}
