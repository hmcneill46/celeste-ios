#!/usr/bin/env python3
"""Apply the locked Stage 24E1 Files portability transform."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil


def digest(path: pathlib.Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def manifest(root: pathlib.Path) -> dict[str, object]:
    records: list[dict[str, object]] = []
    logical = hashlib.sha256()
    for path in sorted(root.rglob("*"), key=lambda item: item.relative_to(root).as_posix()):
        if not path.is_file() or any(part in {"bin", "obj"} for part in path.relative_to(root).parts):
            continue
        relative = path.relative_to(root).as_posix()
        sha = digest(path)
        records.append({"path": relative, "size": path.stat().st_size, "sha256": sha})
        logical.update(relative.encode() + b"\0" + sha.encode() + b"\n")
    return {"fileCount": len(records), "logicalSha256": logical.hexdigest(), "files": records}


def replace_exact(path: pathlib.Path, old: str, new: str, label: str) -> None:
    text = path.read_text()
    if text.count(old) != 1:
        raise SystemExit(f"error: {label} source lock changed (matches={text.count(old)})")
    path.write_text(text.replace(old, new))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=pathlib.Path, required=True)
    parser.add_argument("--templates", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    before = manifest(root)

    shutil.copyfile(args.templates / "IOSDataFilesUI.cs", root / "Celeste" / "IOSDataFilesUI.cs")
    shutil.copyfile(args.templates / "IOSStoragePortabilityE1.cs", root / "Celeste" / "IOSStoragePortabilityE1.cs")
    storage = root / "Celeste" / "IOSStorageHooks.cs"
    replace_exact(storage, "public static class IOSStorageHooks", "public static partial class IOSStorageHooks",
                  "partial iOS storage portability boundary")

    menu = root / "Celeste" / "MenuOptions.cs"
    replace_exact(
        menu,
        '\t\tmenu.Add(iosPromptMode);\n\t\tviewport.Visible = Settings.Instance.Fullscreen;',
        '\t\tmenu.Add(iosPromptMode);\n'
        '\t\tmenu.Add(new TextMenu.Button("Data & Files").Pressed(OpenDataFiles));\n'
        '\t\tmenu.Add(new TextMenu.SubHeader(CelesteIOSFoundation.IOSPortBuildIdentity.DisplayLabel, false));\n'
        '\t\tviewport.Visible = Settings.Instance.Fullscreen;',
        "Apple-specific Data & Files and iOS port identity rows",
    )
    replace_exact(
        menu,
        "\tprivate static void OpenTouchControls()\n\t{",
        '''\tprivate static void OpenDataFiles()
\t{
\t\tmenu.Focused = false;
\t\tIOSDataFilesUI dataFilesUI = new IOSDataFilesUI();
\t\tdataFilesUI.OnClose = delegate
\t\t{
\t\t\tmenu.Focused = true;
\t\t};
\t\tEngine.Scene.Add(dataFilesUI);
\t\tEngine.Scene.OnEndOfFrame += delegate
\t\t{
\t\t\tEngine.Scene.Entities.UpdateLists();
\t\t};
\t}

\tprivate static void OpenTouchControls()
\t{''',
        "Data & Files submenu",
    )

    after = manifest(root)
    report = {
        "schemaVersion": 1,
        "platformTransform": "modern-ios-stage24e1-v3",
        "input": before,
        "output": after,
        "liveStorage": "Library/Application Support/Celeste",
        "externalSemantics": "validated-copy",
        "layoutFormat": "celestetouch-v1-d3-schema2",
        "generatedSourceTracked": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 24E1 iOS tree {after['fileCount']} files {after['logicalSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
