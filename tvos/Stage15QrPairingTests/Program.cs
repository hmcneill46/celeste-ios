using System.Text;
using CelesteTvOSHost;

int passed = 0;
DateTimeOffset now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);

Test("pairing-token-256-bit-shape", () =>
{
    usingProtocol(out Stage10AHttpProtocol protocol);
    string token = protocol.PairingCredentialForQr;
    return token.Length == 64 && token.All(Uri.IsHexDigit) && protocol.PairingState == Stage15PairingState.Available;
});
Test("fresh-token-per-activation", () =>
{
    Stage10AHttpProtocol first = NewProtocol();
    Stage10AHttpProtocol second = NewProtocol();
    return first.PairingCredentialForQr != second.PairingCredentialForQr;
});
Test("pairing-token-distinct-from-access-code", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    return protocol.PairingCredentialForQr != protocol.AccessCode && protocol.AccessCode.Length == 6;
});
Test("fragment-pairing-url", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string token = protocol.PairingCredentialForQr;
    string url = protocol.BuildPairingUrl("http://192.168.1.42:42817/");
    return url == "http://192.168.1.42:42817/pair#" + token && !url.Contains('?');
});
Test("pairing-url-requires-ready-http-base", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    try { _ = protocol.BuildPairingUrl("https://example.invalid/"); return false; }
    catch (InvalidOperationException) { return true; }
});
Test("pair-bootstrap-cleans-fragment-before-post", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string html = Encoding.UTF8.GetString(protocol.Handle(Request("GET", "/pair")).Body);
    return html.Contains("location.hash.slice(1)", StringComparison.Ordinal) &&
        html.Contains("history.replaceState(null,'','/pair')", StringComparison.Ordinal) &&
        html.Contains("fetch('/pair'", StringComparison.Ordinal) && !html.Contains(protocol.PairingCredentialForQr, StringComparison.Ordinal);
});
Test("pair-bootstrap-csp-is-nonce-scoped", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    Stage10AHttpResponse response = protocol.Handle(Request("GET", "/pair"));
    string csp = response.Headers["Content-Security-Policy"];
    return csp.Contains("script-src 'nonce-", StringComparison.Ordinal) && csp.Contains("connect-src 'self'", StringComparison.Ordinal);
});
Test("head-pair-has-no-body", () => NewProtocol().Handle(Request("HEAD", "/pair")).Body.Length == 0);
Test("missing-pairing-token-rejected", () => NewProtocol().Handle(PairRequest("")).StatusCode == 401);
Test("malformed-pairing-token-rejected", () => NewProtocol().Handle(PairRequest(new string('z', 64))).StatusCode == 401);
Test("invalid-pairing-token-rejected", () => NewProtocol().Handle(PairRequest(new string('0', 64))).StatusCode == 401);
Test("valid-pairing-succeeds", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    Stage10AHttpResponse response = protocol.Handle(PairRequest(protocol.PairingCredentialForQr));
    return response.StatusCode == 200 && response.Headers["X-Celeste-Pairing"] == "accepted" &&
        response.Headers["X-Celeste-Authentication"] == "accepted";
});
Test("pairing-produces-normal-session", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string session = Pair(protocol).Session;
    Stage10AHttpResponse root = protocol.Handle(Request("GET", "/", session));
    return root.StatusCode == 200 && Encoding.UTF8.GetString(root.Body).Contains("Connected to your Apple TV", StringComparison.Ordinal);
});
Test("pairing-session-has-csrf-and-revision", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    AuthInfo auth = Pair(protocol);
    Stage10AHttpResponse root = protocol.Handle(Request("GET", "/", auth.Session));
    return TokenFromHtml(root, "const csrf='").Length == 64 && TokenFromHtml(root, "revision='").Length == 64;
});
Test("pairing-session-cookie-security", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    Stage10AHttpResponse response = protocol.Handle(PairRequest(protocol.PairingCredentialForQr));
    string cookie = response.Headers["Set-Cookie"];
    return cookie.Contains("HttpOnly", StringComparison.Ordinal) && cookie.Contains("SameSite=Strict", StringComparison.Ordinal) &&
        cookie.Contains("Max-Age=600", StringComparison.Ordinal);
});
Test("pairing-token-is-one-time", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string token = protocol.PairingCredentialForQr;
    return protocol.Handle(PairRequest(token)).StatusCode == 200 && protocol.Handle(PairRequest(token)).StatusCode == 401 &&
        protocol.PairingState == Stage15PairingState.Consumed;
});
Test("concurrent-pairing-has-one-winner", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    byte[] request = PairRequest(protocol.PairingCredentialForQr);
    Task<Stage10AHttpResponse>[] tasks = { Task.Run(() => protocol.Handle(request)), Task.Run(() => protocol.Handle(request)) };
    Task.WaitAll(tasks);
    return tasks.Count(task => task.Result.StatusCode == 200) == 1 && tasks.Count(task => task.Result.StatusCode == 401) == 1 &&
        protocol.ActiveSessionCount == 1;
});
Test("pairing-token-expires", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol(() => now);
    string token = protocol.PairingCredentialForQr;
    now = now.Add(Stage10AHttpProtocol.PairingLifetime).AddSeconds(1);
    bool result = protocol.PairingState == Stage15PairingState.Expired && protocol.Handle(PairRequest(token)).StatusCode == 401;
    now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    return result;
});
Test("manual-code-survives-pairing-expiry", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol(() => now);
    now = now.Add(Stage10AHttpProtocol.PairingLifetime).AddSeconds(1);
    bool result = protocol.Handle(AuthRequest(protocol.AccessCode)).StatusCode == 200;
    now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    return result;
});
Test("manager-stop-invalidates-pairing", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string token = protocol.PairingCredentialForQr;
    protocol.Stop();
    return protocol.PairingState == Stage15PairingState.Stopped && protocol.PairingCredentialForQr.Length == 0 &&
        protocol.Handle(PairRequest(token)).StatusCode == 503;
});
Test("background-stop-invalidates-pairing", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string token = protocol.PairingCredentialForQr;
    protocol.Stop(); // The host's background observer uses this same terminal stop boundary.
    return protocol.Handle(PairRequest(token)).StatusCode == 503 && protocol.ActiveSessionCount == 0;
});
Test("soft-reload-stop-invalidates-pairing", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    AuthInfo auth = Pair(protocol);
    protocol.Stop(); // Stage 13B stops the manager before materialisation.
    return protocol.Handle(Request("GET", "/", auth.Session)).StatusCode == 503 &&
        protocol.PairingCredentialForQr.Length == 0;
});
Test("new-activation-after-stop-has-fresh-pairing", () =>
{
    Stage10AHttpProtocol first = NewProtocol();
    string old = first.PairingCredentialForQr;
    first.Stop();
    Stage10AHttpProtocol second = NewProtocol();
    return second.PairingState == Stage15PairingState.Available &&
        second.PairingCredentialForQr.Length == 64 && second.PairingCredentialForQr != old;
});
Test("stopped-manager-cannot-build-qr-url", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    protocol.Stop();
    try { _ = protocol.BuildPairingUrl("http://192.168.1.42:42817/"); return false; }
    catch (InvalidOperationException) { return true; }
});
Test("expired-pairing-secret-is-erased", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol(() => now);
    now = now.Add(Stage10AHttpProtocol.PairingLifetime).AddSeconds(1);
    bool result = protocol.PairingState == Stage15PairingState.Expired && protocol.PairingCredentialForQr.Length == 0;
    now = new DateTimeOffset(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    return result;
});
Test("pairing-post-body-limit", () =>
{
    byte[] headers = Raw($"POST /pair HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {Stage10AHttpProtocol.MaximumPairingBodyBytes + 1}\r\n\r\n");
    return Stage10AHttpProtocol.InspectRequestProgress(headers).Rejection?.StatusCode == 413;
});
Test("pairing-route-rejects-query-and-token-path", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    return protocol.Handle(Request("GET", "/pair?token=x")).StatusCode == 400 &&
        protocol.Handle(Request("GET", "/pair/secret")).StatusCode == 401;
});
Test("manual-six-digit-fallback-still-works", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    Stage10AHttpResponse response = protocol.Handle(AuthRequest(protocol.AccessCode));
    return response.StatusCode == 200 && response.Headers["X-Celeste-Authentication"] == "accepted" &&
        !response.Headers.ContainsKey("X-Celeste-Pairing");
});
Test("manual-auth-can-add-session-after-qr-use", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    _ = Pair(protocol);
    Stage10AHttpResponse manual = protocol.Handle(AuthRequest(protocol.AccessCode));
    return manual.StatusCode == 200 && protocol.ActiveSessionCount == 2;
});
Test("paired-session-can-use-stage10-mutation", () =>
{
    Stage10AHttpProtocol protocol = NewWritableProtocol(out FakeMutationAuthority authority);
    AuthInfo auth = Pair(protocol);
    Stage10AHttpResponse root = protocol.Handle(Request("GET", "/", auth.Session));
    auth = auth with { Csrf = TokenFromHtml(root, "const csrf='"), Revision = TokenFromHtml(root, "revision='") };
    Stage10AHttpResponse mutation = protocol.Handle(MutationRequest("/delete/0", auth));
    return mutation.StatusCode == 200 && authority.Generation == 8 && protocol.RestartRequired;
});
Test("pair-session-secrets-remain-distinct", () =>
{
    Stage10AHttpProtocol protocol = NewProtocol();
    string pairing = protocol.PairingCredentialForQr;
    AuthInfo auth = Pair(protocol);
    Stage10AHttpResponse root = protocol.Handle(Request("GET", "/", auth.Session));
    string csrf = TokenFromHtml(root, "const csrf='");
    string revision = TokenFromHtml(root, "revision='");
    return new[] { pairing, auth.Session, csrf, revision }.Distinct(StringComparer.Ordinal).Count() == 4;
});

Console.WriteLine($"STAGE15_QR_PAIRING_TESTS PASS count={passed}");
return;

void Test(string name, Func<bool> action)
{
    if (!action()) throw new InvalidOperationException("FAIL " + name);
    passed++;
    Console.WriteLine("PASS " + name);
}

static void usingProtocol(out Stage10AHttpProtocol protocol) => protocol = NewProtocol();

static Stage10AHttpProtocol NewProtocol(Func<DateTimeOffset>? clock = null) => new(new Stage10AExportSnapshot(7, "logical", new Dictionary<string, byte[]?>
{
    ["settings"] = "settings"u8.ToArray(), ["0"] = "save-zero"u8.ToArray(), ["1"] = null, ["2"] = "save-two"u8.ToArray()
}), clock);

static Stage10AHttpProtocol NewWritableProtocol(out FakeMutationAuthority authority)
{
    authority = new FakeMutationAuthority(new Stage10AExportSnapshot(7, "logical", new Dictionary<string, byte[]?>
    {
        ["settings"] = "settings-valid"u8.ToArray(), ["0"] = "save-valid-zero"u8.ToArray(), ["1"] = null, ["2"] = "save-valid-two"u8.ToArray()
    }));
    FakeMutationAuthority captured = authority;
    return new Stage10AHttpProtocol(authority.Snapshot, mutationHandler: captured.Mutate);
}

static AuthInfo Pair(Stage10AHttpProtocol protocol)
{
    Stage10AHttpResponse response = protocol.Handle(PairRequest(protocol.PairingCredentialForQr));
    if (response.StatusCode != 200) throw new InvalidOperationException("Pairing setup failed.");
    return new AuthInfo(response.Headers["Set-Cookie"].Split(';')[0].Split('=')[1], "", "");
}

static byte[] Request(string method, string path, string? session = null)
{
    string cookie = session == null ? "" : $"Cookie: {Stage10AHttpProtocol.SessionCookieName}={session}\r\n";
    return Raw($"{method} {path} HTTP/1.1\r\nHost: apple-tv\r\n{cookie}\r\n");
}

static byte[] PairRequest(string token)
{
    string body = "token=" + token;
    return Raw($"POST /pair HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n\r\n{body}");
}

static byte[] AuthRequest(string code)
{
    string body = "code=" + code;
    return Raw($"POST /auth HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n\r\n{body}");
}

static byte[] MutationRequest(string path, AuthInfo auth)
{
    return Raw($"POST {path} HTTP/1.1\r\nCookie: {Stage10AHttpProtocol.SessionCookieName}={auth.Session}\r\nX-Celeste-CSRF: {auth.Csrf}\r\nX-Celeste-Revision: {auth.Revision}\r\nContent-Length: 0\r\n\r\n");
}

static string TokenFromHtml(Stage10AHttpResponse response, string prefix)
{
    string html = Encoding.UTF8.GetString(response.Body);
    int start = html.IndexOf(prefix, StringComparison.Ordinal);
    if (start < 0) return "";
    start += prefix.Length;
    int end = html.IndexOf('\'', start);
    return end < 0 ? "" : html[start..end];
}

static byte[] Raw(string text) => Encoding.ASCII.GetBytes(text);

internal sealed record AuthInfo(string Session, string Csrf, string Revision);

internal sealed class FakeMutationAuthority
{
    internal Stage10AExportSnapshot Snapshot { get; private set; }
    internal ulong Generation => Snapshot.Generation;
    internal FakeMutationAuthority(Stage10AExportSnapshot snapshot) => Snapshot = snapshot;
    internal Stage10BMutationResult Mutate(Stage10BMutationCommand command)
    {
        if (command.ExpectedGeneration != Snapshot.Generation || command.ExpectedLogicalHash != Snapshot.LogicalHash)
            return Stage10BMutationResult.ConflictResult(Snapshot);
        Dictionary<string, byte[]?> files = Snapshot.Files.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
        files[command.LogicalName] = command.Payload?.ToArray();
        Snapshot = new Stage10AExportSnapshot(Snapshot.Generation + 1, "logical-next", files);
        return Stage10BMutationResult.Committed(Snapshot, 1000, 2000);
    }
}
