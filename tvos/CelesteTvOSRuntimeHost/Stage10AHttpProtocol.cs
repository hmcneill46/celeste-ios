#if CELESTE_RUNTIME && TVOS_STAGE6_HOST
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace CelesteTvOSHost;

internal sealed record Stage10AExportSnapshot(
    ulong Generation,
    string LogicalHash,
    IReadOnlyDictionary<string, byte[]?> Files
);

internal sealed record Stage10AHttpResponse(int StatusCode, string Reason, IReadOnlyDictionary<string, string> Headers, byte[] Body)
{
    internal byte[] Encode(bool headOnly)
    {
        StringBuilder header = new();
        header.Append("HTTP/1.1 ").Append(StatusCode).Append(' ').Append(Reason).Append("\r\n");
        foreach ((string name, string value) in Headers) header.Append(name).Append(": ").Append(value).Append("\r\n");
        header.Append("Connection: close\r\n\r\n");
        byte[] prefix = Encoding.ASCII.GetBytes(header.ToString());
        if (headOnly || Body.Length == 0) return prefix;
        byte[] result = new byte[prefix.Length + Body.Length];
        Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
        Buffer.BlockCopy(Body, 0, result, prefix.Length, Body.Length);
        return result;
    }
}

internal sealed class Stage10AHttpProtocol
{
    internal const int MaximumRequestLineBytes = 2048;
    internal const int MaximumHeaderBytes = 16 * 1024;
    internal const int MaximumHeaderCount = 48;
    internal const int MaximumAuthBodyBytes = 64;
    internal const int MaximumConcurrentConnections = 4;
    internal const string SessionCookieName = "CelesteSaveSession";
    internal static readonly TimeSpan RequestLifetime = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);

    private static readonly string[] LogicalNames = { "settings", "0", "1", "2" };
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> now;
    private readonly Dictionary<string, DateTimeOffset> sessions = new(StringComparer.Ordinal);
    private Stage10AExportSnapshot snapshot;
    private string accessCode;
    private bool active = true;

    internal Stage10AHttpProtocol(Stage10AExportSnapshot stableSnapshot, Func<DateTimeOffset>? clock = null)
    {
        snapshot = ValidateSnapshot(stableSnapshot);
        now = clock ?? (() => DateTimeOffset.UtcNow);
        accessCode = GenerateAccessCode();
    }

    internal string AccessCode { get { lock (gate) return accessCode; } }
    internal int ActiveSessionCount { get { lock (gate) { PurgeExpired(); return sessions.Count; } } }

    internal void ReplaceSnapshot(Stage10AExportSnapshot stableSnapshot)
    {
        lock (gate) snapshot = ValidateSnapshot(stableSnapshot);
    }

    internal void Stop()
    {
        lock (gate)
        {
            active = false;
            accessCode = "";
            sessions.Clear();
        }
    }

    internal Stage10AHttpResponse Handle(byte[] requestBytes)
    {
        try
        {
            ParsedRequest request = Parse(requestBytes);
            lock (gate)
            {
                if (!active) return Error(503, "Service Unavailable", "Save Manager has stopped.");
                PurgeExpired();
                return Route(request);
            }
        }
        catch (HttpFailure failure)
        {
            return Error(failure.StatusCode, failure.Reason, failure.PublicMessage);
        }
        catch
        {
            return Error(500, "Internal Server Error", "The Save Manager could not complete this request.");
        }
    }

    internal static string GenerateAccessCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    private Stage10AHttpResponse Route(ParsedRequest request)
    {
        bool isHead = request.Method == "HEAD";
        if (request.Method == "POST")
        {
            if (request.Path != "/auth") return MethodNotAllowed("GET, HEAD");
            return Authenticate(request);
        }
        if (request.Method is not ("GET" or "HEAD")) return MethodNotAllowed(request.Path == "/auth" ? "POST" : "GET, HEAD");

        bool authenticated = TryAuthenticate(request.Headers);
        if (request.Path == "/")
        {
            string html = authenticated ? AuthenticatedPage(snapshot) : AccessCodePage(invalid: false);
            return Html(200, "OK", html, isHead);
        }
        if (!authenticated) return Html(401, "Unauthorized", AccessCodePage(invalid: false), isHead);

        if (request.Path == "/download/all")
        {
            byte[] archive = CreateArchive(snapshot);
            Dictionary<string, string> archiveHeaders = BaseHeaders("application/zip", archive.Length);
            archiveHeaders["Content-Disposition"] = "attachment; filename=\"Celeste-saves.zip\"";
            archiveHeaders["X-Celeste-Logical-Name"] = "all";
            archiveHeaders["X-Celeste-Content-SHA256"] = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
            return new Stage10AHttpResponse(200, "OK", archiveHeaders, isHead ? Array.Empty<byte>() : archive);
        }

        string? logicalName = request.Path switch
        {
            "/download/settings" => "settings",
            "/download/0" => "0",
            "/download/1" => "1",
            "/download/2" => "2",
            _ => null
        };
        if (logicalName == null) return Error(404, "Not Found", "That Save Manager route does not exist.");
        byte[]? payload = snapshot.Files[logicalName];
        if (payload == null) return Error(404, "Not Found", "No save is present in that slot.");
        string filename = logicalName == "settings" ? "settings.celeste" : logicalName + ".celeste";
        Dictionary<string, string> headers = BaseHeaders("application/octet-stream", payload.Length);
        headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
        headers["X-Celeste-Logical-Name"] = logicalName;
        headers["X-Celeste-Content-SHA256"] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return new Stage10AHttpResponse(200, "OK", headers, isHead ? Array.Empty<byte>() : payload.ToArray());
    }

    private Stage10AHttpResponse Authenticate(ParsedRequest request)
    {
        if (!request.Headers.TryGetValue("content-type", out string? contentType) ||
            !contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpFailure(415, "Unsupported Media Type", "The access-code form format is unsupported.");
        }
        string body = Encoding.ASCII.GetString(request.Body);
        if (!body.StartsWith("code=", StringComparison.Ordinal) || body.IndexOf('&') >= 0)
            throw new HttpFailure(400, "Bad Request", "The access-code form is malformed.");
        string supplied = DecodeFormValue(body[5..]).Replace(" ", "", StringComparison.Ordinal);
        bool valid = supplied.Length == 6 && accessCode.Length == 6 &&
            CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(supplied), Encoding.ASCII.GetBytes(accessCode));
        if (!valid) return Html(401, "Unauthorized", AccessCodePage(invalid: true), headOnly: false);

        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        sessions[token] = now().Add(SessionLifetime);
        // Return the authenticated page directly. A physical mobile-browser
        // test showed that a standards-valid 303 response could strand the UI
        // before its follow-up GET even though the response was fully sent.
        Stage10AHttpResponse response = Html(200, "OK", AuthenticatedPage(snapshot), headOnly: false);
        Dictionary<string, string> headers = new(response.Headers, StringComparer.Ordinal)
        {
            ["X-Celeste-Authentication"] = "accepted",
            ["Set-Cookie"] = $"{SessionCookieName}={token}; Path=/; Max-Age=600; HttpOnly; SameSite=Strict"
        };
        return response with { Headers = headers };
    }

    private bool TryAuthenticate(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("cookie", out string? cookie)) return false;
        foreach (string part in cookie.Split(';'))
        {
            string candidate = part.Trim();
            string prefix = SessionCookieName + "=";
            if (!candidate.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string token = candidate[prefix.Length..];
            if (token.Length != 64 || token.Any(value => !Uri.IsHexDigit(value))) return false;
            if (!sessions.TryGetValue(token, out DateTimeOffset expiry) || expiry <= now()) return false;
            sessions[token] = now().Add(SessionLifetime);
            return true;
        }
        return false;
    }

    private void PurgeExpired()
    {
        DateTimeOffset current = now();
        foreach (string key in sessions.Where(pair => pair.Value <= current).Select(pair => pair.Key).ToArray()) sessions.Remove(key);
    }

    private static ParsedRequest Parse(byte[] bytes)
    {
        if (bytes.Length > MaximumHeaderBytes + MaximumAuthBodyBytes) throw TooLarge();
        int separator = IndexOf(bytes, "\r\n\r\n"u8);
        if (separator < 0) throw new HttpFailure(400, "Bad Request", "The HTTP headers are incomplete.");
        int headerLength = separator + 4;
        if (headerLength > MaximumHeaderBytes) throw TooLarge();
        if (bytes.Take(headerLength).Any(value => value == 0 || value > 0x7f))
            throw new HttpFailure(400, "Bad Request", "HTTP headers must be ASCII.");

        string headerText = Encoding.ASCII.GetString(bytes, 0, separator);
        string[] lines = headerText.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || Encoding.ASCII.GetByteCount(lines[0]) > MaximumRequestLineBytes) throw TooLarge();
        string[] requestLine = lines[0].Split(' ', StringSplitOptions.None);
        if (requestLine.Length != 3 || requestLine.Any(string.IsNullOrEmpty))
            throw new HttpFailure(400, "Bad Request", "The HTTP request line is malformed.");
        string method = requestLine[0];
        if (method.Any(value => value is < 'A' or > 'Z')) throw new HttpFailure(400, "Bad Request", "The HTTP method is malformed.");
        string path = requestLine[1];
        if (requestLine[2] is not ("HTTP/1.0" or "HTTP/1.1")) throw new HttpFailure(505, "HTTP Version Not Supported", "Use HTTP/1.0 or HTTP/1.1.");
        ValidatePath(path);

        if (lines.Length - 1 > MaximumHeaderCount) throw new HttpFailure(431, "Request Header Fields Too Large", "Too many HTTP headers were supplied.");
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0])) throw new HttpFailure(400, "Bad Request", "Folded or empty HTTP headers are not accepted.");
            int colon = line.IndexOf(':');
            if (colon <= 0) throw new HttpFailure(400, "Bad Request", "An HTTP header is malformed.");
            string name = line[..colon].ToLowerInvariant();
            if (name.Any(value => !(char.IsAsciiLetterOrDigit(value) || value == '-')) || !headers.TryAdd(name, line[(colon + 1)..].Trim()))
                throw new HttpFailure(400, "Bad Request", "Duplicate or malformed HTTP headers are not accepted.");
        }
        if (headers.ContainsKey("transfer-encoding")) throw new HttpFailure(400, "Bad Request", "Transfer-Encoding is not accepted.");
        int contentLength = 0;
        if (headers.TryGetValue("content-length", out string? text) &&
            (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) || contentLength < 0))
            throw new HttpFailure(400, "Bad Request", "Content-Length is invalid.");
        if (contentLength > MaximumAuthBodyBytes) throw TooLarge();
        if (bytes.Length - headerLength != contentLength) throw new HttpFailure(400, "Bad Request", "The request body length is inconsistent.");
        if (method != "POST" && contentLength != 0) throw new HttpFailure(400, "Bad Request", "GET and HEAD requests must not contain a body.");
        return new ParsedRequest(method, path, headers, bytes.AsSpan(headerLength, contentLength).ToArray());
    }

    private static void ValidatePath(string path)
    {
        if (path.Length == 0 || path[0] != '/' || path.Contains('\\') || path.Contains('?') || path.Contains('#') ||
            path.Contains("//", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal) || path.Contains('%'))
        {
            throw new HttpFailure(400, "Bad Request", "The request path is invalid.");
        }
        if (path.Any(value => value < 0x21 || value > 0x7e)) throw new HttpFailure(400, "Bad Request", "The request path is invalid.");
    }

    private static string DecodeFormValue(string value)
    {
        StringBuilder result = new();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '+') { result.Append(' '); continue; }
            if (value[i] != '%') { result.Append(value[i]); continue; }
            if (i + 2 >= value.Length || !byte.TryParse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte decoded))
                throw new HttpFailure(400, "Bad Request", "The access-code form is malformed.");
            result.Append((char)decoded);
            i += 2;
        }
        return result.ToString();
    }

    private static Stage10AExportSnapshot ValidateSnapshot(Stage10AExportSnapshot value)
    {
        if (value.Files.Count != LogicalNames.Length || LogicalNames.Any(name => !value.Files.ContainsKey(name)) ||
            value.Files.Keys.Any(name => !LogicalNames.Contains(name, StringComparer.Ordinal)))
            throw new InvalidOperationException("Save Manager exports must contain exactly settings and save slots 0, 1, and 2.");
        return new Stage10AExportSnapshot(value.Generation, value.LogicalHash,
            value.Files.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal));
    }

    private static byte[] CreateArchive(Stage10AExportSnapshot value)
    {
        using MemoryStream output = new();
        using (ZipArchive archive = new(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string logicalName in LogicalNames)
            {
                byte[]? payload = value.Files[logicalName];
                if (payload == null) continue;
                string filename = logicalName == "settings" ? "settings.celeste" : logicalName + ".celeste";
                ZipArchiveEntry entry = archive.CreateEntry(filename, CompressionLevel.NoCompression);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using Stream stream = entry.Open();
                stream.Write(payload);
            }
        }
        return output.ToArray();
    }

    private static Stage10AHttpResponse Html(int status, string reason, string html, bool headOnly)
    {
        byte[] body = Encoding.UTF8.GetBytes(html);
        return new Stage10AHttpResponse(status, reason, BaseHeaders("text/html; charset=utf-8", body.Length), headOnly ? Array.Empty<byte>() : body);
    }

    private static Stage10AHttpResponse Error(int status, string reason, string message) =>
        Html(status, reason, Page(reason, $"<p>{Escape(message)}</p>"), headOnly: false);

    private static Stage10AHttpResponse MethodNotAllowed(string allowed)
    {
        Stage10AHttpResponse response = Error(405, "Method Not Allowed", "That method is not available in this read-only Save Manager.");
        Dictionary<string, string> headers = new(response.Headers, StringComparer.Ordinal) { ["Allow"] = allowed };
        return response with { Headers = headers };
    }

    private static Dictionary<string, string> BaseHeaders(string contentType, int contentLength) => new(StringComparer.Ordinal)
    {
        ["Content-Type"] = contentType,
        ["Content-Length"] = contentLength.ToString(CultureInfo.InvariantCulture),
        ["Cache-Control"] = "no-store, max-age=0",
        ["Pragma"] = "no-cache",
        ["X-Content-Type-Options"] = "nosniff",
        ["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'",
        ["Referrer-Policy"] = "no-referrer"
    };

    private static string AccessCodePage(bool invalid) => Page("Celeste Save Manager",
        "<p>Enter the six-digit access code shown on your Apple TV.</p>" +
        (invalid ? "<p class=error>That code was not accepted.</p>" : "") +
        "<form method=post action=/auth><label>Access code <input name=code inputmode=numeric autocomplete=one-time-code maxlength=7 required></label><button type=submit>Connect</button></form>");

    private static string AuthenticatedPage(Stage10AExportSnapshot value)
    {
        StringBuilder body = new("<p>Connected to your Apple TV.</p><p>This manager is read-only.</p>" +
            "<p><a href=/download/all download=Celeste-saves.zip>Download all files (.zip)</a></p><ul>");
        foreach (string logicalName in LogicalNames)
        {
            string label = logicalName == "settings" ? "Settings" : $"Save Slot {int.Parse(logicalName, CultureInfo.InvariantCulture) + 1}";
            byte[]? payload = value.Files[logicalName];
            body.Append("<li><strong>").Append(Escape(label)).Append("</strong><br>");
            if (payload == null) body.Append("No save present");
            else
            {
                string filename = logicalName == "settings" ? "settings.celeste" : logicalName + ".celeste";
                body.Append(payload.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(" bytes ")
                    .Append("<a href=/download/").Append(logicalName).Append(" download=").Append(filename).Append(">Download ")
                    .Append(filename).Append("</a>");
            }
            body.Append("</li>");
        }
        body.Append("</ul>");
        return Page("Celeste Save Manager", body.ToString());
    }

    private static string Page(string title, string content) => "<!doctype html><html lang=en><meta charset=utf-8>" +
        "<meta name=viewport content=\"width=device-width,initial-scale=1\"><title>" + Escape(title) + "</title>" +
        "<style>body{background:#111827;color:#f9fafb;font:17px system-ui,sans-serif;max-width:42rem;margin:4rem auto;padding:0 1.3rem}" +
        "h1{font-size:2rem}li{background:#1f2937;margin:.8rem 0;padding:1rem;border-radius:.7rem;list-style:none}ul{padding:0}" +
        "a,button{color:#111827;background:#84ff54;border:0;border-radius:.45rem;padding:.55rem .8rem;font-weight:700;text-decoration:none;display:inline-block;margin:.4rem}" +
        "input{font:inherit;padding:.55rem;width:9rem}.error{color:#fca5a5}</style><h1>" + Escape(title) + "</h1>" + content + "</html>";

    private static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&#39;", StringComparison.Ordinal);

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++) if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }

    private static HttpFailure TooLarge() => new(431, "Request Header Fields Too Large", "The HTTP request is too large.");
    private sealed record ParsedRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, byte[] Body);
    private sealed class HttpFailure(int statusCode, string reason, string publicMessage) : Exception(publicMessage)
    {
        internal int StatusCode { get; } = statusCode;
        internal string Reason { get; } = reason;
        internal string PublicMessage { get; } = publicMessage;
    }
}

internal sealed class Stage10AConnectionGate
{
    private readonly int maximum;
    private bool active;
    private int count;

    internal Stage10AConnectionGate(int maximumConnections)
    {
        if (maximumConnections <= 0) throw new ArgumentOutOfRangeException(nameof(maximumConnections));
        maximum = maximumConnections;
    }

    internal int Count => count;
    internal void Start() { active = true; count = 0; }
    internal bool TryEnter()
    {
        if (!active || count >= maximum) return false;
        count++;
        return true;
    }
    internal void Leave() { if (count > 0) count--; }
    internal void Stop() { active = false; count = 0; }
}

internal static class Stage10AConnectionPolicy
{
    internal static readonly TimeSpan ResponseCloseLifetime = TimeSpan.FromSeconds(5);
}
#endif
