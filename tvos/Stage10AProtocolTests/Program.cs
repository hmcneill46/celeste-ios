using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using CelesteTvOSHost;

int passed = 0;
DateTimeOffset now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
Stage10AHttpProtocol protocol = NewProtocol(() => now);

Test("access-code-csprng-shape", () =>
{
    string first = Stage10AHttpProtocol.GenerateAccessCode();
    string second = Stage10AHttpProtocol.GenerateAccessCode();
    return first.Length == 6 && first.All(char.IsAsciiDigit) && second.Length == 6 && first != second;
});
Test("get-root-code-page", () => protocol.Handle(Request("GET", "/")).StatusCode == 200);
Test("head-root", () => protocol.Handle(Request("HEAD", "/")).Body.Length == 0);
Test("invalid-code-rejected", () => protocol.Handle(AuthRequest(protocol.AccessCode == "000000" ? "000001" : "000000")).StatusCode == 401);

string session = Authenticate(protocol);
Test("correct-code-accepted", () => session.Length == 64);
Test("auth-page-returned-without-redirect", () =>
{
    Stage10AHttpProtocol local = NewProtocol();
    Stage10AHttpResponse response = local.Handle(AuthRequest(local.AccessCode));
    return response.StatusCode == 200 && response.Headers["X-Celeste-Authentication"] == "accepted" &&
        !response.Headers.ContainsKey("Location") && Encoding.UTF8.GetString(response.Body).Contains("Save Slot 1", StringComparison.Ordinal);
});
Test("authenticated-root", () => protocol.Handle(Request("GET", "/", session)).StatusCode == 200);
Test("download-all-link", () => Encoding.UTF8.GetString(protocol.Handle(Request("GET", "/", session)).Body)
    .Contains("href=/download/all download=Celeste-saves.zip", StringComparison.Ordinal));
Test("settings-download", () => Payload(protocol.Handle(Request("GET", "/download/settings", session)), "settings"u8));
Test("slot-download", () => Payload(protocol.Handle(Request("GET", "/download/0", session)), "slot-zero"u8));
Test("download-hash", () => protocol.Handle(Request("GET", "/download/0", session)).Headers["X-Celeste-Content-SHA256"] ==
    Convert.ToHexString(System.Security.Cryptography.SHA256.HashData("slot-zero"u8)).ToLowerInvariant());
Test("download-logical-name", () => protocol.Handle(Request("GET", "/download/0", session)).Headers["X-Celeste-Logical-Name"] == "0");
Test("head-download", () =>
{
    Stage10AHttpResponse response = protocol.Handle(Request("HEAD", "/download/0", session));
    return response.StatusCode == 200 && response.Body.Length == 0 && response.Headers["Content-Length"] == "9";
});
Test("absent-slot", () => protocol.Handle(Request("GET", "/download/1", session)).StatusCode == 404);
Test("download-all-archive", () =>
{
    Stage10AHttpResponse response = protocol.Handle(Request("GET", "/download/all", session));
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
    now = now.Add(Stage10AHttpProtocol.SessionLifetime).AddSeconds(1);
    return protocol.Handle(Request("GET", "/download/0", session)).StatusCode == 401;
});
Test("server-stop-invalidates-session", () =>
{
    Stage10AHttpProtocol local = NewProtocol();
    string token = Authenticate(local);
    local.Stop();
    return local.Handle(Request("GET", "/", token)).StatusCode == 503 && local.AccessCode.Length == 0 && local.ActiveSessionCount == 0;
});
Test("snapshot-rejects-extra-name", () =>
{
    try
    {
        _ = new Stage10AHttpProtocol(new Stage10AExportSnapshot(1, "hash", new Dictionary<string, byte[]?>
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
    Stage10AConnectionGate gate = new(Stage10AHttpProtocol.MaximumConcurrentConnections);
    if (gate.TryEnter()) return false;
    gate.Start();
    for (int i = 0; i < Stage10AHttpProtocol.MaximumConcurrentConnections; i++) if (!gate.TryEnter()) return false;
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
Test("connection-timeout-locked", () => Stage10AHttpProtocol.RequestLifetime == TimeSpan.FromSeconds(10));
Test("response-close-fallback-locked", () => Stage10AConnectionPolicy.ResponseCloseLifetime == TimeSpan.FromSeconds(5));
Test("lan-interface-policy", () =>
    Stage10ALanAddressPolicy.InterfaceRank("en0") < Stage10ALanAddressPolicy.InterfaceRank("en7") &&
    Stage10ALanAddressPolicy.IsExcludedInterface("lo0") && Stage10ALanAddressPolicy.IsExcludedInterface("utun3") &&
    !Stage10ALanAddressPolicy.IsExcludedInterface("en0"));
Test("lan-address-policy", () =>
    Stage10ALanAddressPolicy.IsUsableIpv4(IPAddress.Parse("192.168.1.42")) &&
    Stage10ALanAddressPolicy.IsUsableIpv4(IPAddress.Parse("10.0.0.2")) &&
    !Stage10ALanAddressPolicy.IsUsableIpv4(IPAddress.Loopback) &&
    !Stage10ALanAddressPolicy.IsUsableIpv4(IPAddress.Parse("169.254.1.1")) &&
    Stage10ALanAddressPolicy.FormatUrl("192.168.1.42", 42817) == "http://192.168.1.42:42817/" &&
    Stage10ALanAddressPolicy.FormatUrl("fd00::42", 42817) == "http://[fd00::42]:42817/");
Test("darwin-sockaddr-parsing", () =>
{
    IntPtr address = Marshal.AllocHGlobal(28);
    try
    {
        for (int i = 0; i < 28; i++) Marshal.WriteByte(address, i, 0);
        Marshal.WriteByte(address, 0, 16);
        Marshal.WriteByte(address, 1, 2);
        Marshal.Copy(new byte[] { 192, 168, 4, 9 }, 0, IntPtr.Add(address, 4), 4);
        return Stage10ALanAddressPolicy.ReadDarwinSockAddr(address)?.Equals(IPAddress.Parse("192.168.4.9")) == true;
    }
    finally { Marshal.FreeHGlobal(address); }
});

Console.WriteLine($"STAGE10A_PROTOCOL_TESTS PASS count={passed}");
return;

void Test(string name, Func<bool> action)
{
    if (!action()) throw new InvalidOperationException("FAIL " + name);
    passed++;
    Console.WriteLine("PASS " + name);
}

static Stage10AHttpProtocol NewProtocol(Func<DateTimeOffset>? clock = null) => new(new Stage10AExportSnapshot(7, "logical", new Dictionary<string, byte[]?>
{
    ["settings"] = "settings"u8.ToArray(),
    ["0"] = "slot-zero"u8.ToArray(),
    ["1"] = null,
    ["2"] = "slot-two"u8.ToArray()
}), clock);

static byte[] Request(string method, string path, string? session = null)
{
    string cookie = session == null ? "" : $"Cookie: {Stage10AHttpProtocol.SessionCookieName}={session}\r\n";
    return Raw($"{method} {path} HTTP/1.1\r\nHost: apple-tv\r\n{cookie}\r\n");
}

static byte[] AuthRequest(string code)
{
    string body = "code=" + code;
    return Raw($"POST /auth HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n\r\n{body}");
}

static string Authenticate(Stage10AHttpProtocol target)
{
    Stage10AHttpResponse response = target.Handle(AuthRequest(target.AccessCode));
    if (response.StatusCode != 200) throw new InvalidOperationException("Authentication setup failed.");
    string cookie = response.Headers["Set-Cookie"];
    return cookie.Split(';')[0].Split('=')[1];
}

static byte[] Raw(string value) => Encoding.ASCII.GetBytes(value);
static bool Payload(Stage10AHttpResponse response, ReadOnlySpan<byte> expected) => response.StatusCode == 200 && response.Body.AsSpan().SequenceEqual(expected);
