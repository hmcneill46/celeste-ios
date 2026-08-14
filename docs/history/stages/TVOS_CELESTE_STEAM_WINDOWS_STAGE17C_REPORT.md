# Stage 17C — Steam Windows FNA input and acquisition guides

## Result and baseline

Stage 17C is **PASS — canonical-equivalent** on
`feature/steam-windows-input`, starting from integration commit
`cf2ccd30c98acca7f8789cfe1e5227d40c2f179a`. The immutable
`v1.0.0-rc.1` tag continued to dereference to
`ee52b0868df091746f134d95d4f020f94f23d4fb`.

The newly supplied archive has SHA-256
`c83b01cc6ba64af1681ed05e0b8ae8144980eae4195d1691acefbac606ee2068`.
It was obtained for project compatibility testing from Steam app `504230`,
Windows depot `504231`, manifest `1981411158533599226`, branch `opengl`.
DepotDownloader was used because Steam Console did not retrieve that historical
branch on the test host. This is provenance only: DepotDownloader and the
Windows branch are deliberately not recommended in the public acquisition
guide.

## Archive and runtime audit

The original archive was not modified. A traversal-safe extraction rejected
absolute paths, parent traversal, and links before writing an ignored copy. It
contained one bounded wrapper, 1,229 files, and 1,167,615,009 uncompressed
bytes. DepotDownloader metadata was treated only as local provenance and was
not required by the production profile.

The payload is Celeste 1.4.0.0 using FNA 21.3.5, not XNA. Its managed game
closure is the accepted Steam closure:

- `Celeste.exe` is byte-identical to all three previously accepted Steam
  profiles (`3ee734…`);
- `FNA.dll` is byte-identical to those profiles (`b6c7f5…`);
- `Celeste.Content.dll` is the accepted empty identity assembly (`0b8d61…`);
- the Celeste reference graph is unchanged and includes Steamworks.NET 10.0.0.0.

The Windows `Steamworks.NET.dll` has the same assembly identity and API surface
as the accepted Unix build but a platform-specific binary hash (`6c6b30…`
instead of `92c467…`). Inspection showed the expected ABI distinction:
Windows callback structures use pack 8 while the Unix library uses pack 4.
Stage 17C therefore records a separate exact Windows managed-payload lock; it
does not weaken the existing Steam payload. The dependency is used only to
decompile Celeste and is removed by the existing exact Steam adapter before the
shared tvOS pipeline.

The package uses the already bounded `windows-game-root` resolver. Exact
Windows/Steam markers lock `SDL2.dll`, `steam_api.dll`, and `CSteamworks.dll`;
the profile does not rely on archive names or recursive executable discovery.

## Content and canonicalization

The complete Content tree is byte-identical to the accepted class:

- 1,216 files, 1,158,665,183 bytes;
- Content SHA-256
  `30a1c147d1a3ab0aa45762094e393ed7fd69951dd66e5af063447641e0699c46`;
- all seven FMOD banks match the accepted 665,721,576-byte bank tree.

The unchanged Stage 17B validator first rejected the archive as an unregistered
exact build, while correctly reporting Celeste 1.0.0.0 assembly identity and
FNA 21.3.5. The production profile is:

`steam-windows-fna-1.4.0.0-manifest-1981411158533599226`

It selects the existing `steam-1.4.0.0-to-canonical-a` zero-fuzz adapter and
canonical class `celeste-1.4.0.0-a`. Independent production generation reached
the existing exact locks:

| Boundary | Files | Logical SHA-256 |
| --- | ---: | --- |
| Decompiled canonical source | 920 | `db7b722159fbdef8625c81608165aea162957dce956fcd5b16eeceaa1089a273` |
| Patched canonical source | 922 | `0c6515adadf58251ab2da66c6c61a1f61bd19b85f5dcdd4f1280552af650a4b5` |
| Stage 6 real-audio generated tree | 934 | `1a981bc5994261775c1fea497cd042780fdbbcb24b7390a2f4db6aab1be747d9` |

No Steam Windows-specific data survives into the shared tvOS generation. The
accepted native graph remains
`6286e0545b32e9c56732955d4cf816ed8f5dc0d816ab610dd9fe1752090a01fc`.

## Build deduplication decision

Another full IPA and Apple TV installation were deliberately omitted. This
profile uses the Windows resolver already physically covered by Epic Windows,
the Steam executable and exact adapter already physically covered by Steam
Linux, and the same Content/raw/patched/Stage 6 locks as every accepted input.
Stage 17B established this exact pre-downstream equivalence as genuine product
support. Rebuilding the same approximately 895 MB product would exercise no new
code path and would consume substantial time and disk space without adding
evidence.

The resulting registry contains nine exact profiles in one canonical product
class. Unknown Steam Windows builds, modified assemblies or Content, mixed
store payloads, XNA, ambiguous wrappers, and other versions remain rejected.

## Public acquisition guidance

The README now provides compact, collapsible guides:

- itch.io owners download and extract one exact unmodified 1.4.0.0 FNA package
  from their own library;
- Steam owners are directed to Steam's built-in console and the tested public
  Linux depot command
  `download_depot 504230 504233 5880027853585448535`;
- Epic owners may optionally use the official Legendary CLI with 64-bit Python
  3.10 or newer, discover their account's Celeste App Name, and download one
  supported Mac or Windows payload.

The tested Epic account reported App Name `Salt`, but the docs use a placeholder
and require users to confirm their own result. Steam itself and Legendary are
optional acquisition tools only; neither is a builder dependency, and no store
credentials are stored by this project.

## Validation and repository boundary

The exact-profile policy covers modified Celeste, FNA, Steamworks, Content,
mixed-store, XNA, marker, and ambiguous-layout rejection. The existing Stage
9B/10/11/12B/13B/15/16B and Stage 14 repository/product gates remain in the
Stage 17C verifier chain. Stage 17 input/profile policy passed **56** tests;
the inherited suites passed Stage 10 **66**, Stage 11 **38**, Stage 12B **16**,
Stage 13B **21**, Stage 15 **31**, and Stage 16B **39**, plus Stage 9B and the
Stage 14 repository/product checks. The public validator and builder
validation-only mode both identified the exact Steam Windows profile. Builder
help, host doctor, documentation commands, and **70** repository documentation
links passed. A small verifier-only Bash 3 correction now guards the existing
Stage 17B wrapper's empty argument array under `set -u`; product behavior is
unchanged. A post-push clean feature-branch clone repeated automatic detection,
canonical generation, host doctor, verifiers, and repository privacy checks.

No Celeste file, generated/decompiled source, Steam/Epic credential, store
library, FMOD bank, local private path, save, or signing value is tracked.
