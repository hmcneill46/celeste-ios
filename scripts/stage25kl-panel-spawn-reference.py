"""Host-only pinned references for the selected chapter credits and spawn rules."""
import hashlib
import re
import subprocess
import zipfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def check(value, message):
    if not value:
        raise ValueError(message)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def method(source, signature):
    start = source.index(signature)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]


def reject_tags(dialog, names):
    for name in names:
        check(name.lower() + "_collabcreditstags" not in dialog, "unsupported selected chapter credit tags")


def prepare(package_root, probe, runtime, content, mounts, trees, bindings):
    patches = ROOT / ".build/apple-everest/upstream/Everest/Celeste.Mod.mm/Patches"
    pins = {"Level.cs": "f6416408cf8c05c2d17758eb3a0287b3f72eb30186589aa5407d432cdfe3fc5d",
            "LevelData.cs": "b927d3619603107a21365b8efeaa803c1b895ed44faab257cca0b7d06e934ead"}
    for name, digest in pins.items():
        check(sha(patches / name) == digest, "pinned spawn source differs: " + name)
    getter = method((patches / "Level.cs").read_text(), "public new Vector2 DefaultSpawnPoint")
    getter = method(getter, "get")
    getter = getter[getter.index("{"):].replace("patch_LevelData", "LevelData")
    getter = getter.replace("Session", "level.Session").replace("Bounds", "level.Bounds").replace("GetSpawnPoint(", "level.GetSpawnPoint(")
    record = method((patches / "LevelData.cs").read_text(), "private void CheckForDefaultSpawn")
    record = re.sub(r"\bDefaultSpawn\b", "level.DefaultSpawn", record[record.index("{"):]).replace("spawn.Attributes", "attributes")
    (probe / "PinnedSpawnReference.cs").write_text(
        "using System; using System.Collections.Generic; using System.Globalization; using Celeste; using Microsoft.Xna.Framework;\n"
        "namespace PinnedReference; internal static class SpawnReference {\n"
        "internal static Vector2 Get(Level level) " + getter + "\n"
        "internal static void Record(LevelData level, Dictionary<string,object> attributes, Vector2 coords) " + record + "\n}\n")

    with zipfile.ZipFile(package_root / "CollabUtils2.zip") as archive:
        names = [n for n in archive.namelist() if n.endswith("/CollabUtils2.dll") or n == "CollabUtils2.dll"]
        check(len(names) == 1, "CollabUtils2 DLL ambiguous")
        dll = archive.read(names[0])
    dll_hash = hashlib.sha256(dll).hexdigest()
    check(dll_hash == "ce7d646252a2fc51f1f513071d97eaa1acedf7ddb0871276c688914e37319f60", "pinned CollabUtils2 DLL differs")
    (probe / "PinnedCollab.dll").write_bytes(dll)
    result = subprocess.run(["dotnet", "tool", "run", "ilspycmd", "--", "-t",
        "Celeste.Mod.CollabUtils2.UI.InGameOverworldHelper", str(probe / "PinnedCollab.dll")], cwd=ROOT, capture_output=True, text=True)
    check(result.returncode == 0, "pinned CollabUtils2 UI decompilation failed")
    source = result.stdout
    draw = method(source, "private static void OnChapterPanelDrawCollabCreditsCheckpoint")
    draw_hash = hashlib.sha256(draw.encode()).hexdigest()
    draw = re.sub(r"//[^\n]*", "", draw).replace("(Language)null", "null")
    # ILSpy's unresolved nullable-value ABI; keep the complete empty-tag loop,
    # including the single reserved row, and all draw/scale/fade control flow.
    draw = re.sub(r"\(Color\)\(\(\(\?\?\)item\.(\w+)\) \?\? ([^)]+)\)", r"(item.\1 ?? \2)", draw)
    check("??)" not in draw, "unhandled CollabUtils2 ABI normalization")
    (probe / "PinnedPanelCreditsReference.cs").write_text(
        "using System; using System.Collections.Generic; using System.Linq; using Celeste; using Monocle; using Microsoft.Xna.Framework; using static Celeste.FancyText;\n"
        "namespace PinnedReference; internal static class PanelCreditsReference {\n"
        "static Text panelCollabCredits; static List<CreditsTag> panelCollabCreditsTags = new(); static AreaData collabInGameForcedArea;\n"
        "internal static void Render(Text text, AreaKey area, Vector2 center, int index, float height) { panelCollabCredits=text; collabInGameForcedArea=AreaData.Get(area); OnChapterPanelDrawCollabCreditsCheckpoint(new OuiChapterPanel{height=height},center,index); }\n"
        + draw + "\n}\n")

    applied = runtime / "Celeste/Mod/AppleEverestStatic/AppleEverestCollabRuntime.cs"
    actual = applied.read_text()
    normalized = actual
    accepted_transforms = {
        "Route => AppleEverestCollabModule.Instance?.Session;": "Route => null;",
        "new AppleEverestCollabJournalCover(journal)": "new OuiJournalCover(journal)"}
    for before, after in accepted_transforms.items():
        check(normalized.count(before) == 1, "accepted Collab source transform changed: " + before)
        normalized = normalized.replace(before, after)
    check(normalized == (ROOT / "apple-everest/runtime/AppleEverestCollabRuntime.cs").read_text(), "applied Collab runtime differs beyond accepted module/cover transforms")
    methods = ["private static bool IsForcedChapterPanel", "internal static int ChapterSwapHeight",
               "internal static bool NeedsChapterCheckpointPage", "private static bool UsesSyntheticBookmarks",
               "internal static void ConfigureChapterCheckpoints", "internal static bool ShouldDrawVanillaCheckpoint",
               "internal static void DrawChapterCredits"]
    # Expression-bodied members terminate at ';'; block methods use brace matching.
    extracted = []
    for signature in methods:
        start = actual.index(signature)
        tail = actual[start:]
        arrow = tail.find("=>")
        extracted.append(tail[:tail.index(";") + 1] if 0 <= arrow < tail.index("{") else method(actual, signature))
    (probe / "ActualCollabPanelMethods.cs").write_text(
        "using System; using System.Linq; using System.Collections.Generic; using Celeste; using Microsoft.Xna.Framework; using Monocle;\n"
        "namespace Celeste.Mod; internal static class AppleEverestCollabRuntime {\n"
        "const string ContinueCheckpoint=\"collabutils_continue\"; static Wrapper overworldWrapper; static string forcedMapSid; static bool allowSaving=true;\n"
        "static Dictionary<string,AppleEverestCollabMapDescriptor> Maps=new(); static readonly AppleEverestCollabChapterCredits chapterCredits=new();\n"
        "internal static void Setup(OuiChapterPanel panel,string sid,bool wrapped) {forcedMapSid=sid; overworldWrapper=wrapped?new Wrapper{WrappedScene=panel.Overworld}:null; chapterCredits.Clear();}\n"
        + "\n".join(extracted) + "\n}\n")
    check(actual.count("chapterCredits.Clear();") == 2, "chapter credits must clear on open and close")
    fancy = (runtime / "Celeste/FancyText.cs").read_text()
    parse_wrapper = "\tprivate Text Parse()\n\t{\n\t\treturn global::On.Celeste.FancyText.Invoke_Parse(this, (appleSelf) => appleSelf.AppleEverestOriginal_Parse());\n\t}\n\n\tprivate global::Celeste.FancyText.Text AppleEverestOriginal_Parse()\n\t{"
    check(fancy.count(parse_wrapper) == 1 and fancy.replace(parse_wrapper, "\tprivate Text Parse()\n\t{") ==
          (ROOT / ".build/celeste-ios/current/managed/Celeste/FancyText.cs").read_text(),
          "canonical FancyText body changed beyond its accepted typed HookGen wrapper")
    chapter = (runtime / "Celeste/OuiChapterPanel.cs").read_text()
    check("DrawChapterCredits(this, center, num, height)" in chapter and "ConfigureChapterCheckpoints(this)" in chapter and
          "NeedsChapterCheckpointPage(this)" in chapter and "ChapterSwapHeight(this, toHeight)" in chapter, "actual panel consumers incomplete")
    level = (runtime / "Celeste/Level.cs").read_text()
    check("DefaultSpawnPoint => global::Celeste.Mod.AppleEverestDefaultSpawn.Get(this)" in level and
          "AppleEverestDefaultSpawn.Record(this, child2.Attributes, Spawns[Spawns.Count - 1])" in (runtime / "Celeste/LevelData.cs").read_text(), "actual spawn consumers incomplete")
    canonical_level = (ROOT / ".build/celeste-ios/current/managed/Celeste/Level.cs").read_text()
    # Preserve the actual LoadLevel branch giving saved respawns and explicit
    # StartPosition precedence. This fix only replaces the fallback property.
    respawn = canonical_level.split("if (!Session.RespawnPoint.HasValue)", 1)[1].split("Player player", 1)[0]
    check(respawn in level and "DefaultSpawnPoint" in respawn, "explicit respawn priority changed")

    spawn_cases = []
    for path, parsed in trees.items():
        for group in parsed["tree"]["children"]:
            if group["name"] != "levels":
                continue
            for room in group["children"]:
                attrs = room["attributes"]
                players = [p for g in room.get("children", []) if g["name"] == "entities" for p in g.get("children", []) if p["name"] == "player"]
                spawn_cases.append({"sid": path[5:-4], "room": attrs["name"], "bounds": [attrs[k] for k in ["x", "y", "width", "height"]],
                    "players": [{"id": p["attributes"]["id"], "x": attrs["x"] + p["attributes"]["x"], "y": attrs["y"] + p["attributes"]["y"],
                                 "isDefaultSpawn": p["attributes"].get("isDefaultSpawn", False)} for p in players]})

    # Inspect every mounted selected panel's authored dialog keys. Excluded
    # destinations do not widen the selected credits profile.
    credits = []
    all_dialog = {}
    for (owner, path), mount in mounts.items():
        if path.startswith("Dialog/") and path.lower().endswith(".txt"):
            text = (content / mount["logicalPath"]).read_text(encoding="utf-8-sig")
            for match in re.finditer(r"(?m)^([^#\s=]+)\s*=([^\n]*)(?:\n((?:[ \t]+[^\n]*\n?)*))?", text):
                all_dialog[match[1].lower()] = match[2] + ("\n" + match[3] if match[3] else "")
    names = [re.sub(r"[^A-Za-z0-9]", "_", sid) for sid in bindings]
    reject_tags(all_dialog, names)
    rejected = False
    try:
        reject_tags({names[0].lower() + "_collabcreditstags": "injected unsupported tags"}, names)
    except ValueError:
        rejected = True
    check(rejected, "unsupported tag omission control did not reject")
    for path in trees:
        name = re.sub(r"[^A-Za-z0-9]", "_", path[5:-4])
        check(name.lower() + "_collabcreditstags" not in all_dialog, "unsupported selected chapter credit tags")
        raw = all_dialog.get(name.lower() + "_collabcredits")
        if raw is not None:
            credits.append({"sid": path[5:-4], "name": name, "raw": raw})
    check(len(credits) == 1 and all(s in credits[0]["raw"] for s in ["Hyperlife", "phant", "Nano", "Bissy"]), "original Bing credits not mounted")
    return {"spawnCases": spawn_cases, "panelCredits": credits}, {
        "authority": "PINNED_EVEREST_SPAWN_AND_COLLABUTILS2_TEXT_CREDITS", "spawnSourceSha256": pins,
        "collabDllSha256": dll_hash, "originalCreditsDrawSha256": draw_hash,
        "hostSpawnSourceSha256": sha(probe / "PinnedSpawnReference.cs"), "hostCreditsSourceSha256": sha(probe / "PinnedPanelCreditsReference.cs"),
        "actualPanelMethodsSha256": sha(probe / "ActualCollabPanelMethods.cs"), "acceptedCollabSourceTransforms": accepted_transforms, "originalRoomCount": len(spawn_cases),
        "selectedCreditsCount": len(credits), "selectedCreditTags": 0, "unsupportedTagGateControlRejected": rejected, "savedRespawnBranchUnchanged": True,
        "scope": "Complete pinned credits draw method with empty tag list; host rendering handles observe calls. Actual FancyText parser and glyph drawing remain canonical."}
