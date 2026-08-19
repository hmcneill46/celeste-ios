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
  --prepare-only           generate and compile-check the shared closure only
  --clean                  replace only marked prior canary output
  --reuse-build            package an already-marked completed AOT build
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
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
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
(cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- acquire --profile "$PROFILE" --output "$UPSTREAM")
safe_replace "$CLOSURE" .apple-everest-static-closure
if ((${#MODS[@]} == 0)); then
  MODS=(
    "$REPO_ROOT/apple-everest/canaries/content"
    "$REPO_ROOT/apple-everest/canaries/module-a"
    "$REPO_ROOT/apple-everest/canaries/module-b"
    "$REPO_ROOT/apple-everest/canaries/module-c"
  )
fi
mod_args=()
for mod in "${MODS[@]}"; do mod_args+=(--mod "$mod"); done
(cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- build \
  --profile "$PROFILE" --repo-root "$REPO_ROOT" --upstream "$UPSTREAM" --output "$CLOSURE" \
  "${mod_args[@]}")

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
  (cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- apply \
    --closure "$CLOSURE" --managed-root "$destination/managed")
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
  python3 - "$CLOSURE/compatibility-manifest.json" "$product/$name.ipa" "$product/build-manifest.json" "$platform" "$SIGNING" <<'PY'
import hashlib,json,pathlib,sys
closure=json.loads(pathlib.Path(sys.argv[1]).read_text()); ipa=pathlib.Path(sys.argv[2])
pathlib.Path(sys.argv[3]).write_text(json.dumps({"schemaVersion":1,"platform":sys.argv[4],"configuration":"Release",
 "rid":sys.argv[4]+"-arm64","signing":sys.argv[5],"fullAOT":True,"fullTrim":True,"useInterpreter":False,"jit":False,
 "sharedClosureSha256":closure["sharedClosureSha256"],"ipaBytes":ipa.stat().st_size,
 "ipaSha256":hashlib.sha256(ipa.read_bytes()).hexdigest()},indent=2,sort_keys=True)+"\n")
PY
}

scan_product_runtime() {
  local app="$1" platform_build="$2" assembly assembly_name aot_object_count llvm_object mono_object
  (cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- \
    scan-runtime --assembly "$app/Celeste.dll")
  if [[ -d "$CLOSURE/assemblies" ]]; then
    while IFS= read -r -d '' assembly; do
      [[ -f "$app/$(basename "$assembly")" ]] || {
        echo "error: frozen external assembly missing from product: $(basename "$assembly")" >&2; exit 1; }
      (cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- \
        scan-runtime --assembly "$app/$(basename "$assembly")")
      (cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- \
        verify-preserved-assembly --source "$assembly" --linked "$app/$(basename "$assembly")")
      (cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- \
        verify-referenced-api --source "$app/$(basename "$assembly")" --target "$app/Celeste.dll")
      assembly_name="$(basename "$assembly" .dll)"
      aot_object_count="$(find "$platform_build" -type f -name "$assembly_name.dll.llvm.o" -print | wc -l | tr -d ' ')"
      [[ "$aot_object_count" == 1 ]] || {
        echo "error: expected one LLVM AOT object for $assembly_name, found $aot_object_count" >&2; exit 1; }
      llvm_object="$(find "$platform_build" -type f -name "$assembly_name.dll.llvm.o" -print)"
      mono_object="${llvm_object%.llvm.o}.o"
      [[ -f "$mono_object" ]] || { echo "error: companion Mono AOT object missing for $assembly_name" >&2; exit 1; }
      (cd "$REPO_ROOT/tools/AppleEverestBuilder" && "$DOTNET8" run --project AppleEverestBuilder.csproj -- \
        verify-aot-object --source "$assembly" --object "$llvm_object" --object "$mono_object")
    done < <(find "$CLOSURE/assemblies" -maxdepth 1 -type f -name '*.dll' -print0)
  fi
}

if [[ "$PLATFORM" != tvos ]]; then
  ios_artifacts="$WORK_ROOT/build/ios"
  if ((!REUSE_BUILD)); then
    dotnet publish "$REPO_ROOT/modern-ios/CelesteIOSRuntimeHost/CelesteIOSRuntimeHost.csproj" -c Release -r ios-arm64 --self-contained true \
      --artifacts-path "$ios_artifacts" -p:IOSProductMode=Celeste -p:EnableFmodDeviceFoundation=true \
      -p:CelesteIOSRuntimeRoot="$WORK_ROOT/ios-runtime" -p:CelesteAppleRepoRoot="$REPO_ROOT" \
      -p:AppBundleManifest="$ios_manifest" \
      -p:ApplicationId="$IOS_BUNDLE_ID" -p:ApplicationTitle="Celeste Everest Canary" \
      -p:ArchiveOnBuild=false -p:UseInterpreter=false -p:RunAOTCompilation=true -p:MtouchLink=Full \
      -p:TrimMode=full -p:MtouchUseLlvm=true -p:PublishTrimmed=true "${signing_args[@]}"
  fi
  ios_app="$(locate_app "$ios_artifacts" "$IOS_BUNDLE_ID")"
  scan_product_runtime "$ios_app" "$ios_artifacts"
  package_app ios "$ios_app" Celeste-Everest-Canary-iOS
fi

if [[ "$PLATFORM" != ios ]]; then
  tvos_artifacts="$WORK_ROOT/build/tvos"
  if ((!REUSE_BUILD)); then
    dotnet publish "$REPO_ROOT/tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj" -c Release -r tvos-arm64 -m:1 -p:BuildInParallel=false \
      --artifacts-path "$tvos_artifacts" -p:CelesteLaunchMode=CelesteAudio -p:Stage5BAudioScenario=normal \
      -p:PersistenceEnabled=true -p:PersistenceStorageNamespace=tests -p:CelesteRuntimeRoot="$WORK_ROOT/tvos-runtime" \
      -p:AppBundleManifest="$tvos_manifest" \
      -p:CelesteBrandingEnabled=true -p:ApplicationId="$TVOS_BUNDLE_ID" -p:ApplicationTitle="Celeste Everest Canary" \
      -p:UseInterpreter=false -p:RunAOTCompilation=true -p:PublishTrimmed=true -p:TrimMode=full -p:MtouchLink=Full "${signing_args[@]}"
  fi
  tvos_app="$(locate_app "$tvos_artifacts" "$TVOS_BUNDLE_ID")"
  scan_product_runtime "$tvos_app" "$tvos_artifacts"
  package_app tvos "$tvos_app" Celeste-Everest-Canary-tvOS
fi

printf 'PASS: separate-identity full-AOT Apple Everest canary product(s) built\n'
