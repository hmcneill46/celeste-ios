#!/usr/bin/env python3
"""Verify privacy-safe Stage 6 console evidence without reading save payloads."""

from __future__ import annotations

import argparse
import json
import pathlib
import re


RESTORE = re.compile(r"STAGE6_RESTORE .*generation=(\d+);(?: format=v\d+;)? logical=([0-9a-f]{64});")
COMMIT = re.compile(r"STAGE6_COMMIT .*result=committed; .*generation=(\d+);(?: format=v\d+;)? logical=([0-9a-f]{64});.*slot-bytes=(\d+); bridge-bytes=(\d+);")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--log", required=True, type=pathlib.Path)
    parser.add_argument("--scenario", required=True, choices=("diagnostic", "game", "restart-prepare", "restart-verify", "process-kill"))
    parser.add_argument("--expected-token", type=pathlib.Path)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    args = parser.parse_args()
    text = args.log.read_text(errors="replace")
    forbidden = ("<Settings", "<SaveData", "/Users/", "IDENTITY_FINGERPRINT", "embedded.mobileprovision")
    if any(token in text for token in forbidden):
        raise SystemExit("error: evidence contains payload or private-path material")
    if "STAGE6_DEFAULTS_SIZE_LIMIT_WARNING" in text or "unhandled managed exception" in text:
        raise SystemExit("error: Stage 6 evidence contains a size warning or unhandled exception")
    restores = [(int(generation), digest) for generation, digest in RESTORE.findall(text)]
    commits = [(int(generation), digest, int(slot), int(total)) for generation, digest, slot, total in COMMIT.findall(text)]
    if any(slot > 126976 or total > 262144 for _, _, slot, total in commits):
        raise SystemExit("error: evidence exceeds the Stage 6 size budget")
    if args.scenario == "diagnostic":
        tests = set(re.findall(r"STAGE6_TEST name=([^;]+); result=PASS", text))
        required = {
            "v1-startup-does-not-rewrite",
            "first-v2-write-preserves-old-v1",
            "compressed-checksum-corruption",
            "trailing-compressed-data-rejected",
            "save-uncompressed-limit-inclusive",
            "save-uncompressed-limit-plus-one",
            "large-three-slot-two-generation-headroom",
            "actual-userio-save-slot-write",
            "eight-concurrent-commits-serialize",
            "size-warning-injection-retains-prior",
        }
        if "STAGE9B_DIAGNOSTIC_PASS" not in text or len(tests) < 90 or not required.issubset(tests):
            raise SystemExit(f"error: incomplete failure-injection suite ({len(tests)} tests)")
    elif args.scenario in {"game", "restart-prepare", "process-kill"}:
        if not commits:
            raise SystemExit("error: no complete durable generation commit was observed")
    elif args.scenario == "restart-verify":
        if not restores:
            raise SystemExit("error: no durable generation was restored after restart")
        if args.expected_token is None:
            raise SystemExit("error: restart verification requires --expected-token")
        expected = json.loads(args.expected_token.read_text())
        if restores[-1] != (expected["generation"], expected["logicalSha256"]):
            raise SystemExit("error: restored generation/hash differs from pre-restart evidence")
    summary = {
        "schemaVersion": 1,
        "scenario": args.scenario,
        "restoreCount": len(restores),
        "commitCount": len(commits),
        "lastRestoredGeneration": restores[-1][0] if restores else None,
        "lastCommittedGeneration": commits[-1][0] if commits else None,
        "maximumSlotBytes": max((item[2] for item in commits), default=0),
        "maximumBridgeBytes": max((item[3] for item in commits), default=0),
        "payloadContentsRecorded": False,
        "privateIdentifiersRecorded": False,
        "result": "PASS",
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 6 {args.scenario} evidence; restores={len(restores)} commits={len(commits)}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
