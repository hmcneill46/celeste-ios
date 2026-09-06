#nullable disable
using System.Globalization;
using System.Text.Json;
using AppleEverestBuilder;
using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;

internal static class SelectedProfileGuardTests
{
    internal static int Run(string repository, string temporary)
    {
        const string berryId = "LunaticHelper/StrawberryWithReturn";
        int passed = 0;
        void Reject(Action action)
        {
            try { action(); }
            catch (InvalidOperationException e) when (e.Message.Contains("outside the reviewed")) { passed++; return; }
            throw new Exception("selected profile guard accepted unreviewed data");
        }
        EntityData Berry(bool indices = true) => new() { Name = berryId, Values = indices
            ? new() { ["checkpointID"] = -1, ["order"] = -1, ["winged"] = false }
            : new() { ["winged"] = false } };
        void Normalize(EntityData data, int checkpoint, int order)
        {
            AppleEverestSelectedProfileGuard.RecordBerryNormalization(data, checkpoint, order);
            data.Values["checkpointID"] = checkpoint; data.Values["order"] = order;
        }
        foreach (bool indices in new[] { false, true })
        {
            var data = Berry(indices);
            Normalize(data, 2, 7);
            AppleEverestSelectedProfileGuard.Entity(berryId, data); passed++;
            Normalize(data, 2, 8); // Rebuilding a tracker must preserve the original profile.
            AppleEverestSelectedProfileGuard.Entity(berryId, data); passed++;
            data.Values["order"] = 9;
            Reject(() => AppleEverestSelectedProfileGuard.Entity(berryId, data));
        }
        var unreviewed = Berry(); unreviewed.Values["order"] = 4;
        Reject(() => Normalize(unreviewed, 0, 0));
        var forged = Berry(); forged.Values["checkpointID"] = 0; forged.Values["order"] = 0;
        Reject(() => AppleEverestSelectedProfileGuard.Entity(berryId, forged));
        foreach (string mutation in new[] { "winged", "extra", "checkpointID" })
        {
            var data = Berry(); Normalize(data, 0, 0);
            data.Values[mutation] = mutation == "winged" ? true : 99;
            Reject(() => AppleEverestSelectedProfileGuard.Entity(berryId, data));
        }
        var wrongShape = Berry(); Normalize(wrongShape, 0, 0); wrongShape.Width = 8;
        Reject(() => AppleEverestSelectedProfileGuard.Entity(berryId, wrongShape));

        // Exercise the actual generated guard against compiled fixture bytes,
        // preserving the binary types and the runtime's EntityData projection.
        using var plan = JsonDocument.Parse(File.ReadAllText(Path.Combine(repository,
            "apple-everest/sj-factory-canary-plan-stage25kj.json")));
        var selected = plan.RootElement.GetProperty("representatives").EnumerateArray()
            .Select(row => row.GetProperty("customId").GetString()).ToHashSet();
        var observed = new HashSet<string>();
        string output = Path.Combine(temporary, "guard-compiled-maps"); Directory.CreateDirectory(output);
        string input = Path.Combine(repository, "apple-everest/canaries/stage25kj/Content/Maps");
        foreach (string xml in Directory.GetFiles(input, "*.xml", SearchOption.AllDirectories))
        {
            string logical = ContentCompiler.Stage(xml, "Content/Maps/" + Path.GetRelativePath(input, xml), output);
            using var reader = new BinaryReader(File.OpenRead(Path.Combine(output, logical)));
            if (reader.ReadString() != "CELESTE MAP") throw new Exception("invalid compiled fixture header");
            _ = reader.ReadString(); var table = new string[reader.ReadInt16()];
            for (int i = 0; i < table.Length; i++) table[i] = reader.ReadString();
            Walk(Read(reader, table), "", false);
        }
        if (observed.Count != 73) throw new Exception("compiled guard checks did not cover all 73 selected factories");
        Console.WriteLine("PASS: runtime profile guards accept all 73 compiled factory fixtures; berry normalization and mutation controls passed");
        passed++;
        return passed;

        void Walk(BinaryPacker.Element element, string parent, bool foreground)
        {
            foreground |= element.Name == "Foregrounds";
            if (selected.Contains(element.Name))
            {
                var values = new Dictionary<string, object>(element.Attributes);
                if (parent is "entities" or "triggers")
                {
                    float Number(string key) => values.TryGetValue(key, out object value) ? Convert.ToSingle(value, CultureInfo.InvariantCulture) : 0;
                    var data = new EntityData { Name = element.Name, Position = new(Number("x"), Number("y")),
                        Origin = new(Number("originX"), Number("originY")), Width = (int)Number("width"), Height = (int)Number("height"),
                        Nodes = element.Children.Select(n => new Vector2(Convert.ToSingle(n.Attributes["x"]), Convert.ToSingle(n.Attributes["y"]))).ToArray() };
                    foreach (string key in new[] { "id", "x", "y", "width", "height", "originX", "originY" }) values.Remove(key);
                    data.Values = values;
                    if (data.Name == berryId) Normalize(data, 0, 0);
                    AppleEverestSelectedProfileGuard.Entity(data.Name, data);
                }
                else
                {
                    values["name"] = element.Name; values["fg"] = foreground;
                    AppleEverestSelectedProfileGuard.Backdrop(element.Name, new() { Name = element.Name, Attributes = values });
                }
                observed.Add(element.Name);
            }
            foreach (var child in element.Children) Walk(child, element.Name, foreground);
        }
    }

    private static BinaryPacker.Element Read(BinaryReader reader, string[] table)
    {
        var element = new BinaryPacker.Element { Name = table[reader.ReadInt16()] };
        int attributes = reader.ReadByte();
        for (int i = 0; i < attributes; i++)
        {
            string key = table[reader.ReadInt16()];
            element.Attributes[key] = reader.ReadByte() switch {
                0 => reader.ReadBoolean(), 1 => reader.ReadByte(), 2 => reader.ReadInt16(), 3 => reader.ReadInt32(),
                4 => reader.ReadSingle(), 5 => table[reader.ReadInt16()], 6 => reader.ReadString(),
                7 => reader.ReadBytes(reader.ReadInt16()), _ => throw new Exception("unknown fixture binary type") };
        }
        int children = reader.ReadInt16();
        for (int i = 0; i < children; i++) element.Children.Add(Read(reader, table));
        return element;
    }
}

// Minimal host ABI for the directly linked guard; no gameplay lifecycle is simulated.
namespace Microsoft.Xna.Framework
{
    internal readonly record struct Vector2(float X, float Y)
    {
        internal static Vector2 Zero => default;
        public static Vector2 operator -(Vector2 left, Vector2 right) => new(left.X - right.X, left.Y - right.Y);
    }
}
namespace Celeste
{
    internal sealed class EntityData
    {
        internal string Name;
        internal Vector2 Position, Origin;
        internal int Width, Height;
        internal Vector2[] Nodes;
        internal Dictionary<string, object> Values;
    }
    internal static class BinaryPacker
    {
        internal sealed class Element
        {
            internal string Name;
            internal Dictionary<string, object> Attributes = new();
            internal List<Element> Children = new();
        }
    }
}
