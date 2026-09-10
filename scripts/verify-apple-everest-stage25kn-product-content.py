#!/usr/bin/env python3
"""Require K-N readiness and verify the complete actual three-map app payload."""
from __future__ import annotations
import argparse
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MARKER = "READY_FOR_STAGE25KN_SNAS_PRODUCT_BUILD"
MAPS = {
    "StrawberryJam2021/0-Lobbies/1-Beginner": "a4e3e20a2f0cc878fe43b32fb8025d7650b20cc6265f69e37bf3110a7cdf47c2",
    "StrawberryJam2021/1-Beginner/Bing_Over_Google": "e770a8d193f217d09a6e153fbe272813d26a04d972df947ac812aa5cfe66f347",
    "StrawberryJam2021/1-Beginner/snas": "6ad3172d496e8b5b4ce71f1128fe231b162d534419dc823d2af2cea27fc241d9",
}
CANONICAL_ASSET_AUTHORITY = "afc1bc9fe0086d802a657b956cc412f6fe2d799e09ac07aab58d4059affcfcb3"
FROZEN_IDENTITY_AUTHORITY = "56527ecf21239f1a47b3b15bfb80a1afff13e629a5212a010233785e3b2dbd86"
MANIFEST_IDENTITIES = ("sharedClosureSha256", "managedLogicalSha256", "contentLogicalSha256",
    "registrySha256", "customAudioManifestSha256", "customBankLogicalSetSha256",
    "levelSetProgressionManifestSha256", "collabManifestSha256")


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def require(value, reason):
    if not value:
        raise ValueError(reason)


def logical(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()).hexdigest()


def frozen_authority():
    path = ROOT / "apple-everest/sj-snas-identities-stage25kn.json"
    require(sha(path) == FROZEN_IDENTITY_AUTHORITY, "frozen K-N identity authority differs")
    return json.loads(path.read_text())


def verify_frozen_readiness(ready, manifest):
    frozen = frozen_authority()
    require(ready["identities"] == frozen["identities"] and ready["census"] == frozen["census"] and
            ready["contentPlanSha256"] == frozen["contentPlanSha256"], "fresh K-N identities differ from the three-run authority")
    for field in MANIFEST_IDENTITIES:
        require(manifest[field] == frozen["identities"][field], "actual closure identity differs: " + field)
    require(logical(ready["gateC"]["authority"]) == frozen["identities"]["semanticLogicalSha256"], "semantic proof content differs from frozen identity")
    # Invert only the explicit gate envelope added to the complete composition
    # proof. No map/asset/behavior field is excluded from its logical identity.
    composition = {key: value for key, value in ready["gateD"].items()
                   if key not in {"status", "blocked", "unknown", "constructorAssets"}}
    composition["status"] = "PASS_SOURCE_COMPOSITION_CHECKS"
    require(logical(composition) == frozen["identities"]["compositionLogicalSha256"], "composition proof content differs from frozen identity")


def verify_readiness(ready, manifest):
    require(ready["stage"] == "25K-N" and ready["status"] == "PASS" and ready["marker"] == MARKER,
            "K-N product requires its own four-gate readiness")
    require(ready["sharedClosureSha256"] == manifest["sharedClosureSha256"], "readiness belongs to another closure")
    require(ready["gateA"] == {"occurrences": 1309, "accepted": 1309, "blocked": 0, "unclassified": 0}, "incomplete K-N content/provider gate")
    require(ready["gateB"] == {"factories": 83, "available": 83, "missing": 0,
                              "selectedProfileFactories": 77, "separateLegacyFactories": 6}, "incomplete K-N actual registration gate")
    require(ready["gateC"]["factories"] == 83 and ready["gateC"]["closed"] == 83 and
            ready["gateC"]["blocked"] == 0 and ready["gateC"]["unknown"] == 0, "incomplete K-N semantic gate")
    require(ready["gateD"]["blocked"] == 0 and ready["gateD"]["unknown"] == 0 and
            ready["gateD"]["status"] == "PASS_PRE_AOT_COMPOSITION", "incomplete K-N real composition gate")
    rows = ready["gateD"]["maps"]
    require(len(rows) == 3 and {row["sid"]: row["sha256"] for row in rows} == MAPS, "wrong K-N source map union")
    require(ready["census"] == {"maps": 21, "customOccurrences": 1309, "distinctCustomIds": 83,
                               "rawAuthoredProfiles": 604, "regressionOccurrences": 336}, "incomplete actual K-N census")
    require(ready["physicalAcceptance"] == "PENDING_EXACT_PRODUCT_OBSERVATIONS", "preflight cannot claim physical acceptance")


def expected_custom_maps():
    authority = ROOT / "apple-everest/sj-beginner-expansion-inputs-stage25km.json"
    require(sha(authority) == "29f2dd452bbf10e5e6d03dd19bb17a0719c3f3520ef97164e44a1e4fcadda72d", "immutable regression authority differs")
    regressions = json.loads(authority.read_text())["regressionMaps"]
    require(len(regressions) == 18, "regression authority census differs")
    return {**{path.removeprefix("content/Content/"): digest for path, digest in regressions.items()},
            **{"Maps/" + sid + ".bin": digest for sid, digest in MAPS.items()}}


def verify_map_sets(actual, canonical, selected):
    require(canonical and not (set(canonical) & set(selected)), "canonical map set absent or overlaps the custom union")
    require(actual == canonical | selected, "actual canonical/custom map set differs or includes an unselected map")


def verify_required_files(content, assets):
    require(len(assets) == len({row["logicalPath"] for row in assets}), "duplicate required asset")
    for row in assets:
        path = content / row["logicalPath"]
        require(path.is_file() and path.stat().st_size == row["bytes"] and sha(path) == row["sha256"],
                "required canonical asset missing or changed: " + row["logicalPath"])


def canonical_assets(content):
    authority = ROOT / "apple-everest/sj-snas-canonical-assets-stage25kn.json"
    require(sha(authority) == CANONICAL_ASSET_AUTHORITY, "canonical asset authority differs")
    assets = json.loads(authority.read_text())["assets"]
    verify_required_files(content, assets)
    return assets


def verify_closure_maps(closure):
    manifest = json.loads((closure / "compatibility-manifest.json").read_text())
    expected = expected_custom_maps()
    mounts = [row for row in manifest["contentMounts"] if row["logicalPath"].startswith("Maps/") and row["logicalPath"].endswith(".bin")]
    require(len(mounts) == len(expected) and {row["logicalPath"]: row["sha256"] for row in mounts} == expected,
            "K-N requires exactly lobby, Bing, snas and the 18 regression map mounts")
    content = closure / "content/Content"
    actual = {path.relative_to(content).as_posix(): sha(path) for path in (content / "Maps").rglob("*")
              if path.is_file() and path.suffix.lower() == ".bin"}
    require(actual == expected, "K-N closure map bytes/set differs; all three SJ maps and 18 regressions are mandatory")


def verify_content(app, closure, ready, canonical_content=None):
    verify_closure_maps(closure)
    manifest = json.loads((closure / "compatibility-manifest.json").read_text())
    verify_readiness(ready, manifest)
    verify_frozen_readiness(ready, manifest)
    require(sha(closure / "managed/GeneratedAppleEverestGameplayRegistry.cs") == ready["identities"]["factoryRegistrySha256"],
            "actual generated gameplay registry differs from frozen identity")
    content = app / "Content"
    require(content.is_dir(), "actual app Content missing")
    assets = canonical_assets(content)
    require(ready["gateD"]["canonicalAssets"] == assets and
            ready["gateD"]["canonicalAssetAuthoritySha256"] == CANONICAL_ASSET_AUTHORITY,
            "packaged canonical assets differ from the composition proof")
    files = {path.relative_to(content).as_posix(): path for path in content.rglob("*") if path.is_file()}
    require(len(files) == len({path.casefold() for path in files}), "case-ambiguous packaged content")
    expected_sj = {"Maps/" + sid + ".bin": value for sid, value in MAPS.items()}
    actual_sj = {path: sha(file) for path, file in files.items()
                 if path.lower().startswith("maps/strawberryjam2021/") and path.lower().endswith(".bin")}
    require(actual_sj == expected_sj, "actual app changed or omitted an original SJ map, or admitted an excluded map")
    expected_all = {row["logicalPath"]: row["sha256"] for row in manifest["contentMounts"]
                    if row["logicalPath"].startswith("Maps/") and row["logicalPath"].endswith(".bin")}
    require(expected_all == expected_custom_maps(), "closure changed or omitted a regression map")
    actual_all = {path: sha(file) for path, file in files.items() if path.lower().startswith("maps/") and path.lower().endswith(".bin")}
    canonical_content = canonical_content or ROOT / ".build/celeste-ios/current/content/Content"
    canonical_maps = {path.relative_to(canonical_content).as_posix(): sha(path)
                      for path in (canonical_content / "Maps").rglob("*.bin") if path.is_file()}
    verify_map_sets(actual_all, canonical_maps, expected_all)
    for file in app.rglob("*"):
        require(not file.is_symlink(), "symlink in app payload")
        if not file.is_file():
            continue
        relative = file.relative_to(app).as_posix()
        require(file.suffix.lower() not in {".zip", ".7z", ".rar"}, "source archive entered device app")
        if "/maps/strawberryjam2021/" in ("/" + relative.lower()) and relative.lower().endswith(".bin"):
            require(relative in {"Content/" + path for path in expected_sj}, "nested or excluded original SJ map entered device app")
    for row in manifest["contentMounts"]:
        require(row["logicalPath"] in files and sha(files[row["logicalPath"]]) == row["sha256"], "packaged selected content differs: " + row["logicalPath"])
    artwork = ready["gateD"]["chapterTitleLayout"]["artwork"]
    require(artwork["sha256"] == "0839135f2baafbf652d652177e69456c07bc85343a6c93491e4e2a906034518d" and
            sha(files[artwork["logicalPath"]]) == artwork["sha256"], "packaged wider title artwork differs")
    require(manifest["customAudioBankCount"] == 8, "complete K-N custom bank census differs")
    return {"schemaVersion": 1, "stage": "25K-N", "status": "PASS",
        "sharedClosureSha256": manifest["sharedClosureSha256"], "originalMaps": actual_sj,
        "originalMapCount": 3, "customRegressionMapCount": 21, "excludedSjMaps": 125,
        "verifiedMountedFiles": len(manifest["contentMounts"]), "allMountedContentBytesMatch": True,
        "sourceOriginalMapBytesPreserved": True, "customAudioBankCount": 8,
        "chapterTitleArtwork": artwork, "physicalAcceptance": "SEPARATE_REQUIRED_GATE"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("app", "closure", "readiness", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    report = verify_content(args.app, args.closure, json.loads(args.readiness.read_text()))
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print("PASS: actual K-N app contains three unchanged SJ maps, all 18 regressions and every selected mounted byte; 125 SJ maps excluded")


if __name__ == "__main__":
    main()
