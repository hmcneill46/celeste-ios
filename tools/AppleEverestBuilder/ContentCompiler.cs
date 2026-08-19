using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace AppleEverestBuilder;

internal static class ContentCompiler
{
    public static string Stage(string source, string relative, string contentOutput)
    {
        string logical = relative.StartsWith("Content/", StringComparison.Ordinal)
            ? relative["Content/".Length..]
            : relative;
        if (logical.EndsWith(".asset.json", StringComparison.Ordinal))
        {
            logical = logical[..^".asset.json".Length] + ".png";
            string target = Target(contentOutput, logical);
            GeneratePng(source, target);
            return logical;
        }
        if (logical.StartsWith("Maps/", StringComparison.Ordinal) && logical.EndsWith(".xml", StringComparison.Ordinal))
        {
            logical = logical[..^4] + ".bin";
            string target = Target(contentOutput, logical);
            CompileMap(source, target, logical["Maps/".Length..^4]);
            return logical;
        }
        if (logical.StartsWith("Maps/", StringComparison.Ordinal) && logical.EndsWith(".bin", StringComparison.Ordinal))
        {
            string target = Target(contentOutput, logical);
            NormalizeMapPackage(source, target, logical["Maps/".Length..^4]);
            return logical;
        }
        string destination = Target(contentOutput, logical);
        File.Copy(source, destination, overwrite: true);
        return logical;
    }

    private static string Target(string root, string relative)
    {
        string fullRoot = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(fullRoot, StringComparison.Ordinal)) throw new InvalidDataException("content output escaped closure root");
        Directory.CreateDirectory(Path.GetDirectoryName(result)!);
        return result;
    }

    private static void GeneratePng(string descriptorPath, string output)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllBytes(descriptorPath));
        JsonElement root = document.RootElement;
        int width = root.GetProperty("width").GetInt32();
        int height = root.GetProperty("height").GetInt32();
        if (width is < 1 or > 1024 || height is < 1 or > 1024) throw new InvalidDataException("generated asset dimensions are invalid");
        byte[] top = Hex(root.GetProperty("top").GetString()!);
        byte[] bottom = Hex(root.GetProperty("bottom").GetString()!);
        byte[] accent = Hex(root.GetProperty("accent").GetString()!);
        byte[] raw = new byte[(width * 4 + 1) * height];
        for (int y = 0; y < height; y++)
        {
            int row = y * (width * 4 + 1);
            raw[row] = 0;
            float amount = height == 1 ? 0f : (float)y / (height - 1);
            for (int x = 0; x < width; x++)
            {
                bool line = x < 4 || x >= width - 4 || y < 4 || y >= height - 4 || Math.Abs(x - width / 2) < 2;
                byte[] color = line ? accent : new[]
                {
                    (byte)Math.Round(top[0] + (bottom[0] - top[0]) * amount),
                    (byte)Math.Round(top[1] + (bottom[1] - top[1]) * amount),
                    (byte)Math.Round(top[2] + (bottom[2] - top[2]) * amount)
                };
                int offset = row + 1 + x * 4;
                raw[offset] = color[0]; raw[offset + 1] = color[1]; raw[offset + 2] = color[2]; raw[offset + 3] = 255;
            }
        }
        using MemoryStream compressed = new();
        using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true)) zlib.Write(raw);
        using FileStream stream = new(output, FileMode.Create, FileAccess.Write);
        stream.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        WriteChunk(stream, "IHDR", UInt32(width).Concat(UInt32(height)).Concat(new byte[] { 8, 6, 0, 0, 0 }).ToArray());
        WriteChunk(stream, "IDAT", compressed.ToArray());
        WriteChunk(stream, "IEND", Array.Empty<byte>());
    }

    private static void CompileMap(string xmlPath, string output, string package)
    {
        XmlDocument document = new() { PreserveWhitespace = false };
        document.Load(xmlPath);
        XmlElement root = document.DocumentElement ?? throw new InvalidDataException("map XML has no root element");
        List<string> table = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        void Add(string value) { if (seen.Add(value)) table.Add(value); }
        void Visit(XmlElement element)
        {
            Add(element.Name);
            foreach (XmlAttribute attribute in element.Attributes)
            {
                Add(attribute.Name);
                if (Value(attribute.Value).Type == 5) Add(attribute.Value);
            }
            foreach (XmlElement child in element.ChildNodes.OfType<XmlElement>()) Visit(child);
        }
        Visit(root); Add("innerText");
        Dictionary<string, short> lookup = table.Select((value, index) => (value, index)).ToDictionary(item => item.value, item => checked((short)item.index), StringComparer.Ordinal);
        using BinaryWriter writer = new(File.Open(output, FileMode.Create), Encoding.UTF8, leaveOpen: false);
        writer.Write("CELESTE MAP"); writer.Write(package); writer.Write(checked((short)table.Count));
        foreach (string value in table) writer.Write(value);
        WriteElement(writer, root, lookup);
    }

    private static void NormalizeMapPackage(string source, string output, string package)
    {
        // Everest accepts the generic package label emitted by common map
        // editors (normally "Contribution"), whereas vanilla Celeste insists
        // that this header equal the mounted ModeProperties path.  Normalize
        // only the two length-prefixed header strings at build time.  The
        // string table and complete element body remain the original pinned
        // mod bytes, and malformed/non-Celeste binaries fail closed.
        using FileStream input = File.OpenRead(source);
        using BinaryReader reader = new(input, Encoding.UTF8, leaveOpen: true);
        string magic;
        string sourcePackage;
        try
        {
            magic = reader.ReadString();
            sourcePackage = reader.ReadString();
        }
        catch (EndOfStreamException exception)
        {
            throw new InvalidDataException("map binary has an invalid Celeste header", exception);
        }
        if (magic != "CELESTE MAP") throw new InvalidDataException("map binary has an invalid Celeste header");
        if (sourcePackage.Length is < 1 or > 1024) throw new InvalidDataException("map binary package is invalid");
        long bodyOffset = input.Position;
        byte[] body = new byte[checked((int)(input.Length - bodyOffset))];
        input.ReadExactly(body);
        if (body.Length < sizeof(short)) throw new InvalidDataException("map binary body is truncated");

        if (sourcePackage == package)
        {
            File.Copy(source, output, overwrite: true);
            return;
        }

        using BinaryWriter writer = new(File.Open(output, FileMode.Create), Encoding.UTF8, leaveOpen: false);
        writer.Write(magic);
        writer.Write(package);
        writer.Write(body);
    }

    private static void WriteElement(BinaryWriter writer, XmlElement element, IReadOnlyDictionary<string, short> lookup)
    {
        XmlElement[] children = element.ChildNodes.OfType<XmlElement>().ToArray();
        string text = children.Length == 0 ? NormalizeText(element) : "";
        writer.Write(lookup[element.Name]);
        writer.Write(checked((byte)(element.Attributes.Count + (text.Length > 0 ? 1 : 0))));
        foreach (XmlAttribute attribute in element.Attributes)
        {
            (byte type, object parsed) = Value(attribute.Value);
            writer.Write(lookup[attribute.Name]); writer.Write(type); WriteValue(writer, type, parsed, lookup);
        }
        if (text.Length > 0)
        {
            writer.Write(lookup["innerText"]);
            if (element.Name is "solids" or "bg")
            {
                byte[] encoded = Rle(text); writer.Write((byte)7); writer.Write(checked((short)encoded.Length)); writer.Write(encoded);
            }
            else { writer.Write((byte)6); writer.Write(text); }
        }
        writer.Write(checked((short)children.Length));
        foreach (XmlElement child in children) WriteElement(writer, child, lookup);
    }

    private static string NormalizeText(XmlElement element)
    {
        string value = element.InnerText;
        if (element.Name is "solids" or "bg" or "fgtiles" or "bgtiles" or "objtiles")
            return string.Join("\n", value.Replace("\r", "").Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0));
        return value.Trim();
    }

    private static (byte Type, object Value) Value(string value)
    {
        if (bool.TryParse(value, out bool boolean)) return (0, boolean);
        if (byte.TryParse(value, out byte octet)) return (1, octet);
        if (short.TryParse(value, out short shortValue)) return (2, shortValue);
        if (int.TryParse(value, out int integer)) return (3, integer);
        if (float.TryParse(value, System.Globalization.NumberStyles.Integer | System.Globalization.NumberStyles.AllowDecimalPoint,
                System.Globalization.CultureInfo.InvariantCulture, out float single)) return (4, single);
        return (5, value);
    }

    private static void WriteValue(BinaryWriter writer, byte type, object value, IReadOnlyDictionary<string, short> lookup)
    {
        switch (type)
        {
            case 0: writer.Write((bool)value); break;
            case 1: writer.Write((byte)value); break;
            case 2: writer.Write((short)value); break;
            case 3: writer.Write((int)value); break;
            case 4: writer.Write((float)value); break;
            case 5: writer.Write(lookup[(string)value]); break;
            default: throw new InvalidDataException("unknown map value type");
        }
    }

    private static byte[] Rle(string value)
    {
        List<byte> result = [];
        for (int index = 0; index < value.Length; index++)
        {
            byte count = 1; char current = value[index];
            while (index + 1 < value.Length && value[index + 1] == current && count < byte.MaxValue) { count++; index++; }
            result.Add(count); result.Add(checked((byte)current));
        }
        return result.ToArray();
    }

    private static byte[] Hex(string value)
    {
        if (value.Length != 6) throw new InvalidDataException("asset color must be six hexadecimal digits");
        return Convert.FromHexString(value);
    }

    private static byte[] UInt32(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    private static void WriteChunk(Stream output, string kind, byte[] data)
    {
        byte[] name = Encoding.ASCII.GetBytes(kind);
        output.Write(UInt32(data.Length)); output.Write(name); output.Write(data);
        output.Write(UInt32(unchecked((int)Crc32(name.Concat(data)))));
    }

    private static uint Crc32(IEnumerable<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (byte value in bytes)
        {
            crc ^= value;
            for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xedb88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }
}
