#!/usr/bin/env python3
"""Owned synthetic controls for K-N readiness contracts; no game binaries."""
from __future__ import annotations
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[1]
sys.dont_write_bytecode = True


def module(name, filename):
    spec = importlib.util.spec_from_file_location(name, ROOT / "scripts" / filename)
    value = importlib.util.module_from_spec(spec);spec.loader.exec_module(value);return value


def main():
    gate = module("kn_gate_controls", "preflight-apple-everest-stage25kn.py")
    product = module("kn_product_controls", "verify-apple-everest-stage25kn-product-content.py")
    ledger = json.loads((ROOT / "apple-everest/sj-snas-semantics-stage25kn.json").read_text())
    # Synthetic rows use only the sanitized public ID/profile hashes. They test
    # contract rejection, not extraction, registration or gameplay execution.
    occurrences = []
    for row in ledger["factories"]:
        hashes = row["authoredProfileSha256"]
        for i in range(row["occurrences"]):
            occurrences.append({"kind": row["kind"], "customId": row["customId"], "provider": row["provider"],
                                "profileSha256": hashes[i % len(hashes)]})
    checks = 0;rejected = []
    def positive(action):
        nonlocal checks
        action();checks += 1
    def negative(name, action):
        nonlocal checks
        try: action()
        except (ValueError, KeyError, TypeError, FileNotFoundError):
            checks += 1;rejected.append(name);return
        raise AssertionError("false readiness: " + name)
    positive(lambda: gate.verify_obligations(ledger, occurrences))
    def mutate_ledger(name, mutate):
        changed = copy.deepcopy(ledger);mutate(changed)
        negative(name, lambda: gate.verify_obligations(changed, occurrences))
    mutate_ledger("omitted factory", lambda d: d["factories"].pop())
    mutate_ledger("duplicate factory", lambda d: d["factories"].__setitem__(1, copy.deepcopy(d["factories"][0])))
    mutate_ledger("wrong provider", lambda d: d["factories"][0].__setitem__("provider", "UnsupportedProvider"))
    mutate_ledger("unproved authored profile", lambda d: d["factories"][0]["authoredProfileSha256"].append("0" * 64))
    mutate_ledger("missing proof", lambda d: d["factories"][0]["requiredProofs"].pop())
    mutate_ledger("unknown scope", lambda d: d["factories"][0].__setitem__("scope", "UNKNOWN"))
    mutate_ledger("no scope default", lambda d: d["factories"][0].pop("scope"))
    mutate_ledger("historical physical transfer", lambda d: d.__setitem__("historicalPhysicalResultsTransferred", True))
    mutate_ledger("wrong historical baseline", lambda d: d.__setitem__("baselineSemanticAuthoritySha256", "0" * 64))
    mutate_ledger("wrong stage", lambda d: d.__setitem__("stage", "25K-M"))
    mutate_ledger("wrong declared census", lambda d: d["census"].__setitem__("factories", 73))
    mutate_ledger("unresolved requirement", lambda d: d["openRequirements"].append("unproved constructor"))
    mutate_ledger("unbound issue", lambda d: d["factories"][0]["issues"].append("UNKNOWN_REQUIREMENT"))
    mutate_ledger("missing issue ledger", lambda d: d.pop("issueLedger"))
    mutate_ledger("missing generation obligations", lambda d: d.__setitem__("generationSourceBindings", []))
    mutate_ledger("substituted generation obligation", lambda d: d["generationSourceBindings"][0].__setitem__("path", "apple-everest/sj-snas-factory-contract-stage25kn.json"))
    mutate_ledger("changed generation hash", lambda d: d["generationSourceBindings"][0].__setitem__("sha256", "0" * 64))
    new_index = next(i for i, r in enumerate(ledger["factories"]) if r["scope"] == "NEW_FINITE_IMPLEMENTATION")
    mutate_ledger("new type without implementation", lambda d: d["factories"][new_index].__setitem__("sourceBindings", []))
    mutate_ledger("new type with unrelated implementation", lambda d: d["factories"][new_index]["sourceBindings"][0].__setitem__("path", "apple-everest/runtime/AppleEverestProgressionSnapshotCodec.cs"))
    negative("omitted authored occurrence", lambda: gate.verify_obligations(ledger, occurrences[:-1]))
    bad = copy.deepcopy(occurrences);bad[0]["profileSha256"] = "f" * 64
    negative("unsupported source profile", lambda: gate.verify_obligations(ledger, bad))
    bad = copy.deepcopy(occurrences);bad[0]["provider"] = "WrongPackage"
    negative("wrong source provider", lambda: gate.verify_obligations(ledger, bad))
    audio = {"status": "PASS_SOURCE_BOUND_AUDIO_NAMES", "originalSourcesUnchanged": True,
        "result": {"status": "PASS", "checks": 152, "reloads": 50, "routeTransitions": 5,
            "realGeneratedAudioMethods": True, "actualAudioStateApply": True, "productionRegistryAndLifecycle": True,
            "boundary": "PROJECT_OWNED_FMOD_RETURN_FIXTURES; NO_NATIVE_BANK_OR_AUDIBLE_PLAYBACK_PROOF"},
        "methodSha256": {key: "a" * 64 for key in ("GetEventName", "SetMusic", "SetAmbience", "Stop", "SetParameter",
            "CreateInstance", "AppleEverestOriginal_CreateInstance", "GetEventDescription")},
        "sourceSha256": {key: "b" * 64 for key in ("Audio.cs", "AudioState.cs", "AudioTrackState.cs", "MEP.cs",
            "AppleEverestCustomAudioRuntime.cs", "AppleEverestCustomAudioLifecycle.cs")},
        "probeSourceSha256": {key: "c" * 64 for key in ("AudioNameRuntimeFixture", "AudioNameRuntimeProgram")},
        "scriptSha256": "d" * 64}
    ready = {"stage": "25K-N", "status": "PASS", "marker": product.MARKER, "sharedClosureSha256": "a" * 64,
        "gateA": {"occurrences": 1309, "accepted": 1309, "blocked": 0, "unclassified": 0},
        "gateB": {"factories": 83, "available": 83, "missing": 0, "selectedProfileFactories": 77, "separateLegacyFactories": 6},
        "gateC": {"factories": 83, "closed": 83, "blocked": 0, "unknown": 0},
        "gateD": {"status": "PASS_PRE_AOT_COMPOSITION", "blocked": 0, "unknown": 0,
                  "audioEventNameExecution": audio,
                  "maps": [{"sid": sid, "sha256": digest} for sid, digest in product.MAPS.items()]},
        "census": {"maps": 21, "customOccurrences": 1309, "distinctCustomIds": 83, "rawAuthoredProfiles": 604, "regressionOccurrences": 336},
        "physicalAcceptance": "PENDING_EXACT_PRODUCT_OBSERVATIONS"}
    manifest = {"sharedClosureSha256": "a" * 64}
    positive(lambda: product.verify_readiness(ready, manifest))
    def mutate_ready(name, mutate):
        changed = copy.deepcopy(ready);mutate(changed)
        negative(name, lambda: product.verify_readiness(changed, manifest))
    for key in ["gateA", "gateB", "gateC", "gateD", "census", "physicalAcceptance"]:
        mutate_ready("missing " + key, lambda d, key=key: d.pop(key))
    mutate_ready("old K-L marker", lambda d: d.__setitem__("marker", "READY_FOR_REAL_SJ_PRODUCT_BUILD"))
    mutate_ready("stale closure", lambda d: d.__setitem__("sharedClosureSha256", "b" * 64))
    mutate_ready("old content counts", lambda d: d["gateA"].__setitem__("occurrences", 920))
    mutate_ready("unknown content", lambda d: d["gateA"].__setitem__("unclassified", 1))
    mutate_ready("missing registration", lambda d: d["gateB"].__setitem__("missing", 1))
    mutate_ready("omitted legacy scope", lambda d: d["gateB"].__setitem__("separateLegacyFactories", 0))
    mutate_ready("unknown semantic closure", lambda d: d["gateC"].__setitem__("unknown", 1))
    mutate_ready("blocked composition", lambda d: d["gateD"].__setitem__("blocked", 1))
    mutate_ready("registration-only composition", lambda d: d["gateD"].__setitem__("status", "PASS_REGISTRATION_SCOPE_ONLY"))
    mutate_ready("omitted source map", lambda d: d["gateD"]["maps"].pop())
    mutate_ready("duplicate source map", lambda d: d["gateD"]["maps"].__setitem__(1, copy.deepcopy(d["gateD"]["maps"][0])))
    mutate_ready("wrong source map hash", lambda d: d["gateD"]["maps"][0].__setitem__("sha256", "0" * 64))
    mutate_ready("host claims physical PASS", lambda d: d.__setitem__("physicalAcceptance", "PASS"))
    mutate_ready("missing audio execution", lambda d: d["gateD"].pop("audioEventNameExecution"))
    for key in ("status", "originalSourcesUnchanged", "result", "methodSha256", "sourceSha256", "probeSourceSha256", "scriptSha256"):
        mutate_ready("missing audio " + key, lambda d, key=key: d["gateD"]["audioEventNameExecution"].pop(key))
    for key, value in (("checks", 0), ("reloads", 0), ("routeTransitions", 0), ("actualAudioStateApply", False),
                       ("realGeneratedAudioMethods", False), ("productionRegistryAndLifecycle", False), ("boundary", "PHYSICAL_PASS")):
        mutate_ready("incomplete audio " + key, lambda d, key=key, value=value: d["gateD"]["audioEventNameExecution"]["result"].__setitem__(key, value))
    mutate_ready("omitted music execution", lambda d: d["gateD"]["audioEventNameExecution"]["methodSha256"].pop("SetMusic"))
    mutate_ready("invalid audio source hash", lambda d: d["gateD"]["audioEventNameExecution"].__setitem__("scriptSha256", ""))
    positive(product.frozen_authority)
    positive(lambda: product.verify_authority_version({"appVersion": "0.1.1", "appBuild": "48"}, "0.1.1", "48"))
    negative("stale product build authority", lambda: product.verify_authority_version({"appVersion": "0.1.1", "appBuild": "47"}, "0.1.1", "48"))
    negative("wrong product version authority", lambda: product.verify_authority_version({"appVersion": "0.2.0", "appBuild": "48"}, "0.1.1", "48"))
    # Owned synthetic proof bodies exercise the actual frozen-proof consumer;
    # their hashes are computed locally, without third-party proof fixtures.
    semantic = {"ownedSemanticTrace": ["construct", "added", "removed"]}
    composition = {"status": "PASS_SOURCE_COMPOSITION_CHECKS", "ownedMapAssets": ["owned-atlas"]}
    frozen = {"identities": {key: hashlib.sha256(key.encode()).hexdigest() for key in product.MANIFEST_IDENTITIES},
              "census": copy.deepcopy(ready["census"]), "contentPlanSha256": "c" * 64}
    frozen["identities"].update({"factoryRegistrySha256": "d" * 64,
        "semanticLogicalSha256": product.logical(semantic), "compositionLogicalSha256": product.logical(composition)})
    bound = copy.deepcopy(ready)
    bound.update({"identities": copy.deepcopy(frozen["identities"]), "contentPlanSha256": frozen["contentPlanSha256"]})
    bound["gateC"]["authority"] = semantic
    bound["gateD"] = {**composition, "status": "PASS_PRE_AOT_COMPOSITION", "blocked": 0, "unknown": 0,
                      "constructorAssets": "owned fixture"}
    bound_manifest = {key: frozen["identities"][key] for key in product.MANIFEST_IDENTITIES}
    with patch.object(product, "frozen_authority", return_value=frozen):
        positive(lambda: product.verify_frozen_readiness(bound, bound_manifest))
        for key in product.MANIFEST_IDENTITIES:
            changed = copy.deepcopy(bound_manifest);changed.pop(key)
            negative("missing actual frozen manifest " + key, lambda: product.verify_frozen_readiness(bound, changed))
            changed = copy.deepcopy(bound_manifest);changed[key] = "0" * 64
            negative("changed actual frozen manifest " + key, lambda: product.verify_frozen_readiness(bound, changed))
        for key in ["identities", "census", "contentPlanSha256"]:
            changed = copy.deepcopy(bound);changed.pop(key)
            negative("missing frozen evidence " + key, lambda: product.verify_frozen_readiness(changed, bound_manifest))
        changed = copy.deepcopy(bound);changed["identities"]["factoryRegistrySha256"] = "0" * 64
        negative("changed frozen gameplay registry", lambda: product.verify_frozen_readiness(changed, bound_manifest))
        changed = copy.deepcopy(bound);changed["gateC"]["authority"]["ownedSemanticTrace"].pop()
        negative("tampered semantic execution with unchanged identity", lambda: product.verify_frozen_readiness(changed, bound_manifest))
        changed = copy.deepcopy(bound);changed["gateD"]["ownedMapAssets"].clear()
        negative("tampered composition with unchanged identity", lambda: product.verify_frozen_readiness(changed, bound_manifest))
    with tempfile.TemporaryDirectory(prefix="stage25kn-owned-authority-controls-") as temporary:
        root = Path(temporary);path = root / "apple-everest/sj-snas-identities-stage25kn.json";path.parent.mkdir()
        with patch.object(product, "ROOT", root):
            negative("missing frozen authority", product.frozen_authority)
            path.write_text(json.dumps(frozen))
            negative("substituted frozen authority", product.frozen_authority)
    digest = lambda data: hashlib.sha256(data).hexdigest()
    canonical = {"Maps/OwnedVanilla.bin": digest(b"owned canonical fixture")}
    selected = {"Maps/OwnedSelected.bin": digest(b"owned selected fixture")}
    actual = canonical | selected
    positive(lambda: product.verify_map_sets(actual, canonical, selected))
    negative("extra unselected payload map", lambda: product.verify_map_sets(actual | {"Maps/Extra.bin": digest(b"extra")}, canonical, selected))
    negative("missing selected payload map", lambda: product.verify_map_sets(canonical, canonical, selected))
    negative("changed selected payload bytes", lambda: product.verify_map_sets(actual | {"Maps/OwnedSelected.bin": digest(b"changed")}, canonical, selected))
    negative("changed canonical bytes", lambda: product.verify_map_sets(actual | {"Maps/OwnedVanilla.bin": digest(b"changed")}, canonical, selected))
    negative("canonical/selected overlap", lambda: product.verify_map_sets(actual, canonical, actual))
    negative("missing canonical evidence", lambda: product.verify_map_sets(selected, {}, selected))
    source_binding = {"productionSourceBindings": gate.production_source_inventory()}
    positive(lambda: gate.verify_compilation_source_binding(source_binding))
    negative("old compilation flags without source binding", lambda: gate.verify_compilation_source_binding({"compilation": {"FreshProductionCompilation": True}}))
    changed = copy.deepcopy(source_binding);changed["productionSourceBindings"][0]["Sha256"] = "0" * 64
    negative("stale owned production source", lambda: gate.verify_compilation_source_binding(changed))
    changed = copy.deepcopy(source_binding);changed["productionSourceBindings"].pop()
    negative("omitted production source binding", lambda: gate.verify_compilation_source_binding(changed))
    with tempfile.TemporaryDirectory(prefix="stage25kn-owned-asset-controls-") as temporary:
        root = Path(temporary);path = root / "Graphics/ColorGrading/feelingdown.png";path.parent.mkdir(parents=True)
        data = b"project-owned synthetic asset bytes";path.write_bytes(data)
        assets = [{"logicalPath": path.relative_to(root).as_posix(), "bytes": len(data), "sha256": digest(data)}]
        positive(lambda: product.verify_required_files(root, assets))
        path.write_bytes(data[:-1]+b"!")
        negative("changed required colorgrade bytes", lambda: product.verify_required_files(root, assets))
        path.unlink()
        negative("missing required colorgrade", lambda: product.verify_required_files(root, assets))
    print(json.dumps({"status": "PASS", "checks": checks, "rejected": rejected, "scope": "SYNTHETIC_CONTRACT_CONTROLS_NOT_GAMEPLAY"}, indent=2))


if __name__ == "__main__":
    main()
