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

internal sealed record Stage10BMutationCommand(
    string LogicalName,
    byte[]? Payload,
    ulong ExpectedGeneration,
    string ExpectedLogicalHash
);

internal sealed record Stage10BMutationResult(
    bool Success,
    bool Changed,
    bool Conflict,
    string FailureCategory,
    Stage10AExportSnapshot Snapshot,
    int EnvelopeBytes,
    int BridgeBytes,
    bool RestartRequired
)
{
    internal static Stage10BMutationResult Committed(Stage10AExportSnapshot snapshot, int envelopeBytes, int bridgeBytes) =>
        new(true, true, false, "none", snapshot, envelopeBytes, bridgeBytes, true);
    internal static Stage10BMutationResult Unchanged(Stage10AExportSnapshot snapshot, bool restartRequired) =>
        new(true, false, false, "none", snapshot, 0, 0, restartRequired);
    internal static Stage10BMutationResult ConflictResult(Stage10AExportSnapshot snapshot) =>
        new(false, false, true, "stale-revision", snapshot, 0, 0, false);
    internal static Stage10BMutationResult Failed(string category, Stage10AExportSnapshot snapshot, bool restartRequired) =>
        new(false, false, false, category, snapshot, 0, 0, restartRequired);
}

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

internal readonly record struct Stage10ARequestProgress(bool Complete, int ExpectedBytes, Stage10AHttpResponse? Rejection);

internal enum Stage15PairingState
{
    Available,
    Consumed,
    Expired,
    Stopped
}

internal sealed class Stage10AHttpProtocol
{
    internal const int MaximumRequestLineBytes = 2048;
    internal const int MaximumHeaderBytes = 16 * 1024;
    internal const int MaximumHeaderCount = 48;
    internal const int MaximumAuthBodyBytes = 64;
    internal const int MaximumPairingBodyBytes = 80;
    internal const int MaximumSettingsUploadBytes = 64 * 1024;
    internal const int MaximumSaveUploadBytes = 256 * 1024;
    internal const int MaximumRequestBodyBytes = MaximumSaveUploadBytes;
    internal const int MaximumConcurrentConnections = 4;
    internal const string SessionCookieName = "CelesteSaveSession";
    internal const string CsrfHeaderName = "x-celeste-csrf";
    internal const string RevisionHeaderName = "x-celeste-revision";
    internal static readonly TimeSpan RequestLifetime = TimeSpan.FromSeconds(10);
    internal static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan PairingLifetime = TimeSpan.FromMinutes(3);

    private static readonly string[] LogicalNames = { "settings", "0", "1", "2" };
    private readonly object gate = new();
    private readonly Func<DateTimeOffset> now;
    private readonly Func<Stage10BMutationCommand, Stage10BMutationResult>? mutator;
    private readonly Dictionary<string, SessionState> sessions = new(StringComparer.Ordinal);
    private readonly byte[] revisionSecret = RandomNumberGenerator.GetBytes(32);
    private byte[] pairingCredential = RandomNumberGenerator.GetBytes(32);
    private readonly DateTimeOffset pairingExpiry;
    private Stage10AExportSnapshot snapshot;
    private string revision;
    private string accessCode;
    private bool active = true;
    private bool restartRequired;
    private bool pairingConsumed;
    private bool pairingExpired;

    internal Stage10AHttpProtocol(
        Stage10AExportSnapshot stableSnapshot,
        Func<DateTimeOffset>? clock = null,
        Func<Stage10BMutationCommand, Stage10BMutationResult>? mutationHandler = null)
    {
        snapshot = ValidateSnapshot(stableSnapshot);
        now = clock ?? (() => DateTimeOffset.UtcNow);
        mutator = mutationHandler;
        accessCode = GenerateAccessCode();
        revision = ComputeRevision(snapshot);
        pairingExpiry = now().Add(PairingLifetime);
    }

    internal string AccessCode { get { lock (gate) return accessCode; } }
    internal int ActiveSessionCount { get { lock (gate) { PurgeExpired(); return sessions.Count; } } }
    internal bool RestartRequired { get { lock (gate) return restartRequired; } }
    internal string RevisionForDiagnostics { get { lock (gate) return revision; } }
    internal Stage15PairingState PairingState { get { lock (gate) return PairingStateUnsafe(); } }
    internal string PairingCredentialForQr
    {
        get
        {
            lock (gate)
                return PairingStateUnsafe() == Stage15PairingState.Available
                    ? Convert.ToHexString(pairingCredential).ToLowerInvariant()
                    : "";
        }
    }

    internal void ReplaceSnapshot(Stage10AExportSnapshot stableSnapshot)
    {
        lock (gate)
        {
            snapshot = ValidateSnapshot(stableSnapshot);
            revision = ComputeRevision(snapshot);
        }
    }

    internal void Stop()
    {
        lock (gate)
        {
            active = false;
            accessCode = "";
            revision = "";
            sessions.Clear();
            pairingConsumed = true;
            CryptographicOperations.ZeroMemory(pairingCredential);
            pairingCredential = Array.Empty<byte>();
            CryptographicOperations.ZeroMemory(revisionSecret);
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
            return Error(500, "Internal Server Error", "The Save Manager could not complete this request. Your previous save was kept.");
        }
    }

    internal static Stage10ARequestProgress InspectRequestProgress(byte[] bytes)
    {
        if (bytes.Length > MaximumHeaderBytes + MaximumRequestBodyBytes)
            return new(false, 0, Error(413, "Content Too Large", "The upload is larger than this port accepts."));
        int separator = IndexOf(bytes, "\r\n\r\n"u8);
        if (separator < 0)
        {
            if (bytes.Length >= MaximumHeaderBytes)
                return new(false, 0, Error(431, "Request Header Fields Too Large", "The HTTP headers are too large."));
            return new(false, 0, null);
        }

        int headerLength = separator + 4;
        if (headerLength > MaximumHeaderBytes)
            return new(false, 0, Error(431, "Request Header Fields Too Large", "The HTTP headers are too large."));
        try
        {
            Framing framing = ParseFraming(bytes.AsSpan(0, headerLength).ToArray());
            int bodyLimit = BodyLimit(framing.Method, framing.Path);
            if (framing.ContentLength > bodyLimit)
                return new(false, 0, Error(413, "Content Too Large", "The upload is larger than this port accepts."));
            int expected = checked(headerLength + framing.ContentLength);
            if (bytes.Length > expected)
                return new(false, expected, Error(400, "Bad Request", "Extra request bytes are not accepted."));
            return new(bytes.Length == expected, expected, null);
        }
        catch (HttpFailure failure)
        {
            return new(false, 0, Error(failure.StatusCode, failure.Reason, failure.PublicMessage));
        }
        catch
        {
            return new(false, 0, Error(400, "Bad Request", "The HTTP request framing is invalid."));
        }
    }

    internal static string GenerateAccessCode() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", CultureInfo.InvariantCulture);

    internal string BuildPairingUrl(string baseUrl)
    {
        lock (gate)
        {
            string credential = PairingStateUnsafe() == Stage15PairingState.Available
                ? Convert.ToHexString(pairingCredential).ToLowerInvariant()
                : "";
            if (credential.Length != 64 || !Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) ||
                uri.Scheme != Uri.UriSchemeHttp || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("A ready numeric HTTP listener URL is required for QR pairing.");
            return baseUrl.TrimEnd('/') + "/pair#" + credential;
        }
    }

    private Stage10AHttpResponse Route(ParsedRequest request)
    {
        if (request.Method == "POST" && request.Path == "/pair") return Pair(request);
        if (request.Method == "POST" && request.Path == "/auth") return Authenticate(request);

        SessionState? session = TryAuthenticate(request.Headers);
        if (request.Method == "POST") return Mutate(request, session);

        bool isHead = request.Method == "HEAD";
        if (request.Method is not ("GET" or "HEAD")) return MethodNotAllowed("GET, HEAD, POST");
        if (request.Path == "/pair") return PairingBootstrap(isHead);
        if (request.Path == "/")
            return session == null ? Html(200, "OK", AccessCodePage(invalid: false), isHead) :
                AuthenticatedResponse(200, "OK", session, session.Notice, isHead);
        if (session == null) return Html(401, "Unauthorized", AccessCodePage(invalid: false), isHead);

        if (request.Path == "/download/all")
        {
            byte[] archive = CreateArchive(snapshot);
            Dictionary<string, string> archiveHeaders = BaseHeaders("application/zip", archive.Length, null);
            archiveHeaders["Content-Disposition"] = "attachment; filename=\"Celeste-saves.zip\"";
            archiveHeaders["X-Celeste-Logical-Name"] = "all";
            archiveHeaders["X-Celeste-Content-SHA256"] = Convert.ToHexString(SHA256.HashData(archive)).ToLowerInvariant();
            return new Stage10AHttpResponse(200, "OK", archiveHeaders, isHead ? Array.Empty<byte>() : archive);
        }

        string? logicalName = DownloadTarget(request.Path);
        if (logicalName == null) return Error(404, "Not Found", "That Save Manager route does not exist.");
        byte[]? payload = snapshot.Files[logicalName];
        if (payload == null) return Error(404, "Not Found", "No save is present in that slot.");
        string filename = Filename(logicalName);
        Dictionary<string, string> headers = BaseHeaders("application/octet-stream", payload.Length, null);
        headers["Content-Disposition"] = $"attachment; filename=\"{filename}\"";
        headers["X-Celeste-Logical-Name"] = logicalName;
        headers["X-Celeste-Content-SHA256"] = Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant();
        return new Stage10AHttpResponse(200, "OK", headers, isHead ? Array.Empty<byte>() : payload.ToArray());
    }

    private Stage10AHttpResponse Authenticate(ParsedRequest request)
    {
        if (!request.ContentLengthPresent)
            throw new HttpFailure(411, "Length Required", "The access-code form requires Content-Length.");
        if (!request.Headers.TryGetValue("content-type", out string? contentType) ||
            !contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpFailure(415, "Unsupported Media Type", "The access-code form format is unsupported.");
        }
        string body = Encoding.ASCII.GetString(request.Body);
        if (!body.StartsWith("code=", StringComparison.Ordinal) || body.IndexOf('&') >= 0)
            throw new HttpFailure(400, "Bad Request", "The access-code form is malformed.");
        string supplied = DecodeFormValue(body[5..]).Replace(" ", "", StringComparison.Ordinal);
        bool valid = supplied.Length == 6 && accessCode.Length == 6 && FixedEquals(supplied, accessCode);
        if (!valid) return Html(401, "Unauthorized", AccessCodePage(invalid: true), headOnly: false);

        return StartSession("Connected successfully.", includeManagerPage: true, paired: false);
    }

    private Stage10AHttpResponse PairingBootstrap(bool headOnly)
    {
        string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        return Html(200, "OK", PairingPage(nonce), headOnly, nonce);
    }

    private Stage10AHttpResponse Pair(ParsedRequest request)
    {
        if (!request.ContentLengthPresent)
            return PairingRejected();
        if (!request.Headers.TryGetValue("content-type", out string? contentType) ||
            !contentType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
            return PairingRejected();
        string body = Encoding.ASCII.GetString(request.Body);
        if (!body.StartsWith("token=", StringComparison.Ordinal) || body.IndexOf('&') >= 0)
            return PairingRejected();
        string supplied;
        try { supplied = DecodeFormValue(body[6..]); }
        catch (HttpFailure) { return PairingRejected(); }
        if (!TryConsumePairingCredential(supplied)) return PairingRejected();
        return StartSession("Connected by QR.", includeManagerPage: false, paired: true);
    }

    private Stage10AHttpResponse StartSession(string notice, bool includeManagerPage, bool paired)
    {
        string token = RandomHex(32);
        SessionState session = new(now().Add(SessionLifetime), RandomHex(32)) { Notice = notice };
        sessions[token] = session;
        Stage10AHttpResponse response;
        if (includeManagerPage)
        {
            response = AuthenticatedResponse(200, "OK", session, notice, headOnly: false);
        }
        else
        {
            byte[] body = "paired"u8.ToArray();
            response = new Stage10AHttpResponse(200, "OK", BaseHeaders("text/plain; charset=utf-8", body.Length, null), body);
        }
        Dictionary<string, string> headers = new(response.Headers, StringComparer.Ordinal)
        {
            ["X-Celeste-Authentication"] = "accepted",
            ["Set-Cookie"] = $"{SessionCookieName}={token}; Path=/; Max-Age=600; HttpOnly; SameSite=Strict"
        };
        if (paired) headers["X-Celeste-Pairing"] = "accepted";
        return response with { Headers = headers };
    }

    private bool TryConsumePairingCredential(string supplied)
    {
        byte[] candidate = new byte[32];
        bool shapeValid = supplied.Length == 64;
        if (shapeValid)
        {
            try
            {
                byte[] parsed = Convert.FromHexString(supplied);
                shapeValid = parsed.Length == candidate.Length;
                if (shapeValid) parsed.CopyTo(candidate, 0);
                CryptographicOperations.ZeroMemory(parsed);
            }
            catch (FormatException) { shapeValid = false; }
        }
        byte[] expected = pairingCredential.Length == 32 ? pairingCredential : new byte[32];
        bool matches = CryptographicOperations.FixedTimeEquals(candidate, expected);
        CryptographicOperations.ZeroMemory(candidate);
        bool valid = shapeValid && matches && PairingStateUnsafe() == Stage15PairingState.Available;
        if (!valid) return false;
        pairingConsumed = true;
        CryptographicOperations.ZeroMemory(pairingCredential);
        pairingCredential = Array.Empty<byte>();
        return true;
    }

    private Stage15PairingState PairingStateUnsafe()
    {
        if (!active) return Stage15PairingState.Stopped;
        if (pairingExpired) return Stage15PairingState.Expired;
        if (!pairingConsumed && now() >= pairingExpiry)
        {
            pairingExpired = true;
            CryptographicOperations.ZeroMemory(pairingCredential);
            pairingCredential = Array.Empty<byte>();
            return Stage15PairingState.Expired;
        }
        return pairingConsumed || pairingCredential.Length == 0
            ? Stage15PairingState.Consumed
            : Stage15PairingState.Available;
    }

    private Stage10AHttpResponse Mutate(ParsedRequest request, SessionState? session)
    {
        MutationRoute? route = MutationTarget(request.Path);
        if (route == null)
        {
            if (request.Path.StartsWith("/replace/", StringComparison.Ordinal) ||
                request.Path.StartsWith("/delete/", StringComparison.Ordinal) ||
                request.Path.StartsWith("/reset/", StringComparison.Ordinal))
                return Error(404, "Not Found", "That Save Manager target does not exist.");
            return MethodNotAllowed("GET, HEAD");
        }
        if (session == null) return Error(401, "Unauthorized", "Authenticate with the temporary access code first.");
        if (!request.Headers.TryGetValue(CsrfHeaderName, out string? csrf) || !FixedEquals(csrf, session.Csrf))
            return Error(403, "Forbidden", "The Save Manager action token is invalid or expired.");
        if (!request.Headers.TryGetValue(RevisionHeaderName, out string? suppliedRevision) || !FixedEquals(suppliedRevision, revision))
            return Error(409, "Conflict", "The save changed since this page was opened. Refresh and try again.");
        if (!request.ContentLengthPresent)
            return Error(411, "Length Required", "This action requires an explicit Content-Length.");

        byte[]? payload = null;
        if (route.Replace)
        {
            if (!request.Headers.TryGetValue("content-type", out string? contentType) ||
                !contentType.Equals("application/octet-stream", StringComparison.OrdinalIgnoreCase))
                return Error(415, "Unsupported Media Type", "Choose a Celeste .celeste file for replacement.");
            if (request.Body.Length == 0)
                return Error(422, "Unprocessable Content", route.LogicalName == "settings"
                    ? "That Settings file could not be validated."
                    : "That file is not valid Celeste SaveData.");
            payload = request.Body.ToArray();
        }
        else if (request.Body.Length != 0)
        {
            return Error(400, "Bad Request", "Delete and reset actions do not accept a request body.");
        }

        if (mutator == null) return Error(503, "Service Unavailable", "Writable persistence is unavailable.");
        Stage10BMutationResult result = mutator(new Stage10BMutationCommand(
            route.LogicalName, payload, snapshot.Generation, snapshot.LogicalHash));
        if (result.Conflict)
        {
            snapshot = ValidateSnapshot(result.Snapshot);
            revision = ComputeRevision(snapshot);
            return Error(409, "Conflict", "The save changed since this page was opened. Refresh and try again.");
        }
        if (!result.Success)
            return MutationFailure(result.FailureCategory, route.LogicalName);

        snapshot = ValidateSnapshot(result.Snapshot);
        revision = ComputeRevision(snapshot);
        restartRequired |= result.RestartRequired;
        string target = Label(route.LogicalName);
        string notice = result.Changed
            ? $"{Escape(target)} was {(route.Replace ? "replaced" : route.LogicalName == "settings" ? "reset" : "deleted")} successfully."
            : $"{Escape(target)} was already in that state; no durable generation was changed.";
        session.Notice = notice;
        Stage10AHttpResponse response = AuthenticatedResponse(200, "OK", session, notice, headOnly: false);
        Dictionary<string, string> headers = new(response.Headers, StringComparer.Ordinal)
        {
            ["X-Celeste-Mutation"] = result.Changed ? "committed" : "unchanged",
            ["X-Celeste-Mutation-Target"] = route.LogicalName,
            ["X-Celeste-Reload-Required"] = restartRequired ? "true" : "false",
            ["X-Celeste-Restart-Required"] = restartRequired ? "true" : "false"
        };
        return response with { Headers = headers };
    }

    private static Stage10AHttpResponse MutationFailure(string category, string logicalName)
    {
        string message = category switch
        {
            "raw-uncompressed-limit" or "compressed-entry-limit" or "envelope-limit" or "bridge-total-limit" =>
                "The file is larger than this port accepts. Your previous save was kept.",
            "serializer-invalid" => logicalName == "settings"
                ? "That Settings file could not be validated. Your previous settings were kept."
                : "That file is not valid Celeste SaveData. Your previous save was kept.",
            "userdefaults-size-warning" => "tvOS rejected the durable size budget. Your previous save was kept.",
            _ => "The durable write could not be verified. Your previous save was kept."
        };
        return Error(422, "Unprocessable Content", message);
    }

    private SessionState? TryAuthenticate(IReadOnlyDictionary<string, string> headers)
    {
        if (!headers.TryGetValue("cookie", out string? cookie)) return null;
        foreach (string part in cookie.Split(';'))
        {
            string candidate = part.Trim();
            string prefix = SessionCookieName + "=";
            if (!candidate.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string token = candidate[prefix.Length..];
            if (token.Length != 64 || token.Any(value => !Uri.IsHexDigit(value))) return null;
            if (!sessions.TryGetValue(token, out SessionState? session) || session.Expiry <= now()) return null;
            session.Expiry = now().Add(SessionLifetime);
            return session;
        }
        return null;
    }

    private void PurgeExpired()
    {
        DateTimeOffset current = now();
        foreach (string key in sessions.Where(pair => pair.Value.Expiry <= current).Select(pair => pair.Key).ToArray()) sessions.Remove(key);
    }

    private string ComputeRevision(Stage10AExportSnapshot value)
    {
        byte[] material = Encoding.ASCII.GetBytes(value.Generation.ToString(CultureInfo.InvariantCulture) + ":" + value.LogicalHash);
        return Convert.ToHexString(HMACSHA256.HashData(revisionSecret, material)).ToLowerInvariant();
    }

    private static ParsedRequest Parse(byte[] bytes)
    {
        if (bytes.Length > MaximumHeaderBytes + MaximumRequestBodyBytes)
            throw new HttpFailure(413, "Content Too Large", "The upload is larger than this port accepts.");
        int separator = IndexOf(bytes, "\r\n\r\n"u8);
        if (separator < 0)
            throw new HttpFailure(400, "Bad Request", "The HTTP request is incomplete.");
        int headerLength = separator + 4;
        if (headerLength > MaximumHeaderBytes)
            throw new HttpFailure(431, "Request Header Fields Too Large", "The HTTP headers are too large.");
        Framing framing = ParseFraming(bytes.AsSpan(0, headerLength).ToArray());
        int bodyLimit = BodyLimit(framing.Method, framing.Path);
        if (framing.ContentLength > bodyLimit)
            throw new HttpFailure(413, "Content Too Large", "The upload is larger than this port accepts.");
        int expected = checked(headerLength + framing.ContentLength);
        if (bytes.Length < expected)
            throw new HttpFailure(400, "Bad Request", "The HTTP request is incomplete.");
        if (bytes.Length > expected)
            throw new HttpFailure(400, "Bad Request", "Extra request bytes are not accepted.");
        Dictionary<string, string> headers = ParseHeaders(bytes.AsSpan(0, separator).ToArray(), out _, out _, out _);
        if (framing.Method != "POST" && framing.ContentLength != 0)
            throw new HttpFailure(400, "Bad Request", "GET and HEAD requests must not contain a body.");
        return new ParsedRequest(framing.Method, framing.Path, headers,
            bytes.AsSpan(headerLength, framing.ContentLength).ToArray(), framing.ContentLengthPresent);
    }

    private static Framing ParseFraming(byte[] headerBytes)
    {
        int separator = IndexOf(headerBytes, "\r\n\r\n"u8);
        if (separator < 0) throw new HttpFailure(400, "Bad Request", "The HTTP headers are incomplete.");
        Dictionary<string, string> headers = ParseHeaders(headerBytes.AsSpan(0, separator).ToArray(),
            out string method, out string path, out _);
        if (headers.ContainsKey("transfer-encoding"))
            throw new HttpFailure(400, "Bad Request", "Transfer-Encoding is not accepted.");
        bool present = headers.TryGetValue("content-length", out string? text);
        int contentLength = 0;
        if (present && (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out contentLength) || contentLength < 0))
            throw new HttpFailure(400, "Bad Request", "Content-Length is invalid.");
        return new Framing(method, path, contentLength, present);
    }

    private static Dictionary<string, string> ParseHeaders(
        byte[] bytes,
        out string method,
        out string path,
        out string version)
    {
        if (bytes.Any(value => value == 0 || value > 0x7f))
            throw new HttpFailure(400, "Bad Request", "HTTP headers must be ASCII.");
        string[] lines = Encoding.ASCII.GetString(bytes).Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || Encoding.ASCII.GetByteCount(lines[0]) > MaximumRequestLineBytes) throw HeaderTooLarge();
        string[] requestLine = lines[0].Split(' ', StringSplitOptions.None);
        if (requestLine.Length != 3 || requestLine.Any(string.IsNullOrEmpty))
            throw new HttpFailure(400, "Bad Request", "The HTTP request line is malformed.");
        method = requestLine[0];
        path = requestLine[1];
        version = requestLine[2];
        if (method.Any(value => value is < 'A' or > 'Z'))
            throw new HttpFailure(400, "Bad Request", "The HTTP method is malformed.");
        if (version is not ("HTTP/1.0" or "HTTP/1.1"))
            throw new HttpFailure(505, "HTTP Version Not Supported", "Use HTTP/1.0 or HTTP/1.1.");
        ValidatePath(path);
        if (lines.Length - 1 > MaximumHeaderCount)
            throw new HttpFailure(431, "Request Header Fields Too Large", "Too many HTTP headers were supplied.");
        Dictionary<string, string> headers = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.Length == 0 || char.IsWhiteSpace(line[0]))
                throw new HttpFailure(400, "Bad Request", "Folded or empty HTTP headers are not accepted.");
            int colon = line.IndexOf(':');
            if (colon <= 0) throw new HttpFailure(400, "Bad Request", "An HTTP header is malformed.");
            string name = line[..colon].ToLowerInvariant();
            if (name.Any(value => !(char.IsAsciiLetterOrDigit(value) || value == '-')) ||
                !headers.TryAdd(name, line[(colon + 1)..].Trim()))
                throw new HttpFailure(400, "Bad Request", "Duplicate or malformed HTTP headers are not accepted.");
        }
        return headers;
    }

    private static int BodyLimit(string method, string path)
    {
        if (method != "POST") return 0;
        if (path == "/auth") return MaximumAuthBodyBytes;
        if (path == "/pair") return MaximumPairingBodyBytes;
        MutationRoute? mutation = MutationTarget(path);
        if (mutation == null || !mutation.Replace) return 0;
        return mutation.LogicalName == "settings" ? MaximumSettingsUploadBytes : MaximumSaveUploadBytes;
    }

    private static void ValidatePath(string path)
    {
        if (path.Length == 0 || path[0] != '/' || path.Contains('\\') || path.Contains('?') || path.Contains('#') ||
            path.Contains("//", StringComparison.Ordinal) || path.Contains("..", StringComparison.Ordinal) || path.Contains('%'))
            throw new HttpFailure(400, "Bad Request", "The request path is invalid.");
        if (path.Any(value => value < 0x21 || value > 0x7e))
            throw new HttpFailure(400, "Bad Request", "The request path is invalid.");
    }

    private static string DecodeFormValue(string value)
    {
        StringBuilder result = new();
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '+') { result.Append(' '); continue; }
            if (value[i] != '%') { result.Append(value[i]); continue; }
            if (i + 2 >= value.Length || !byte.TryParse(value.AsSpan(i + 1, 2), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture, out byte decoded))
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
            throw new InvalidOperationException("Save Manager snapshots must contain exactly settings and save slots 0, 1, and 2.");
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
                ZipArchiveEntry entry = archive.CreateEntry(Filename(logicalName), CompressionLevel.NoCompression);
                entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using Stream stream = entry.Open();
                stream.Write(payload);
            }
        }
        return output.ToArray();
    }

    private Stage10AHttpResponse AuthenticatedResponse(int status, string reason, SessionState session, string? notice, bool headOnly)
    {
        string nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        string html = AuthenticatedPage(snapshot, session.Csrf, revision, restartRequired, notice, nonce);
        return Html(status, reason, html, headOnly, nonce);
    }

    private static Stage10AHttpResponse Html(int status, string reason, string html, bool headOnly, string? nonce = null)
    {
        byte[] body = Encoding.UTF8.GetBytes(html);
        return new Stage10AHttpResponse(status, reason, BaseHeaders("text/html; charset=utf-8", body.Length, nonce),
            headOnly ? Array.Empty<byte>() : body);
    }

    private static Stage10AHttpResponse Error(int status, string reason, string message) =>
        Html(status, reason, Page(reason, $"<p>{Escape(message)}</p><p><a href=/>Return to Save Manager</a></p>"), headOnly: false);

    private static Stage10AHttpResponse MethodNotAllowed(string allowed)
    {
        Stage10AHttpResponse response = Error(405, "Method Not Allowed", "That method is not available in this Save Manager.");
        Dictionary<string, string> headers = new(response.Headers, StringComparer.Ordinal) { ["Allow"] = allowed };
        return response with { Headers = headers };
    }

    private static Dictionary<string, string> BaseHeaders(string contentType, int contentLength, string? nonce)
    {
        string script = nonce == null ? "" : $"; script-src 'nonce-{nonce}'";
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Content-Type"] = contentType,
            ["Content-Length"] = contentLength.ToString(CultureInfo.InvariantCulture),
            ["Cache-Control"] = "no-store, max-age=0",
            ["Pragma"] = "no-cache",
            ["X-Content-Type-Options"] = "nosniff",
            ["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; connect-src 'self'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'" + script,
            ["Referrer-Policy"] = "no-referrer"
        };
    }

    private static string AccessCodePage(bool invalid) => Page("Celeste Save Manager",
        "<p>Enter the six-digit access code shown on your Apple TV.</p>" +
        (invalid ? "<p class=error>That code was not accepted.</p>" : "") +
        "<form method=post action=/auth><label>Access code <input name=code inputmode=numeric autocomplete=one-time-code maxlength=7 required></label><button type=submit>Connect</button></form>");

    private static string PairingPage(string nonce) => Page("Celeste Save Manager",
        "<p id=status>Connecting to your Apple TV...</p>" +
        "<p><a href=/>Use the six-digit access code instead</a></p>" +
        "<script nonce=\"" + nonce + "\">'use strict';" +
        "const status=document.getElementById('status'),token=location.hash.slice(1);" +
        "history.replaceState(null,'','/pair');" +
        "const fail=()=>status.textContent='This QR code is no longer valid. Scan the current code on your Apple TV or use the access code instead.';" +
        "if(!/^[0-9a-f]{64}$/.test(token)){fail();}else{fetch('/pair',{method:'POST',credentials:'same-origin',headers:{'Content-Type':'application/x-www-form-urlencoded'},body:'token='+encodeURIComponent(token)}).then(r=>{if(r.ok){location.replace('/');}else{fail();}}).catch(fail);}" +
        "</script>");

    private static Stage10AHttpResponse PairingRejected() =>
        Html(401, "Unauthorized", Page("QR code unavailable",
            "<p>This QR code is no longer valid. Scan the current code on your Apple TV or use the access code instead.</p><p><a href=/>Use access code</a></p>"), headOnly: false);

    private static string AuthenticatedPage(
        Stage10AExportSnapshot value,
        string csrf,
        string revision,
        bool restartRequired,
        string? notice,
        string nonce)
    {
        StringBuilder body = new("<p>Connected to your Apple TV.</p>");
        if (!string.IsNullOrEmpty(notice)) body.Append("<p class=success>").Append(notice).Append("</p>");
        if (restartRequired)
            body.Append("<p class=warning><strong>Return to your Apple TV and press Confirm to reload Celeste.</strong><br>Celeste will validate and load the new state without quitting the app. If reload fails, fully close Celeste from the Apple TV app switcher and reopen it.</p>");
        body.Append("<p><a href=/download/all download=Celeste-saves.zip>Download backup (.zip)</a></p><ul>");
        foreach (string logicalName in LogicalNames)
        {
            string label = Label(logicalName);
            byte[]? payload = value.Files[logicalName];
            body.Append("<li><strong>").Append(Escape(label)).Append("</strong><br>");
            if (payload == null) body.Append("No save present<br>");
            else
            {
                body.Append(payload.Length.ToString("N0", CultureInfo.InvariantCulture)).Append(" bytes ")
                    .Append("<a href=/download/").Append(logicalName).Append(" download=").Append(Filename(logicalName)).Append(">Download</a><br>");
            }
            body.Append("<label class=replace>Choose replacement <input type=file accept=.celeste data-file=").Append(logicalName).Append("></label>")
                .Append("<button type=button data-replace=").Append(logicalName).Append(">Replace</button>");
            if (logicalName == "settings")
                body.Append("<button type=button data-reset=settings>Reset Settings</button>");
            else if (payload != null)
                body.Append("<button class=danger type=button data-delete=").Append(logicalName).Append(">Delete</button>");
            body.Append("<div class=result id=result-").Append(logicalName).Append("></div></li>");
        }
        body.Append("</ul><p>Files are validated before they replace anything. Invalid or interrupted uploads keep the previous durable save.</p>")
            .Append("<script nonce=\"").Append(nonce).Append("\">'use strict';")
            .Append("const csrf='").Append(csrf).Append("',revision='").Append(revision).Append("';")
            .Append("async function act(path,body,type,target){const r=await fetch(path,{method:'POST',credentials:'same-origin',headers:{'X-Celeste-CSRF':csrf,'X-Celeste-Revision':revision,...(type?{'Content-Type':type}:{})},body});const t=await r.text();if(r.ok){location.replace('/');}else{const e=document.getElementById('result-'+target);e.textContent='Request failed ('+r.status+'). '+new DOMParser().parseFromString(t,'text/html').body.innerText;}}")
            .Append("document.querySelectorAll('[data-replace]').forEach(b=>b.onclick=()=>{const n=b.dataset.replace,f=document.querySelector('[data-file=\"'+n+'\"]').files[0];if(!f){document.getElementById('result-'+n).textContent='Choose a .celeste file first.';return;}const label=n==='settings'?'Settings':'Save Slot '+(Number(n)+1);if(confirm('Replace '+label+' with '+f.name+' ('+f.size+' bytes)?'))act('/replace/'+n,f,'application/octet-stream',n);});")
            .Append("document.querySelectorAll('[data-delete]').forEach(b=>b.onclick=()=>{const n=b.dataset.delete;if(confirm('Delete Save Slot '+(Number(n)+1)+'? This intentionally changes the current save state.'))act('/delete/'+n,null,null,n);});")
            .Append("document.querySelectorAll('[data-reset]').forEach(b=>b.onclick=()=>{if(confirm('Reset Settings to Celeste defaults? Save slots are not changed. Return to the Apple TV and press Confirm to reload.'))act('/reset/settings',null,null,'settings');});")
            .Append("</script>");
        return Page("Celeste Save Manager", body.ToString());
    }

    private static string Page(string title, string content) => "<!doctype html><html lang=en><meta charset=utf-8>" +
        "<meta name=viewport content=\"width=device-width,initial-scale=1\"><title>" + Escape(title) + "</title>" +
        "<style>body{background:#111827;color:#f9fafb;font:17px system-ui,sans-serif;max-width:44rem;margin:3rem auto;padding:0 1.3rem}" +
        "h1{font-size:2rem}li{background:#1f2937;margin:.8rem 0;padding:1rem;border-radius:.7rem;list-style:none}ul{padding:0}" +
        "a,button{color:#111827;background:#84ff54;border:0;border-radius:.45rem;padding:.55rem .8rem;font-weight:700;text-decoration:none;display:inline-block;margin:.4rem}" +
        "button.danger{background:#fca5a5}.replace{display:block;margin:.6rem 0}input{font:inherit;padding:.55rem}.error{color:#fca5a5}" +
        ".success{background:#14532d;padding:1rem;border-radius:.6rem}.warning{background:#713f12;padding:1rem;border-radius:.6rem}.result{margin:.4rem;color:#fca5a5}</style>" +
        "<h1>" + Escape(title) + "</h1>" + content + "</html>";

    private static MutationRoute? MutationTarget(string path) => path switch
    {
        "/replace/settings" => new("settings", true),
        "/replace/0" => new("0", true),
        "/replace/1" => new("1", true),
        "/replace/2" => new("2", true),
        "/reset/settings" => new("settings", false),
        "/delete/0" => new("0", false),
        "/delete/1" => new("1", false),
        "/delete/2" => new("2", false),
        _ => null
    };

    private static string? DownloadTarget(string path) => path switch
    {
        "/download/settings" => "settings", "/download/0" => "0", "/download/1" => "1", "/download/2" => "2", _ => null
    };
    private static string Filename(string logicalName) => logicalName == "settings" ? "settings.celeste" : logicalName + ".celeste";
    private static string Label(string logicalName) => logicalName == "settings" ? "Settings" : $"Save Slot {int.Parse(logicalName, CultureInfo.InvariantCulture) + 1}";
    private static string RandomHex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();
    private static bool FixedEquals(string first, string second)
    {
        byte[] a = Encoding.ASCII.GetBytes(first);
        byte[] b = Encoding.ASCII.GetBytes(second);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
    private static string Escape(string value) => value.Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal).Replace("'", "&#39;", StringComparison.Ordinal);
    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
        return -1;
    }
    private static HttpFailure HeaderTooLarge() => new(431, "Request Header Fields Too Large", "The HTTP request headers are too large.");

    private sealed record ParsedRequest(
        string Method,
        string Path,
        IReadOnlyDictionary<string, string> Headers,
        byte[] Body,
        bool ContentLengthPresent);
    private sealed record Framing(string Method, string Path, int ContentLength, bool ContentLengthPresent);
    private sealed record MutationRoute(string LogicalName, bool Replace);
    private sealed class SessionState(DateTimeOffset expiry, string csrf)
    {
        internal DateTimeOffset Expiry { get; set; } = expiry;
        internal string Csrf { get; } = csrf;
        internal string? Notice { get; set; }
    }
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
