#!/usr/bin/env python3
"""Validate privacy-safe Stage 3B preflight and first-frame runtime evidence."""

from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import json
import pathlib
import re
import struct
from typing import Any


READERS = {
    "CharReader",
    "EffectReader",
    "ListReader<T>",
    "RectangleReader",
    "SpriteFontReader",
    "Texture2DReader",
    "Vector3Reader",
}
SETTINGS_CASES = {
    "first-run-defaults=PASS",
    "default-round-trip=PASS",
    "non-default-round-trip=PASS",
    "UserIO-round-trip=PASS",
    "malformed-and-unknown=PASS",
    "legacy-XML=PASS",
}
FORBIDDEN_PATTERNS = {
    "unhandled managed exception": re.compile(r"unhandled managed exception", re.IGNORECASE),
    "managed fatal checkpoint": re.compile(r"STAGE3B_FATAL", re.IGNORECASE),
    "low-level FMOD guard": re.compile(r"FMOD LOW-LEVEL|fmod-low-level=[1-9]|low-level=[1-9]", re.IGNORECASE),
    "tvStubs invocation": re.compile(r"tvStubs (?:invoked|call detected)|STAGE3B_TVSTUB", re.IGNORECASE),
    "content load failure": re.compile(r"ContentLoadException|missing-content", re.IGNORECASE),
    "native import failure": re.compile(r"DllNotFoundException|EntryPointNotFoundException", re.IGNORECASE),
}
PRIVATE_PATTERNS = {
    "absolute user path": re.compile("/" + "Users/"),
    "email address": re.compile(r"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}"),
    "Apple identifier": re.compile(r"\b[A-Fa-f0-9]{8}-[A-Fa-f0-9-]{27,}\b"),
}


def labelled(value: str) -> tuple[str, pathlib.Path]:
    if "=" not in value:
        raise argparse.ArgumentTypeError("expected LABEL=PATH")
    label, raw_path = value.split("=", 1)
    if not re.fullmatch(r"[a-z0-9][a-z0-9._-]*", label):
        raise argparse.ArgumentTypeError("labels must use lowercase letters, numbers, '.', '_' or '-'")
    return label, pathlib.Path(raw_path)


def read_text(path: pathlib.Path) -> str:
    if not path.is_file():
        raise SystemExit(f"error: evidence file is missing: {path.name}")
    return path.read_text(encoding="utf-8", errors="replace")


def require(text: str, needle: str, label: str) -> None:
    if needle not in text:
        raise SystemExit(f"error: {label} is missing required evidence: {needle}")


def reject_failures(text: str, label: str) -> None:
    for name, pattern in FORBIDDEN_PATTERNS.items():
        if pattern.search(text):
            raise SystemExit(f"error: {label} contains forbidden evidence: {name}")


def reflection_manifest(text: str, label: str) -> dict[str, dict[str, Any]]:
    values: dict[str, dict[str, Any]] = {}
    pattern = re.compile(
        r"reflection discovery PASS: category=([^;\r\n]+); count=(\d+); sha256=([0-9a-f]{64})"
    )
    for category, count, digest in pattern.findall(text):
        if category in values:
            raise SystemExit(f"error: {label} repeats reflection category {category}")
        values[category] = {"count": int(count), "sha256": digest}
    if len(values) != 10:
        raise SystemExit(f"error: {label} has {len(values)} reflection categories, expected 10")
    return dict(sorted(values.items()))


def verify_preflight(path: pathlib.Path, label: str) -> dict[str, Any]:
    text = read_text(path)
    reject_failures(text, label)
    require(text, "mode=CelestePreflight", label)
    require(text, "native interoperability self-test PASS", label)
    require(text, "tvStubs call sites=0", label)
    require(text, "settings preflight PASS:", label)
    for case in SETTINGS_CASES:
        require(text, case, label)
    readers = re.findall(r"XNB reader PASS: ([^ ]+(?:<T>)?) asset=", text)
    if len(readers) != 7 or set(readers) != READERS:
        raise SystemExit(f"error: {label} XNB readers are {sorted(set(readers))}, expected {sorted(READERS)}")
    require(text, "name=preflight-complete; settings=PASS; reflection=PASS; xnb-readers=7/7; fmod-low-level=0", label)
    require(text, "CelestePreflight: run loop returned cleanly", label)
    return {
        "result": "PASS",
        "settingsCases": sorted(SETTINGS_CASES),
        "xnbReaders": sorted(READERS),
        "reflection": reflection_manifest(text, label),
        "nativeInterop": "PASS",
        "fmodLowLevelCalls": 0,
        "tvStubsCalls": 0,
    }


def line_timestamp(line: str) -> dt.datetime | None:
    match = re.match(r"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3})", line)
    if not match:
        return None
    return dt.datetime.strptime(match.group(1), "%Y-%m-%d %H:%M:%S.%f")


def checkpoint_timestamp(text: str, checkpoint: str) -> dt.datetime:
    for line in text.splitlines():
        if f"name={checkpoint}" in line:
            value = line_timestamp(line)
            if value is not None:
                return value
    raise SystemExit(f"error: checkpoint has no timestamp: {checkpoint}")


def heartbeats(text: str) -> list[dict[str, Any]]:
    result: list[dict[str, Any]] = []
    pattern = re.compile(
        r"STAGE3B_CELESTE_HEARTBEAT draws=(\d+); updates=(\d+); "
        r"scene=([^;\r\n]+); entities=(\d+); renderers=(\d+); fmod-low-level=(\d+)"
    )
    for line in text.splitlines():
        match = pattern.search(line)
        if not match:
            continue
        timestamp = line_timestamp(line)
        if timestamp is None:
            raise SystemExit("error: heartbeat is missing a parseable timestamp")
        result.append({
            "time": timestamp,
            "draws": int(match.group(1)),
            "updates": int(match.group(2)),
            "scene": match.group(3),
            "entities": int(match.group(4)),
            "renderers": int(match.group(5)),
            "fmodLowLevel": int(match.group(6)),
        })
    return result


def lifecycle_order(text: str, label: str) -> bool:
    markers = ["lifecycle resign-active", "lifecycle background", "lifecycle foreground"]
    positions = [text.find(marker) for marker in markers]
    if any(position < 0 for position in positions) or positions != sorted(positions):
        raise SystemExit(f"error: {label} lacks the ordered background/foreground lifecycle cycle")
    resumed = text.find("lifecycle active", positions[-1])
    if resumed < 0:
        raise SystemExit(f"error: {label} lacks active state after foreground")
    after_resume = text[resumed:]
    if "STAGE3B_CELESTE_HEARTBEAT" not in after_resume:
        raise SystemExit(f"error: {label} lacks a Celeste heartbeat after foreground")
    return True


def verify_celeste(path: pathlib.Path, label: str, require_lifecycle: bool) -> dict[str, Any]:
    text = read_text(path)
    reject_failures(text, label)
    for needle in (
        "mode=Celeste",
        "target=",
        "native interoperability self-test PASS",
        "tvStubs call sites=0",
        "CELESTE : 1.4.0.0",
        "FNA: FNA3D Driver: Metal",
        "name=celeste-entry-invocation; method=Celeste.Celeste.Run",
        "name=settings-load",
        "name=celeste-constructor",
        "name=content-manager-created",
        "name=graphics-device-available",
        "name=first-celeste-update; scene=Celeste.",
        "name=first-celeste-draw; scene=Celeste.",
        "name=no-audio-initialized",
    ):
        require(text, needle, label)
    samples = heartbeats(text)
    if len(samples) < 13:
        raise SystemExit(f"error: {label} has {len(samples)} heartbeats, fewer than the sustained-run minimum")
    if any(sample["fmodLowLevel"] != 0 for sample in samples):
        raise SystemExit(f"error: {label} reached an FMOD low-level guard")
    if any(later["draws"] <= earlier["draws"] or later["updates"] <= earlier["updates"]
           for earlier, later in zip(samples, samples[1:])):
        raise SystemExit(f"error: {label} heartbeat counters do not increase monotonically")
    if not any(sample["scene"] == "Celeste.Overworld" and sample["renderers"] > 0 for sample in samples):
        raise SystemExit(f"error: {label} never proves a rendered Celeste.Overworld frame")
    first_draw = checkpoint_timestamp(text, "first-celeste-draw")
    sustained_seconds = (samples[-1]["time"] - first_draw).total_seconds()
    if sustained_seconds < 60:
        raise SystemExit(f"error: {label} sustained frames for {sustained_seconds:.3f}s, expected at least 60s")
    lifecycle = lifecycle_order(text, label) if require_lifecycle else False
    return {
        "result": "PASS",
        "celesteVersion": "1.4.0.0",
        "renderer": "Metal",
        "gpu": "Apple A15 GPU" if "Device Name: Apple A15 GPU" in text else "Apple tvOS simulator GPU",
        "firstScene": re.search(r"name=first-celeste-draw; scene=([^;]+)", text).group(1),
        "finalScene": samples[-1]["scene"],
        "heartbeatCount": len(samples),
        "firstHeartbeatDraw": samples[0]["draws"],
        "lastHeartbeatDraw": samples[-1]["draws"],
        "drawProgression": samples[-1]["draws"] - samples[0]["draws"],
        "sustainedSecondsAfterFirstDraw": round(sustained_seconds, 3),
        "backgroundForeground": lifecycle,
        "fmodLowLevelCalls": 0,
        "tvStubsCalls": 0,
    }


def verify_second_launch(path: pathlib.Path, label: str) -> dict[str, Any]:
    text = read_text(path)
    reject_failures(text, label)
    for needle in (
        "mode=Celeste",
        "CELESTE : 1.4.0.0",
        "FNA: FNA3D Driver: Metal",
        "name=celeste-entry-invocation",
        "name=first-celeste-update",
        "name=first-celeste-draw",
        "name=no-audio-initialized",
    ):
        require(text, needle, label)
    samples = heartbeats(text)
    if len(samples) < 3 or not any(sample["scene"] == "Celeste.Overworld" for sample in samples):
        raise SystemExit(f"error: {label} does not prove a clean advancing second launch")
    return {
        "result": "PASS",
        "heartbeatCount": len(samples),
        "finalScene": samples[-1]["scene"],
        "lastHeartbeatDraw": samples[-1]["draws"],
        "fmodLowLevelCalls": 0,
        "tvStubsCalls": 0,
    }


def verify_png(path: pathlib.Path, label: str) -> dict[str, Any]:
    data = path.read_bytes() if path.is_file() else b""
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"error: {label} is not a valid PNG evidence file")
    width, height = struct.unpack(">II", data[16:24])
    if width < 640 or height < 360:
        raise SystemExit(f"error: {label} visual evidence is unexpectedly small ({width}x{height})")
    return {
        "sha256": hashlib.sha256(data).hexdigest(),
        "width": width,
        "height": height,
    }


def verify_diagnostics(path: pathlib.Path, label: str, policy: dict[str, Any]) -> dict[str, Any]:
    text = read_text(path)
    if re.search(r"\berror (?:IL|AOT|MT|NETSDK|CS)\d+\b", text):
        raise SystemExit(f"error: {label} contains a compiler/linker error diagnostic")
    diagnostics = re.findall(r"warning (IL\d+): (.*)", text)
    if not diagnostics:
        raise SystemExit(f"error: {label} contains no trim diagnostics to validate")
    rules = policy.get("rules", [])
    observed_rules: set[int] = set()
    for code, message in diagnostics:
        matches = [index for index, rule in enumerate(rules)
                   if rule["code"] == code and rule["messageContains"] in message]
        if len(matches) != 1:
            raise SystemExit(f"error: {label} diagnostic {code} matched {len(matches)} warning-policy rules")
        observed_rules.add(matches[0])
    missing = [rules[index]["code"] + ":" + rules[index]["messageContains"]
               for index in range(len(rules)) if index not in observed_rules]
    if missing:
        raise SystemExit(f"error: {label} did not exercise {len(missing)} locked warning-policy rules")
    return {
        "result": "PASS",
        "occurrenceCount": len(diagnostics),
        "uniqueDiagnosticCount": len(set(diagnostics)),
        "codes": sorted({code for code, _ in diagnostics}),
        "policyRuleCount": len(rules),
        "unexplainedCount": 0,
    }


def main() -> None:
    parser = argparse.ArgumentParser(
        description="Validate Stage 3B preflight, sustained-frame, lifecycle, second-launch and visual evidence without copying logs."
    )
    parser.add_argument("--reference-preflight", required=True, type=pathlib.Path,
                        help="Debug/untrimmed reference preflight log")
    parser.add_argument("--preflight", action="append", default=[], type=labelled, metavar="LABEL=PATH",
                        help="candidate trimmed/full-AOT preflight log; repeatable")
    parser.add_argument("--celeste", action="append", default=[], type=labelled, metavar="LABEL=PATH",
                        help="sustained first-launch Celeste log; repeatable")
    parser.add_argument("--second-launch", action="append", default=[], type=labelled, metavar="LABEL=PATH",
                        help="clean second-launch Celeste log; repeatable")
    parser.add_argument("--visual", action="append", default=[], type=labelled, metavar="LABEL=PATH",
                        help="PNG visual evidence; repeatable")
    parser.add_argument("--diagnostic-log", action="append", default=[], type=labelled, metavar="LABEL=PATH",
                        help="Release/device trim diagnostic log; repeatable")
    parser.add_argument(
        "--warning-policy",
        type=pathlib.Path,
        default=pathlib.Path(__file__).resolve().parent.parent / "managed/celeste-stage3b-warning-policy.json",
        help="tracked warning-policy JSON (default: managed/celeste-stage3b-warning-policy.json)",
    )
    parser.add_argument("--output", type=pathlib.Path,
                        help="write a privacy-safe JSON summary containing labels, counts and hashes only")
    args = parser.parse_args()
    if not args.preflight or not args.celeste or not args.second_launch:
        parser.error("at least one --preflight, --celeste and --second-launch are required")

    reference = verify_preflight(args.reference_preflight, "reference-preflight")
    candidates = {label: verify_preflight(path, label) for label, path in args.preflight}
    for label, candidate in candidates.items():
        if candidate["reflection"] != reference["reflection"]:
            raise SystemExit(f"error: {label} reflection discovery differs from the reference preflight")
    celeste = {label: verify_celeste(path, label, require_lifecycle=True) for label, path in args.celeste}
    second = {label: verify_second_launch(path, label) for label, path in args.second_launch}
    visuals = {label: verify_png(path, label) for label, path in args.visual}
    warning_policy = json.loads(args.warning_policy.read_text(encoding="utf-8"))
    diagnostics = {label: verify_diagnostics(path, label, warning_policy)
                   for label, path in args.diagnostic_log}
    if len(visuals) > 1 and len({value["sha256"] for value in visuals.values()}) != len(visuals):
        raise SystemExit("error: visual evidence contains duplicate frames")

    result = {
        "schemaVersion": 1,
        "result": "PASS",
        "referencePreflight": reference,
        "candidatePreflights": dict(sorted(candidates.items())),
        "celesteFirstLaunches": dict(sorted(celeste.items())),
        "celesteSecondLaunches": dict(sorted(second.items())),
        "visualEvidence": dict(sorted(visuals.items())),
        "trimDiagnostics": dict(sorted(diagnostics.items())),
        "privacy": "summary stores labels, counts, dimensions and hashes only; raw evidence remains ignored",
    }
    encoded = json.dumps(result, indent=2, sort_keys=True) + "\n"
    for name, pattern in PRIVATE_PATTERNS.items():
        if pattern.search(encoded):
            raise SystemExit(f"error: generated evidence summary contains {name}")
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(encoded, encoding="utf-8")
    print("Stage 3B runtime evidence: PASS")
    for label, value in sorted(celeste.items()):
        print(f"{label}: {value['heartbeatCount']} heartbeats; {value['sustainedSecondsAfterFirstDraw']:.3f}s; {value['finalScene']}")


if __name__ == "__main__":
    main()
