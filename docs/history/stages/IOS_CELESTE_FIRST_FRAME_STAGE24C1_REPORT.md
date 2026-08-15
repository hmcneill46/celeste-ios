# Stage 24C1 — canonical Celeste first frame and real FMOD on modern iOS

Status: **PASS**

Stage 24C1 connected the accepted modern iOS foundation to the same canonical
Celeste 1.4.0.0 FNA pipeline used by tvOS. A physical, full-AOT iPhone build
reached a stable title/menu through direct FNA3D Metal, loaded all seven real
FMOD banks, produced audible music and UI sounds, accepted DualSense menu input,
and preserved one runtime through repeated background/foreground cycles.

This is an experimental controller-first milestone, not a finished iOS port.
Touch controls and comprehensive gameplay/save/lifecycle acceptance remain
later work.

## Baseline and commits

- Starting commit: `e8802098b9ffcd772a70aed5d5e29e7cf9e32e01`
- Feature branch: `feature/modern-ios-celeste-first-frame`
- Accepted implementation commit: `dca52f46a15473f582c106cb7a59799d4b434b12`
- Acceptance-record commit: the branch-tip commit containing this report
- RC1 remained `ee52b0868df091746f134d95d4f020f94f23d4fb`
- RC2 remained `641e86e4ed164cdf93f602ce2f11436449654d6e`
- Deferred `origin/release/v1.0.0-rc.3` remained
  `c8134c8ca7924cf12f48527e714b5242c6024927`
- No RC3 tag was created and no GitHub Actions workflow was used

The real product build used the accepted user-owned
`itch-linux-fna-1.4.0.0` profile. No proprietary executable, generated source,
Content, FMOD bank, signing material, save, or device identifier was tracked.

## Canonical pipeline and locks

There is one canonical game pipeline. The iOS preparation command calls the
existing exact profile validator, Stage 3A–3C generation, Stage 5B audio
adaptation, and Stage 6 serializer/AOT pipeline. It then applies one bounded,
versioned iOS derivation. It does not contain another decompiler or a copied
920-file game tree.

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Canonical Content | 1,216 | `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46` |
| Canonical raw source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Canonical patched source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Shared Stage-6 real-audio tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |
| Final narrow iOS tree | 928 | `cbd740fef7f312ab961fc098f113046122c7babc86e70f78f68d88078c25b361` |

The final iOS transform removes tvOS-only Save Manager, QR, soft-reload,
Controller Prompts, Performance HUD, Leave, and compressed-persistence bridges;
adapts the FMOD output and Application Support storage boundaries; and removes
the desktop application-Quit row. It uses semantic iOS symbols and does not
compile iOS as tvOS.

All nine exact Celeste input profiles passed the 56-test closed-registry suite
and still normalize to canonical class `celeste-1.4.0.0-a`.

## Shared Apple product architecture

Stage 24C1 deliberately avoided a directory-wide ownership refactor. Where
low-risk and directly useful, iOS compiles the existing source rather than a
copy. Historical tvOS locations that now contain Apple-shared behavior are
identified below for a later cleanup after controller-gameplay acceptance.

| Component | Classification | Current owner/location | Recommended long-term owner/location | Changed during C1? | Rationale |
| --- | --- | --- | --- | ---: | --- |
| Nine-profile input registry and canonical class | Shared | `managed/`, `scripts/celeste-managed.py` | shared canonical input layer | No | Store/source OS is normalized before either Apple target. |
| Decompilation and source normalization | Shared | Stage 3 scripts under `scripts/` | shared Celeste generation layer | No | iOS invokes the exact existing pipeline; no second generator exists. |
| General Celeste/Monocle AOT transforms | Shared core + platform adapter | Stage 3/5/6 scripts and `managed/patches/` | shared Apple-safe transform core plus narrow target transforms | Yes, iOS orchestration only | Most changes are identical; product services and storage are platform-specific. |
| Content validation and staging | Shared core + platform adapter | profile registry and stage scripts | shared Content policy plus per-bundle staging adapter | Yes | Hash/inventory is common; resource-bundle placement is host-specific. |
| Settings and SaveData serializers | Shared | `managed/templates/TvOSSettingsSerializer.cs`, `TvOSSaveDataSerializer.cs` | semantically named shared Apple serializer templates | No implementation fork | iOS reuses and deterministically renames the exact serializers. |
| AOT/reflection inventory and linker roots | Shared | `tvos/CelesteTvOSRuntimeHost/CelesteLinker.xml` | shared Apple managed-runtime policy directory | No | iOS consumes the same closure; the present owner name is historical. |
| Legacy XNB readers | Shared | `tvos/CelesteTvOSRuntimeHost/LegacyXnbReaders.cs` | shared Apple FNA extensions | Yes, wiring only | iOS links the same source; the rejected alternative copy was removed. |
| Bundle `TitleContainer` implementation | Shared | `tvos/CelesteTvOSRuntimeHost/TvOSTitleContainer.cs` | shared Apple FNA extensions with a semantic name | Yes, wiring only | Both Apple products need the same bundle-safe reader. |
| FNA managed project | Shared core + platform adapter | `FNA/`, `tvos/FNA.TvOS`, `modern-ios/FNA.iOS` | shared FNA source plus target projects | Yes | Runtime code is common; SDK/native references differ. |
| FMOD managed game adaptation and seven-bank contract | Shared core + platform adapter | Stage 5B/6 generated transforms | shared FMOD game layer plus output adapter | Yes | Bank/event behavior is common; tvOS SDL output and iOS CoreAudio output differ. |
| FMOD native packaging | Shared core + platform adapter | tvOS/native builders and `scripts/prepare-fmod-ios.sh` | shared version/export policy plus target packagers | No broad move | SDK/version is common, platform archives are necessarily distinct. |
| Atomic game-state policy | Shared core + platform adapter | shared serializers plus `IOSStorageHooks` and tvOS persistence bridge | shared logical-file contract plus target persistence authorities | Yes | iOS uses native atomic Application Support files; tvOS uses compressed A/B UserDefaults. |
| Runtime/lifecycle concepts | Shared core + platform adapter | tvOS host and `IOSCelesteLifecycle` | small shared lifecycle contract plus UIKit target adapters | Yes | One-runtime and audio/haptic intent is common; OS callbacks differ. |
| Controller input | Shared core + platform adapter | FNA/SDL and platform hosts | shared FNA input plus target presentation/touch adapters | No new observer | DualSense uses the same FNA path; iOS touch remains separate and deferred. |
| Controller Prompt selection | Deferred | tvOS host/generated bridge | shared controller-family policy plus optional target UI adapters | No | iOS C1 needs raw controller menu input, not prompt-setting productionization. |
| Save Manager protocol/security/browser core | Deferred | tvOS host | reusable protocol core plus optional platform presentation/lifecycle adapters | No | iOS C1 intentionally has no listener or Save Manager. |
| High-level soft reload concepts | Deferred | tvOS host/generated bridge | shared state-reload concepts if iOS later adopts mutation features | No | Not needed for first frame and unsafe to generalize speculatively. |
| Graceful application Leave | tvOS-only | tvOS host/generated bridge | tvOS platform service | No | iOS must not pretend its exit policy is tvOS Home/Leave. Its desktop Quit row is simply omitted. |
| Metal Performance HUD preference/UI | tvOS-only in C1 | tvOS host | independently optional platform capability | No | iOS C1 does not bootstrap or expose the tvOS diagnostic feature. |
| Save Manager presentation/lifecycle | tvOS-only in C1 | tvOS host | tvOS adapter unless a future iOS product explicitly adopts it | No | Focus, Home lifecycle, and current user need are tvOS-specific. |
| UIWindowScene, safe areas, landscape presentation | iOS-only | `modern-ios/CelesteIOSRuntimeHost` and Stage 24B SDL patch | modern iOS host | Yes | These are genuine iPhone/iPad presentation boundaries. |
| Touch controls/UI | iOS-only, deferred | bounded Stage 24B state contract only | modern iOS input/presentation adapter | No | Stage 24D owns production touch controls. |
| Top Shelf and Siri Remote focus behavior | tvOS-only | tvOS product | tvOS platform layer | No | They have no iOS equivalent. |
| Build-time static-AOT Everest/mod experiment | Deferred | not implemented | canonical build-time transform layer above shared Apple runtime | No | One deterministic source pipeline and no JIT are the correct prerequisites. |

### Architecture answers

1. **One canonical pipeline:** yes; iOS and tvOS consume the same validated
   canonical source/Content and shared Stage 3–6 transforms.
2. **Genuinely shared today:** input profiles, canonicalization, most generated
   transforms, serializers, Content locks, AOT inventory, FNA source, legacy
   XNB readers, bundle reader, FMOD game adaptation, and bank expectations.
3. **Shared behavior in tvOS locations:** `LegacyXnbReaders.cs`,
   `TvOSTitleContainer.cs`, `CelesteLinker.xml`, and parts of historically named
   Stage scripts/templates are now consumed by both products.
4. **Future splits:** linker/FNA extensions should move to a semantic shared
   Apple location; Stage transforms should separate common game policy from
   tvOS persistence/Save Manager/Leave adapters; lifecycle intent can gain a
   tiny common contract after active-game evidence exists.
5. **Implement common fixes once:** normally yes at the canonical/shared
   transform or FNA layer. Current historical file names do not require copies.
6. **Independent platform features:** yes; the iOS derivation omits tvOS-only
   services, while UIKit storage/presentation is supplied by narrow iOS hooks.
7. **Conditionals stay narrow:** yes; semantic symbols appear at host/storage/
   native boundaries, not as widespread `IOS`/`TVOS` branches in game code.
8. **No second iOS game implementation:** confirmed. Generated iOS material is
   an ignored deterministic derivation of the single shared Stage-6 tree.
9. **Post-first-gameplay cleanup:** move historically tvOS-owned shared FNA and
   linker assets; give serializer/templates semantic names; extract only proven
   common lifecycle/controller-family concepts. Do this after C2, not by risking
   C1 acceptance.
10. **Static-AOT Everest suitability:** yes as a foundation. The design has one
    deterministic build-time source pipeline, fixed AOT closure, and optional
    platform adapters; it adds no JIT or runtime mod loader. Everest itself is
    not implemented or claimed.

## Host, rendering, and package

The product uses one managed entry, public `SDL_UIKitRunApp`, one direct
`Celeste.Run`, one FNA `Game`, and one Celeste-owned FMOD Studio system. The
probe host is a separate build mode and cannot initialize another FMOD runtime
inside the Celeste product.

- Toolchain: macOS 26.3 arm64; Xcode 26.6 (17F113), iOS SDK 26.5;
  .NET SDK 10.0.302/workload 26.5.10301
- Target: `net10.0-ios26.5`, minimum iOS 15.0, arm64 IOS
- Configuration: Release, LLVM, full trimming, full AOT,
  `UseInterpreter=false`, no JIT entitlement or interpreter library
- Direct renderer: FNA3D Metal; no MoltenVK
- Final publish/package time: 369 seconds
- IPA size: 880,673,624 bytes
- IPA SHA-256:
  `6b948abcc9621ce2b47154cb18d534248b51916718562da9e78ddc2f44978a34`
- iOS native lock:
  `9fb302d221180e39f270ea5ebf48e18433b67bd0a40943c042a227fe0f8ad6a2`
- tvOS native lock:
  `6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`

The package includes exactly the locked 1,216-file Content aggregate and seven
banks. It advertises both iPhone and iPad families, both landscape
orientations, and no file-sharing entitlement. iPad packaging is verified;
real-Celeste iPad physical presentation is not claimed in C1.

The real-Celeste simulator lane remains deliberately unavailable because FMOD
1.10.09 provides no arm64 Apple-silicon Simulator slice. The already accepted
Stage 24B foundation simulator continues to exercise iPhone/iPad Metal,
orientation, safe-area, aspect-fit, and same-runtime lifecycle without game
audio. C1 does not substitute a newer FMOD SDK or an interpreter.

## Physical iPhone acceptance

A development-signed, same-identity replacement preserved the app container.
Privacy-safe device logs proved:

- direct `FNA3D Driver: Metal`;
- first Celeste update and draw in `Celeste.GameLoader`;
- a 2796×1290 Retina backbuffer;
- one host startup and one Celeste/FNA runtime per process;
- FMOD native version `0x00011009` (1.10.09) with managed API 1.10.20;
- all seven banks ready, exposing 922 events, 118 buses, and 3 VCAs;
- no missing native symbol, managed fatal, interpreter, or JIT requirement.

Physical observation confirmed a stable title/main menu, audible title/game
music and UI/game SFX, DualSense navigation/Confirm/Cancel, both landscape
orientations without crop/stretch/portrait presentation, and three repeated
background/foreground cycles with rendering, audio, and controller recovery on
the original runtime. The desktop application-Quit row is absent.

The controller-first product exceeded the minimum C1 smoke: Prologue and
Chapter 1 were played successfully, including the expected chapter-dependent
DualSense light color. This additional observation is useful evidence but does
not replace the comprehensive gameplay/death/recovery matrix assigned to C2.

### AOT storage defect found and fixed

The first physical candidate serialized Settings and SaveData correctly but
its `System.IO` atomic writer reached a JIT-generated `SafeFileHandle`
constructor stub, which full-AOT iOS rejected. Celeste displayed `Save Failed`;
the user correctly continued without treating that candidate as accepted.

The narrow fix retained the four-file allowlist and Application Support root,
but moved the actual atomic replacement to the public native Foundation
`NSData` API. This is the correct shared-core/platform-adapter boundary rather
than weakening AOT or copying tvOS persistence.

The rebuilt physical candidate logged successful atomic writes for
`settings.celeste` and slot `0.celeste` with no save/AOT exception. After real
app-switcher termination and cold relaunch, Settings and SaveData both restored
physically; startup independently recorded that Settings existed. The durable
files remain confined to `Library/Application Support/Celeste/{Saves,Backups}`.
Broad three-slot failure/recovery stress remains C2 scope.

## Verification and regressions

- Stage 24C1 source/product verifier: 70 checks
- Modern iOS foundation deterministic tests: 31
- Exact input-profile tests: 56
- iOS native verifier: accepted five-component device/Simulator lock
- Full package verifier: PASS
- Current tvOS chain: Stage 9B persistence; Stage 10 protocol 66; Stage 11
  prompts 38; Stage 12B Quit 16; Stage 13B reload 21; Stage 15 QR 31;
  Stage 16B HUD 39; Stage 22B continuity 57 plus 85 source/product checks
- Stage 8B repository/docs and Stage 14 feature/link verifiers: PASS
- Shell syntax, Python compilation, documentation links, `git diff --check`,
  and repository/privacy gates: PASS

The privacy manifest still declares no tracking or collected data and only the
accepted app-container file-timestamp reason. iOS adds no listener, Bonjour,
analytics, cloud service, Save Manager, HUD bootstrap, Top Shelf, or unrelated
entitlement. Generated/proprietary/private evidence remains ignored.

## Known limitations and Stage 24C2 recommendation

- No touch controls or touch UI; a physical controller is required.
- Real-Celeste Simulator audio/execution is unavailable with FMOD 1.10.09.
- No iOS Save Manager, QR, soft reload, Performance HUD, document import, or
  graceful-Leave feature is claimed.
- iPad is package-compatible but does not yet have real-game physical
  acceptance or polished platform UI.
- Save migration, three-slot recovery/fault stress, and long active-game
  lifecycle acceptance are not complete.
- No App Store workflow and no Everest/mod support exist.

Stage 24C2 should retain this exact canonical/shared architecture and focus on
sustained controller-first gameplay, death/respawn, chapter transitions,
three-slot save/load and atomic-recovery stress, process and lifecycle restore
during active Levels, Settings/SaveData compatibility, controller-family prompt
policy where appropriate, and real iPad presentation if hardware is available.
Only after that evidence should historically tvOS-located shared FNA/linker
assets be moved or broader lifecycle abstractions be extracted. Stage 24D
should remain the separate production touch-controls stage.
