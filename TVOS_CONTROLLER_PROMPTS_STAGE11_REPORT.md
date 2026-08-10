# tvOS Controller Prompts — Stage 11 Report

## Result

Stage 11 started from commit
`6c44d6e7017d2c5551d6ca9121110edf47757eb6` on `tvos-port`. The final commit
is recorded by Git rather than embedded recursively in this file.

The implementation adds one tvOS-only **Controller Prompts** slider to the
existing Celeste Options menu. It selects artwork only. No gameplay binding,
FNA button value, SaveData field, Settings XML element, or Stage 9B persistence
entry is changed.

The same-identity physical run and the signed/unsigned product gates passed.

## Locked Celeste 1.4.0.0 inventory

The repository-owned inventory parser reads only the user's validated
`Content/Graphics/Atlases/Gui.meta` metadata. It verifies the locked SHA-256,
422-path count, four family prefixes, and the 24 controller inputs accepted by
the generated Settings/remapping graph. It writes only path names and counts to
an ignored privacy-safe manifest; no atlas pixels or game content are tracked.

| Family | Prefix | Family assets | Direct required inputs | Existing Celeste fallback inputs | Complete/selectable |
| --- | --- | ---: | ---: | ---: | --- |
| Xbox | `controls/xb1` | 10 | 10 | 14 | yes, 24/24 |
| PlayStation | `controls/ps4` | 15 | 14 | 10 | yes, 24/24 |
| Nintendo Switch | `controls/ns` | 24 | 24 | 0 | yes, 24/24 |
| Stadia | `controls/stadia` | 10 | 10 | 14 | yes, 24/24 |

The fallback counts are Celeste's own deliberate `controls/fallback` artwork,
not newly synthesized assets. All five requested menu choices—Automatic plus
the four manual families—are therefore available. Nintendo selection changes
which family draws the existing logical binding; it does not introduce
Nintendo-style semantic button swapping.

## Existing selection mechanism and generated bridge

Locked Celeste already routes controller artwork through
`Input.GuiInputPrefix`. The game knows exact GUIDs for two PlayStation
controllers, two Nintendo controllers, and Stadia, and otherwise uses its Xbox
family for an attached unknown gamepad. Stage 11 renames that exact body to an
internal automatic helper and wraps only its returned atlas prefix.

The deterministic Stage 6 transformation copies
`TvOSControllerPromptBridge.cs`, inserts one normal `TextMenu.Slider` beside
the existing Save Manager entry, and adds no `Oui` subtype. The bridge exposes
only requested mode, mode change, and effective prefix callbacks. Both clean
generated trees reproduced exactly:

| Generated tree | Files | Logical SHA-256 |
| --- | ---: | --- |
| no-audio | 930 | `30a21aa33aa9db47d0d1c6d958ca2712276b454b12fcf604caed18989a53b8ec` |
| real audio | 931 | `1e719cc8de333c4d1750701038284a6d4174d4ea598861e6b480febdad1a75ff` |

Their normalized manifests were byte-identical across independent generation
roots. Generated Celeste source and content remain ignored.

## Preference storage and isolation

The host component stores exactly one validated string in
`NSUserDefaults.StandardUserDefaults`:

```text
key: CelesteTvOS.ControllerPrompts.v1
values: Automatic | Xbox | PlayStation | NintendoSwitch | Stadia
```

Missing, corrupt, or unsupported values resolve to Automatic. An unchanged
selection performs no write. No controller identity, vendor name, Apple object,
or detected effective family is persisted.

The locked generated `Settings.cs` and `TvOSSettingsSerializer.cs` are
byte-for-byte unchanged from their pre-Stage-11 inputs. The key does not enter
Settings, SaveData, the compressed A/B envelope, bridge size accounting, or the
Save Manager's fixed four-name boundary. Consequently Settings replace/reset
does not alter Controller Prompts, and changing prompts does not create a
Stage 9B generation. The existing `CA92.1` UserDefaults privacy declaration
already covers this additional tiny app-private preference; no new privacy API
category or entitlement is introduced.

## Manual and Automatic resolution

Manual modes are absolute:

| Mode | Effective family prefix |
| --- | --- |
| Xbox | `xb1` |
| PlayStation | `ps4` |
| Nintendo Switch | `ns` |
| Stadia | `stadia` |

Automatic uses the following deterministic order:

1. preserve Celeste/FNA's exact `ps4`, `ns`, or `stadia` GUID result;
2. if Celeste returned its generic Xbox fallback, use the current eligible
   Apple controller's authoritative DualSense/DualShock 4 → PlayStation or
   Xbox → Xbox product category;
3. preserve keyboard state where applicable;
4. otherwise retain Celeste's safe Xbox/default fallback.

The host observes connect, disconnect, did-become-current, and
did-stop-being-current notifications instead of polling each frame. It compares
native controller handles, ignores non-extended and Apple Remote categories,
prefers `GCController.current`, and then uses stable enumeration order. Unknown
controllers do not crash or produce blank glyphs.

Current Apple documentation used for this decision:

- [GCController](https://developer.apple.com/documentation/gamecontroller/gccontroller)
- [Product category constants](https://developer.apple.com/documentation/gamecontroller/product-category-constants)
- [Controller current-state notifications](https://developer.apple.com/documentation/gamecontroller/gccontrollerdidbecomecurrentnotification)

Apple exposes authoritative DualSense, DualShock 4, and Xbox categories but no
dedicated Nintendo Switch or Stadia constants in the accepted SDK. Stage 11
therefore makes no broad vendor-string guess. Nintendo/Stadia Automatic works
only where Celeste/FNA's locked exact identity already recognizes the device;
manual modes remain available for every controller.

## Deterministic and rendering tests

The pure policy suite contains 38 deterministic tests covering fixed-key
parsing, all value round trips, invalid fallback, no-op writes, all manual
mappings, known FNA families, DualSense/DualShock/Xbox categories, unknown and
keyboard fallbacks, Siri Remote exclusion, current/multiple/disconnected
controllers, and binding/save-byte isolation.

The locked atlas inventory separately resolves every one of the 24 required
logical inputs for every manual family. The generated verifier proves:

- `Settings.cs` is unchanged;
- the exact Settings XML serializer is unchanged;
- the slider and prefix hook appear exactly once;
- no host key appears in Settings, SaveData, Stage 9B, or Stage 10B sources;
- no generated artwork, GUI atlas, save, FMOD bank, IPA, or profile is tracked;
- built simulator/device/IPA products contain the selector boundary.

The trimmed arm64 simulator host and signed Release `tvos-arm64` product
compiled with full trimming, full AOT, and `UseInterpreter=false`, without new
Stage 11 trim or AOT warnings. Existing generated Celeste warning baselines
remain unchanged.

On the physical Apple TV, the existing production v2 persistence generation
restored before the game entered, the first Celeste draw completed, and all
seven FMOD banks loaded. Apple Game Controller then reported the connected
extended DualSense as the PlayStation family. The user selected and visually
accepted Xbox, PlayStation, Nintendo Switch, and Stadia artwork, returned to
Automatic and observed PlayStation artwork, and confirmed movement, Jump,
Dash, Grab, Pause, Confirm, and Cancel remained unchanged.

A manual PlayStation choice survived a terminated-process relaunch. The user
then returned the preference to Automatic; a second terminated-process relaunch
restored Automatic and again resolved the connected DualSense to PlayStation.

The same run opened Save Manager, authenticated from an external browser,
downloaded ordinary files, and exited through Back. The listener stopped and
normal gameplay/audio resumed. The read snapshot and subsequent Settings exits
were logically unchanged at the same Stage 9B generation. Static key-isolation
verification proves that Settings replace/reset cannot address the host prompt
key; the accepted Stage 10B mutation and restart-required implementation was
otherwise unchanged.

## Acceptance results

| Gate | Result |
| --- | --- |
| Locked family inventory | PASS; all four manual families resolve 24/24 |
| Deterministic policy tests | PASS; 38/38 |
| Independent managed regeneration | PASS; both manifests identical |
| Settings XML / SaveData isolation | PASS; byte/logical identity unchanged |
| Stage 9B regression | PASS; v2 generation restored and unchanged by prompt/download-only use |
| Stage 10B regression | PASS; 66 protocol tests plus real browser download/Back lifecycle |
| Full-AOT trimmed device publish | PASS; no interpreter |
| Signing-ready unsigned IPA | PASS; 854 MiB, Stage 8A and Stage 11 verification |
| Same-identity physical visual matrix | PASS; all four manual families user-verified |
| DualSense Automatic → PlayStation | PASS; authoritative product category and visual result |
| Restart persistence | PASS; manual and Automatic modes restored independently |
| Save Manager preference independence | PASS; separate fixed key and unchanged four-file boundary |

## Public build and fresh-clone result

The normal checkout produced and verified a same-identity signed device app
and a signing-ready unsigned IPA. The signed app installed as a replacement,
preserved the existing production saves, reached first draw, loaded all seven
FMOD banks, and passed the physical matrix above. Its companion unsigned IPA
was 854 MiB and passed the Stage 8A and Stage 11 package verifiers.

A second genuinely clean candidate clone contained no repository-local native,
managed, content, artwork, app, IPA, or configuration outputs. The documented
recursive submodule command and host doctor passed. Its full public builder
path rebuilt Stage 1 and reproduced logical set SHA-256
`61c1d97b7a585144b2b60ec0ed46f2d70f1f6239ec1732a17cc3101c3293fe39`,
regenerated the exact Stage 11 managed hashes, and emitted an 854 MiB
signing-ready IPA with SHA-256
`b53e6fc8cf0f08ad996690900cd4bf91fa8a330b2b75a6787dd2d22be4af8900`.
The full Stage 8A/9B/10B/11 verifier chain passed against that package and the
fresh tree exposed no generated or proprietary Git candidates.

## Deliberate evidence limits

DualSense was the only physical controller available. Xbox Automatic is based
on Apple's authoritative public product category and deterministic tests, not
a second physical controller. Nintendo Switch and Stadia Automatic are claimed
only for Celeste/FNA's exact locked identities; their manual artwork modes were
physically visual-checked with the DualSense driving the menu. A separate Apple
TV operating-system restart was not repeated solely for this scalar: process
termination/relaunch and same-identity replacement were tested, while Stage 6
already physically proves standard app-private UserDefaults across Apple TV
restart. Settings replace/reset independence is proved by the disjoint fixed
key boundary and deterministic Stage 10B/11 tests without destructively
changing the user's production Settings during this acceptance.

## Public and legal boundary

Tracked additions are host/platform source, a metadata-only content lock and
inventory tool, deterministic tests/verifiers, transformation/template changes,
documentation, and this report. They contain no proprietary controller artwork,
Celeste source/content, FMOD material, saves, signing values, device/controller
identifiers, or private paths.
