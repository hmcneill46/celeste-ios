using System.Text;

namespace AppleEverestBuilder;

internal static class MapDataCompatibilityPatch
{
    internal const string LookupTarget =
        "\t\t\t\tint num7 = strawberry.Int(\"checkpointID\");\n" +
        "\t\t\t\tint num8 = strawberry.Int(\"order\");\n" +
        "\t\t\t\tif (ModeData.StrawberriesByCheckpoint[num7, num8] == null)";

    private const string MethodBoundary =
        "\t}\n\n\tpublic int[] GetStrawberries(out int total)";

    private const string Helper =
        "\t}\n\n" +
        "\tprivate static EntityData AppleEverestNormalizeAndGet(ref EntityData[,] map, EntityData strawberry, List<LevelData> levels, int maximumCheckpoint, out int y, out int x)\n" +
        "\t{\n" +
        "\t\ty = strawberry.Int(\"checkpointIDParented\", strawberry.Int(\"checkpointID\", -1));\n" +
        "\t\tif (y < 0)\n" +
        "\t\t{\n" +
        "\t\t\ty = AppleEverestCheckpointForLevel(strawberry, levels);\n" +
        "\t\t}\n" +
        "\t\tif (y > maximumCheckpoint)\n" +
        "\t\t{\n" +
        "\t\t\ty = maximumCheckpoint;\n" +
        "\t\t}\n" +
        "\t\tx = strawberry.Int(\"order\", -1);\n" +
        "\t\tif (x < 0)\n" +
        "\t\t{\n" +
        "\t\t\tx = 0;\n" +
        "\t\t}\n" +
        "\t\tAppleEverestEnsureTracker(ref map, y, x);\n" +
        "\t\tif (map[y, x] != null)\n" +
        "\t\t{\n" +
        "\t\t\tx = 0;\n" +
        "\t\t\twhile (true)\n" +
        "\t\t\t{\n" +
        "\t\t\t\tAppleEverestEnsureTracker(ref map, y, x);\n" +
        "\t\t\t\tif (map[y, x] == null)\n" +
        "\t\t\t\t{\n" +
        "\t\t\t\t\tbreak;\n" +
        "\t\t\t\t}\n" +
        "\t\t\t\tx++;\n" +
        "\t\t\t}\n" +
        "\t\t}\n" +
        "\t\tif (strawberry.Values == null)\n" +
        "\t\t{\n" +
        "\t\t\tstrawberry.Values = new Dictionary<string, object>();\n" +
        "\t\t}\n" +
        "\t\tstrawberry.Values[\"checkpointID\"] = y;\n" +
        "\t\tstrawberry.Values[\"order\"] = x;\n" +
        "\t\treturn map[y, x];\n" +
        "\t}\n\n" +
        "\tprivate static int AppleEverestCheckpointForLevel(EntityData strawberry, List<LevelData> levels)\n" +
        "\t{\n" +
        "\t\tif (strawberry?.Level == null || levels == null)\n" +
        "\t\t{\n" +
        "\t\t\treturn 0;\n" +
        "\t\t}\n" +
        "\t\tint checkpoint = 0;\n" +
        "\t\tforeach (LevelData level in levels)\n" +
        "\t\t{\n" +
        "\t\t\tif (level?.Entities != null)\n" +
        "\t\t\t{\n" +
        "\t\t\t\tforeach (EntityData entity in level.Entities)\n" +
        "\t\t\t\t{\n" +
        "\t\t\t\t\tif (entity?.Name == \"checkpoint\") checkpoint++;\n" +
        "\t\t\t\t}\n" +
        "\t\t\t}\n" +
        "\t\t\tif (object.ReferenceEquals(level, strawberry.Level)) return checkpoint;\n" +
        "\t\t}\n" +
        "\t\treturn 0;\n" +
        "\t}\n\n" +
        "\tprivate static void AppleEverestEnsureTracker(ref EntityData[,] map, int y, int x)\n" +
        "\t{\n" +
        "\t\tif (map.GetLength(0) <= y || map.GetLength(1) <= x)\n" +
        "\t\t{\n" +
        "\t\t\tEntityData[,] expanded = new EntityData[y + 10, x + 25];\n" +
        "\t\t\tint oldHeight = map.GetLength(1);\n" +
        "\t\t\tint newHeight = expanded.GetLength(1);\n" +
        "\t\t\tint oldWidth = map.GetLength(0);\n" +
        "\t\t\tfor (int column = 0; column < oldWidth; column++)\n" +
        "\t\t\t{\n" +
        "\t\t\t\tArray.Copy(map, column * oldHeight, expanded, column * newHeight, oldHeight);\n" +
        "\t\t\t}\n" +
        "\t\t\tmap = expanded;\n" +
        "\t\t}\n" +
        "\t}\n\n" +
        "\tpublic int[] GetStrawberries(out int total)";

    internal static void Apply(string path)
    {
        string source = ApplySource(File.ReadAllText(path));
        File.WriteAllText(path, source, new UTF8Encoding(false));
    }

    internal static string ApplySource(string source)
    {
        source = ReplaceExactlyOnce(source, LookupTarget,
            "\t\t\t\tint num7;\n" +
            "\t\t\t\tint num8;\n" +
            "\t\t\t\tif (AppleEverestNormalizeAndGet(ref ModeData.StrawberriesByCheckpoint, strawberry, Levels, ModeData.Checkpoints?.Length ?? 0, out num7, out num8) == null)");
        return ReplaceExactlyOnce(source, MethodBoundary, Helper);
    }

    // Host-side semantic oracle for the bounded pinned-Everest berry tracker
    // normalization emitted above. It is never linked into either product.
    internal static (int Checkpoint, int Order, T? Existing) NormalizeAndGetForTest<T>(
        ref T?[,] map, int checkpoint, int order, int maximumCheckpoint)
    {
        if (checkpoint < 0) checkpoint = 0;
        if (checkpoint > maximumCheckpoint) checkpoint = maximumCheckpoint;
        if (order < 0) order = 0;
        EnsureForTest(ref map, checkpoint, order);
        if (map[checkpoint, order] is not null)
        {
            order = 0;
            while (true)
            {
                EnsureForTest(ref map, checkpoint, order);
                if (map[checkpoint, order] is null) break;
                order++;
            }
        }
        return (checkpoint, order, map[checkpoint, order]);
    }

    internal static int CheckpointForRoomForTest(
        IReadOnlyList<(string Room, int Checkpoints)> rooms, string strawberryRoom)
    {
        int checkpoint = 0;
        foreach ((string room, int checkpoints) in rooms)
        {
            checkpoint += Math.Max(0, checkpoints);
            if (room == strawberryRoom) return checkpoint;
        }
        return 0;
    }

    private static void EnsureForTest<T>(ref T?[,] map, int y, int x)
    {
        if (map.GetLength(0) > y && map.GetLength(1) > x) return;
        T?[,] expanded = new T?[y + 10, x + 25];
        int oldHeight = map.GetLength(1);
        int newHeight = expanded.GetLength(1);
        int oldWidth = map.GetLength(0);
        for (int column = 0; column < oldWidth; column++)
            Array.Copy(map, column * oldHeight, expanded, column * newHeight, oldHeight);
        map = expanded;
    }

    private static string ReplaceExactlyOnce(string source, string needle, string replacement)
    {
        int first = source.IndexOf(needle, StringComparison.Ordinal);
        if (first < 0 || source.IndexOf(needle, first + needle.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidDataException("locked MapData compatibility target must occur exactly once");
        return source[..first] + replacement + source[(first + needle.Length)..];
    }
}
