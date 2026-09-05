#!/usr/bin/env python3
"""Fail-closed verification for Stage 25K-H selected-factory closure."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import plistlib
import re
import subprocess
import sys
import tempfile
import zipfile


START = "e38a886f54a719ad973f21f4ef84587d52b49718"
KG = "e0d7c1988a9e5a896001735e5fe21d2251446bd0"
EVEREST = "4bbde91b8dbaaddef2ceec75ca0cd6d59b3b8d00"
MAX_ZIP = "abfc5d167936410e9b6665018335ee654455e30710caffe01540b232da468fee"
MAX_DLL = "6f463f2c014eb60c4abb61e395eb5065455d4bf469f4c83fce9814fb7bfcdfa6"
SHARED_CLOSURE = "9a067cc582f8a53a95f9150e32917f96891f91752c6582cae4b834b291374cca"
TUTORIAL = "MaxHelpingHand/CustomTutorialWithNoBird"
NPC = "MaxHelpingHand/MoreCustomNPC"
DIMENSIONS = {
    "PROVIDER_PACKAGE", "PROVIDER_ASSEMBLY", "CONCRETE_TYPE_OR_LOWERING", "BASE_CHAIN",
    "CONSTRUCTOR", "CONSTRUCTOR_PARAMETER_TYPES", "BASE_CONSTRUCTOR", "FIELD_PROPERTY_TYPES",
    "TYPE_INITIALIZER", "CONSTRUCTOR_CALL_GRAPH", "LIFECYCLE", "INTERACTION",
    "COROUTINE_STATE_MACHINE", "MODULE_LOAD", "HOOKS", "REFLECTION", "CONTENT",
}
CLASSIFICATIONS = {"ACCEPTED_VANILLA", "ACCEPTED_STATIC_RUNTIME",
                   "ACCEPTED_STATIC_SEMANTIC_LOWERING", "PRESENT_BUT_UNREACHABLE",
                   "UNSUPPORTED_REQUIRED", "UNKNOWN"}


class Checks:
    def __init__(self): self.count = 0
    def require(self, value, message):
        if not value: raise SystemExit("FAIL: " + message)
        self.count += 1


def load(path): return json.loads(path.read_text())
def file_hash(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def canonical(value): return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False) + "\n").encode()
def artifact_hash(value):
    copy = dict(value)
    claimed = copy.pop("artifactSha256")
    return claimed == hashlib.sha256(canonical(copy)).hexdigest()
def git(root, *args):
    return subprocess.run(["git", "-C", str(root), *args], check=True,
                          capture_output=True, text=True).stdout.strip()


def verify_product(c, manifest_path, platform, ipa_path=None):
    manifest = load(manifest_path)
    c.require(manifest == {**manifest, "schemaVersion": 1} and
              (manifest["platform"], manifest["configuration"], manifest["rid"], manifest["signing"]) ==
              (platform, "Release", platform + "-arm64", "development"), platform + " signed Release manifest")
    c.require(manifest["fullAOT"] and manifest["fullTrim"] and not manifest["useInterpreter"] and
              not manifest["jit"] and manifest["sharedClosureSha256"] ==
              SHARED_CLOSURE,
              platform + " full-AOT boundary and closure")
    if ipa_path:
        c.require(ipa_path.is_file() and ipa_path.stat().st_size == manifest["ipaBytes"] and
                  file_hash(ipa_path) == manifest["ipaSha256"], platform + " exact IPA identity")
        with zipfile.ZipFile(ipa_path) as archive:
            names = archive.namelist()
            info_name = next(name for name in names if re.fullmatch(r"Payload/[^/]+\.app/Info\.plist", name))
            info = plistlib.loads(archive.read(info_name))
            maps = [name for name in names if name.lower().endswith(".bin") and "Maps/" in name]
            c.require(not any("StrawberryJam2021/0-Lobbies/1-Beginner" in name or
                              "StrawberryJam2021/1-Beginner/Bing_Over_Google" in name for name in names),
                      platform + " excludes real Strawberry Jam maps")
            c.require(not any(name.lower().endswith(("maxhelpinghand.dll", "monomod.runtimeDetour.dll".lower(),
                                                     "mono.cecil.dll")) for name in names),
                      platform + " excludes host/helper runtime binaries")
            if platform == "ios":
                c.require(info.get("UIDeviceFamily") == [1, 2] and
                          str(info.get("MinimumOSVersion", "")).startswith("15"),
                          "universal iPhone/iPad product metadata")
            else:
                c.require(info.get("UIDeviceFamily") == [3] and
                          str(info.get("MinimumOSVersion", "")).startswith("16"),
                          "Apple TV product metadata")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=pathlib.Path)
    parser.add_argument("--inspection", type=pathlib.Path)
    parser.add_argument("--max-census", type=pathlib.Path)
    parser.add_argument("--max-archive", type=pathlib.Path)
    parser.add_argument("--everest-root", type=pathlib.Path)
    parser.add_argument("--reproduction-json", type=pathlib.Path)
    parser.add_argument("--ios-manifest", type=pathlib.Path)
    parser.add_argument("--ios-ipa", type=pathlib.Path)
    parser.add_argument("--tvos-manifest", type=pathlib.Path)
    parser.add_argument("--tvos-ipa", type=pathlib.Path)
    parser.add_argument("--physical-json", type=pathlib.Path)
    parser.add_argument("--disk-json", type=pathlib.Path)
    parser.add_argument("--require-clean", action="store_true")
    args = parser.parse_args()
    root = (args.repo_root or pathlib.Path(__file__).resolve().parents[1]).resolve()
    ae = root / "apple-everest"
    c = Checks()
    paths = {name: ae / name for name in ["selected-factory-type-closure-stage25kh.json",
        "everest-base-entity-semantics-stage25kh.json", "maxhelpinghand-selected-semantics-stage25kh.json",
        "sj-preintegration-readiness-stage25kh.json"]}
    required = [*paths.values(), root / "scripts/generate-apple-everest-stage25kh.py",
        root / "scripts/reproduce-apple-everest-stage25kh.py", root / "scripts/build-apple-everest-stage25kh.sh",
        root / "scripts/verify-apple-everest-stage25kh.sh",
        root / "tools/AppleEverestBuilder/SelectedFactoryTypeClosure.cs",
        root / "tools/AppleEverestBuilder/FactoryTypeInspector.cs",
        root / "tools/AppleEverestBuilder/tests/SelectedFactoryTypeClosureTests.cs",
        root / "apple-everest/runtime/semantics/AppleEverestEverestBaseEntitySemantics.cs",
        root / "apple-everest/canaries/stage25kh/factory-closure.json",
        root / "apple-everest/canaries/stage25kh/Content/Maps/AppleEverest/Stage25KH.xml",
        root / "apple-everest/runtime/AppleEverestDialogFragmentParser.cs",
        root / "tools/AppleEverestBuilder/tests/DialogFragmentParserTests.cs",
        root / "docs/history/stages/APPLE_EVEREST_BASE_ENTITY_MAXHELPINGHAND_STAGE25KH_REPORT.md"]
    for path in required: c.require(path.is_file(), "required file " + str(path.relative_to(root)))
    graph, base, max_helper, ready = (load(paths[name]) for name in paths)
    c.require(all(row["schemaVersion"] == 1 and row.get("stage") == "25K-H"
                  for row in (graph, base, max_helper, ready)), "artifact schemas")
    c.require(all(artifact_hash(row) for row in (graph, base, max_helper, ready)), "artifact hashes")

    summary = graph["summary"]
    c.require(summary == {"selectedFactories": 73, "fullyClosed": 73, "blocked": 0, "unknown": 0,
                           "baseEdges": 191, "constructors": 73, "moduleLoadDependencies": 73},
              "73-factory post-fix closure census")
    factories, nodes = graph["factories"], graph["nodes"]
    c.require(len(factories) == 73 and len(nodes) == 73 * len(DIMENSIONS), "complete factory/node graph")
    keys = {(row["kind"], row["customId"]) for row in factories}
    c.require(len(keys) == 73 and {TUTORIAL, NPC}.issubset({row["customId"] for row in factories}),
              "unique selected factory keys and two repaired entities")
    node_by_id = {row["id"]: row for row in nodes}
    c.require(len(node_by_id) == len(nodes) and all(row["kind"] in DIMENSIONS and
              row["classification"] in CLASSIFICATIONS and row["evidence"] for row in nodes),
              "closed node schema and evidence")
    for factory in factories:
        reached, pending = set(), list(factory["rootNodeIds"])
        while pending:
            node_id = pending.pop()
            c.require(node_id in node_by_id, "resolved graph edge " + node_id)
            if node_id in reached: continue
            reached.add(node_id); pending.extend(node_by_id[node_id]["dependencies"])
        c.require({node_by_id[node_id]["kind"] for node_id in reached} == DIMENSIONS,
                  "all closure dimensions for " + factory["customId"])
        c.require(factory["classification"] in CLASSIFICATIONS - {"PRESENT_BUT_UNREACHABLE",
                  "UNSUPPORTED_REQUIRED", "UNKNOWN"} and factory["baseChain"] and factory["constructor"],
                  "accepted type/base/constructor for " + factory["customId"])
    c.require(graph["pins"] == {"everestTag": "stable-1.6458.0", "everestCommit": EVEREST,
              "maxHelpingHandVersion": "1.40.9", "maxHelpingHandZipSha256": MAX_ZIP,
              "maxHelpingHandDllSha256": MAX_DLL}, "exact Everest and MaxHelpingHand pins")
    negative = graph["historicalNegative"]
    c.require(negative["contentIdCensus"] == {"total": 920, "accepted": 920, "blocked": 0,
              "unclassified": 0} and negative["preFixTypeClosure"]["selectedFactories"] == 73 and
              negative["preFixTypeClosure"]["fullyClosed"] == 71 and
              negative["preFixTypeClosure"]["blocked"] == 2 and
              negative["preFixTypeClosure"]["unknown"] == 0 and
              all(value in negative["preFixTypeClosure"]["blockers"] for value in
                  [TUTORIAL + " -> missing CustomBirdTutorial closure",
                   NPC + " -> missing CustomNPC closure"]), "historical K-G negative reproduction")

    c.require(base["class"] == "STATIC_EVEREST_ENTITY_BASE_SEMANTICS" and
              base["architecture"]["shape"] == "FLATTENED_TYPED_SELECTED_ENTITIES" and
              base["everest"] == {"tag": "stable-1.6458.0", "commit": EVEREST,
                "customBirdTutorialSourceSha256": "e353517fb2271caca39b364df86203b689015b530823f6a16b0194f1cd339e67",
                "customNpcSourceSha256": "d89d1ad0d2e71c171746a8664c50bb54e41a4af3073afe267217f54d166ffa1d"},
              "pinned bounded flattened Everest base semantics")
    c.require(not any(base["runtime"].values()), "no empty stubs, runtime hooks/IL/reflection/DynamicData")
    c.require(base["customBirdTutorial"]["constructor"] == ".ctor(EntityData,Vector2)" and
              "right pointer GUI" in base["customBirdTutorial"]["acceptedMembers"] and
              base["customNpc"]["constructor"] == ".ctor(EntityData,Vector2,EntityID)" and
              {row["member"] for row in base["customNpc"]["privateMembersUsedByMax"]} == {"textures", "scale"},
              "exact two Everest base profiles")

    c.require((max_helper["version"], max_helper["archiveSha256"], max_helper["dllSha256"]) ==
              ("1.40.9", MAX_ZIP, MAX_DLL), "MaxHelpingHand distributed authority")
    c.require(max_helper["distributedSurface"]["types"] == 447 and
              max_helper["distributedSurface"]["methods"] == 2178 and
              max_helper["distributedSurface"]["unknown"] == 0 and
              max_helper["moduleLoadSurface"]["unknown"] == 0, "zero-unknown full Max load audit")
    tutorial = max_helper["customTutorialWithNoBird"]
    npc = max_helper["moreCustomNpc"]
    c.require(tutorial["authoredAttributes"]["id"] == 893 and
              tutorial["authoredAttributes"]["direction"] == "Right" and
              tutorial["authoredAttributes"]["controls"] == "dialog:SJ2021_lobby_gym_tutorial_controls" and
              tutorial["authoredNodes"] == [] and tutorial["resolvedDefaults"] ==
              {"hasPointer": True, "caw": False, "faceLeft": False} and
              not tutorial["birdHook"]["selectedOrigCalled"] and
              tutorial["renderIl"]["selectedEffect"].startswith("37 scaled one-pixel vertical slices"),
              "exact selected tutorial attributes, hook, and pointer IL result")
    c.require(npc["authoredAttributes"]["id"] == 1604 and len(npc["authoredNodes"]) == 2 and
              npc["authoredAttributes"]["dialogId"] == "StrawberryJam2021_0_Lobbies_1_Beginner_Credits" and
              not npc["resolvedDefaults"]["autoSkipEnabled"] and npc["resolvedDefaults"]["customFont"] == "" and
              {row["member"] for row in npc["reflection"]} == {"textures", "scale"} and
              npc["talkIlHook"]["selectedEffect"] == "Textbox.Say arguments and coroutine are unchanged" and
              npc["dialogPresentation"]["parsed"].startswith("{portrait MADELINE left normal}") and
              npc["dialogPresentation"]["cleanedText"] == "STAGE 25K-H NPC TALK PASS",
              "exact selected NPC attributes, reflection, and Talk ILHook result")
    c.require(not any(max_helper["device"].values()), "Max DLL/runtime hook/IL/reflection/scanner omitted")
    c.require(max_helper["content"]["selectedTwoRequiredMaxAssets"] == [] and
              len(max_helper["content"]["retainedAcceptedFactoryPrefixes"]) == 3 and
              not max_helper["content"]["wholeHelperContentIncluded"], "bounded Max content allow-list")

    c.require(ready["status"] == "GREEN" and ready["readiness"] ==
              "READY_FOR_K_I_REAL_SJ_INTEGRATION_RETRY" and
              ready["contentIdCensus"] == {"totalSelectedOccurrences": 920, "accepted": 920,
                                             "blocked": 0, "unclassified": 0} and
              ready["postFix"] == {"contentIdBlockers": 0, "typeClosureBlockers": 0,
                                     "unclassified": 0, "additionalSelectedBlocker": False} and
              not ready["maps"]["packagedInKh"] and ready["claims"] ==
              {"developmentIntegrationReady": True, "allPlatformReleaseReady": False,
               "fullStrawberryJamSupported": False}, "strong two-dimensional readiness gate")

    closure_source = required[8].read_text()
    c.require(all(token in closure_source for token in DIMENSIONS) and
              all(token in closure_source for token in CLASSIFICATIONS) and
              "ValidateAvailableFactories" in closure_source and "Visit(" in closure_source,
              "generic recursive build-time closure implementation")
    c.require(not re.search(r"StrawberryJam|MaxHelpingHand|CustomBird|CustomNPC", closure_source),
              "generic analyzer has no map/helper hard-coding")
    tests = required[10].read_text()
    c.require(all(token in tests for token in ["missing direct base class rejected",
              "missing transitive base class rejected", "unresolved constructor parameter type rejected",
              "unresolved base constructor rejected", "unsupported static constructor rejected",
              "unsupported selected module-load hook rejected", "required reflection without lowering rejected",
              "required IL hook without frozen plan rejected", "method signature containing unresolved runtime type rejected",
              "known factory ID with incomplete semantic closure rejected"]), "ten fail-closed regression cases")
    runtime = required[11].read_text()
    c.require(all(token in runtime for token in ["AppleEverestCustomTutorialWithNoBird : Entity",
              "AppleEverestDirectionalTutorialGui : Entity", "AppleEverestMoreCustomNpc : NPC",
              "direction != \"Right\"", "for (int index = 0; index <= 36; index++)",
              "data.Nodes.Length != 2", "Textbox.Say(dialog, null)"]), "typed exact-profile runtime lowering")
    c.require(not any(token in runtime for token in ["using System.Reflection", "DynamicData<", "DynData<",
              "Assembly.Load(", "new ILHook", "RuntimeDetour"]), "lowering has no dynamic runtime mechanism")
    canary = required[13].read_text()
    c.require(f'name="{TUTORIAL}" id="893"' in canary and f'name="{NPC}" id="1604"' in canary and
              'direction="Right"' in canary and canary.count("<node ") == 2,
              "data-only canary uses real selected IDs and profiles")
    dialog_parser = required[14].read_text()
    dialog_tests = required[15].read_text()
    c.require("{portrait " in dialog_parser and "AppleEverestDialogFragmentEntry" in dialog_parser and
              "language.Dialog[key] = entry.Raw" in (root / "apple-everest/runtime/AppleEverestStaticRuntime.cs").read_text() and
              "[MADELINE left normal]STAGE 25K-H NPC TALK PASS" in dialog_tests and
              "STAGE 25K-H NPC TALK PASS" in dialog_tests,
              "mod dialog fragments preserve portrait commands and cleaned text")

    if args.inspection or args.max_census:
        c.require(args.inspection is not None and args.max_census is not None,
                  "inspection and Max census supplied together")
        with tempfile.TemporaryDirectory(prefix="stage25kh-verify-") as temporary:
            generated = pathlib.Path(temporary)
            subprocess.run([sys.executable, str(root / "scripts/generate-apple-everest-stage25kh.py"),
                "--inspection", str(args.inspection.resolve()), "--max-census", str(args.max_census.resolve()),
                "--output-root", str(generated)], cwd=root, check=True)
            for name, tracked in paths.items():
                c.require((generated / name).read_bytes() == tracked.read_bytes(), "reproduced " + name)
            c.require((generated / "canaries/stage25kh/factory-closure.json").read_bytes() ==
                      (ae / "canaries/stage25kh/factory-closure.json").read_bytes(), "reproduced canary closure")
    if args.max_archive:
        c.require(args.max_archive.is_file() and file_hash(args.max_archive) == MAX_ZIP,
                  "independently reacquired exact MaxHelpingHand archive")
        with zipfile.ZipFile(args.max_archive) as archive:
            member = next(name for name in archive.namelist() if name.endswith("MaxHelpingHand.dll"))
            c.require(hashlib.sha256(archive.read(member)).hexdigest() == MAX_DLL,
                      "distributed MaxHelpingHand DLL hash")
    if args.everest_root:
        ev = args.everest_root.resolve()
        c.require(git(ev, "rev-parse", "HEAD") == EVEREST and
                  file_hash(ev / "Celeste.Mod.mm/Mod/Entities/CustomBirdTutorial.cs") ==
                  base["everest"]["customBirdTutorialSourceSha256"] and
                  file_hash(ev / "Celeste.Mod.mm/Mod/Entities/CustomNPC.cs") ==
                  base["everest"]["customNpcSourceSha256"], "exact pinned Everest reference source")
    if args.reproduction_json:
        reproduction = load(args.reproduction_json)
        c.require(reproduction["runsIdentical"] and reproduction["completeTreeCompared"] and
                  reproduction["fileCount"] == 1245 and
                  reproduction["closure"]["sharedClosureSha256"] == SHARED_CLOSURE and
                  reproduction["omitMaxHelpingHand"]["baseSemanticSourceRemoved"] and
                  all(row["failedBeforeProductGeneration"] for row in reproduction["omitBaseProfiles"]),
                  "three runs and omission controls")
    if args.ios_manifest:
        verify_product(c, args.ios_manifest.resolve(), "ios", args.ios_ipa.resolve() if args.ios_ipa else None)
    if args.tvos_manifest:
        verify_product(c, args.tvos_manifest.resolve(), "tvos", args.tvos_ipa.resolve() if args.tvos_ipa else None)
    if args.physical_json:
        physical = load(args.physical_json)
        c.require(physical["iphone"]["status"] == "PASS" and physical["appleTv"]["status"] in
                  {"PASS", "TVOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE"} and physical["ipad"]["status"] in
                  {"PASS", "IPADOS_PHYSICAL_PENDING_HARDWARE_UNAVAILABLE"}, "physical-device disposition")
        c.require(physical["sharedClosureSha256"] == SHARED_CLOSURE,
                  "physical acceptance uses the final shared closure")
        if physical["iphone"]["status"] == "PASS":
            c.require(args.ios_manifest is not None and physical["iphone"]["productSha256"] ==
                      load(args.ios_manifest)["ipaSha256"], "iPhone acceptance uses the exact iOS product")
        if physical["appleTv"]["status"] == "PASS":
            c.require(args.tvos_manifest is not None and physical["appleTv"]["productSha256"] ==
                      load(args.tvos_manifest)["ipaSha256"], "Apple TV acceptance uses the exact tvOS product")
    if args.disk_json:
        disk = load(args.disk_json)
        c.require(disk["beforeCleanupKiB"] > 0 and disk["afterCleanupKiB"] >= 25 * 1024 * 1024 and
                  disk["beforeIosAotKiB"] >= 25 * 1024 * 1024 and disk["beforeTvosAotKiB"] > 0 and
                  disk["finalKiB"] > 0, "disk measurements and pre-iOS target")

    report = required[-1].read_text()
    c.require(set(map(str, range(1, 98))).issubset(set(re.findall(r"^\s*(\d+)\.\s", report, re.MULTILINE))),
              "all 97 closeout questions answered")
    stage_paths = required + [root / "docs/APPLE_EVEREST_STATIC_AOT.md", root / "docs/APPLE_EVEREST_COMPATIBILITY.md"]
    forbidden = re.compile(r"/Users/|/private/|mobileprovision|BEGIN (?:RSA |EC )?PRIVATE KEY", re.I)
    c.require(not any(forbidden.search(path.read_text(errors="replace")) for path in stage_paths), "privacy scan")
    tracked = git(root, "ls-files").splitlines()
    c.require(not any(path.lower().endswith((".zip", ".dll", ".bank", ".ipa", ".bin", ".guids.txt"))
                      for path in tracked if "stage25kh" in path.lower()), "no tracked binary inputs/products")
    c.require(git(root, "rev-parse", "--abbrev-ref", "HEAD") == "feature/apple-everest-everest-base-entities",
              "exact K-H feature branch")
    c.require(subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor", START, "HEAD"]).returncode == 0,
              "K-H descends from exact K-F tip")
    c.require(subprocess.run(["git", "-C", str(root), "merge-base", "--is-ancestor", KG, "HEAD"]).returncode != 0,
              "K-G diagnostic commit was not merged")
    c.require(git(root, "diff", "--name-only", START, "--", ".github/workflows") == "", "zero Actions changes")
    c.require(git(root, "rev-parse", "ios-v0.1.1-rc.1^{}") == "27e16b4724d94d3991b99c4795f680fcb0e5830c" and
              git(root, "rev-parse", "v1.0.0-rc.1^{}") == "ee52b0868df091746f134d95d4f020f94f23d4fb" and
              git(root, "rev-parse", "v1.0.0-rc.2^{}") == "641e86e4ed164cdf93f602ce2f11436449654d6e" and
              git(root, "rev-parse", "origin/release/v1.0.0-rc.3") ==
              "c8134c8ca7924cf12f48527e714b5242c6024927", "protected refs unchanged")
    c.require(subprocess.run(["git", "-C", str(root), "rev-parse", "v1.0.0-rc.3^{}"],
                             capture_output=True).returncode != 0, "rc3 tag remains absent")
    c.require(not args.require_clean or git(root, "status", "--porcelain") == "", "clean worktree")
    print(f"PASS: Stage 25K-H selected-factory closure ({c.count} checks)")


if __name__ == "__main__":
    main()
