using System.Text;
using System.Text.Json;
using CelesteTvOSHost;

int passed = 0;
DateTimeOffset epoch = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

Test("preferred-port", () => SaveManagerContinuityPolicy.PreferredPort == 49728);
Test("service-identifier", () => SaveManagerContinuityPolicy.ServiceIdentifier == "celeste-save-manager");
Test("protocol-version", () => SaveManagerContinuityPolicy.ProtocolVersion == 1);
Test("poll-policy", () => SaveManagerContinuityPolicy.PollIntervalMilliseconds == 2000 &&
    SaveManagerContinuityPolicy.PollTimeoutMilliseconds == 1500 &&
    SaveManagerContinuityPolicy.DisconnectFailureThreshold == 3);
Test("instance-128-bit-shape", () => SaveManagerContinuityPolicy.IsValidInstanceId(NewProtocol().InstanceId));
Test("fresh-instance-per-activation", () => NewProtocol().InstanceId != NewProtocol().InstanceId);
Test("instance-validator-rejects-invalid", () =>
    !SaveManagerContinuityPolicy.IsValidInstanceId(null) &&
    !SaveManagerContinuityPolicy.IsValidInstanceId(new string('a', 31)) &&
    !SaveManagerContinuityPolicy.IsValidInstanceId(new string('A', 32)) &&
    !SaveManagerContinuityPolicy.IsValidInstanceId(new string('z', 32)));

Test("status-get-exact-schema", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    SaveManagerHttpResponse response = protocol.Handle(Request("GET", "/status"));
    using JsonDocument document = JsonDocument.Parse(response.Body);
    string[] names = document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray();
    return response.StatusCode == 200 && names.SequenceEqual(new[] { "active", "authenticated", "instance", "protocol", "service" }) &&
        document.RootElement.GetProperty("service").GetString() == "celeste-save-manager" &&
        document.RootElement.GetProperty("protocol").GetInt32() == 1 &&
        document.RootElement.GetProperty("instance").GetString() == protocol.InstanceId &&
        document.RootElement.GetProperty("active").GetBoolean() &&
        !document.RootElement.GetProperty("authenticated").GetBoolean();
});
Test("status-json-headers", () =>
{
    SaveManagerHttpResponse response = NewProtocol().Handle(Request("GET", "/status"));
    return response.Headers["Content-Type"] == "application/json; charset=utf-8" &&
        response.Headers["Cache-Control"].Contains("no-store", StringComparison.Ordinal) &&
        response.Headers["Pragma"] == "no-cache" && response.Headers["X-Content-Type-Options"] == "nosniff" &&
        response.Headers["Referrer-Policy"] == "no-referrer";
});
Test("status-head-metadata-no-body", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    SaveManagerHttpResponse get = protocol.Handle(Request("GET", "/status"));
    SaveManagerHttpResponse head = protocol.Handle(Request("HEAD", "/status"));
    return head.StatusCode == 200 && head.Body.Length == 0 &&
        head.Headers["Content-Length"] == get.Body.Length.ToString();
});
Test("status-post-rejected", () => NewProtocol().Handle(Request("POST", "/status")).StatusCode == 405);
Test("status-body-rejected", () => NewProtocol().Handle(Raw(
    "GET /status HTTP/1.1\r\nContent-Length: 1\r\n\r\nx")).StatusCode is 400 or 413);
Test("status-unauthenticated-false", () => !StatusAuthenticated(NewProtocol(), null));
Test("status-authenticated-true", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string session = Authenticate(protocol);
    return StatusAuthenticated(protocol, session);
});
Test("status-auth-is-non-sliding", () =>
{
    DateTimeOffset now = epoch;
    SaveManagerHttpProtocol protocol = NewProtocol(() => now);
    string session = Authenticate(protocol);
    now = epoch.AddMinutes(9);
    if (!StatusAuthenticated(protocol, session)) return false;
    now = epoch.Add(SaveManagerHttpProtocol.SessionLifetime).AddSeconds(1);
    return !StatusAuthenticated(protocol, session);
});
Test("normal-auth-remains-sliding", () =>
{
    DateTimeOffset now = epoch;
    SaveManagerHttpProtocol protocol = NewProtocol(() => now);
    string session = Authenticate(protocol);
    now = epoch.AddMinutes(9);
    if (protocol.Handle(Request("GET", "/", session)).StatusCode != 200) return false;
    now = epoch.AddMinutes(11);
    return StatusAuthenticated(protocol, session);
});
Test("status-is-not-manager-activity", () =>
    !NewProtocol().HandleWithActivity(Request("GET", "/status")).CountsAsManagerActivity);
Test("status-head-is-not-manager-activity", () =>
    !NewProtocol().HandleWithActivity(Request("HEAD", "/status")).CountsAsManagerActivity);
Test("rejected-status-is-not-manager-activity", () =>
    !NewProtocol().HandleWithActivity(Raw("GET /status HTTP/1.1\r\nContent-Length: 1\r\n\r\nx")).CountsAsManagerActivity);
Test("ordinary-request-is-manager-activity", () =>
    NewProtocol().HandleWithActivity(Request("GET", "/")).CountsAsManagerActivity);
Test("continuous-status-never-counts-as-manager-activity", () =>
{
    DateTimeOffset now = epoch;
    SaveManagerHttpProtocol protocol = NewProtocol(() => now);
    for (int poll = 0; poll < 391; poll++)
    {
        if (protocol.HandleWithActivity(Request("GET", "/status")).CountsAsManagerActivity) return false;
        now = now.AddSeconds(2);
    }
    return now > epoch.AddMinutes(13);
});
Test("session-expiry-precedes-unchanged-manager-deadline", () =>
{
    DateTimeOffset now = epoch;
    DateTimeOffset managerDeadline = epoch.AddMinutes(12);
    SaveManagerHttpProtocol protocol = NewProtocol(() => now);
    string session = Authenticate(protocol);
    for (int poll = 0; poll < 301; poll++)
    {
        if (protocol.HandleWithActivity(Request("GET", "/status", session)).CountsAsManagerActivity) return false;
        now = now.AddSeconds(2);
    }
    return now > epoch.AddMinutes(10) && now < managerDeadline && !StatusAuthenticated(protocol, session) &&
        managerDeadline == epoch.AddMinutes(12);
});

Test("auth-page-has-hidden-instance", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string html = Text(protocol.Handle(Request("GET", "/")));
    return html.Contains("type=hidden name=instance value=\"" + protocol.InstanceId + "\"", StringComparison.Ordinal);
});
Test("auth-missing-instance-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(Form("/auth", "code=" + protocol.AccessCode)).StatusCode == 400;
});
Test("auth-wrong-instance-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(AuthRequest(protocol.AccessCode, new string('0', 32))).StatusCode == 401;
});
Test("auth-malformed-instance-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(AuthRequest(protocol.AccessCode, "BAD")).StatusCode == 401;
});
Test("auth-correct-instance-wrong-code-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string wrong = protocol.AccessCode == "000000" ? "000001" : "000000";
    return protocol.Handle(AuthRequest(wrong, protocol.InstanceId)).StatusCode == 401;
});
Test("auth-correct-code-wrong-instance-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(AuthRequest(protocol.AccessCode, new string('1', 32))).StatusCode == 401;
});
Test("auth-correct-code-and-instance-succeeds", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(AuthRequest(protocol.AccessCode, protocol.InstanceId)).StatusCode == 200;
});
Test("auth-unexpected-field-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(Form("/auth", $"code={protocol.AccessCode}&instance={protocol.InstanceId}&extra=x")).StatusCode == 400;
});
Test("auth-duplicate-field-rejected", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    return protocol.Handle(Form("/auth", $"code={protocol.AccessCode}&code={protocol.AccessCode}")).StatusCode == 400;
});
Test("repeated-code-old-instance-rejected", () =>
{
    SaveManagerHttpProtocol first = NewProtocol(accessCodeFactory: () => "123456");
    string oldInstance = first.InstanceId;
    first.Stop();
    SaveManagerHttpProtocol second = NewProtocol(accessCodeFactory: () => "123456");
    return oldInstance != second.InstanceId && second.Handle(AuthRequest("123456", oldInstance)).StatusCode == 401 &&
        second.Handle(AuthRequest("123456", second.InstanceId)).StatusCode == 200;
});
Test("old-cookie-false-after-reopen", () =>
{
    SaveManagerHttpProtocol first = NewProtocol();
    string cookie = Authenticate(first);
    first.Stop();
    return !StatusAuthenticated(NewProtocol(), cookie);
});
Test("old-qr-rejected-after-reopen", () =>
{
    SaveManagerHttpProtocol first = NewProtocol();
    string token = first.PairingCredentialForQr;
    first.Stop();
    return NewProtocol().Handle(PairRequest(token)).StatusCode == 401;
});
Test("status-after-stop-is-terminal", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    protocol.Stop();
    SaveManagerProtocolResult result = protocol.HandleWithActivity(Request("GET", "/status"));
    return result.Response.StatusCode == 503 && !result.CountsAsManagerActivity && protocol.InstanceId.Length == 0;
});
Test("status-exposes-no-secrets-or-save-data", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string json = Text(protocol.Handle(Request("GET", "/status")));
    return !json.Contains(protocol.AccessCode, StringComparison.Ordinal) &&
        !json.Contains(protocol.PairingCredentialForQr, StringComparison.Ordinal) &&
        !json.Contains("csrf", StringComparison.OrdinalIgnoreCase) &&
        !json.Contains("revision", StringComparison.OrdinalIgnoreCase) &&
        !json.Contains("generation", StringComparison.OrdinalIgnoreCase) &&
        !json.Contains("logical", StringComparison.OrdinalIgnoreCase) &&
        !json.Contains("settings", StringComparison.OrdinalIgnoreCase) &&
        !json.Contains("slot", StringComparison.OrdinalIgnoreCase);
});

Test("fallback-exact-posix-48", () => SaveManagerContinuityPolicy.ShouldUseEphemeralFallback(true, false, true, 48));
Test("fallback-rejects-other-posix", () => !SaveManagerContinuityPolicy.ShouldUseEphemeralFallback(true, false, true, 49));
Test("fallback-rejects-other-domain", () => !SaveManagerContinuityPolicy.ShouldUseEphemeralFallback(true, false, false, 48));
Test("fallback-only-from-preferred", () => !SaveManagerContinuityPolicy.ShouldUseEphemeralFallback(false, false, true, 48));
Test("fallback-only-once", () => !SaveManagerContinuityPolicy.ShouldUseEphemeralFallback(true, true, true, 48));

Test("browser-initial-connected", () => Transition(0, true, true, true, true).State == SaveManagerBrowserContinuityState.Connected);
Test("browser-one-failure-checking", () => Transition(0, false, false, false, true) is
    { State: SaveManagerBrowserContinuityState.Checking, ConsecutiveFailures: 1, ControlsEnabled: false });
Test("browser-two-failures-checking", () => Transition(1, false, false, false, true) is
    { State: SaveManagerBrowserContinuityState.Checking, ConsecutiveFailures: 2, ControlsEnabled: false });
Test("browser-three-failures-disconnected", () => Transition(2, false, false, false, true) is
    { State: SaveManagerBrowserContinuityState.Disconnected, ConsecutiveFailures: 3, ControlsEnabled: false });
Test("browser-valid-response-recovers", () => Transition(2, true, true, true, true) is
    { State: SaveManagerBrowserContinuityState.Connected, ConsecutiveFailures: 0, ControlsEnabled: true });
Test("browser-new-instance-reopened", () => Transition(0, true, false, false, true).State == SaveManagerBrowserContinuityState.Reopened);
Test("browser-session-expired", () => Transition(0, true, true, false, true).State == SaveManagerBrowserContinuityState.SessionExpired);
Test("manual-page-unauthenticated-is-connected", () => Transition(0, true, true, false, false).State == SaveManagerBrowserContinuityState.Connected);
Test("controls-only-enabled-when-connected", () =>
    !Transition(0, true, false, false, true).ControlsEnabled &&
    !Transition(0, true, true, false, true).ControlsEnabled &&
    Transition(0, true, true, true, true).ControlsEnabled);

Test("browser-continuity-script-contract", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string session = Authenticate(protocol);
    SaveManagerHttpResponse response = protocol.Handle(Request("GET", "/", session));
    string html = Text(response);
    return html.Contains("fetch('/status'", StringComparison.Ordinal) &&
        html.Contains("setInterval(continuityPoll,2000)", StringComparison.Ordinal) &&
        html.Contains("controller.abort(),1500", StringComparison.Ordinal) &&
        html.Contains("if(continuityPolling)return", StringComparison.Ordinal) &&
        html.Contains("continuityFailures<3?'checking':'disconnected'", StringComparison.Ordinal) &&
        html.Contains("continuityReconnect.addEventListener('click',()=>location.assign('/'))", StringComparison.Ordinal);
});
Test("reconnect-visible-disabled-while-healthy-on-both-pages", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string manual = Text(protocol.Handle(Request("GET", "/")));
    string authenticated = Text(protocol.Handle(Request("GET", "/", Authenticate(protocol))));
    return manual.Contains("id=continuity-reconnect type=button disabled", StringComparison.Ordinal) &&
        authenticated.Contains("id=continuity-reconnect type=button disabled", StringComparison.Ordinal) &&
        manual.Contains("continuityReconnect.disabled=kind==='connected'||kind==='checking'", StringComparison.Ordinal) &&
        authenticated.Contains("continuityReconnect.disabled=kind==='connected'||kind==='checking'", StringComparison.Ordinal) &&
        !manual.Contains("id=continuity-reconnect type=button hidden", StringComparison.Ordinal) &&
        !authenticated.Contains("id=continuity-reconnect type=button hidden", StringComparison.Ordinal);
});
Test("browser-exact-schema-and-wrong-service-protection", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string html = Text(protocol.Handle(Request("GET", "/")));
    return html.Contains("active,authenticated,instance,protocol,service", StringComparison.Ordinal) &&
        html.Contains("j.service==='celeste-save-manager'", StringComparison.Ordinal) &&
        html.Contains("j.protocol===1", StringComparison.Ordinal) &&
        html.Contains("/^[0-9a-f]{32}$/", StringComparison.Ordinal);
});
Test("browser-controls-are-real-disabled-targets", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string session = Authenticate(protocol);
    string html = Text(protocol.Handle(Request("GET", "/", session)));
    return html.Contains("data-continuity-control", StringComparison.Ordinal) &&
        html.Contains("c.disabled=!enabled", StringComparison.Ordinal) &&
        html.Contains("c.removeAttribute('href')", StringComparison.Ordinal) &&
        html.Contains("if(!continuityConnected)return", StringComparison.Ordinal);
});
Test("browser-csp-reconnect-is-strict", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    SaveManagerHttpResponse response = protocol.Handle(Request("GET", "/"));
    string html = Text(response);
    string csp = response.Headers["Content-Security-Policy"];
    return csp.Contains("script-src 'nonce-", StringComparison.Ordinal) &&
        csp.Contains("connect-src 'self'", StringComparison.Ordinal) &&
        !csp.Contains("script-src 'unsafe-inline'", StringComparison.Ordinal) &&
        !html.Contains(" onclick=", StringComparison.OrdinalIgnoreCase);
});
Test("browser-avoids-websocket-sse-cors", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol();
    string html = Text(protocol.Handle(Request("GET", "/")));
    return !html.Contains("WebSocket", StringComparison.OrdinalIgnoreCase) &&
        !html.Contains("EventSource", StringComparison.OrdinalIgnoreCase) &&
        !html.Contains("Access-Control-Allow-Origin", StringComparison.OrdinalIgnoreCase);
});
Test("qr-semantics-unchanged", () =>
{
    SaveManagerHttpProtocol protocol = NewProtocol(() => epoch);
    string token = protocol.PairingCredentialForQr;
    string url = protocol.BuildPairingUrl("http://192.168.1.42:49728/");
    return token.Length == 64 && url.EndsWith("/pair#" + token, StringComparison.Ordinal) &&
        SaveManagerHttpProtocol.PairingLifetime == TimeSpan.FromMinutes(3) &&
        protocol.Handle(PairRequest(token)).StatusCode == 200 &&
        protocol.Handle(PairRequest(token)).StatusCode == 401;
});

Console.WriteLine($"STAGE22B_CONTINUITY_TESTS PASS count={passed}");
return;

void Test(string name, Func<bool> action)
{
    if (!action()) throw new InvalidOperationException("FAIL " + name);
    passed++;
    Console.WriteLine("PASS " + name);
}

static SaveManagerBrowserContinuityTransition Transition(
    int failures, bool valid, bool same, bool authenticated, bool required) =>
    SaveManagerContinuityPolicy.Transition(failures, valid, same, authenticated, required);

static SaveManagerHttpProtocol NewProtocol(
    Func<DateTimeOffset>? clock = null,
    Func<string>? accessCodeFactory = null) =>
    new(new SaveExportSnapshot(7, "logical", new Dictionary<string, byte[]?>
    {
        ["settings"] = "settings"u8.ToArray(),
        ["0"] = "slot-zero"u8.ToArray(),
        ["1"] = null,
        ["2"] = "slot-two"u8.ToArray()
    }), clock, accessCodeFactory: accessCodeFactory);

static bool StatusAuthenticated(SaveManagerHttpProtocol protocol, string? session)
{
    using JsonDocument document = JsonDocument.Parse(protocol.Handle(Request("GET", "/status", session)).Body);
    return document.RootElement.GetProperty("authenticated").GetBoolean();
}

static string Authenticate(SaveManagerHttpProtocol protocol)
{
    SaveManagerHttpResponse response = protocol.Handle(AuthRequest(protocol.AccessCode, protocol.InstanceId));
    if (response.StatusCode != 200) throw new InvalidOperationException("Authentication setup failed.");
    return response.Headers["Set-Cookie"].Split(';')[0].Split('=')[1];
}

static byte[] Request(string method, string path, string? session = null)
{
    string cookie = session == null ? "" : $"Cookie: {SaveManagerHttpProtocol.SessionCookieName}={session}\r\n";
    return Raw($"{method} {path} HTTP/1.1\r\nHost: apple-tv\r\n{cookie}\r\n");
}

static byte[] AuthRequest(string code, string instance) =>
    Form("/auth", "code=" + code + "&instance=" + instance);

static byte[] PairRequest(string token) => Form("/pair", "token=" + token);

static byte[] Form(string path, string body) => Raw(
    $"POST {path} HTTP/1.1\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {body.Length}\r\n\r\n{body}");

static byte[] Raw(string value) => Encoding.ASCII.GetBytes(value);
static string Text(SaveManagerHttpResponse response) => Encoding.UTF8.GetString(response.Body);
