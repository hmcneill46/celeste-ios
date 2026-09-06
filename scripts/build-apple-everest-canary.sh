#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
PROFILE="$REPO_ROOT/apple-everest/profiles/stable-1.6458.0.json"
UPSTREAM="$REPO_ROOT/.build/apple-everest/upstream/Everest"
WORK_ROOT="$REPO_ROOT/.build/apple-everest/production-canary"
CLOSURE="$WORK_ROOT/shared-closure"
OUTPUT="$REPO_ROOT/artifacts/apple-everest/canary"
DOTNET8="$REPO_ROOT/.build/apple-everest/toolchain/dotnet8/dotnet"
DOTNET9="$REPO_ROOT/.build/apple-everest/toolchain/dotnet9/dotnet"
BUILDER_PROJECT="$REPO_ROOT/tools/AppleEverestBuilder/AppleEverestBuilder.csproj"
IL_WORKER_PROJECT="$REPO_ROOT/tools/AppleEverestIlWorker/AppleEverestIlWorker.csproj"
PLATFORM="all"
SIGNING="unsigned"
TEAM_ID=""
IOS_BUNDLE_ID=""
TVOS_BUNDLE_ID=""
IOS_DEVICE_ID=""
TVOS_DEVICE_ID=""
PREPARE_ONLY=0
CLEAN=0
REUSE_BUILD=0
CONFIGURED_FIXTURE=0
FACTORY_CLOSURE=""
FACTORY_PREFLIGHT=""
AUTHORED_FACTORY_PROFILES=""
MODS=()

usage() {
  cat <<'EOF'
Usage: scripts/build-apple-everest-canary.sh [options]

Internal shared Apple Everest full-AOT product builder. It never changes the
normal vanilla iOS or tvOS build paths. With no --mod it uses the tracked canaries.

Options:
  --platform ios|tvos|all  target platform(s), default all
  --signing unsigned|development
  --team-id ID             local development team (never persisted by this script)
  --ios-bundle-id ID       separate experimental iOS identity
  --tvos-bundle-id ID      separate experimental tvOS identity
  --ios-device-id ID       provision the iOS canary for this local device
  --tvos-device-id ID      provision the tvOS canary for this local device
  --mod ZIP_OR_DIR         explicit ordinary Everest input; may be repeated
  --prepare-only           generate and apply the shared closure without publishing
  --clean                  replace only marked prior canary output
  --reuse-build            package an already-marked completed AOT build
  --configured-fixture     build one exact hash-locked configured-detour fixture
  --factory-closure JSON   validate selected factory closure before generation
  --factory-preflight JSON package-backed selected graph; requires authored profiles
  --authored-factory-profiles JSON exact extracted selected profiles for preflight
  --work-root DIRECTORY    isolated ignored build root below .build/apple-everest
  --output DIRECTORY       isolated product root below artifacts/apple-everest
  -h, --help               show this help
EOF
}

while (($#)); do
  case "$1" in
    --platform) PLATFORM="$2"; shift 2 ;;
    --signing) SIGNING="$2"; shift 2 ;;
    --team-id) TEAM_ID="$2"; shift 2 ;;
    --ios-bundle-id) IOS_BUNDLE_ID="$2"; shift 2 ;;
    --tvos-bundle-id) TVOS_BUNDLE_ID="$2"; shift 2 ;;
    --ios-device-id) IOS_DEVICE_ID="$2"; shift 2 ;;
    --tvos-device-id) TVOS_DEVICE_ID="$2"; shift 2 ;;
    --mod) MODS+=("$2"); shift 2 ;;
    --prepare-only) PREPARE_ONLY=1; shift ;;
    --clean) CLEAN=1; shift ;;
    --reuse-build) REUSE_BUILD=1; shift ;;
    --configured-fixture) CONFIGURED_FIXTURE=1; shift ;;
    --factory-closure) FACTORY_CLOSURE="$2"; shift 2 ;;
    --factory-preflight) FACTORY_PREFLIGHT="$2"; shift 2 ;;
    --authored-factory-profiles) AUTHORED_FACTORY_PROFILES="$2"; shift 2 ;;
    --work-root) WORK_ROOT="$2"; shift 2 ;;
    --output) OUTPUT="$2"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
WORK_ROOT="$(python3 - "$REPO_ROOT/.build/apple-everest" "$WORK_ROOT" <<'PY'
import pathlib,sys
base,value=map(lambda value:pathlib.Path(value).resolve(),sys.argv[1:])
if value==base or not value.is_relative_to(base):raise SystemExit('work root must be below .build/apple-everest')
print(value)
PY
)"
OUTPUT="$(python3 - "$REPO_ROOT/artifacts/apple-everest" "$OUTPUT" <<'PY'
import pathlib,sys
base,value=map(lambda value:pathlib.Path(value).resolve(),sys.argv[1:])
if value==base or not value.is_relative_to(base):raise SystemExit('output must be below artifacts/apple-everest')
print(value)
PY
)"
CLOSURE="$WORK_ROOT/shared-closure"
if [[ -n "$FACTORY_PREFLIGHT" || -n "$AUTHORED_FACTORY_PROFILES" ]]; then
  (( ! REUSE_BUILD )) || { echo "error: package-backed factory products require a fresh AOT build" >&2; exit 2; }
  [[ -f "$FACTORY_PREFLIGHT" && -f "$AUTHORED_FACTORY_PROFILES" ]] || {
    echo "error: factory preflight requires both graph and authored profile files" >&2; exit 2; }
  FACTORY_PREFLIGHT="$(python3 -c 'import pathlib,sys;print(pathlib.Path(sys.argv[1]).resolve())' "$FACTORY_PREFLIGHT")"
  AUTHORED_FACTORY_PROFILES="$(python3 -c 'import pathlib,sys;print(pathlib.Path(sys.argv[1]).resolve())' "$AUTHORED_FACTORY_PROFILES")"
fi
if [[ -n "$FACTORY_CLOSURE" ]]; then
  [[ -f "$FACTORY_CLOSURE" ]] || { echo "error: factory closure does not exist" >&2; exit 2; }
  FACTORY_CLOSURE="$(python3 -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "$FACTORY_CLOSURE")"
fi
for index in "${!MODS[@]}"; do
  [[ -e "${MODS[$index]}" ]] || { echo "error: mod input does not exist: ${MODS[$index]}" >&2; exit 2; }
  MODS[$index]="$(python3 -c 'import pathlib,sys; print(pathlib.Path(sys.argv[1]).resolve())' "${MODS[$index]}")"
done
for device in "$IOS_DEVICE_ID" "$TVOS_DEVICE_ID"; do
  [[ -z "$device" || "$device" =~ ^[A-Fa-f0-9-]{24,40}$ ]] || { echo "error: invalid device identifier" >&2; exit 2; }
done
[[ "$PLATFORM" == ios || "$PLATFORM" == tvos || "$PLATFORM" == all ]] || { echo "error: invalid --platform" >&2; exit 2; }
[[ "$SIGNING" == unsigned || "$SIGNING" == development ]] || { echo "error: invalid --signing" >&2; exit 2; }
if [[ "$SIGNING" == development && ! "$TEAM_ID" =~ ^[A-Z0-9]{10}$ ]]; then
  echo "error: development signing requires --team-id" >&2; exit 2
fi
for identity in "$IOS_BUNDLE_ID" "$TVOS_BUNDLE_ID"; do
  [[ -z "$identity" || "$identity" =~ ^[A-Za-z][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)+$ ]] || { echo "error: invalid bundle identifier" >&2; exit 2; }
done

local_identity() {
  python3 - "$1" <<'PY'
import pathlib,re,sys
p=pathlib.Path(sys.argv[1])
if p.exists():
    m=re.search(r"<ApplicationId>([^<]+)</ApplicationId>",p.read_text())
    if m: print(m.group(1).strip())
PY
}
[[ -n "$IOS_BUNDLE_ID" ]] || { base="$(local_identity "$REPO_ROOT/modern-ios/Local.Build.props")"; [[ -n "$base" ]] && IOS_BUNDLE_ID="${base}.everestcanary"; }
[[ -n "$TVOS_BUNDLE_ID" ]] || { base="$(local_identity "$REPO_ROOT/tvos/Local.Build.props")"; [[ -n "$base" ]] && TVOS_BUNDLE_ID="${base}.everestcanary"; }
if (( ! PREPARE_ONLY )); then
  [[ "$PLATFORM" == tvos || -n "$IOS_BUNDLE_ID" ]] || { echo "error: provide --ios-bundle-id" >&2; exit 2; }
  [[ "$PLATFORM" == ios || -n "$TVOS_BUNDLE_ID" ]] || { echo "error: provide --tvos-bundle-id" >&2; exit 2; }
fi

safe_replace() {
  local target="$1" marker="$2"
  if [[ -e "$target" ]]; then
    ((CLEAN)) || { echo "error: output exists; use --clean: $target" >&2; exit 1; }
    [[ -f "$target/$marker" ]] || { echo "error: refusing unmarked canary output: $target" >&2; exit 1; }
    [[ "$target" == "$WORK_ROOT"/* || "$target" == "$OUTPUT" ]] || { echo "error: unsafe cleanup target" >&2; exit 1; }
    find "$target" -depth -delete
  fi
}

"$SCRIPT_DIR/bootstrap-apple-everest-host.sh"
(cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- acquire --profile "$PROFILE" --output "$UPSTREAM")
(cd "$UPSTREAM/external/MonoMod" && "$DOTNET9" build src/MonoMod.Utils/MonoMod.Utils.csproj \
  -c Release -f net8.0 -p:RestoreLockedMode=false --nologo >/dev/null)
(cd "$UPSTREAM/external/MonoMod" && "$DOTNET9" build src/MonoMod.RuntimeDetour/MonoMod.RuntimeDetour.csproj \
  -c Release -f net8.0 -p:RestoreLockedMode=false --nologo >/dev/null)
MONOMOD_UTILS="$UPSTREAM/external/MonoMod/artifacts/bin/MonoMod.Utils/release_net8.0/MonoMod.Utils.dll"
MONOMOD_RUNTIME_DETOUR="$UPSTREAM/external/MonoMod/artifacts/bin/MonoMod.RuntimeDetour/release_net8.0"
[[ -f "$MONOMOD_UTILS" ]] || { echo "error: exact pinned MonoMod.Utils host build is missing" >&2; exit 1; }
for host_dependency in MonoMod.RuntimeDetour.dll MonoMod.Core.dll MonoMod.Iced.dll; do
  [[ -f "$MONOMOD_RUNTIME_DETOUR/$host_dependency" ]] || {
    echo "error: exact pinned direct-ILHook host dependency is missing: $host_dependency" >&2; exit 1; }
done
(cd "$REPO_ROOT" && dotnet restore "$IL_WORKER_PROJECT" \
  -p:MonoModUtilsPath="$MONOMOD_UTILS" --locked-mode --nologo >/dev/null)
(cd "$REPO_ROOT" && dotnet build "$IL_WORKER_PROJECT" -c Release --no-restore \
  -p:MonoModUtilsPath="$MONOMOD_UTILS" --nologo >/dev/null)
for host_dependency in MonoMod.RuntimeDetour.dll MonoMod.Core.dll MonoMod.Iced.dll; do
  cp "$MONOMOD_RUNTIME_DETOUR/$host_dependency" \
    "$REPO_ROOT/tools/AppleEverestIlWorker/bin/Release/net10.0/$host_dependency"
done
safe_replace "$CLOSURE" .apple-everest-static-closure
if ((${#MODS[@]} == 0)); then
  MODS=(
    "$REPO_ROOT/apple-everest/canaries/content"
    "$REPO_ROOT/apple-everest/canaries/module-a"
    "$REPO_ROOT/apple-everest/canaries/module-b"
    "$REPO_ROOT/apple-everest/canaries/module-c"
  )
fi
# The exact Stage 25H fixture has no playable map of its own. Mount the
# project-owned data-only room in Canary products so physical acceptance tests
# the real frozen spinner/block behavior rather than only successful startup.
static_il_fixture_sha="677e8fbd067340d7b3133cc908e4ecafc0f5deab2c38b7eeb79a62eb5f61d523"
static_il_compose_fixture_sha="df291c0175df46682791fb6373c47eb557c47483eca3db96895eba9b5bbe85b5"
static_direct_ilhook_fixture_sha="6a0649518d49cd0d17b84da3be53929cdd602d89d922e2d3ab87c524345e3807"
chrono_custom_audio_fixture_sha="af46039437fbed52e72941d07e0d9ce6657dacc838516459f7e9e995fea07a18"
djmaphelper_littleepic_fixture_sha="95ab02d657213031b70c3079738b21be399e567ca8a32b1fe8e8effc0778eedb"
include_static_il_canary=0
include_static_il_compose_canary=0
include_static_direct_ilhook_canary=0
include_custom_audio_canary=0
include_dj_frozen_il_canary=0
for mod in "${MODS[@]}"; do
  if [[ -f "$mod" && "$(shasum -a 256 "$mod" | awk '{print $1}')" == "$static_il_fixture_sha" ]]; then
    include_static_il_canary=1
  fi
  if [[ -f "$mod" && "$(shasum -a 256 "$mod" | awk '{print $1}')" == "$static_il_compose_fixture_sha" ]]; then
    include_static_il_compose_canary=1
  fi
  if [[ -f "$mod" && "$(shasum -a 256 "$mod" | awk '{print $1}')" == "$static_direct_ilhook_fixture_sha" ]]; then
    include_static_direct_ilhook_canary=1
  fi
  if [[ -f "$mod" && "$(shasum -a 256 "$mod" | awk '{print $1}')" == "$chrono_custom_audio_fixture_sha" ]]; then
    include_custom_audio_canary=1
  fi
  if [[ -f "$mod" && "$(shasum -a 256 "$mod" | awk '{print $1}')" == "$djmaphelper_littleepic_fixture_sha" ]]; then
    include_dj_frozen_il_canary=1
  fi
done
if ((include_static_il_canary)); then
  MODS+=("$REPO_ROOT/apple-everest/canaries/static-il-content")
fi
if ((include_static_il_compose_canary)); then
  MODS+=("$REPO_ROOT/apple-everest/canaries/static-il-compose-content")
fi
if ((include_static_direct_ilhook_canary)); then
  MODS+=("$REPO_ROOT/apple-everest/canaries/static-direct-ilhook-content")
fi
if ((include_custom_audio_canary)); then
  MODS+=("$REPO_ROOT/apple-everest/canaries/custom-audio-content")
fi
if ((include_dj_frozen_il_canary)); then
  MODS+=("$REPO_ROOT/apple-everest/canaries/dj-frozen-il-content")
fi
mod_args=()
for mod in "${MODS[@]}"; do mod_args+=(--mod "$mod"); done
build_args=(build --profile "$PROFILE" --repo-root "$REPO_ROOT" --upstream "$UPSTREAM" --output "$CLOSURE")
[[ -z "$FACTORY_CLOSURE" ]] || build_args+=(--factory-closure "$FACTORY_CLOSURE")
if ((CONFIGURED_FIXTURE)); then
  ((${#MODS[@]} == 1)) || { echo "error: --configured-fixture requires exactly one --mod" >&2; exit 2; }
  (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- build-configured-fixture \
    --profile "$PROFILE" --repo-root "$REPO_ROOT" --upstream "$UPSTREAM" --output "$CLOSURE" \
    --mod "${MODS[0]}")
else
  (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- "${build_args[@]}" "${mod_args[@]}")
fi

python3 - "$CLOSURE/compatibility-manifest.json" "$REPO_ROOT/apple-everest/sj-factory-authored-profiles-stage25kj.json" "$FACTORY_PREFLIGHT" <<'PY'
import json,pathlib,sys
closure=json.loads(pathlib.Path(sys.argv[1]).read_text())
profiles=json.loads(pathlib.Path(sys.argv[2]).read_text())
names={row['name'] for row in closure['selectedMods']}
selected={row['provider'] for row in profiles['factories']}-{'EverestCore'}
required=selected<=names or bool(names & {'AppleEverestStage25KJCanary','AppleEverestStage25KJInteractions'})
if required and not sys.argv[3]:raise SystemExit('K-J selected factory inputs require package-backed compiled preflight before any product')
PY

if [[ -n "$FACTORY_PREFLIGHT" ]]; then
  preflight_runtime="$WORK_ROOT/preflight-runtime"
  safe_replace "$preflight_runtime" .apple-everest-preflight-runtime
  cp -cR "$REPO_ROOT/.build/celeste-ios/current/managed" "$preflight_runtime"
  touch "$preflight_runtime/.apple-everest-preflight-runtime"
  (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- apply \
    --closure "$CLOSURE" --managed-root "$preflight_runtime")
  (cd "$REPO_ROOT" && dotnet build "$preflight_runtime/Celeste.Modern.csproj" -c Release --nologo \
    -p:CelesteAppleRepoRoot="$REPO_ROOT" -p:CelesteManagedGeneratedRoot="$preflight_runtime" \
    -p:AppleEverestStaticIlDotnet="$DOTNET9")
  preflight_assembly="$preflight_runtime/bin/Release/net10.0-ios26.5/Celeste.dll"
  [[ -f "$preflight_assembly" ]] || { echo "error: compiled preflight target absent" >&2; exit 1; }
  (cd "$REPO_ROOT" && dotnet exec --fx-version 10.0.10 \
    "$REPO_ROOT/tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll" \
    preflight-factory-closure --manifest "$FACTORY_PREFLIGHT" --authored-profiles "$AUTHORED_FACTORY_PROFILES" \
    --assembly "$preflight_assembly" --closure "$CLOSURE" --repo-root "$REPO_ROOT" \
    --profile "$PROFILE" --upstream "$UPSTREAM" --canonical-managed-root "$REPO_ROOT/.build/celeste-ios/current/managed" \
    --dotnet "$(command -v dotnet)" --output "$WORK_ROOT/production-preflight.json" "${mod_args[@]}")
  if [[ -d "$CLOSURE/content/Content/Maps/AppleEverestStage25KJ/FactoryProfiles" ]]; then
    python3 "$SCRIPT_DIR/inspect-apple-everest-stage25kj-canary-profiles.py" \
      --closure "$CLOSURE" --output "$WORK_ROOT/compiled-canary-profiles.json"
    (cd "$REPO_ROOT" && dotnet exec --fx-version 10.0.10 \
      "$REPO_ROOT/tools/AppleEverestBuilder/bin/Debug/net8.0/AppleEverestBuilder.dll" \
      inspect-compiled-factories --assembly "$preflight_assembly" --manifest "$FACTORY_PREFLIGHT" \
      --authored-profiles "$WORK_ROOT/compiled-canary-profiles.json" --output "$WORK_ROOT/compiled-canary-guards.json")
  fi
fi

prepare_platform() {
  local platform="$1" base destination
  destination="$WORK_ROOT/$platform-runtime"
  safe_replace "$destination" .apple-everest-derived-runtime
  mkdir -p "$destination"
  if [[ "$platform" == ios ]]; then
    base="$REPO_ROOT/.build/celeste-ios/current"
    [[ -f "$base/managed/Celeste.Modern.csproj" && -d "$base/content/Content" && -d "$base/banks/Content/FMOD" ]] || {
      echo "error: prepare the accepted modern-iOS Celeste runtime first" >&2; exit 1; }
    cp -cR "$base/managed" "$base/content" "$base/banks" "$destination/"
  else
    base="$REPO_ROOT/.build/celeste-runtime/stage6-current/audio"
    [[ -f "$base/managed/Celeste.Modern.csproj" && -d "$base/content/Content" ]] || {
      echo "error: prepare the accepted tvOS Stage 6 audio runtime first" >&2; exit 1; }
    cp -cR "$base/managed" "$base/content" "$destination/"
  fi
  cp -cR "$CLOSURE/content/." "$destination/content/"
  (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- apply \
    --closure "$CLOSURE" --managed-root "$destination/managed")
  if [[ "$platform" == tvos ]]; then
    python3 "$SCRIPT_DIR/apply-apple-port-build-identity.py" \
      --version-source "$REPO_ROOT/modern-ios/IOSPortVersion.props" \
      --managed-root "$destination/managed" --platform tvos
  fi
  touch "$destination/.apple-everest-derived-runtime"
}

[[ "$PLATFORM" == tvos ]] || prepare_platform ios
[[ "$PLATFORM" == ios ]] || prepare_platform tvos

if ((PREPARE_ONLY)); then
  printf 'PASS: one shared Apple Everest closure prepared for %s\n' "$PLATFORM"
  exit 0
fi

if ((REUSE_BUILD)); then
  [[ -f "$WORK_ROOT/build/.apple-everest-canary-build" ]] || { echo "error: no marked canary build is available to reuse" >&2; exit 1; }
else
  safe_replace "$WORK_ROOT/build" .apple-everest-canary-build
  mkdir -p "$WORK_ROOT/build"
  touch "$WORK_ROOT/build/.apple-everest-canary-build"
fi

if [[ -e "$OUTPUT" ]]; then
  [[ -f "$OUTPUT/.apple-everest-canary-products" ]] || { echo "error: refusing unmarked product output" >&2; exit 1; }
else
  mkdir -p "$OUTPUT"
  touch "$OUTPUT/.apple-everest-canary-products"
fi
for product_platform in ios tvos; do
  [[ "$PLATFORM" == all || "$PLATFORM" == "$product_platform" ]] || continue
  product_path="$OUTPUT/$product_platform"
  if [[ -e "$product_path" ]]; then
    ((CLEAN)) || { echo "error: product output exists; use --clean: $product_path" >&2; exit 1; }
    [[ -f "$product_path/.apple-everest-canary-platform" ]] || { echo "error: refusing unmarked platform product" >&2; exit 1; }
    find "$product_path" -depth -delete
  fi
done

signing_args=(-p:EnableCodeSigning=false)
if [[ "$SIGNING" == development ]]; then
  if [[ "$PLATFORM" != tvos && -n "$IOS_DEVICE_ID" ]]; then
    "$SCRIPT_DIR/configure-ios-personal-team.sh" --team-id "$TEAM_ID" --bundle-id "$IOS_BUNDLE_ID" \
      --device-id "$IOS_DEVICE_ID" --output ".build/apple-everest/provisioning/ios" \
      --props-output ".build/apple-everest/provisioning/ios.props"
  fi
  if [[ "$PLATFORM" != ios && -n "$TVOS_DEVICE_ID" ]]; then
    "$SCRIPT_DIR/configure-tvos-personal-team.sh" --team-id "$TEAM_ID" --bundle-id "$TVOS_BUNDLE_ID" \
      --device-id "$TVOS_DEVICE_ID" --output ".build/tvos-self-build/provisioning-everest-canary" \
      --props-output ".build/apple-everest/provisioning/tvos.props"
  fi
  signing_args=(-p:EnableCodeSigning=true -p:DevelopmentTeam="$TEAM_ID" -p:CodesignKey="Apple Development" -p:CodesignProvision=Automatic -p:ProvisioningType=automatic)
fi

canary_manifest() {
  local source="$1" destination="$2"
  python3 - "$source" "$destination" <<'PY'
import pathlib, plistlib, sys
source, destination = map(pathlib.Path, sys.argv[1:])
value = plistlib.loads(source.read_bytes())
value["CFBundleDisplayName"] = "Celeste Everest Canary"
value["CFBundleName"] = "Celeste Everest Canary"
destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_bytes(plistlib.dumps(value, fmt=plistlib.FMT_XML, sort_keys=False))
PY
}
ios_manifest="$WORK_ROOT/build/ios-canary-Info.plist"
tvos_manifest="$WORK_ROOT/build/tvos-canary-Info.plist"
[[ "$PLATFORM" == tvos ]] || canary_manifest "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/Info.plist" "$ios_manifest"
[[ "$PLATFORM" == ios ]] || canary_manifest "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/Info.plist" "$tvos_manifest"

locate_app() {
  python3 - "$1" "$2" <<'PY'
import pathlib,plistlib,sys
root,bundle=pathlib.Path(sys.argv[1]),sys.argv[2]
matches=[]
for app in root.rglob("*.app"):
    try: info=plistlib.loads((app/"Info.plist").read_bytes())
    except Exception: continue
    if info.get("CFBundleIdentifier")==bundle: matches.append(app)
packaged=[p for p in matches if "ipa" in p.parts and "Payload" in p.parts]
if len(packaged)==1: matches=packaged
if len(matches)!=1: raise SystemExit(f"expected one unambiguous packaged app for requested bundle, found {len(matches)}")
print(matches[0])
PY
}

package_app() {
  local platform="$1" app="$2" name="$3" product
  product="$OUTPUT/$platform"
  mkdir -p "$product/Payload"
  touch "$product/.apple-everest-canary-platform"
  ditto --norsrc "$app" "$product/Payload/$name.app"
  (cd "$product" && ditto -c -k --norsrc --keepParent Payload "$name.ipa")
  python3 - "$CLOSURE/compatibility-manifest.json" "$product/$name.ipa" "$product/build-manifest.json" "$platform" "$SIGNING" "$REPO_ROOT" "$WORK_ROOT/build/$platform/aot-provenance/receipt.json" "$FACTORY_PREFLIGHT" <<'PY'
import hashlib,json,pathlib,subprocess,sys
closure=json.loads(pathlib.Path(sys.argv[1]).read_text()); ipa=pathlib.Path(sys.argv[2])
source={"sourceCommit":subprocess.check_output(["git","-C",sys.argv[6],"rev-parse","HEAD"],text=True).strip(),
        "sourceTreeDirty":bool(subprocess.check_output(["git","-C",sys.argv[6],"status","--porcelain"],text=True).strip())}
if sys.argv[8]:
    receipt=json.loads(pathlib.Path(sys.argv[7]).read_text())
    if receipt["phase"] != "AFTER_NATIVE_LINK" or any(receipt[key] != value for key,value in source.items()):
        raise SystemExit("source revision changed between AOT compilation and product packaging")
pathlib.Path(sys.argv[3]).write_text(json.dumps({"schemaVersion":1,"platform":sys.argv[4],"configuration":"Release",
 "rid":sys.argv[4]+"-arm64","signing":sys.argv[5],"fullAOT":True,"fullTrim":True,"useInterpreter":False,"jit":False,
 **source,
 "sharedClosureSha256":closure["sharedClosureSha256"],"ipaBytes":ipa.stat().st_size,
 "ipaSha256":hashlib.sha256(ipa.read_bytes()).hexdigest()},indent=2,sort_keys=True)+"\n")
PY
}

scan_product_runtime() {
  local app="$1" platform_build="$2" assembly assembly_name aot_object_count llvm_object mono_object
  if find "$app" -type f \( \
      -iname 'AppleEverestIlWorker*' -o -iname 'Mono.Cecil*' -o \
      -iname 'MonoMod.Cil*' -o -iname 'MonoMod.Utils*' -o \
      -iname 'MonoMod.RuntimeDetour*' -o -iname 'AppleEverestStaticIl.targets' -o \
      -iname 'AppleEverestStaticIl.plan.json' \) -print | grep -q .; then
    echo "error: host-only static-IL transformation material entered the device product" >&2
    exit 1
  fi
  (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- \
    scan-runtime --assembly "$app/Celeste.dll")
  if [[ -n "$FACTORY_PREFLIGHT" ]]; then
    python3 "$REPO_ROOT/scripts/verify-apple-everest-aot-factory-product.py" \
      --app "$app" --build "$platform_build" --manifest "$FACTORY_PREFLIGHT" \
      --authored-profiles "$AUTHORED_FACTORY_PROFILES" --output "$platform_build/linked-selected-factories.json"
    python3 - "$app" <<'PY'
import pathlib,sys
root=pathlib.Path(sys.argv[1])
for path in root.rglob('*'):
    if path.is_file() and '/maps/strawberryjam2021/' in ('/'+path.relative_to(root).as_posix().lower()):
        raise SystemExit('real Strawberry Jam map content entered a factory-only product')
print('PASS: actual product has no original Strawberry Jam map content')
PY
  fi
  if [[ -d "$CLOSURE/assemblies" ]]; then
    while IFS= read -r -d '' assembly; do
      [[ -f "$app/$(basename "$assembly")" ]] || {
        echo "error: frozen external assembly missing from product: $(basename "$assembly")" >&2; exit 1; }
      (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- \
        scan-runtime --assembly "$app/$(basename "$assembly")")
      (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- \
        verify-preserved-assembly --source "$assembly" --linked "$app/$(basename "$assembly")")
      (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- \
        verify-referenced-api --source "$app/$(basename "$assembly")" --target "$app/Celeste.dll")
      assembly_name="$(basename "$assembly" .dll)"
      aot_object_count="$(find "$platform_build" -type f -name "$assembly_name.dll.llvm.o" -print | wc -l | tr -d ' ')"
      [[ "$aot_object_count" == 1 ]] || {
        echo "error: expected one LLVM AOT object for $assembly_name, found $aot_object_count" >&2; exit 1; }
      llvm_object="$(find "$platform_build" -type f -name "$assembly_name.dll.llvm.o" -print)"
      mono_object="${llvm_object%.llvm.o}.o"
      [[ -f "$mono_object" ]] || { echo "error: companion Mono AOT object missing for $assembly_name" >&2; exit 1; }
      (cd /private/tmp && "$DOTNET8" run --project "$BUILDER_PROJECT" -- \
        verify-aot-object --source "$assembly" --object "$llvm_object" --object "$mono_object")
    done < <(find "$CLOSURE/assemblies" -maxdepth 1 -type f -name '*.dll' -print0)
  fi
}

record_product_disk() {
  python3 - "$WORK_ROOT" "$1" <<'PY'
import datetime,json,pathlib,shutil,sys
root=pathlib.Path(sys.argv[1])
record={'phase':sys.argv[2],'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'freeGiB':round(shutil.disk_usage(root).free/2**30,3)}
with (root/'disk-measurements.jsonl').open('a') as stream: stream.write(json.dumps(record,sort_keys=True)+'\n')
print('disk:',record['phase'],record['freeGiB'],'GiB free')
PY
}

if [[ "$PLATFORM" != tvos ]]; then
  ios_artifacts="$WORK_ROOT/build/ios"
  ios_aot_proof=""
  if [[ -n "$FACTORY_PREFLIGHT" ]]; then ios_aot_proof="$ios_artifacts/aot-provenance"; fi
  if ((!REUSE_BUILD)); then
    record_product_disk before-ios-aot
    (cd "$REPO_ROOT" && dotnet publish "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj" -c Release -r ios-arm64 -m:1 -p:BuildInParallel=false --self-contained true \
      --artifacts-path "$ios_artifacts" -p:IOSProductMode=Celeste -p:EnableFmodDeviceFoundation=true \
      -p:CelesteIOSRuntimeRoot="$WORK_ROOT/ios-runtime" -p:CelesteAppleRepoRoot="$REPO_ROOT" \
      -p:AppBundleManifest="$ios_manifest" \
      -p:AppleEverestAotProofRoot="$ios_aot_proof" \
      -p:ApplicationId="$IOS_BUNDLE_ID" -p:ApplicationTitle="Celeste Everest Canary" \
      -p:ArchiveOnBuild=false -p:UseInterpreter=false -p:RunAOTCompilation=true -p:MtouchLink=Full \
      -p:TrimMode=full -p:MtouchUseLlvm=true -p:PublishTrimmed=true "${signing_args[@]}")
  fi
  ios_app="$(locate_app "$ios_artifacts" "$IOS_BUNDLE_ID")"
  scan_product_runtime "$ios_app" "$ios_artifacts"
  package_app ios "$ios_app" Celeste-Everest-Canary-iOS
fi

if [[ "$PLATFORM" != ios ]]; then
  tvos_artifacts="$WORK_ROOT/build/tvos"
  tvos_aot_proof=""
  if [[ -n "$FACTORY_PREFLIGHT" ]]; then tvos_aot_proof="$tvos_artifacts/aot-provenance"; fi
  if ((!REUSE_BUILD)); then
    record_product_disk before-tvos-aot
    (cd "$REPO_ROOT" && dotnet publish "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj" -c Release -r tvos-arm64 -m:1 -p:BuildInParallel=false \
      --artifacts-path "$tvos_artifacts" -p:CelesteLaunchMode=CelesteAudio -p:Stage5BAudioScenario=normal \
      -p:PersistenceEnabled=true -p:PersistenceStorageNamespace=tests -p:CelesteRuntimeRoot="$WORK_ROOT/tvos-runtime" \
      -p:AppBundleManifest="$tvos_manifest" \
      -p:AppleEverestAotProofRoot="$tvos_aot_proof" \
      -p:CelesteBrandingEnabled=true -p:ApplicationId="$TVOS_BUNDLE_ID" -p:ApplicationTitle="Celeste Everest Canary" \
      -p:UseInterpreter=false -p:RunAOTCompilation=true -p:PublishTrimmed=true -p:TrimMode=full -p:MtouchLink=Full "${signing_args[@]}")
  fi
  tvos_app="$(locate_app "$tvos_artifacts" "$TVOS_BUNDLE_ID")"
  scan_product_runtime "$tvos_app" "$tvos_artifacts"
  package_app tvos "$tvos_app" Celeste-Everest-Canary-tvOS
fi

record_product_disk after-products
printf 'PASS: separate-identity full-AOT Apple Everest canary product(s) built\n'
