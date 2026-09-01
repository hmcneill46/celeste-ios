using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Celeste.Mod;

internal static class ProgressionScaleAudit
{
    private sealed record InputMap(string Sid, string[] Rooms, Berry[] Berries,
        string[] Checkpoints, bool Heart, bool Cassette);
    private sealed record Berry(string Room, int Id);

    internal static void Write(string inputPath, string outputPath)
    {
        InputMap[] maps = JsonSerializer.Deserialize<InputMap[]>(File.ReadAllBytes(inputPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        if (maps.Length != 128) throw new InvalidDataException("Stage 25K-C progression shape must contain 128 maps");

        object[] fixtures =
        [
            Measure("all-incomplete", maps, 0),
            Measure("representative-partial", maps, 1),
            Measure("all-complete", maps, 2),
        ];
        object result = new
        {
            schemaVersion = 1,
            mapCount = maps.Length,
            schema = "AEVPSV1",
            replicaCapBytes = AppleEverestProgressionCompression.MaximumReplicaBytes,
            allThreeSlotsAbReplicaCapBytes = AppleEverestProgressionCompression.MaximumTotalReplicaBytes,
            fixtures
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        File.WriteAllText(outputPath, JsonSerializer.Serialize(result,
            new JsonSerializerOptions { WriteIndented = true }) + "\n");
    }

    private static object Measure(string name, InputMap[] maps, int state)
    {
        AppleEverestProgressionArea[] areas = maps.Select((map, index) =>
        {
            bool populated = state == 2 || state == 1 && index % 3 == 0;
            AppleEverestProgressionEntityId[] berries = populated
                ? map.Berries.Select(value => new AppleEverestProgressionEntityId(value.Room, value.Id)).ToArray()
                : [];
            string[] checkpoints = populated ? map.Checkpoints : [];
            AppleEverestProgressionMode active = new(map.Berries.Length, populated, populated, populated,
                populated ? index * 3 + 1 : 0, populated ? (index + 1L) * 1000000 : 0,
                populated ? (index + 1L) * 900000 : 0, populated ? (index + 1L) * 950000 : 0,
                populated ? index % 17 : 0, populated ? index % 11 : 0,
                populated && map.Heart, berries, checkpoints);
            AppleEverestProgressionMode empty = new(map.Berries.Length, false, false, false,
                0, 0, 0, 0, 0, 0, false, [], []);
            string levelSet = map.Sid.Contains('/') ? map.Sid[..map.Sid.LastIndexOf('/')] : map.Sid;
            string compatibility = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(map.Sid))).ToLowerInvariant();
            return new AppleEverestProgressionArea(map.Sid, levelSet, compatibility,
                populated && map.Cassette, [active, empty, empty]);
        }).ToArray();
        AppleEverestProgressionSnapshot snapshot = new(0, 1, new byte[32], new byte[32], areas, null, []);
        byte[] raw = AppleEverestProgressionSnapshotCodec.Encode(snapshot);
        byte[] compressed = AppleEverestProgressionCompression.Encode(raw);
        return new
        {
            name,
            rawBytes = raw.Length,
            compressedBytes = compressed.Length,
            replicaPercent = Math.Round(compressed.Length * 100.0 /
                AppleEverestProgressionCompression.MaximumReplicaBytes, 4),
            allThreeSlotsAbBytes = compressed.Length * 6,
            withinReplicaCap = compressed.Length <= AppleEverestProgressionCompression.MaximumReplicaBytes,
            rawSha256 = Convert.ToHexString(SHA256.HashData(raw)).ToLowerInvariant(),
            compressedSha256 = Convert.ToHexString(SHA256.HashData(compressed)).ToLowerInvariant()
        };
    }
}
