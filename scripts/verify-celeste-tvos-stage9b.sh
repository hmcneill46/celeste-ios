#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/verify-celeste-tvos-stage9b.sh [options]

Verify the compressed v2 persistence policy, implementation, diagnostic
evidence, retained ignored Chapter 5 fixtures, and an optional app bundle.

Options:
  --diagnostic-log FILE       Verify the 90+ test simulator/device log
  --retained-fixture-root DIR Verify ignored first/latest/max Stage 9A payloads
  --app DIR                   Verify a built app through the Stage 6 package gate
  --platform NAME             simulator or device (required with --app)
  --skip-stage6-foundation    Skip the existing generated/native foundation gate
  -h, --help                  Show help
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
DIAGNOSTIC_LOG=""
RETAINED_ROOT=""
APP_DIR=""
PLATFORM=""
SKIP_STAGE6=0

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --diagnostic-log) DIAGNOSTIC_LOG="$(repo_path "$2")"; shift 2 ;;
    --retained-fixture-root) RETAINED_ROOT="$(repo_path "$2")"; shift 2 ;;
    --app) APP_DIR="$(repo_path "$2")"; shift 2 ;;
    --platform) PLATFORM="$2"; shift 2 ;;
    --skip-stage6-foundation) SKIP_STAGE6=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; exit 2 ;;
  esac
done

for command in python3 git shasum; do
  command -v "$command" >/dev/null || { echo "error: missing tool: $command" >&2; exit 1; }
done

python3 - "$REPO_ROOT" <<'PY'
import json, pathlib, sys
repo=pathlib.Path(sys.argv[1])
policy=json.loads((repo/"managed/celeste-stage6-policy.json").read_text())
storage=policy["storage"]
expected={
    "formatVersion":2,
    "previousFormatVersion":1,
    "legacyMigrationVersion":0,
    "hardTotalBudgetBytes":262144,
    "hardEnvelopeBudgetBytes":126976,
    "hardCompressedEntryBudgetBytes":98304,
}
for key,value in expected.items():
    if storage.get(key)!=value: raise SystemExit(f"error: Stage 9B policy mismatch: {key}")
if storage.get("compression")!={"algorithmId":1,"name":"zlib","level":9,"scope":"independent-logical-entry"}:
    raise SystemExit("error: Stage 9B compression lock mismatch")
files=policy["writableFiles"]
if [x["logicalName"] for x in files] != ["settings","0","1","2"]:
    raise SystemExit("error: durable allow-list changed")
if [x["maximumPayloadBytes"] for x in files] != [65536,262144,262144,262144]:
    raise SystemExit("error: v2 uncompressed safety limits changed")
store=(repo/"tvos/CelesteTvOSRuntimeHost/Stage6PersistenceStore.cs").read_text()
for token in (
    "FormatVersion = 2", "PreviousFormatVersion = 1", "LegacyFormatVersion = 0",
    "ZLibStream", "CompressionLevel.SmallestSize", "DecompressBounded",
    '"compressed-hash-mismatch"', '"uncompressed-hash-mismatch"',
    '"unsupported-compression"', '"CelesteTvOS.Persistence.v1"',
    "ExportLogicalPayloadForFutureSaveManager",
):
    if token not in store: raise SystemExit(f"error: Stage 9B implementation token missing: {token}")
print("PASS: v2 policy, bounded compression, compatibility, and fixed allow-list")
PY

if [[ "$SKIP_STAGE6" -eq 0 ]]; then
  stage6_args=(--skip-toolchain)
  if [[ -n "$APP_DIR" ]]; then
    [[ -n "$PLATFORM" ]] || { echo "error: --platform is required with --app" >&2; exit 2; }
    stage6_args+=(--app "$APP_DIR" --platform "$PLATFORM")
  fi
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage6.sh" "${stage6_args[@]}"
fi

if [[ -n "$DIAGNOSTIC_LOG" ]]; then
  [[ -f "$DIAGNOSTIC_LOG" ]] || { echo "error: diagnostic log not found" >&2; exit 2; }
  output="${DIAGNOSTIC_LOG%.*}.summary.json"
  "$REPO_ROOT/scripts/verify-celeste-tvos-stage6-evidence.py" \
    --log "$DIAGNOSTIC_LOG" --scenario diagnostic --output "$output"
  grep -Fq 'STAGE9B_THREE_SLOT ' "$DIAGNOSTIC_LOG" || { echo "error: three-slot fixture evidence missing" >&2; exit 1; }
  echo "PASS: Stage 9B diagnostic evidence"
fi

if [[ -n "$RETAINED_ROOT" ]]; then
  [[ -d "$RETAINED_ROOT" ]] || { echo "error: retained fixture root not found" >&2; exit 2; }
  expected="$REPO_ROOT/.build/celeste-runtime/stage9b-retained-expected.sha256"
  mkdir -p "$(dirname "$expected")"
  printf '%s  %s\n' \
    7684bbe9ba363c8071b6dfc7ee25a91d7a0905ff5a196d5a38b5a9763aaa4266 "$RETAINED_ROOT/first-over.celeste" \
    33eb7e8e2a1e29bbd1de5a5d77457fb33ed56cb69751a5990caf5ac7de976afe "$RETAINED_ROOT/latest-over.celeste" \
    89c003ebfa51d09dd10c7509b3948d1789e6265108df93ffdac2f7e1695d31df "$RETAINED_ROOT/max-over.celeste" > "$expected"
  shasum -a 256 -c "$expected" >/dev/null
  git -C "$REPO_ROOT" check-ignore --no-index -q -- "$RETAINED_ROOT/first-over.celeste" || {
    echo "error: retained proprietary fixture root is not ignored" >&2; exit 1;
  }
  echo "PASS: retained Chapter 5 fixtures match private Stage 9A evidence and remain ignored"
fi

if git -C "$REPO_ROOT" ls-files | grep -E '\.(celeste|bank|a|xcframework|ipa|mobileprovision)$' >/dev/null; then
  echo "error: proprietary/generated binary material is tracked" >&2
  exit 1
fi
echo "PASS: Stage 9B repository isolation"
