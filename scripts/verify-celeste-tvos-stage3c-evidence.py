#!/usr/bin/env python3
"""Validate ignored Stage 3C runtime logs without copying private evidence."""

from __future__ import annotations

import argparse
import json
import pathlib
import re
import sys
from typing import Iterable


FORBIDDEN = (
    "unhandled managed exception",
    "STAGE3B_FATAL",
    "FMOD LOW-LEVEL",
    "tvStubs invoked",
    "tvStubs call detected",
    "ContentLoadException",
)


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def require(text: str, token: str, label: str) -> None:
    if token not in text:
        fail(f"missing {label}: {token}")


def require_order(text: str, tokens: Iterable[str]) -> None:
    offset = 0
    for token in tokens:
        found = text.find(token, offset)
        if found < 0:
            fail(f"ordered checkpoint missing after byte {offset}: {token}")
        offset = found + len(token)


def monotonic_values(text: str, marker: str) -> list[float]:
    values: list[float] = []
    for line in text.splitlines():
        if marker not in line:
            continue
        match = re.search(r"\bt=([0-9]+(?:\.[0-9]+)?)", line)
        if match:
            values.append(float(match.group(1)))
    return values


def checkpoint_time(text: str, name: str) -> float:
    for line in text.splitlines():
        if "STAGE3C_CHECKPOINT" in line and f"name={name}" in line:
            match = re.search(r"\bt=([0-9]+(?:\.[0-9]+)?)", line)
            if match:
                return float(match.group(1))
    fail(f"checkpoint has no monotonic timestamp: {name}")
    return 0.0


def validate_runtime(text: str, scenario: str, minimum_seconds: int) -> dict[str, object]:
    for token in FORBIDDEN:
        if token in text:
            fail(f"forbidden runtime failure detected: {token}")
    if re.search(r"fmod-low-level=[1-9]", text):
        fail("FMOD low-level guard count is nonzero")

    if scenario == "preflight":
        require(text, "SaveData preflight PASS:", "SaveData preflight")
        require(text, "savedata=PASS; reflection=PASS; xnb-readers=7/7; fmod-low-level=0", "complete preflight")
        return {"scenario": scenario, "preflight": "PASS"}

    common = [
        "name=prologue-diagnostic-route",
        "name=bird-tutorial-entered-sequence",
        "name=bird-tutorial-entered",
    ]
    if scenario == "normal":
        action = [
            "name=controller-input-received",
            "name=dash-initiated",
            "name=music-cue-requested; source=post-dash",
            "name=cutscene-completion-requested",
        ]
    elif scenario == "skip":
        action = [
            "name=cutscene-skip-requested",
            "name=cutscene-skip-completing",
            "name=music-cue-requested; source=cutscene-skip",
        ]
    else:
        action = []
        require(text, "scenario=manual", "manual diagnostic scenario")

    tail = [
        "name=transition-fade-entered",
        "name=progression-state-updated",
        "name=save-data-serialization-entered",
        "name=save-data-serialization-completed",
        "name=next-scene-constructed; scene=Celeste.Overworld",
        "name=next-scene-first-update; scene=Celeste.Overworld",
        "name=next-scene-first-draw; scene=Celeste.Overworld",
    ]
    require_order(text, [*common, *action, *tail])
    require(text, "action=start", "rumble start")
    require(text, "action=stop", "rumble stop")
    if "STAGE3C_RUMBLE_WATCHDOG" in text:
        fail("rumble watchdog intervened during accepted Prologue run")
    heartbeat_scope = text
    if scenario == "manual":
        active_disconnect = text.find("haptic-active-before-stop=True")
        if active_disconnect >= 0:
            heartbeat_scope = text[active_disconnect:]
    for line in heartbeat_scope.splitlines():
        if "STAGE3C_POST_TRANSITION_HEARTBEAT" in line and "rumble-active=0" not in line:
            fail("post-disconnect heartbeat retained active rumble" if scenario == "manual"
                 else "post-transition heartbeat retained active rumble")

    first_draw = checkpoint_time(text, "next-scene-first-draw")
    heartbeats = monotonic_values(text, "STAGE3C_POST_TRANSITION_HEARTBEAT")
    if not heartbeats or heartbeats[-1] - first_draw < minimum_seconds:
        observed = 0.0 if not heartbeats else heartbeats[-1] - first_draw
        fail(f"post-transition evidence is only {observed:.3f}s; need {minimum_seconds}s")
    require(text, "name=diagnostic-clean-exit-requested", "clean diagnostic exit")
    return {
        "scenario": scenario,
        "postTransitionSeconds": round(heartbeats[-1] - first_draw, 3),
        "lastHeartbeat": heartbeats[-1],
        "rumble": "start-and-stop-PASS",
        "save": "PASS",
        "nextScene": "Celeste.Overworld",
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--log", required=True, help="ignored raw console log")
    parser.add_argument("--scenario", choices=("fault", "preflight", "normal", "skip", "manual"), required=True)
    parser.add_argument("--platform", choices=("simulator", "device"), required=True)
    parser.add_argument("--minimum-post-transition-seconds", type=int, default=60)
    parser.add_argument("--require-lifecycle", action="store_true")
    parser.add_argument("--require-controller-cycle", action="store_true")
    parser.add_argument("--output", help="optional privacy-safe JSON summary")
    args = parser.parse_args()

    path = pathlib.Path(args.log)
    if not path.is_file():
        fail(f"log does not exist: {path}")
    text = path.read_text(encoding="utf-8", errors="replace")
    require(text, f"target={'simulator' if args.platform == 'simulator' else 'physical-device'}", "target platform")

    if args.scenario == "fault":
        require_order(text, [
            "name=bird-tutorial-entered",
            "name=dash-initiated",
            "name=music-cue-requested; source=post-dash",
            "name=fatal",
        ])
        require(text, "type=System.NullReferenceException", "preserved fault type")
        require(text, "reason=unhandled-exception", "exception-safe rumble stop")
        if "name=save-data-serialization-entered" in text:
            fail("preserved fault unexpectedly reached SaveData serialization")
        result: dict[str, object] = {
            "scenario": "fault",
            "earliestCause": "null no-audio music EventInstance triggerCue",
            "saveReached": False,
        }
    else:
        result = validate_runtime(text, args.scenario, args.minimum_post_transition_seconds)

    if args.require_lifecycle:
        require(text, "reason=resign-active", "resign-active haptic stop")
        require(text, "reason=background", "background haptic stop")
        require(text, "lifecycle foreground", "foreground lifecycle")
        result["lifecycle"] = "PASS"
    if args.require_controller_cycle:
        require_order(text, [
            "name=manual-haptic-probe-started",
            "reason=controller-disconnect",
            "haptic-active-before-stop=True",
            "name=controller-reconnected;",
            "name=controller-input-after-reconnect",
        ])
        result["controllerCycle"] = "PASS"

    result.update({"schemaVersion": 1, "platform": args.platform, "logSha256": __import__("hashlib").sha256(path.read_bytes()).hexdigest()})
    if args.output:
        output = pathlib.Path(args.output)
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(result, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    main()
