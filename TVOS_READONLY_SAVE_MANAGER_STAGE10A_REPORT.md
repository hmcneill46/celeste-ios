# Celeste tvOS Stage 10A read-only Save Manager report

## Result and scope

Stage 10A started from
`7d689dbb0ee6b1ac4f655573eb33b3b8c12b57ac`. The accepted change is committed
with message `feat: add read-only tvOS save manager`; its final commit is
recorded in Git rather than embedded here to avoid a self-referential file.

The feature is a deliberately activated, read-only, same-LAN maintenance mode.
Normal startup and gameplay leave the listener and Bonjour advertisement
completely dormant. It cannot upload, replace, delete, or edit a save, browse
the filesystem, read the UserDefaults A/B keys, or decode the compressed v2
envelope.

The locked toolchain remained .NET SDK 10.0.302, workload set 10.0.302.0, Xcode
26.6, tvOS SDK 26.5, and deployment target tvOS 16.0. The Stage 1 logical hash
remained `61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`;
no native dependency was rebuilt.

## UI integration

The Stage 6 generated-source transform inserts one tvOS-only **Save Manager**
button into the existing `OuiOptions` `TextMenu`. Selecting it hides and
unfocuses the Options menu and adds a Celeste `Entity` overlay tagged for HUD,
paused, and frozen updates. No new `Oui` subtype, reflected type discovery, or
AOT factory entry was required.

The TV overlay shows a starting state, same-network guidance, up to two usable
numeric LAN URLs, a grouped six-digit code, unavailable/failure states, and a
single controller instruction to stop and return. Confirm or Back closes it;
underlying Options/game input remains suppressed until the overlay exits.

The host temporarily disables the tvOS idle timer only while Save Manager is
active. It restores normal idle behaviour on every stop path. This corrected a
physical test in which the screensaver caused `resign-active` while the user
was reading the page; real resign-active and background notifications still
stop the service rather than keeping it alive.

## Listener, port, address, and Bonjour

The host uses Apple's Network framework `NWListener` with TCP parameters and an
unspecified port. The port is read and displayed only from the listener's ready
state, matching Apple's documented rule that the listener port becomes
available when ready. A `_celeste-save._tcp` Bonjour service named **Celeste
Save Manager** is advertised only for that listener lifetime.

The address path uses the Darwin public `getifaddrs`/`freeifaddrs` boundary,
which compiled and ran under full AOT. It considers only up interfaces, rejects
loopback, tunnel/VPN, peer-to-peer, bridge, cellular, unspecified, multicast,
and IPv4 link-local candidates, ranks ordinary Ethernet/Wi-Fi-style interfaces
first, and displays at most three deterministic alternatives. Accepted IPv4 is
private LAN space; a usable non-link-local IPv6 address is bracketed correctly.
The exact physical address and port remain only in ignored evidence.

The first physical candidate used `.NET NetworkInterface` enumeration, which
threw `NetworkInformationException` on tvOS before listener creation. Replacing
that call with the narrow Darwin boundary fixed startup without a native
library or reflection dependency.

Current Apple [TN3179](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy)
explicitly lists tvOS as not supporting the Local Network privacy prompt. No
prompt appeared on the physical Apple TV. The bundle nevertheless includes a
focused `NSLocalNetworkUsageDescription` and the exact `NSBonjourServices`
declaration; no multicast, User Management, iCloud, App Group, or other paid
entitlement was added. Apple's
[`NWListener.port`](https://developer.apple.com/documentation/network/nwlistener/port)
and [`NSBonjourServices`](https://developer.apple.com/documentation/bundleresources/information-property-list/nsbonjourservices)
documentation informed the ready-state and service declarations.

## Authentication and HTTP boundary

Each activation creates a new six-digit code with
`RandomNumberGenerator.GetInt32`. The code is never persisted and routine logs
contain only `access-code=not-logged`. A compile-time-only physical automation
lane may emit it solely into ignored local acceptance evidence; the product
verifier rejects any production app containing that lane.

`POST /auth` is the only state-changing HTTP method and changes only in-memory
authentication state. Comparison is fixed-time. Success returns the manager
page directly with a new random 256-bit in-memory token in an `HttpOnly`,
`SameSite=Strict`, ten-minute cookie. Sessions slide while used and are erased
when the manager stops. The original 303-after-auth response was standards
valid but intermittently stranded the physical mobile browser; the direct 200
response removed that browser-specific transition without weakening the
authentication boundary.

The fixed routes are:

- `GET`/`HEAD /`
- `POST /auth`
- `GET`/`HEAD /download/all`
- `GET`/`HEAD /download/settings`
- `GET`/`HEAD /download/0`
- `GET`/`HEAD /download/1`
- `GET`/`HEAD /download/2`

The all-files route returns a deterministic `Celeste-saves.zip` containing the
present ordinary files in `settings`, `0`, `1`, `2` order. Entries use a fixed
timestamp and no recompression, and were byte-compared with every individual
export. It avoids mobile-browser multiple-download throttling while retaining
the individual routes.

All other paths and methods fail closed. The parser limits the request line to
2,048 bytes, headers to 16 KiB and 48 fields, the auth body to 64 bytes, request
lifetime to ten seconds, and simultaneous connections to four. It rejects
folding, duplicate headers, transfer encoding, inconsistent framing, request
bodies on GET/HEAD, malformed or encoded paths, traversal, and percent-encoded
ambiguity. Responses use explicit type and length, `no-store`, download
disposition, CSP, no-referrer, no-sniff, and connection close.

## Physical connection-limit diagnosis

The repeated real-browser failure was reproduced autonomously. Setting
`NWListener.ConnectionLimit = 4` on the physical tvOS binding acted as a
four-connection **lifetime** accept budget, not the reusable concurrent bound
the candidate intended. The listener accepted the access page, wrong or right
authentication, and enough requests to total four, then never accepted a
fifth. That precisely explained the observed pattern of two downloads after a
fresh authentication and another pair only after reopening Save Manager.

The native lifetime limit was removed. A repository-owned gate now enforces
four simultaneous connections and releases the slot on send completion,
native failure/cancellation, request timeout, the five-second defensive
response-close fallback, or server stop. The verifier rejects reintroduction of
the native limit.

Two independent full-AOT physical automation launches each accepted 37 TCP
connections. Each launch completed 16 sequential downloads, 16 four-wide
concurrent downloads, four initial individual downloads, and one all-files ZIP
after root/authentication requests. Observed concurrency peaked at four and
returned to zero; there were no request timeouts, response-close fallbacks,
managed fatals, missing payloads, or hash mismatches. The second activation
used a different access code. Process termination made the old URL unreachable.

A final user-assisted test used a real browser on another physical LAN device
against the production (automation-free) app. The wrong code was rejected, the
correct code authenticated, the authenticated page rendered, and **Download all
files (.zip)** downloaded successfully with all four expected `.celeste` entries.
After Back/Stop, the former numeric URL no longer connected. Controller and real
FMOD audio resumed, a normal Celeste save completed, and a clean second launch
restored the production Stage 9B state and reached first draw.

## Persistence export boundary

Opening the manager waits for `UserIO.Saving` to finish, requests a Stage 9B
flush/read-back verification, and captures one stable immutable snapshot.
`Stage6PersistenceStore` is the only component allowed to export data. It calls
the existing Stage 9B authority for exactly `settings`, `0`, `1`, and `2`, then
returns copies of the normal, uncompressed `.celeste` payloads. Absent slots
remain absent.

The web layer cannot access standard defaults, the production keys, A/B
generations, compressed entries, a session directory, or an arbitrary logical
name. Physical downloads for all four populated logical files matched the
hashes logged by the already serializer-validated snapshot on both automation
launches. Creating the read snapshot did not advance the durable generation.

## Lifecycle

The listener, Bonjour advertisement, active native connections, URL, access
code, and sessions are cleared on UI Back/Stop, overlay removal, scene end,
resign-active, background, listener failure, host disposal, process shutdown,
or twelve minutes of manager inactivity. It never silently restarts after a
background transition. Reopening performs a new flush/read-back check, address
selection, port bind, code generation, and session setup.

Normal gameplay logs `STAGE10A_DORMANT` before any explicit activation. The
physical automation runs reached the real Celeste first draw, restored the
existing Stage 9B generation, loaded all seven FMOD banks, advertised Bonjour,
served the complete request matrix, stopped cleanly, and launched again.

## Deterministic and product verification

The standalone protocol harness passes 36 tests covering CSPRNG shape,
authentication, direct authenticated page, session expiry and invalidation,
GET/HEAD, fixed individual and archive downloads, absent slots, hash metadata,
method/path/traversal/framing rejection, all parser bounds, connection-gate
reuse beyond four lifetime requests, response fallback, LAN selection, and
Darwin sockaddr parsing.

The arm64 simulator Release package passed the Stage 9B/10A static package
gates. A separate full-AOT simulator launch stopped in the pre-existing Stage 2
`LifecycleMonitor` UIKit-notification binding before Stage 10A construction:
the simulator attempted to JIT an `NSString` constructor wrapper in AOT-only
mode. No Save Manager listener was started. This does not occur in the required
full-AOT physical-device lane, which completed the production and automation
acceptance above; Stage 10A's simulator coverage is therefore the deterministic
protocol harness plus package validation, not a claimed simulator runtime pass.

Release `tvos-arm64` physical builds use full trimming, full AOT, and
`UseInterpreter=false`. The final production verifier requires the Stage 10A
tokens and Info.plist declarations and rejects the compile-time automation
token. The public builder reused the accepted Stage 1 packages, regenerated
the ignored game/audio/artwork inputs, produced and verified the signing-ready
unsigned IPA, and performed a same-identity Personal Team replacement install.

A genuinely new candidate clone then initialized every recursive submodule and
ran the complete public `--mode ipa --clean` path without local configuration or
generated outputs. It freshly rebuilt and verified Stage 1, regenerated all
ignored inputs, and produced a 895,480,226-byte signing-ready unsigned IPA with
SHA-256 `07337863dfb87d1221bfcaeb0c16b327f0a0952d5685141c92e8592444345415`.
The Stage 10A/8A package gates confirmed `Payload/Celeste.app`, arm64 TVOS,
tvOS 16.0, full AOT/trimming, seven banks, unsigned/no profile, Save Manager
product tokens, and a clean clone after generated-output isolation.

No generated Celeste source, game content, FMOD archive/header/bank, downloaded
save, ZIP, app bundle, IPA, LAN address, access code, signing value, profile,
device identifier, or raw console evidence is tracked. All such evidence lives
below ignored `.build/`, `artifacts/`, or `dist/` roots.

## Failures and corrections

1. `.NET NetworkInformation` enumeration failed physically; `getifaddrs`
   replaced it.
2. The tvOS screensaver resigned the maintenance screen; active-only idle
   suppression was added with complete restoration.
3. Redirect-after-auth stalled one mobile browser; successful auth now returns
   the authenticated page directly.
4. New-tab download links exposed mobile popup/download throttling; links now
   stay in-page and a one-action all-files ZIP is primary.
5. The decisive four-connection lifetime `NWListener.ConnectionLimit` bug was
   reproduced and removed; the reusable managed concurrent gate remains.
6. The free Personal Team profile expired during acceptance; the same existing
   Personal Team/profile was refreshed. No alternate identity or new app
   identity was used.
7. An additional full-AOT simulator launch exposed the pre-existing Stage 2
   UIKit notification-wrapper limitation described above. It was not hidden or
   attributed to the Save Manager; required physical AOT and simulator protocol
   validation remain passing.

## Remaining work

Stage 10A is read-only. Import, schema validation of externally supplied files,
replacement, deletion, conflict handling, destructive-operation confirmation,
and rollback UX belong to a later stage. The service is temporary HTTP on a
trusted same-LAN network, not TLS, cloud sync, internet access, or remote
administration. No claim is made that every router/VPN/interface topology or
browser has been certified.
