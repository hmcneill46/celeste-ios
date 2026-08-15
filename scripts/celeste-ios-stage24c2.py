#!/usr/bin/env python3
"""Apply the narrow Stage 24C2 iOS durability integration after Stage 24C1."""

from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil


EXPECTED_BLOCKS = {
    "save": "ed250c5ddcc7c11c77efea3fe929442f6bcb2b48a997abfbfb388151b10b5a81",
    "load": "90620c6d610b514145a07c62fd435f2cc09ad0a3dd73ab798d6b44eec195dd5d",
    "exists-delete": "e016b55785462b9fe9729de81f9aeb7d7c4ad41a4a1d301ab1599ee28da3f35b",
}


def digest(path: pathlib.Path) -> str:
    value = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


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


def replace_locked_block(text: str, start: str, end: str, expected: str, replacement: str, label: str) -> str:
    if text.count(start) != 1 or text.count(end) != 1:
        raise SystemExit(f"error: {label} anchors changed")
    first = text.index(start)
    last = text.index(end, first)
    old = text[first:last]
    actual = hashlib.sha256(old.encode()).hexdigest()
    if actual != expected:
        raise SystemExit(f"error: {label} source lock changed ({actual})")
    return text[:first] + replacement + text[last:]


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=pathlib.Path, required=True)
    parser.add_argument("--templates", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()
    root = args.root.resolve()
    before = manifest(root)
    if before["fileCount"] != 928:
        raise SystemExit("error: Stage 24C2 expected the 928-file Stage 24C1 iOS tree")

    shutil.copyfile(args.templates / "IOSStorageHooksC2.cs", root / "Celeste" / "IOSStorageHooks.cs")

    user_io = root / "Celeste" / "UserIO.cs"
    text = user_io.read_text()
    save = '''\tpublic static bool Save<T>(string path, byte[] data) where T : class
\t{
\t\tIOSStorageHooks.ValidateLogicalName(path);
\t\tbool flag = false;
\t\ttry
\t\t{
\t\t\tflag = IOSStorageHooks.SaveLogicalFile(path, data);
\t\t}
\t\tcatch (Exception ex)
\t\t{
\t\t\tConsole.WriteLine("ERROR: " + ex.ToString());
\t\t\tErrorLog.Write(ex);
\t\t}
\t\tif (!flag)
\t\t{
\t\t\tConsole.WriteLine("Save Failed");
\t\t}
\t\telse
\t\t{
\t\t\tIOSStorageHooks.FileChanged(path, "write");
\t\t}
\t\treturn flag;
\t}
'''
    load = '''\tpublic static T Load<T>(string path, bool backup = false) where T : class
\t{
\t\tT result = null;
\t\ttry
\t\t{
\t\t\tbyte[] data = IOSStorageHooks.ReadLogicalFile(path, backup);
\t\t\tif (data == null)
\t\t\t{
\t\t\t\treturn result;
\t\t\t}
\t\t\tusing (Stream stream = new MemoryStream(data, writable: false))
\t\t\t{
\t\t\t\tresult = Deserialize<T>(stream);
\t\t\t\treturn result;
\t\t\t}
\t\t}
\t\tcatch (Exception ex)
\t\t{
\t\t\tConsole.WriteLine("ERROR: " + ex.ToString());
\t\t\tErrorLog.Write(ex);
\t\t\treturn result;
\t\t}
\t}
'''
    exists_delete = '''\tpublic static bool Exists(string path)
\t{
\t\treturn IOSStorageHooks.LogicalFileExists(path);
\t}

\tpublic static bool Delete(string path)
\t{
\t\tIOSStorageHooks.ValidateLogicalName(path);
\t\tbool deleted = IOSStorageHooks.DeleteLogicalFile(path);
\t\tif (deleted)
\t\t{
\t\t\tIOSStorageHooks.FileChanged(path, "delete");
\t\t}
\t\treturn deleted;
\t}
'''
    text = replace_locked_block(text, "\tpublic static bool Save<T>", "\n\tpublic static T Load<T>",
                                EXPECTED_BLOCKS["save"], save, "UserIO Save")
    text = replace_locked_block(text, "\tpublic static T Load<T>", "\n\tprivate static T Deserialize<T>",
                                EXPECTED_BLOCKS["load"], load, "UserIO Load")
    text = replace_locked_block(text, "\tpublic static bool Exists", "\n\tpublic static void Close",
                                EXPECTED_BLOCKS["exists-delete"], exists_delete, "UserIO Exists/Delete")
    user_io.write_text(text)

    project = root / "Celeste.Modern.csproj"
    project_text = project.read_text()
    anchor = '''    <ProjectReference Include="$(CelesteAppleRepoRoot)/modern-ios/FNA.iOS/FNA.iOS.csproj"
                      AdditionalProperties="CustomAfterMicrosoftCommonTargets=$(CelesteAppleRepoRoot)/modern-ios/CelesteIOSRuntimeHost/IOSFnaExtension.targets;IOSCelesteRuntimeEnabled=true;CelesteAppleRepoRoot=$(CelesteAppleRepoRoot)" />'''
    reference = anchor + '''
    <ProjectReference Include="$(CelesteAppleRepoRoot)/modern-ios/CelesteIOSFoundation/CelesteIOSFoundation.csproj" />'''
    if project_text.count(anchor) != 1 or "CelesteIOSFoundation.csproj" in project_text:
        raise SystemExit("error: generated Celeste project reference boundary changed")
    project.write_text(project_text.replace(anchor, reference))

    after = manifest(root)
    report = {
        "schemaVersion": 1,
        "platformTransform": "modern-ios-stage24c2-v1",
        "input": before,
        "output": after,
        "storagePolicy": "Foundation atomic primary plus one previous-good backup",
        "logicalNames": ["settings", "0", "1", "2"],
        "maximumLogicalFileBytes": 64 * 1024 * 1024,
        "generatedSourceTracked": False,
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n")
    print(f"PASS: Stage 24C2 iOS tree {after['fileCount']} files {after['logicalSha256']}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
