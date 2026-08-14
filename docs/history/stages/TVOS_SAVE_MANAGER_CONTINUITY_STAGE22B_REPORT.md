# Stage 22B — Save Manager browser continuity

## Scope and baseline

Stage 22B productionised the GREEN Stage 22A design on
`feature/save-manager-continuity`, starting from
`4c07843d8b8069be00aba3626b58f6053299bdaa`. The final feature commit is the
commit containing this report, with its exact SHA recorded in the acceptance
response. This work is post-RC2: `v1.0.0-rc.1` remains at
`ee52b0868df091746f134d95d4f020f94f23d4fb`, `v1.0.0-rc.2` remains at
`641e86e4ed164cdf93f602ce2f11436449654d6e`, and the private cloud template
remains pinned to product source
`b5f2ec2fdd5c65c533d86aae750ee60dcaf5009e`.

The change is confined to the deliberately activated local Save Manager. It
does not change Celeste gameplay, save formats, persistence keys, serializers,
controller input, audio, native dependencies, or the cloud builder.

## Listener and activation design

Save Manager now prefers TCP port `49728` through the public Network framework
fixed-port listener API and sets `ReuseLocalAddress = true`. Immediate normal
reopens therefore retain one practical browser origin. If and only if the first
listener fails with Network framework's POSIX-domain Darwin error 48
(`EADDRINUSE`), the service completely disposes that failed listener and makes
one system-selected-port fallback attempt. The same protocol instance, access
code, QR credential, and 128-bit activation identifier remain authoritative
through that bind fallback. The Apple TV labels its displayed address as
temporary. All other listener failures retain the existing failed state.

Bonjour remains `_celeste-save._tcp` and active only while Save Manager is
open. Callback source identity prevents a late event from a disposed preferred
or fallback listener from changing the current activation. The reusable
managed four-connection gate remains the concurrency boundary; the native
`NWListener.ConnectionLimit` lifetime accept-budget property is still
deliberately unused.

## Status and authentication boundary

Every activation creates a fresh cryptographically random 16-byte identifier,
rendered as exactly 32 lowercase hexadecimal characters. Fixed `GET` and `HEAD
/status` routes return exactly:

```json
{"service":"celeste-save-manager","protocol":1,"instance":"<32 lowercase hex>","active":true,"authenticated":false}
```

`authenticated` reflects the request's normal session cookie. No access code,
QR token, session token, CSRF token, revision, LAN address, save name, size, or
save data is exposed. `POST /status` is rejected, responses retain the existing
no-store/no-referrer/no-sniff security headers, and HEAD has the representation
length without a response body.

Status authentication is explicitly non-sliding and status requests do not
count as manager activity. Consequently the existing ten-minute sliding browser
session still expires without real authenticated work and polling cannot defeat
the twelve-minute manager inactivity stop. Ordinary accepted protocol activity
retains the existing behavior.

Manual authentication now submits the visible six-digit code together with a
hidden activation identifier. Both are bounded, parsed as an exact two-field
form, and fixed-time compared. Even if a later random six-digit code repeats,
an old page cannot authenticate the new activation. QR pairing remains a
separate 256-bit, one-time, three-minute credential and produces the same
normal session cookie as before.

## Browser state machine and CSP

The manual-authentication and authenticated manager pages poll `/status` every
two seconds with a 1.5-second abort timeout and no overlapping request. The
explicit states are Connected, Checking, Disconnected, Reopened, and Session
expired. A single transient failure enters Checking; three consecutive failures
enter Disconnected. A valid response with a different activation enters
Reopened, while the same activation without the required session enters Session
expired.

Downloads, file inputs, and mutation buttons are actually disabled outside
Connected. The visible Reconnect button is disabled while Connected or
Checking, then enabled for a terminal stale state; it performs a same-origin
root navigation from the existing nonce-bearing script. There is no inline event attribute, CSP
`unsafe-inline`, WebSocket, Server-Sent Events, CORS, LAN scan, or automatic
reauthentication. The pairing bootstrap remains intentionally transient and
unchanged.

## Files and verification

Production changes cover the focused continuity policy, Save Manager listener,
HTTP protocol, Celeste-rendered temporary-address message, public builder gates,
live acceptance helpers, deterministic tests, and current documentation. No
historical Stage 20/21 report was rewritten.

The new Stage 22B suite contains 57 deterministic tests, and its source plus
package verifier records 94 checks. It covers port and
fallback policy, exact status schema and methods, non-sliding/non-activity
timers, instance-bound authentication, stale activation material, browser
states, CSP, disabled controls, parser limits, unchanged QR semantics, and
persistence isolation. The inherited Save Manager protocol suite remains 66
tests and QR suite remains 31 tests. The final gate also runs the inherited
persistence, Controller Prompts, graceful Quit, soft reload, Performance HUD,
input-profile, builder, cloud, repository, and documentation checks.

## Product and physical acceptance

The final Release build completed through the normal public builder in 9m41s.
It used full trimming, full AOT, and `UseInterpreter=false`; the signed
same-identity app installed and reached its first Celeste draw with all seven
FMOD banks. The signing-ready unsigned IPA is 895,782,069 bytes with SHA-256
`92ff521701bef82266c7bd55c82acbd21950a50d6b141ef2e0b40c085d1ec96b`.
Package verification found arm64 TVOS, minimum tvOS 16.0, no usable signature
or embedded provisioning profile in the IPA, and all accepted product tokens.

The physical matrix used a real iPhone browser and the signed same-identity
Apple TV app. Ten normal close/reopen cycles retained port `49728`; the final
Reconnect-button refinement was then repeated for three additional cycles.
Both the unauthenticated and authenticated pages kept Reconnect visible but
disabled while healthy, transitioned through Checking and Disconnected after
closure, detected the fresh activation as Reopened, and required fresh
authentication. Home/background stopped the listener, did not reactivate it on
foreground, and a deliberate reopen returned to `49728`.

A temporary ignored diagnostic build exclusively occupied `49728`. The
production listener made exactly one system-selected-port fallback, displayed
the temporary-address warning, and served a working QR-authenticated manager.
After closure released the conflict, the next activation returned to `49728`.
The diagnostic occupier was then removed, its absence and the full verifier
chain were checked, and the exact clean production source was rebuilt,
same-identity installed, and launched successfully.

On that clean final candidate, a Settings mutation initiated while an active
Level existed committed successfully; Confirm stopped networking and returned
the existing FNA/FMOD runtime to a functional main menu with normal audio and
controller input. Save Manager subsequently reopened on `49728`. Stage 12B's
Leave Celeste screen and Back path also remained functional. Existing save
slots and host preferences survived every replacement installation. Exact old
code, QR, cookie, status-schema, timer, and connection-gate isolation are locked
by the 57 deterministic tests; the browser's physical fresh-authentication
boundary independently confirmed that stale session state was not reused.

The accepted native logical hash must remain
`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.
Canonical Celeste input/source locks and all seven FMOD banks remain unchanged.

## Security, privacy, and documentation

Continuity identity is public and ephemeral; all authentication and mutation
security remains in the accepted access-code/QR/session/CSRF/revision layers.
No runtime secret, LAN address, user save, game asset, FMOD input, generated
Celeste source, app/IPA, signing identity, or device identifier is tracked.
Current README, status, building, and troubleshooting material explain the
stable normal address, temporary fallback, stale-page notices, disabled
controls, and fresh-authentication requirement without exposing protocol detail
as normal user burden.
