#!/usr/bin/env bash

# Read-only diagnostics for the Celeste tvOS port. This script prints state;
# it does not install packages, initialize submodules, boot simulators, or edit files.
# Use --redact to replace personal paths, device/host names and identifiers, and
# signing-account metadata in the output while retaining engineering evidence.

set -o pipefail

script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" 2>/dev/null && pwd -P)
repo_dir=$(CDPATH= cd -- "$script_dir/.." 2>/dev/null && pwd -P)

usage() {
    printf 'Usage: %s [--redact]\n' "$(basename -- "$0")"
    printf '  no option  Print complete local diagnostics.\n'
    printf '  --redact   Print a safe-to-share, output-only redacted diagnostic.\n'
}

redact_output() {
    local line match suffix rest in_physical_devices=0

    while IFS= read -r line || [ -n "$line" ]; do
        # Replace the most specific path first so $REPO_ROOT is not turned into
        # $HOME/Projects/... by the broader home-directory replacement.
        line=${line//"$repo_dir"/'$REPO_ROOT'}
        if [ -n "${HOME:-}" ]; then
            line=${line//"$HOME"/'$HOME'}
        fi

        # FMOD is a licensed, user-mounted SDK. Preserve paths below its root,
        # but never reveal the machine-specific volume name in shareable output.
        while [[ "$line" =~ /Volumes/[^/]*/FMOD[[:space:]]Programmers[[:space:]]API ]]; do
            match=${BASH_REMATCH[0]}
            line=${line/"$match"/'$FMOD_SDK_ROOT'}
        done

        # devicectl separates columns with runs of spaces. Keep the availability,
        # pairing state and model suffix following the identifier.
        if [[ "$line" == '$ xcrun devicectl list devices' ]]; then
            in_physical_devices=1
        elif [[ "$line" == '===== '* ]]; then
            in_physical_devices=0
        fi
        if [ "$in_physical_devices" -eq 1 ] &&
           [[ "$line" =~ [A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12} ]]; then
            match=${BASH_REMATCH[0]}
            suffix=${line#*"$match"}
            line="<PHYSICAL_DEVICE_NAME>   <DEVICE_HOSTNAME>   <DEVICE_ID>$suffix"
        fi

        # The second uname field is the host name; retain the OS/kernel details.
        if [[ "$line" == Darwin\ * ]]; then
            rest=${line#Darwin }
            rest=${rest#* }
            line="Darwin <HOSTNAME> $rest"
        fi

        # Physical identifiers were handled above. UUIDs elsewhere in the
        # diagnostic are simulator identifiers.
        while [[ "$line" =~ [A-F0-9]{8}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{12} ]]; do
            match=${BASH_REMATCH[0]}
            line=${line/"$match"/'<SIMULATOR_ID>'}
        done
        while [[ "$line" =~ [A-F0-9]{40} ]]; do
            match=${BASH_REMATCH[0]}
            line=${line/"$match"/'<IDENTITY_FINGERPRINT>'}
        done
        while [[ "$line" =~ [-[:alnum:]._%+]+@[-[:alnum:].]+\.[[:alpha:]]{2,} ]]; do
            match=${BASH_REMATCH[0]}
            line=${line/"$match"/'<APPLE_ID>'}
        done
        if [[ "$line" == *'<IDENTITY_FINGERPRINT>'* ]]; then
            while [[ "$line" =~ \([A-Z0-9]{10}\) ]]; do
                match=${BASH_REMATCH[0]}
                line=${line/"$match"/'(<TEAM_ID>)'}
            done
        fi

        while [[ "$line" == *' ' ]] || [[ "$line" == *$'\t' ]]; do
            line=${line%?}
        done

        printf '%s\n' "$line"
    done
}

case "${1:-}" in
    '')
        if [ "$#" -ne 0 ]; then
            usage >&2
            exit 2
        fi
        ;;
    --redact)
        if [ "$#" -ne 1 ]; then
            usage >&2
            exit 2
        fi
        "$0" --emit-unredacted-for-redactor 2>&1 | redact_output
        diagnostic_status=${PIPESTATUS[0]}
        exit "$diagnostic_status"
        ;;
    --emit-unredacted-for-redactor)
        if [ "$#" -ne 1 ]; then
            usage >&2
            exit 2
        fi
        ;;
    -h|--help)
        usage
        exit 0
        ;;
    *)
        usage >&2
        exit 2
        ;;
esac

section() {
    printf '\n===== %s =====\n' "$1"
}

run() {
    printf '\n$'
    printf ' %q' "$@"
    printf '\n'
    "$@" 2>&1
    status=$?
    if [ "$status" -ne 0 ]; then
        printf '[exit %s]\n' "$status"
    fi
    return 0
}

run_shell() {
    printf '\n$ %s\n' "$1"
    /bin/bash -o pipefail -c "$1" 2>&1
    status=$?
    if [ "$status" -ne 0 ]; then
        printf '[exit %s]\n' "$status"
    fi
    return 0
}

show_command() {
    name=$1
    if command -v "$name" >/dev/null 2>&1; then
        printf '%-18s %s\n' "$name" "$(command -v "$name")"
    else
        printf '%-18s %s\n' "$name" 'NOT FOUND'
    fi
}

section 'Invocation'
printf 'timestamp_utc: %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
printf 'script: %s\n' "$script_dir/$(basename -- "$0")"
printf 'repository: %s\n' "$repo_dir"

section 'macOS and hardware'
run sw_vers
run uname -a
run uname -m
if command -v sysctl >/dev/null 2>&1; then
    run sysctl -n machdep.cpu.brand_string
fi

section 'Xcode selection and SDKs'
run xcode-select -p
run xcodebuild -version
run xcodebuild -showsdks
for sdk_name in iphoneos iphonesimulator appletvos appletvsimulator; do
    printf '\n-- %s --\n' "$sdk_name"
    run xcrun --sdk "$sdk_name" --show-sdk-version
    run xcrun --sdk "$sdk_name" --show-sdk-path
done

section 'tvOS framework availability in selected SDK'
appletvos_sdk=$(xcrun --sdk appletvos --show-sdk-path 2>/dev/null || true)
if [ -n "$appletvos_sdk" ] && [ -d "$appletvos_sdk/System/Library/Frameworks" ]; then
    for framework_name in AVFoundation AudioToolbox CoreGraphics Metal QuartzCore OpenGLES GameController CoreMotion MobileCoreServices ImageIO CoreHaptics CoreBluetooth IOSurface UIKit Foundation; do
        if [ -d "$appletvos_sdk/System/Library/Frameworks/$framework_name.framework" ]; then
            printf '%-24s present\n' "$framework_name"
        else
            printf '%-24s ABSENT\n' "$framework_name"
        fi
    done
else
    printf 'AppleTVOS SDK path unavailable.\n'
fi

section 'Simulator runtimes, Apple TV devices, and paired devices'
run xcrun simctl list runtimes
run_shell "xcrun simctl list devicetypes | awk '/Apple TV/{print}'"
run_shell "xcrun simctl list devices available | awk '/^-- tvOS/{show=1; print; next} /^-- /{show=0} show'"
run xcrun devicectl list devices

section '.NET SDK, runtimes, workloads, and tvOS packs'
show_command dotnet
if command -v dotnet >/dev/null 2>&1; then
    run dotnet --info
    run dotnet --list-sdks
    run dotnet --list-runtimes
    run dotnet workload list
    run_shell "dotnet workload search tvos | sed -n '1,80p'"
    dotnet_root=$(dirname -- "$(command -v dotnet)")
    if [ -L "$(command -v dotnet)" ]; then
        dotnet_target=$(readlink "$(command -v dotnet)")
        case "$dotnet_target" in
            /*) dotnet_root=$(dirname -- "$dotnet_target") ;;
        esac
    fi
    run_shell "find /usr/local/share/dotnet/packs -maxdepth 1 -type d \\( -iname '*tvos*' -o -iname '*ios*' \\) -print 2>/dev/null | sort"
    run_shell "find /usr/local/share/dotnet/sdk-manifests -maxdepth 3 -type d -iname '*tvos*' -print 2>/dev/null | sort"
fi

section 'Mono, MSBuild, and Xamarin Apple tooling'
for tool_name in mono msbuild xbuild mcs csc; do
    show_command "$tool_name"
done
if command -v mono >/dev/null 2>&1; then run mono --version; fi
if command -v msbuild >/dev/null 2>&1; then run msbuild -version; fi
if command -v xbuild >/dev/null 2>&1; then run xbuild /version; fi
if [ -x /Library/Frameworks/Xamarin.iOS.framework/Versions/Current/bin/mtouch ]; then
    run /Library/Frameworks/Xamarin.iOS.framework/Versions/Current/bin/mtouch --version
fi
run_shell "find -L /Library/Frameworks/Xamarin.iOS.framework/Versions/Current -maxdepth 7 \\( -name 'Xamarin.TVOS.dll' -o -name 'Xamarin.TVOS.CSharp.targets' -o -name 'Xamarin.AppleTVOS.sdk' -o -name 'Xamarin.AppleTVSimulator.sdk' \\) -print 2>/dev/null | sort"
if [ -f /Library/Frameworks/Xamarin.iOS.framework/Versions/Current/Version ]; then
    run sed -n 1,20p /Library/Frameworks/Xamarin.iOS.framework/Versions/Current/Version
fi

section 'Native build tools'
for tool_name in clang cmake ninja python3 python git make gmake pkg-config autoconf automake meson nasm yasm brew; do
    show_command "$tool_name"
done
if command -v clang >/dev/null 2>&1; then run clang --version; fi
if command -v cmake >/dev/null 2>&1; then run cmake --version; fi
if command -v ninja >/dev/null 2>&1; then run ninja --version; fi
if command -v python3 >/dev/null 2>&1; then run python3 --version; fi
if command -v git >/dev/null 2>&1; then run git --version; fi
if command -v make >/dev/null 2>&1; then run make --version; fi
if command -v gmake >/dev/null 2>&1; then run gmake --version; fi
if command -v pkg-config >/dev/null 2>&1; then run pkg-config --version; fi
if command -v autoconf >/dev/null 2>&1; then run autoconf --version; fi
for apple_tool in ar libtool lipo otool vtool codesign; do
    printf '%-18s ' "$apple_tool"
    xcrun -f "$apple_tool" 2>/dev/null || printf 'NOT FOUND\n'
done

section 'Code-signing identities'
run security find-identity -v -p codesigning

section 'Repository state'
if [ -d "$repo_dir/.git" ] || git -C "$repo_dir" rev-parse --git-dir >/dev/null 2>&1; then
    run git -C "$repo_dir" rev-parse HEAD
    run git -C "$repo_dir" branch --show-current
    run git -C "$repo_dir" status --short --branch
    run git -C "$repo_dir" remote -v
    run git -C "$repo_dir" submodule status --recursive
    run git -C "$repo_dir" submodule foreach --recursive 'printf "path=%s commit=%s branch=%s\\n" "$sm_path" "$(git rev-parse HEAD)" "$(git symbolic-ref --short -q HEAD || printf detached)"; git status --short'
else
    printf 'Not a Git worktree: %s\n' "$repo_dir"
fi

section 'Pinned native-source checkout presence'
builder_dir="$repo_dir/fnalibs-ios-builder-celeste"
for source_name in SDL2 FNA3D FAudio Theorafile MoltenVK; do
    if [ -d "$builder_dir/$source_name" ]; then
        printf '%-12s present' "$source_name"
        if git -C "$builder_dir/$source_name" rev-parse HEAD >/dev/null 2>&1; then
            printf ' commit=%s' "$(git -C "$builder_dir/$source_name" rev-parse HEAD)"
        fi
        printf '\n'
    else
        printf '%-12s NOT CHECKED OUT\n' "$source_name"
    fi
done

section 'Checked-in native archive architectures'
for archive_path in "$repo_dir"/fnalibs-ios-builder-celeste/prebuilt/*.a "$repo_dir"/celestemeow/*.a; do
    if [ -f "$archive_path" ]; then
        printf '%s: ' "$(basename -- "$archive_path")"
        lipo -archs "$archive_path" 2>&1 || file "$archive_path"
    fi
done

section 'Mounted FMOD 1.10.09 evidence'
fmod_revision=$(find /Volumes -maxdepth 8 -path '*/FMOD Programmers API/doc/revision.txt' -print -quit 2>/dev/null)
if [ -n "$fmod_revision" ]; then
    printf 'revision_file: %s\n' "$fmod_revision"
    run_shell "sed -n '/1\\.10\\.09/{p;q;}' \"$fmod_revision\""
    fmod_api_dir=$(dirname -- "$(dirname -- "$fmod_revision")")/api
    if [ -f "$fmod_api_dir/lowlevel/inc/fmod_common.h" ]; then
        run_shell "grep '^#define FMOD_VERSION ' \"$fmod_api_dir/lowlevel/inc/fmod_common.h\""
    fi
    for archive_path in \
        "$fmod_api_dir/lowlevel/lib/libfmod_iphoneos.a" \
        "$fmod_api_dir/studio/lib/libfmodstudio_iphoneos.a" \
        "$fmod_api_dir/lowlevel/lib/libfmod_appletvos.a" \
        "$fmod_api_dir/lowlevel/lib/libfmod_appletvsimulator.a" \
        "$fmod_api_dir/studio/lib/libfmodstudio_appletvos.a" \
        "$fmod_api_dir/studio/lib/libfmodstudio_appletvsimulator.a"; do
        if [ -f "$archive_path" ]; then
            printf '%s: ' "$archive_path"
            lipo -archs "$archive_path" 2>&1 || file "$archive_path"
        else
            printf '%s: MISSING\n' "$archive_path"
        fi
    done
else
    printf 'No mounted FMOD SDK revision.txt found below /Volumes.\n'
fi

section 'Diagnostic summary'
if command -v cmake >/dev/null 2>&1; then
    printf 'cmake: available (legacy/manual lanes only; public self-builder does not require it)\n'
else
    printf 'cmake: absent (acceptable for the public self-builder; legacy updatelibs.sh may require it)\n'
fi
if command -v ninja >/dev/null 2>&1; then
    printf 'ninja: available (not required by the public self-builder)\n'
else
    printf 'ninja: absent (acceptable for the public self-builder)\n'
fi
if command -v dotnet >/dev/null 2>&1 && dotnet workload list 2>/dev/null | grep -Eq '^[[:space:]]*tvos[[:space:]]'; then
    printf '.NET tvOS workload: installed\n'
else
    printf '.NET tvOS workload: NOT INSTALLED\n'
fi
if xcrun --sdk appletvos --show-sdk-path >/dev/null 2>&1 && xcrun --sdk appletvsimulator --show-sdk-path >/dev/null 2>&1; then
    printf 'Apple tvOS SDKs: device and simulator available\n'
else
    printf 'Apple tvOS SDKs: INCOMPLETE\n'
fi
printf 'No changes were intentionally made by this diagnostic.\n'
