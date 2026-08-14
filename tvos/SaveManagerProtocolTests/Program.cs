using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using CelesteTvOSHost;

int passed = 0;
DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
SaveManagerHttpProtocol protocol = NewProtocol(() => now);

Test("access-code-csprng-shape", () =>
{
    string first = SaveManagerHttpProtocol.GenerateAccessCode();
    string second = SaveManagerHttpProtocol.GenerateAccessCode();
    return first.Length == 6 && first.All(char.IsAsciiDigit) && second.Length == 6 && first != second;
});
Test("get-root-code-page", () => protocol.Handle(Request("GET", "/")).StatusCode == 200);
Test("head-root", () => protocol.Handle(Request("HEAD", "/")).Body.Length == 0);
Test("invalid-code-rejected", () => protocol.Handle(AuthRequest(protocol.AccessCode == "000000" ? "000001" : "000000")).StatusCode == 401);

string session = Authenticate(protocol);
Test("correct-code-accepted", () => session.Length == 64);
Test("auth-page-returned-without-redirect", () =>
{
    SaveManagerHttpProtocol local = NewProtocol();
    SaveManagerHttpResponse response = local.Handle(AuthRequest(local.AccessCode));
    return response.StatusCode == 200 && response.Headers["X-Celeste-Authentication"] == "accepted" &&
        !response.Headers.ContainsKey("Location") && Encoding.UTF8.GetString(response.Body).Contains("Save Slot 1", StringComparison.Ordinal);
});
Test("authenticated-root", () => protocol.Handle(Request("GET", "/", session)).StatusCode == 200);
Test("browser-mutation-csp-allows-same-origin-fetch", () =>
    protocol.Handle(Request("GET", "/", session)).Headers["Content-Security-Policy"]
        .Contains("connect-src 'self'", StringComparison.Ordinal));
Test("browser-mutation-refresh-uses-get-root", () =>
{
    string html = Encoding.UTF8.GetString(protocol.Handle(Request("GET", "/", session)).Body);
    return html.Contains("location.replace('/')", StringComparison.Ordinal) &&
        !html.Contains("location.reload()", StringComparison.Ordinal);
});
Test("download-all-link", () => Encoding.UTF8.GetString(protocol.Handle(Request("GET", "/", session)).Body)
    .Contains("href=/download/all download=Celeste-saves.zip", StringComparison.Ordinal));
Test("settings-download", () => Payload(protocol.Handle(Request("GET", "/download/settings", session)), "settings"u8));
Test("slot-download", () => Payload(protocol.Handle(Request("GET", "/download/0", session)), "slot-zero"u8));
Test("download-hash", () => protocol.Handle(Request("GET", "/download/0", session)).Headers["X-Celeste-Content-SHA256"] ==
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("slot-zero"u8)).ToLowerInvariant());
Test("download-logical-name", () => protocol.Handle(Request("GET", "/download/0", session)).Headers["X-Celeste-Logical-Name"] == "0");
Test("head-download", () =>
{
    SaveManagerHttpResponse response = protocol.Handle(Request("HEAD", "/download/0", session));
    return response.StatusCode == 200 && response.Body.Length == 0 && response.Headers["Content-Length"] == "9";
});
Test("absent-slot", () => protocol.Handle(Request("GET", "/download/1", session)).StatusCode == 404);
Test("download-all-archive", () =>
{
    SaveManagerHttpResponse response = protocol.Handle(Request("GET", "/download/all", session));
    using MemoryStream input = new(response.Body);
    using System.IO.Compression.ZipArchive archive = new(input, System.IO.Compression.ZipArchiveMode.Read);
    string[] names = archive.Entries.Select(entry => entry.FullName).ToArray();
    if (!names.SequenceEqual(new[] { "settings.celeste", "0.celeste", "2.celeste" })) return false;
    using Stream stream = archive.GetEntry("0.celeste")!.Open();
    using MemoryStream payload = new(); stream.CopyTo(payload);
    return payload.ToArray().AsSpan().SequenceEqual("slot-zero"u8);
});
Test("unknown-route", () => protocol.Handle(Request("GET", "/anything", session)).StatusCode == 404);
Test("delete-rejected", () => protocol.Handle(Request("DELETE", "/download/0", session)).StatusCode == 405);
Test("post-outside-auth-rejected", () => protocol.Handle(Raw("POST /download/0 HTTP/1.1\r\nContent-Length: 0\r\n\r\n")).StatusCode == 405);
Test("plain-traversal-rejected", () => protocol.Handle(Request("GET", "/download/../0", session)).StatusCode == 400);
Test("encoded-traversal-rejected", () => protocol.Handle(Request("GET", "/download/%2e%2e/0", session)).StatusCode == 400);
Test("malformed-request-line", () => protocol.Handle(Raw("GET /\r\n\r\n")).StatusCode == 400);
Test("oversized-request-line", () => protocol.Handle(Raw("GET /" + new string('a', 2100) + " HTTP/1.1\r\n\r\n")).StatusCode == 431);
Test("oversized-headers", () => protocol.Handle(Raw("GET / HTTP/1.1\r\nX-A: " + new string('a', 17000) + "\r\n\r\n")).StatusCode == 431);
Test("excessive-header-count", () =>
{
    string headers = string.Concat(Enumerable.Range(0, 49).Select(index => $"X-{index}: a\r\n"));
    return protocol.Handle(Raw("GET / HTTP/1.1\r\n" + headers + "\r\n")).StatusCode == 431;
});
Test("duplicate-content-length-rejected", () => protocol.Handle(Raw("POST /auth HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: 11\r\nContent-Length: 11\r\n\r\ncode=000000")).StatusCode == 400);
Test("transfer-encoding-rejected", () => protocol.Handle(Raw("POST /auth HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n0\r\n\r\n")).StatusCode == 400);
Test("session-expiration", () =>
{
    now = now.Add(SaveManagerHttpProtocol.SessionLifetime).AddSeconds(1);
    return protocol.Handle(Request("GET", "/download/0", session)).StatusCode == 401;
});
Test("server-stop-invalidates-session", () =>
{
    SaveManagerHttpProtocol local = NewProtocol();
    string token = Authenticate(local);
    local.Stop();
    return local.Handle(Request("GET", "/", token)).StatusCode == 503 && local.AccessCode.Length == 0 && local.ActiveSessionCount == 0;
});
Test("snapshot-rejects-extra-name", () =>
{
    try
    {
        _ = new SaveManagerHttpProtocol(new SaveExportSnapshot(1, "hash", new Dictionary<string, byte[]?>
        {
            ["settings"] = null, ["0"] = null, ["1"] = null, ["2"] = null, ["A"] = new byte[] { 1 }
        }));
        return false;
    }
    catch (InvalidOperationException) { return true; }
});
Test("snapshot-does-not-expose-v2", () => !Encoding.UTF8.GetString(protocol.Handle(Request("GET", "/", Authenticate(protocol))).Body)
    .Contains("CelesteTvOS.Persistence", StringComparison.Ordinal));
Test("server-start-and-connection-bound", () =>
{
    SaveManagerConnectionGate gate = new(SaveManagerHttpProtocol.MaximumConcurrentConnections);
    if (gate.TryEnter()) return false;
    gate.Start();
    for (int i = 0; i < SaveManagerHttpProtocol.MaximumConcurrentConnections; i++) if (!gate.TryEnter()) return false;
    if (gate.TryEnter()) return false;
    gate.Leave();
    if (!gate.TryEnter()) return false;
    gate.Leave();
    // The bound is concurrent, never a four-connection lifetime budget.
    // Reusing a slot well beyond four requests guards the physical regression.
    for (int i = 0; i < 32; i++)
    {
        if (!gate.TryEnter()) return false;
        gate.Leave();
    }
    gate.Stop();
    return gate.Count == 0 && !gate.TryEnter();
});
Test("connection-timeout-locked", () => SaveManagerHttpProtocol.RequestLifetime == TimeSpan.FromSeconds(10));
Test("response-close-fallback-locked", () => SaveManagerConnectionPolicy.ResponseCloseLifetime == TimeSpan.FromSeconds(5));
Test("lan-interface-policy", () =>
    SaveManagerLanAddressPolicy.InterfaceRank("en0") < SaveManagerLanAddressPolicy.InterfaceRank("en7") &&
    SaveManagerLanAddressPolicy.IsExcludedInterface("lo0") && SaveManagerLanAddressPolicy.IsExcludedInterface("utun3") &&
    !SaveManagerLanAddressPolicy.IsExcludedInterface("en0"));
Test("lan-address-policy", () =>
    SaveManagerLanAddressPolicy.IsUsableIpv4(IPAddress.Parse("192.168.1.42")) &&
    SaveManagerLanAddressPolicy.IsUsableIpv4(IPAddress.Parse("10.0.0.2")) &&
    !SaveManagerLanAddressPolicy.IsUsableIpv4(IPAddress.Loopback) &&
    !SaveManagerLanAddressPolicy.IsUsableIpv4(IPAddress.Parse("169.254.1.1")) &&
    SaveManagerLanAddressPolicy.FormatUrl("192.168.1.42", 42817) == "http://192.168.1.42:42817/" &&
    SaveManagerLanAddressPolicy.FormatUrl("fd00::42", 42817) == "http://[fd00::42]:42817/");
Test("darwin-sockaddr-parsing", () =>
{
    IntPtr address = Marshal.AllocHGlobal(28);
    try
    {
        for (int i = 0; i < 28; i++) Marshal.WriteByte(address, i, 0);
        Marshal.WriteByte(address, 0, 16);
        Marshal.WriteByte(address, 1, 2);
        Marshal.Copy(new byte[] { 192, 168, 4, 9 }, 0, IntPtr.Add(address, 4), 4);
        return SaveManagerLanAddressPolicy.ReadDarwinSockAddr(address)?.Equals(IPAddress.Parse("192.168.4.9")) == true;
    }
    finally { Marshal.FreeHGlobal(address); }
});

Test("unauthenticated-replace-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    return writable.Handle(MutationRequest("/replace/0", null, null, null, "save-valid"u8.ToArray())).StatusCode == 401;
});
Test("unauthenticated-delete-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    return writable.Handle(MutationRequest("/delete/0", null, null, null, null)).StatusCode == 401;
});
Test("invalid-csrf-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/delete/0", auth.Session, new string('0', 64), auth.Revision, null)).StatusCode == 403;
});
Test("mutation-expired-session-rejected", () =>
{
    DateTimeOffset clock = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _, () => clock);
    AuthInfo auth = AuthenticateWithTokens(writable);
    clock = clock.Add(SaveManagerHttpProtocol.SessionLifetime).AddSeconds(1);
    return writable.Handle(MutationRequest("/delete/0", auth.Session, auth.Csrf, auth.Revision, null)).StatusCode == 401;
});
Test("wrong-mutation-target-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/replace/9", auth.Session, auth.Csrf, auth.Revision, "save-valid"u8.ToArray())).StatusCode is >= 400 and < 500;
});
Test("oversized-upload-rejected-from-headers", () =>
{
    byte[] headers = Raw($"POST /replace/0 HTTP/1.1\r\nContent-Type: application/octet-stream\r\nContent-Length: {SaveManagerHttpProtocol.MaximumSaveUploadBytes + 1}\r\n\r\n");
    SaveManagerRequestProgress progress = SaveManagerHttpProtocol.InspectRequestProgress(headers);
    return progress.Rejection?.StatusCode == 413 && progress.ExpectedBytes == 0;
});
Test("oversized-settings-rejected-from-headers", () =>
{
    byte[] headers = Raw($"POST /replace/settings HTTP/1.1\r\nContent-Type: application/octet-stream\r\nContent-Length: {SaveManagerHttpProtocol.MaximumSettingsUploadBytes + 1}\r\n\r\n");
    return SaveManagerHttpProtocol.InspectRequestProgress(headers).Rejection?.StatusCode == 413;
});
Test("missing-upload-length-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(Raw($"POST /replace/0 HTTP/1.1\r\nCookie: {SaveManagerHttpProtocol.SessionCookieName}={auth.Session}\r\nX-Celeste-CSRF: {auth.Csrf}\r\nX-Celeste-Revision: {auth.Revision}\r\nContent-Type: application/octet-stream\r\n\r\n")).StatusCode == 411;
});
Test("unsupported-upload-content-type-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    byte[] request = MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "save-valid"u8.ToArray(), "text/xml");
    return writable.Handle(request).StatusCode == 415;
});
Test("empty-save-upload-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, Array.Empty<byte>())).StatusCode == 422;
});
Test("malformed-save-upload-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "invalid"u8.ToArray())).StatusCode == 422;
});
Test("settings-on-save-route-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "settings-valid"u8.ToArray())).StatusCode == 422;
});
Test("save-on-settings-route-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/replace/settings", auth.Session, auth.Csrf, auth.Revision, "save-valid"u8.ToArray())).StatusCode == 422;
});
Test("exact-save-replacement-roundtrip", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = AuthenticateWithTokens(writable);
    byte[] exact = "save-valid\r\nexact-bytes"u8.ToArray();
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, exact));
    SaveManagerHttpResponse download = writable.Handle(Request("GET", "/download/0", auth.Session));
    return response.StatusCode == 200 && response.Headers["X-Celeste-Mutation"] == "committed" &&
        authority.Generation == 8 && authority.CommitCount == 1 && download.Body.SequenceEqual(exact);
});
Test("exact-settings-replacement-roundtrip", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = AuthenticateWithTokens(writable);
    byte[] exact = "settings-valid\nexact"u8.ToArray();
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/replace/settings", auth.Session, auth.Csrf, auth.Revision, exact));
    return response.StatusCode == 200 && authority.Snapshot.Files["settings"]!.SequenceEqual(exact);
});
Test("failed-replacement-preserves-previous", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    byte[] before = authority.Snapshot.Files["0"]!.ToArray();
    AuthInfo auth = AuthenticateWithTokens(writable);
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "invalid"u8.ToArray()));
    return response.StatusCode == 422 && authority.Generation == 7 && authority.CommitCount == 0 && authority.Snapshot.Files["0"]!.SequenceEqual(before) && !writable.RestartRequired;
});
Test("delete-populated-slot", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = AuthenticateWithTokens(writable);
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/delete/0", auth.Session, auth.Csrf, auth.Revision, null));
    return response.StatusCode == 200 && authority.Snapshot.Files["0"] == null && authority.Generation == 8 &&
        writable.Handle(Request("GET", "/download/0", auth.Session)).StatusCode == 404;
});
Test("delete-absent-slot-is-noop", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = AuthenticateWithTokens(writable);
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/delete/1", auth.Session, auth.Csrf, auth.Revision, null));
    return response.StatusCode == 200 && response.Headers["X-Celeste-Mutation"] == "unchanged" &&
        authority.Generation == 7 && authority.CommitCount == 0 && !writable.RestartRequired;
});
Test("settings-reset-semantics", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = AuthenticateWithTokens(writable);
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/reset/settings", auth.Session, auth.Csrf, auth.Revision, null));
    return response.StatusCode == 200 && authority.Snapshot.Files["settings"] == null && writable.RestartRequired;
});
Test("stale-revision-conflict", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    return writable.Handle(MutationRequest("/delete/0", auth.Session, auth.Csrf, new string('a', 64), null)).StatusCode == 409;
});
Test("successful-mutation-refreshes-revision", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    SaveManagerHttpResponse response = writable.Handle(MutationRequest("/delete/0", auth.Session, auth.Csrf, auth.Revision, null));
    string nextRevision = TokenFromHtml(response, "revision='");
    return response.StatusCode == 200 && nextRevision.Length == 64 && nextRevision != auth.Revision;
});
Test("two-stale-concurrent-mutations-conflict", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = AuthenticateWithTokens(writable);
    byte[] first = MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "save-valid-first"u8.ToArray());
    byte[] second = MutationRequest("/replace/2", auth.Session, auth.Csrf, auth.Revision, "save-valid-second"u8.ToArray());
    Task<SaveManagerHttpResponse>[] requests = { Task.Run(() => writable.Handle(first)), Task.Run(() => writable.Handle(second)) };
    Task.WaitAll(requests);
    return requests.Count(task => task.Result.StatusCode == 200) == 1 &&
        requests.Count(task => task.Result.StatusCode == 409) == 1 && authority.CommitCount == 1;
});
Test("zip-download-reflects-replacement", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    byte[] exact = "save-valid-zip-new"u8.ToArray();
    SaveManagerHttpResponse changed = writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, exact));
    AuthInfo refreshed = auth with { Revision = TokenFromHtml(changed, "revision='") };
    SaveManagerHttpResponse archiveResponse = writable.Handle(Request("GET", "/download/all", refreshed.Session));
    using MemoryStream input = new(archiveResponse.Body);
    using System.IO.Compression.ZipArchive archive = new(input, System.IO.Compression.ZipArchiveMode.Read);
    using Stream stream = archive.GetEntry("0.celeste")!.Open();
    using MemoryStream extracted = new(); stream.CopyTo(extracted);
    return extracted.ToArray().SequenceEqual(exact);
});
Test("reload-required-only-after-success", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    AuthInfo auth = AuthenticateWithTokens(writable);
    _ = writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "invalid"u8.ToArray()));
    if (writable.RestartRequired) return false;
    SaveManagerHttpResponse changed = writable.Handle(MutationRequest("/replace/0", auth.Session, auth.Csrf, auth.Revision, "save-valid"u8.ToArray()));
    string html = Encoding.UTF8.GetString(changed.Body);
    return writable.RestartRequired &&
        html.Contains("press Confirm to reload Celeste", StringComparison.Ordinal) &&
        html.Contains("app switcher", StringComparison.Ordinal);
});
Test("premature-body-eof-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    return writable.Handle(Raw("POST /replace/0 HTTP/1.1\r\nContent-Length: 10\r\nContent-Type: application/octet-stream\r\n\r\nshort")).StatusCode == 400;
});
Test("extra-body-bytes-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    return writable.Handle(Raw("POST /replace/0 HTTP/1.1\r\nContent-Length: 1\r\nContent-Type: application/octet-stream\r\n\r\nXX")).StatusCode == 400;
});
Test("request-pipelining-rejected", () =>
{
    SaveManagerHttpProtocol writable = NewWritableProtocol(out _);
    return writable.Handle(Raw("GET / HTTP/1.1\r\n\r\nGET / HTTP/1.1\r\n\r\n")).StatusCode == 400;
});
Test("mutation-connection-gate-releases", () =>
{
    SaveManagerConnectionGate gate = new(SaveManagerHttpProtocol.MaximumConcurrentConnections);
    gate.Start();
    for (int request = 0; request < 64; request++) { if (!gate.TryEnter()) return false; gate.Leave(); }
    return gate.Count == 0;
});

Console.WriteLine($"STAGE10A_PROTOCOL_TESTS PASS count={passed}");
return;

void Test(string name, Func<bool> action)
{
    if (!action()) throw new InvalidOperationException("FAIL " + name);
    passed++;
    Console.WriteLine("PASS " + name);
}

static SaveManagerHttpProtocol NewProtocol(Func<DateTimeOffset>? clock = null) => new(new SaveExportSnapshot(7, "logical", new Dictionary<string, byte[]?>
{
    ["settings"] = "settings"u8.ToArray(),
    ["0"] = "slot-zero"u8.ToArray(),
    ["1"] = null,
    ["2"] = "slot-two"u8.ToArray()
}), clock);

static byte[] Request(string method, string path, string? session = null)
{
    string cookie = session == null ? "" : $"Cookie: {SaveManagerHttpProtocol.SessionCookieName}={session}\r\n";
    return Raw($"{method} {path} HTTP/1.1\r\nHost: apple-tv\r\n{cookie}\r\n");
}

static byte[] AuthRequest(string code)
{
    string body = "code=" + code;
    return Raw($"POST /auth HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n\r\n{body}");
}

static string Authenticate(SaveManagerHttpProtocol target)
{
    SaveManagerHttpResponse response = target.Handle(AuthRequest(target.AccessCode));
    if (response.StatusCode != 200) throw new InvalidOperationException("Authentication setup failed.");
    string cookie = response.Headers["Set-Cookie"];
    return cookie.Split(';')[0].Split('=')[1];
}

static AuthInfo AuthenticateWithTokens(SaveManagerHttpProtocol target)
{
    SaveManagerHttpResponse response = target.Handle(AuthRequest(target.AccessCode));
    if (response.StatusCode != 200) throw new InvalidOperationException("Authentication setup failed.");
    string session = response.Headers["Set-Cookie"].Split(';')[0].Split('=')[1];
    return new AuthInfo(session, TokenFromHtml(response, "const csrf='"), TokenFromHtml(response, "revision='"));
}

static string TokenFromHtml(SaveManagerHttpResponse response, string prefix)
{
    string html = Encoding.UTF8.GetString(response.Body);
    int start = html.IndexOf(prefix, StringComparison.Ordinal);
    if (start < 0) return "";
    start += prefix.Length;
    int end = html.IndexOf('\'', start);
    return end < 0 ? "" : html[start..end];
}

static byte[] MutationRequest(
    string path,
    string? session,
    string? csrf,
    string? revision,
    byte[]? body,
    string contentType = "application/octet-stream")
{
    byte[] payload = body ?? Array.Empty<byte>();
    StringBuilder header = new($"POST {path} HTTP/1.1\r\nHost: apple-tv\r\n");
    if (session != null) header.Append($"Cookie: {SaveManagerHttpProtocol.SessionCookieName}={session}\r\n");
    if (csrf != null) header.Append($"X-Celeste-CSRF: {csrf}\r\n");
    if (revision != null) header.Append($"X-Celeste-Revision: {revision}\r\n");
    if (body != null) header.Append($"Content-Type: {contentType}\r\n");
    header.Append($"Content-Length: {payload.Length}\r\n\r\n");
    byte[] prefix = Encoding.ASCII.GetBytes(header.ToString());
    byte[] result = new byte[prefix.Length + payload.Length];
    Buffer.BlockCopy(prefix, 0, result, 0, prefix.Length);
    Buffer.BlockCopy(payload, 0, result, prefix.Length, payload.Length);
    return result;
}

static SaveManagerHttpProtocol NewWritableProtocol(out FakeMutationAuthority authority, Func<DateTimeOffset>? clock = null)
{
    authority = new FakeMutationAuthority(new SaveExportSnapshot(7, "logical", new Dictionary<string, byte[]?>
    {
        ["settings"] = "settings-valid-original"u8.ToArray(),
        ["0"] = "save-valid-original-zero"u8.ToArray(),
        ["1"] = null,
        ["2"] = "save-valid-original-two"u8.ToArray()
    }));
    FakeMutationAuthority captured = authority;
    return new SaveManagerHttpProtocol(authority.Snapshot, clock, captured.Mutate);
}

static byte[] Raw(string value) => Encoding.ASCII.GetBytes(value);
static bool Payload(SaveManagerHttpResponse response, ReadOnlySpan<byte> expected) => response.StatusCode == 200 && response.Body.AsSpan().SequenceEqual(expected);

internal sealed record AuthInfo(string Session, string Csrf, string Revision);

internal sealed class FakeMutationAuthority
{
    private readonly object gate = new();
    internal SaveExportSnapshot Snapshot { get; private set; }
    internal ulong Generation => Snapshot.Generation;
    internal int CommitCount { get; private set; }

    internal FakeMutationAuthority(SaveExportSnapshot initial) => Snapshot = initial;

    internal SaveMutationResult Mutate(SaveMutationCommand command)
    {
        lock (gate)
        {
            if (command.ExpectedGeneration != Snapshot.Generation || command.ExpectedLogicalHash != Snapshot.LogicalHash)
                return SaveMutationResult.ConflictResult(Snapshot);
            if (command.Payload != null)
            {
                string text = Encoding.UTF8.GetString(command.Payload);
                bool valid = command.LogicalName == "settings"
                    ? text.StartsWith("settings-valid", StringComparison.Ordinal)
                    : text.StartsWith("save-valid", StringComparison.Ordinal);
                if (!valid) return SaveMutationResult.Failed("serializer-invalid", Snapshot, restartRequired: CommitCount > 0);
            }
            Dictionary<string, byte[]?> files = Snapshot.Files.ToDictionary(
                pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
            byte[]? prior = files[command.LogicalName];
            bool changed = command.Payload == null ? prior != null : prior == null || !prior.SequenceEqual(command.Payload);
            if (!changed) return SaveMutationResult.Unchanged(Snapshot, CommitCount > 0);
            files[command.LogicalName] = command.Payload?.ToArray();
            CommitCount++;
            string logical = "logical-" + (Snapshot.Generation + 1).ToString();
            Snapshot = new SaveExportSnapshot(Snapshot.Generation + 1, logical, files);
            return SaveMutationResult.Committed(Snapshot, 1000, 2000);
        }
    }
}
