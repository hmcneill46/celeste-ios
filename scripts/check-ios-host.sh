#!/usr/bin/env bash
set -euo pipefail

expected_dotnet="10.0.302"
expected_workload_set="10.0.302.0"
expected_xcode="26.6"
expected_xcode_build="17F113"
expected_sdk="26.5"
expected_macos="26.3"

fail() { echo "error: $*" >&2; exit 1; }
for tool in dotnet sw_vers uname xcodebuild xcrun; do
  command -v "$tool" >/dev/null || fail "missing required host command: $tool"
done
[[ "$(uname -m)" == "arm64" ]] || fail "requires an Apple-silicon arm64 Mac"
[[ "$(sw_vers -productVersion)" == "$expected_macos" ]] || fail "requires macOS $expected_macos"
[[ "$(dotnet --version)" == "$expected_dotnet" ]] || fail "requires .NET SDK $expected_dotnet"
[[ "$(dotnet workload --version)" == "$expected_workload_set" ]] || fail "requires workload set $expected_workload_set"
xcode_version="$(xcodebuild -version)"
grep -Fxq "Xcode $expected_xcode" <<<"$xcode_version" || fail "requires Xcode $expected_xcode"
grep -Fxq "Build version $expected_xcode_build" <<<"$xcode_version" || fail "requires Xcode build $expected_xcode_build"
[[ "$(xcrun --sdk iphoneos --show-sdk-version)" == "$expected_sdk" ]] || fail "iPhoneOS SDK drift"
[[ "$(xcrun --sdk iphonesimulator --show-sdk-version)" == "$expected_sdk" ]] || fail "iPhoneSimulator SDK drift"
workloads="$(dotnet workload list)"
grep -Eq '^ios[[:space:]]+26\.5\.10301/10\.0\.100' <<<"$workloads" || fail "exact iOS workload is unavailable"
grep -Eq '^tvos[[:space:]]+26\.5\.10301/10\.0\.100' <<<"$workloads" || fail "exact tvOS workload is unavailable"
printf 'PASS: modern iOS host doctor (.NET %s, Xcode %s %s, iOS SDK %s)\n' \
  "$expected_dotnet" "$expected_xcode" "$expected_xcode_build" "$expected_sdk"
