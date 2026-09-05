#!/usr/bin/env python3
"""Acquire only the exact public inputs needed to reproduce the K-I stop."""
import argparse
from concurrent.futures import ThreadPoolExecutor
import hashlib
import json
from pathlib import Path
import urllib.request

ROOT = Path(__file__).resolve().parents[1]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise ValueError("independent acquisition requires a fresh output directory")
    args.output.mkdir(parents=True)
    ae = ROOT / "apple-everest"
    graph = json.loads((ae / "strawberry-jam-dependency-graph-stage25kc.json").read_text())
    plan = json.loads((ae / "sj-beginner-content-stage25ki.json").read_text())
    factories = json.loads((ae / "selected-factory-type-closure-stage25kh.json").read_text())
    selected = {row["name"] for row in plan["packages"]} | {
        row["provider"] for row in factories["factories"] if row["provider"] != "EverestCore"}
    pins = [row for row in graph["nodes"] if row["name"] in selected]
    if len(pins) != len(selected):
        raise ValueError("public input pin missing")

    def acquire(pin):
        target = args.output / (pin["name"] + ".zip")
        temporary = args.output / (pin["name"] + ".download")
        mirror = "https://celestemodupdater.0x0a.de/banana-mirror/" + pin["gameBananaFileId"] + ".zip"
        failures = []
        for url in (mirror, pin["publicUrl"]):
            try:
                request = urllib.request.Request(url, headers={"User-Agent": "AppleEverest-stage25ki-reproducer/1"})
                digest = hashlib.sha256()
                size = 0
                with urllib.request.urlopen(request, timeout=30) as stream, temporary.open("wb") as output:
                    for chunk in iter(lambda: stream.read(1024 * 1024), b""):
                        size += len(chunk)
                        if size > pin["zipBytes"]:
                            raise ValueError("public input exceeded exact size")
                        digest.update(chunk)
                        output.write(chunk)
                if size != pin["zipBytes"] or digest.hexdigest() != pin["zipSha256"]:
                    raise ValueError("public input identity differs")
                temporary.rename(target)
                print("ACQUIRED exact " + pin["name"], flush=True)
                return {"name": pin["name"], "sha256": digest.hexdigest(), "bytes": size,
                        "publicUrl": pin["publicUrl"], "acquiredFrom": url}
            except (OSError, ValueError) as exception:
                failures.append(str(exception))
        raise RuntimeError(pin["name"] + ": " + "; ".join(failures))

    with ThreadPoolExecutor(max_workers=3) as executor:
        records = list(executor.map(acquire, sorted(pins, key=lambda row: row["name"])))
    (args.output / "acquisition.json").write_text(json.dumps({"schemaVersion": 1,
        "inputsCopiedFromIgnoredFixtures": False, "packages": records}, indent=2) + "\n")
    print(f"PASS: independently acquired and hash-verified {len(records)} public inputs")


if __name__ == "__main__":
    main()
