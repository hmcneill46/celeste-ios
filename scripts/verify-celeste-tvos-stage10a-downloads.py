#!/usr/bin/env python3
"""Compare browser downloads with privacy-safe device export evidence."""

from __future__ import annotations

import argparse
import hashlib
import pathlib
import re


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify Stage 10A downloaded .celeste payloads")
    parser.add_argument("--log", required=True, type=pathlib.Path)
    parser.add_argument("--downloads", required=True, type=pathlib.Path)
    args = parser.parse_args()
    text = args.log.read_text(errors="replace")
    evidence = dict(re.findall(r"STAGE10A_HTTP .*?logical=(settings|0|1|2); download-sha256=([0-9a-f]{64})", text))
    if not evidence:
        raise SystemExit("error: no successful Stage 10A download evidence was captured")
    expected_names = {"settings": "settings.celeste", "0": "0.celeste", "1": "1.celeste", "2": "2.celeste"}
    verified = []
    for logical, expected_hash in sorted(evidence.items()):
        path = args.downloads / expected_names[logical]
        if not path.is_file():
            raise SystemExit(f"error: captured {logical} download is missing from --downloads")
        actual = hashlib.sha256(path.read_bytes()).hexdigest()
        if actual != expected_hash:
            raise SystemExit(f"error: downloaded {logical} payload does not match the exported hash")
        verified.append(logical)
    unexpected = [item.name for item in args.downloads.glob("*.celeste") if item.name not in expected_names.values()]
    if unexpected:
        raise SystemExit("error: unexpected .celeste download names: " + ", ".join(sorted(unexpected)))
    print(f"PASS: {len(verified)} downloaded logical payload(s) match the Stage 9B-validated export hashes")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
