using System.Text;
using System.Text.Json;

namespace CelesteIOSFoundation;

public enum IOSPortableDocumentKind
{
    CelesteLogicalFile,
    TouchLayout,
}

public readonly record struct IOSPortableDocument(
    string FileName,
    IOSPortableDocumentKind Kind,
    byte[] Data);

public readonly record struct IOSExternalReadResult(
    bool Cancelled,
    byte[]? Data,
    string? ErrorMessage)
{
    public static IOSExternalReadResult Cancel() => new(true, null, null);
    public static IOSExternalReadResult Failure(string message) => new(false, null, message);
    public static IOSExternalReadResult Success(byte[] data) => new(false, data, null);
}

/// <summary>
/// Platform-neutral request surface between generated Celeste UI and the
/// UIKit host. The host owns document pickers, coordinated external reads,
/// temporary exports; game code owns canonical validation, confirmation and the
/// durability transaction. No external URL crosses this boundary.
/// </summary>
public static class IOSFilePortabilityBridge
{
    public static Action<IOSPortableDocumentKind, int, Action<IOSExternalReadResult>>? ImportRequested { get; set; }
    public static Action<IReadOnlyList<IOSPortableDocument>, bool, Action<bool, string?>>? ExportRequested { get; set; }
    public static Action<bool>? SystemPresentationChanged { get; set; }

    public static bool IsAvailable => ImportRequested is not null && ExportRequested is not null;

    public static bool RequestImport(
        IOSPortableDocumentKind kind,
        int maximumBytes,
        Action<IOSExternalReadResult> completed)
    {
        ArgumentNullException.ThrowIfNull(completed);
        Action<IOSPortableDocumentKind, int, Action<IOSExternalReadResult>>? handler = ImportRequested;
        if (handler is null) return false;
        handler(kind, maximumBytes, completed);
        return true;
    }

    public static bool RequestExport(
        IReadOnlyList<IOSPortableDocument> documents,
        bool share,
        Action<bool, string?> completed)
    {
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(completed);
        if (documents.Count == 0) return false;
        Action<IReadOnlyList<IOSPortableDocument>, bool, Action<bool, string?>>? handler = ExportRequested;
        if (handler is null) return false;
        handler(documents, share, completed);
        return true;
    }

    public static void Clear()
    {
        ImportRequested = null;
        ExportRequested = null;
        SystemPresentationChanged = null;
    }
}

public readonly record struct TouchLayoutShareDocument(
    string? PhoneProfile,
    string? TabletProfile)
{
    public const int MaximumBytes = 32 * 1024;
    public const int FormatVersion = 1;
    public const string Extension = "celestetouch";
    public const string TypeIdentifier = "io.github.roootthefox.celeste.touch-layout";

    public byte[] Encode()
    {
        if (!ValidateProfile(PhoneProfile) || !ValidateProfile(TabletProfile) ||
            (PhoneProfile is null && TabletProfile is null))
            throw new InvalidDataException("The touch layout document has no valid profile.");

        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("format", "Celeste Touch Layout");
            writer.WriteNumber("version", FormatVersion);
            if (PhoneProfile is not null) writer.WriteString("phoneProfile", PhoneProfile);
            if (TabletProfile is not null) writer.WriteString("tabletProfile", TabletProfile);
            writer.WriteEndObject();
        }
        byte[] result = stream.ToArray();
        if (result.Length > MaximumBytes)
            throw new InvalidDataException("The touch layout document is too large.");
        return result;
    }

    public static bool TryDecode(ReadOnlyMemory<byte> data, out TouchLayoutShareDocument document)
    {
        document = default;
        if (data.Length is < 2 or > MaximumBytes) return false;
        try
        {
            using JsonDocument json = JsonDocument.Parse(data, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 8,
            });
            JsonElement root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return false;
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in root.EnumerateObject())
                if (!names.Add(property.Name) || property.Name is not ("format" or "version" or "phoneProfile" or "tabletProfile"))
                    return false;
            if (!names.Contains("format") || !names.Contains("version") || names.Count is < 3 or > 4 ||
                root.GetProperty("format").GetString() != "Celeste Touch Layout" ||
                root.GetProperty("version").GetInt32() != FormatVersion)
                return false;
            string? phone = root.TryGetProperty("phoneProfile", out JsonElement p) && p.ValueKind == JsonValueKind.String
                ? p.GetString() : null;
            string? tablet = root.TryGetProperty("tabletProfile", out JsonElement t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() : null;
            if (!ValidateProfile(phone) || !ValidateProfile(tablet) || (phone is null && tablet is null)) return false;
            document = new TouchLayoutShareDocument(phone, tablet);
            return true;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static bool ValidateProfile(string? encoded)
    {
        if (encoded is null) return true;
        return TouchLayoutCodec.TryDecode(encoded, out TouchLayoutProfile profile) &&
               TouchLayoutPolicy.IsStructurallyValid(profile);
    }
}
