#!/usr/bin/env python3
"""Validate ignored Stage 5B gameplay-audio logs without exposing private data."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import re


FORBIDDEN = (
    "unhandled managed exception",
    "STAGE3B_FATAL",
    "STAGE5B_AUDIO seq=",  # handled specially below for fmod-error/audio-fatal
    "tvStubs invoked",
    "tvStubs call detected",
    "FMOD LOW-LEVEL",
    "EntryPointNotFoundException",
    "DllNotFoundException",
    "ERR_VERSION",
    "ERR_HEADER_MISMATCH",
    "ContentLoadException",
)


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        fail(f"missing {label}: {token}")


def count(text: str, token: str) -> int:
    return sum(token in line for line in text.splitlines())


def parse(path: pathlib.Path, scenario: str, require_lifecycle: bool) -> dict[str, object]:
    text = path.read_text(encoding="utf-8", errors="replace")
    for token in FORBIDDEN:
        if token == "STAGE5B_AUDIO seq=":
            continue
        if token in text:
            fail(f"forbidden runtime marker: {token}")
    for line in text.splitlines():
        if "STAGE5B_AUDIO" in line and ("name=fmod-error" in line or "name=audio-fatal" in line or "name=audio-shutdown-failed" in line):
            fail(f"real FMOD failure marker: {line.strip()}")
    if re.search(r"fmod-low-level=[1-9]", text):
        fail("legacy no-audio low-level guard was reached")

    common = (
        "mode=CelesteAudio",
        "native interoperability self-test PASS",
        "real Celeste audio enabled",
        "name=audio-initialization-entered; managed=0x00011014",
        "name=native-version; native=0x00011009; managed=0x00011014",
        "name=fmod-sdl-registered; target=studio-low-level",
        "name=audio-initialized; studio=OK; low-level=OK; fmod-sdl=registered",
        "name=banks-ready; banks=7;",
        "STAGE5B_AUDIO_HEARTBEAT",
    )
    for token in common:
        require(text, token, "real Celeste audio checkpoint")
    bank_count = count(text, "name=bank-loaded;")
    if bank_count != 7:
        fail(f"expected exactly seven bank-loaded checkpoints, found {bank_count}")
    bank_names = (
        "Master Bank.bank", "Master Bank.strings.bank", "music.bank", "sfx.bank", "ui.bank",
        "dlc_music.bank", "dlc_sfx.bank",
    )
    for name in bank_names:
        require(text, f"file={name};", f"bank {name}")

    require(text, "name=event-created;", "real Celeste event creation")
    require(text, "name=event-start;", "real Celeste event start")
    result: dict[str, object] = {
        "schemaVersion": 1,
        "scenario": scenario,
        "nativeRuntime": "1.10.09",
        "managedApi": "1.10.20",
        "banks": 7,
        "audioHeartbeats": count(text, "STAGE5B_AUDIO_HEARTBEAT"),
        "musicEvents": count(text, "category=music"),
        "ambienceEvents": count(text, "category=ambience"),
        "uiEvents": count(text, "category=ui"),
        "gameplaySfxEvents": count(text, "category=gameplay-sfx"),
        "parameterEvents": count(text, "name=event-parameter"),
        "vcaReadbacks": count(text, "name=vca-volume"),
        "logSha256": hashlib.sha256(path.read_bytes()).hexdigest(),
    }

    if scenario in {"normal", "lifecycle", "second-launch"}:
        if result["musicEvents"] == 0:
            fail("normal gameplay log contains no music event evidence")
    if scenario == "normal":
        for category, key in (("ui", "uiEvents"), ("gameplay-sfx", "gameplaySfxEvents")):
            if result[key] == 0:
                fail(f"manual gameplay log contains no {category} event evidence")
        require(text, "scene-audio-context; scene=Celeste.Level;", "normal Level gameplay")
        require(text, "snapshot:/pause_menu", "Pause snapshot")
        require(text, "source=Die", "death entry")
        require(text, "source=DeathRoutine", "death/respawn lifecycle")
        require(text, "STAGE3C_RUMBLE", "gameplay haptic evidence")
        if result["vcaReadbacks"] < 6 or "requested=0.00" not in text or "requested=1.00" not in text:
            fail("manual gameplay log lacks zero/intermediate/full Settings VCA write/read evidence")
        requested = [float(value) for value in re.findall(r"name=vca-volume;[^\n]+requested=([0-9]+\.[0-9]+)", text)]
        if not any(0.0 < value < 1.0 for value in requested):
            fail("manual gameplay log lacks an intermediate Settings VCA value")
    if scenario.startswith("prologue-"):
        require(text, "name=event-trigger-cue", "real Prologue music cue")
        require(text, "name=next-scene-first-draw; scene=Celeste.Overworld", "post-Prologue scene")
        require(text, "name=diagnostic-clean-exit-requested", "clean diagnostic exit")
        require(text, "name=audio-shutdown-entered", "Studio shutdown entry")
        require(text, "name=audio-shutdown-completed", "Studio shutdown completion")
        result["triggerCue"] = "PASS"
        result["nextScene"] = "Celeste.Overworld"
        result["shutdown"] = "PASS"
    if require_lifecycle:
        require(text, "name=lifecycle-audio-paused; reason=resign-active; readback=true", "resign-active audio pause")
        require(text, "name=lifecycle-audio-resumed; reason=active; readback=false", "foreground audio resume")
        result["lifecycle"] = "PASS"
    return result


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log", required=True, help="ignored raw device console log")
    parser.add_argument("--scenario", choices=("normal", "lifecycle", "prologue-normal", "prologue-skip", "second-launch"), required=True)
    parser.add_argument("--require-lifecycle", action="store_true")
    parser.add_argument("--output", help="optional ignored privacy-safe JSON summary")
    args = parser.parse_args()
    path = pathlib.Path(args.log)
    if not path.is_file():
        fail(f"log does not exist: {path}")
    result = parse(path, args.scenario, args.require_lifecycle)
    if args.output:
        output = pathlib.Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    main()
