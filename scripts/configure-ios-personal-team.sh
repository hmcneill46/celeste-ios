#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
TEAM_ID=""
BUNDLE_ID=""
DEVICE_ID=""
OUTPUT="$REPO_ROOT/.build/ios-host/provisioning"
PROPS_OUTPUT="$REPO_ROOT/modern-ios/Local.Build.props"

usage() {
  cat <<'EOF'
Usage: scripts/configure-ios-personal-team.sh --team-id ID --bundle-id ID --device-id ID [options]

Ask Xcode to create/refresh a local iOS development profile, then write the
ignored modern-ios/Local.Build.props used by the physical foundation build.
An Apple account must already be signed into Xcode. No private values enter Git.

Options:
  --output DIR         ignored provisioning-helper root
  --props-output FILE  ignored generated props destination
EOF
}

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }

while (($#)); do
  case "$1" in
    --team-id) [[ $# -ge 2 ]] || exit 2; TEAM_ID="$2"; shift 2 ;;
    --bundle-id) [[ $# -ge 2 ]] || exit 2; BUNDLE_ID="$2"; shift 2 ;;
    --device-id) [[ $# -ge 2 ]] || exit 2; DEVICE_ID="$2"; shift 2 ;;
    --output) [[ $# -ge 2 ]] || exit 2; OUTPUT="$(repo_path "$2")"; shift 2 ;;
    --props-output) [[ $# -ge 2 ]] || exit 2; PROPS_OUTPUT="$(repo_path "$2")"; shift 2 ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done
[[ "$TEAM_ID" =~ ^[A-Z0-9]{10}$ ]] || { echo "error: invalid team identifier" >&2; exit 2; }
[[ "$BUNDLE_ID" =~ ^[A-Za-z][A-Za-z0-9-]*(\.[A-Za-z0-9-]+)+$ ]] || { echo "error: invalid bundle identifier" >&2; exit 2; }
[[ "$DEVICE_ID" =~ ^[A-Fa-f0-9-]{24,40}$ ]] || { echo "error: invalid device identifier" >&2; exit 2; }
case "$OUTPUT" in "$REPO_ROOT/.build/"*) ;; *) echo "error: helper output must remain below ignored .build" >&2; exit 2 ;; esac
case "$PROPS_OUTPUT" in "$REPO_ROOT/.build/"*|"$REPO_ROOT/modern-ios/Local.Build.props") ;; *) echo "error: props output must be the normal ignored file or remain below .build" >&2; exit 2 ;; esac
if [[ -e "$OUTPUT" ]]; then
  [[ -f "$OUTPUT/.ios-provisioning-helper" ]] || { echo "error: refusing unmarked helper root" >&2; exit 1; }
  find "$OUTPUT" -depth -delete
fi
mkdir -p "$OUTPUT/Provisioning.xcodeproj" "$OUTPUT/build" "$OUTPUT/logs"
touch "$OUTPUT/.ios-provisioning-helper"

python3 - "$OUTPUT" <<'PY'
import pathlib, sys
root = pathlib.Path(sys.argv[1])
(root / "main.m").write_text("#import <UIKit/UIKit.h>\nint main(int argc, char **argv) { @autoreleasepool { return 0; } }\n")
(root / "Info.plist").write_text('''<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleExecutable</key><string>$(EXECUTABLE_NAME)</string>
<key>CFBundleIdentifier</key><string>$(PRODUCT_BUNDLE_IDENTIFIER)</string>
<key>CFBundleName</key><string>Provisioning</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>1</string>
<key>CFBundleShortVersionString</key><string>1</string>
<key>LSRequiresIPhoneOS</key><true/><key>MinimumOSVersion</key><string>15.0</string>
<key>UIDeviceFamily</key><array><integer>1</integer><integer>2</integer></array>
</dict></plist>\n''')
(root / "Provisioning.xcodeproj/project.pbxproj").write_text('''// !$*UTF8*$!
{ archiveVersion = 1; classes = {}; objectVersion = 56; objects = {
1001 = { isa = PBXProject; buildConfigurationList = 1002; compatibilityVersion = "Xcode 14.0"; developmentRegion = en; mainGroup = 1003; productRefGroup = 1004; projectDirPath = ""; projectRoot = ""; targets = (1005); };
1003 = { isa = PBXGroup; children = (1006,1007,1004); sourceTree = "<group>"; };
1004 = { isa = PBXGroup; children = (1008); name = Products; sourceTree = "<group>"; };
1005 = { isa = PBXNativeTarget; buildConfigurationList = 1009; buildPhases = (1010); buildRules = (); dependencies = (); name = Provisioning; productName = Provisioning; productReference = 1008; productType = "com.apple.product-type.application"; };
1006 = { isa = PBXFileReference; lastKnownFileType = sourcecode.c.objc; path = main.m; sourceTree = "<group>"; };
1007 = { isa = PBXFileReference; lastKnownFileType = text.plist.xml; path = Info.plist; sourceTree = "<group>"; };
1008 = { isa = PBXFileReference; explicitFileType = wrapper.application; includeInIndex = 0; path = Provisioning.app; sourceTree = BUILT_PRODUCTS_DIR; };
1010 = { isa = PBXSourcesBuildPhase; buildActionMask = 2147483647; files = (1011); runOnlyForDeploymentPostprocessing = 0; };
1011 = { isa = PBXBuildFile; fileRef = 1006; };
1002 = { isa = XCConfigurationList; buildConfigurations = (1012); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release; };
1009 = { isa = XCConfigurationList; buildConfigurations = (1013); defaultConfigurationIsVisible = 0; defaultConfigurationName = Release; };
1012 = { isa = XCBuildConfiguration; buildSettings = {}; name = Release; };
1013 = { isa = XCBuildConfiguration; buildSettings = { CODE_SIGN_STYLE = Automatic; GENERATE_INFOPLIST_FILE = NO; INFOPLIST_FILE = Info.plist; PRODUCT_NAME = "$(TARGET_NAME)"; SDKROOT = iphoneos; TARGETED_DEVICE_FAMILY = "1,2"; IPHONEOS_DEPLOYMENT_TARGET = 15.0; }; name = Release; };
}; rootObject = 1001; }\n''')
PY

xcodebuild -project "$OUTPUT/Provisioning.xcodeproj" -scheme Provisioning -configuration Release \
  -sdk iphoneos -destination "id=$DEVICE_ID" -derivedDataPath "$OUTPUT/build" \
  DEVELOPMENT_TEAM="$TEAM_ID" PRODUCT_BUNDLE_IDENTIFIER="$BUNDLE_ID" \
  CODE_SIGN_STYLE=Automatic -allowProvisioningUpdates -allowProvisioningDeviceRegistration \
  > "$OUTPUT/logs/xcodebuild-private.log" 2>&1 || {
    echo "error: Xcode automatic iOS provisioning failed; inspect the ignored private log" >&2
    exit 1
  }

mkdir -p "$(dirname "$PROPS_OUTPUT")"
python3 - "$PROPS_OUTPUT" "$BUNDLE_ID" "$TEAM_ID" <<'PY'
import html, pathlib, sys
path, bundle, team = pathlib.Path(sys.argv[1]), sys.argv[2], sys.argv[3]
path.write_text(f'''<Project>
  <!-- Generated locally; this file and all private values are ignored by Git. -->
  <PropertyGroup>
    <ApplicationId>{html.escape(bundle)}</ApplicationId>
    <CodesignKey>Apple Development</CodesignKey>
    <DevelopmentTeam>{html.escape(team)}</DevelopmentTeam>
    <CodesignProvision>Automatic</CodesignProvision>
    <ProvisioningType>automatic</ProvisioningType>
  </PropertyGroup>
</Project>\n''')
PY
echo "Personal Team iOS development provisioning is ready (private values remain ignored)."
