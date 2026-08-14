#!/usr/bin/env python3
"""Deterministic Stage 18C cloud-orchestration tests with synthetic inputs."""

from __future__ import annotations

import hashlib
import importlib.util
import json
import os
import pathlib
import stat
import subprocess
import tempfile
import zipfile


class Tests:
    def __init__(self) -> None:
        self.count = 0

    def check(self, condition: bool, label: str) -> None:
        if not condition:
            raise SystemExit(f"error: Stage 18C test failed: {label}")
        self.count += 1
        print(f"PASS {self.count:02d}: {label}")


def load_module(path: pathlib.Path):
    spec = importlib.util.spec_from_file_location("cloud_prepare_inputs", path)
    assert spec and spec.loader
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def expect_input_error(module, callback) -> bool:
    try:
        callback()
    except module.InputError:
        return True
    return False


def asset(name: str, size: int = 100, digest: str = "") -> dict:
    return {"name": name, "size": size, "digest": digest}


def release(*assets: dict) -> dict:
    return {"isDraft": False, "isPrerelease": False, "assets": list(assets)}


def create_zip(path: pathlib.Path, entries: list[tuple[str, bytes, int | None]]) -> None:
    with zipfile.ZipFile(path, "w") as archive:
        for name, data, mode in entries:
            info = zipfile.ZipInfo(name)
            if mode is not None:
                info.create_system = 3
                info.external_attr = mode << 16
            archive.writestr(info, data)


def run_bash(script: pathlib.Path, command: str, environment: dict[str, str]) -> subprocess.CompletedProcess[str]:
    env = dict(os.environ, **environment)
    return subprocess.run(
        ["bash", "-c", f"source {script!s}; {command}"],
        text=True,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        env=env,
    )


def main() -> int:
    repo = pathlib.Path(__file__).resolve().parents[1]
    template = repo / "cloud-builder-template"
    helper = template / "scripts/prepare-inputs.py"
    shell = template / "scripts/cloud-common.sh"
    build_workflow = (template / ".github/workflows/build.yml").read_text()
    cleanup_workflow = (template / ".github/workflows/cleanup.yml").read_text()
    module = load_module(helper)
    tests = Tests()

    tests.check(module.repository_is_private({"private": True, "visibility": "private"}),
                "private GitHub API response accepted")
    tests.check(not module.repository_is_private({"private": False, "visibility": "public"}),
                "public GitHub API response rejected")

    valid = module.classify_release_assets(release(asset("Celeste.zip"), asset("FMOD.dmg")))
    tests.check(set(valid) >= {"game", "fmod"} and valid["game"]["name"] == "Celeste.zip",
                "one ZIP plus one DMG classified")
    tests.check(expect_input_error(module, lambda: module.classify_release_assets(release(asset("FMOD.dmg")))),
                "zero ZIP rejected")
    tests.check(expect_input_error(module, lambda: module.classify_release_assets(
        release(asset("one.zip"), asset("two.zip"), asset("FMOD.dmg")))),
        "multiple ZIPs rejected")
    tests.check(expect_input_error(module, lambda: module.classify_release_assets(release(asset("Celeste.zip")))),
                "zero DMG rejected")
    tests.check(expect_input_error(module, lambda: module.classify_release_assets(
        release(asset("Celeste.zip"), asset("one.dmg"), asset("two.dmg")))),
        "multiple DMGs rejected")
    tests.check(expect_input_error(module, lambda: module.classify_release_assets(
        release(asset("Celeste.zip"), asset("FMOD.dmg"), asset("notes.bin")))),
        "unexpected extra asset rejected")
    tests.check(expect_input_error(module, lambda: module.classify_release_assets(
        release(asset("Celeste.zip", module.RELEASE_ASSET_LIMIT), asset("FMOD.dmg")))),
        "Release asset at two GiB rejected")

    with tempfile.TemporaryDirectory(prefix="stage18c-downloads-") as temporary:
        root = pathlib.Path(temporary)
        (root / "Celeste.zip").write_bytes(b"game")
        (root / "FMOD.dmg").write_bytes(b"fmod")
        manifest = module.classify_release_assets(release(
            asset("Celeste.zip", 4, "sha256:" + hashlib.sha256(b"game").hexdigest()),
            asset("FMOD.dmg", 4, "sha256:" + hashlib.sha256(b"fmod").hexdigest()),
        ))
        verified = module.verify_downloaded_assets(manifest, root)
        tests.check(verified["game"]["sha256"] == hashlib.sha256(b"game").hexdigest(),
                    "download size and server digest verified")
        manifest["game"]["serverDigest"] = "sha256:" + "0" * 64
        tests.check(expect_input_error(module, lambda: module.verify_downloaded_assets(manifest, root)),
                    "download digest mismatch rejected")

    with tempfile.TemporaryDirectory(prefix="stage18c-zip-") as temporary:
        root = pathlib.Path(temporary)
        normal = root / "normal.zip"
        create_zip(normal, [
            ("Celeste/Celeste.exe", b"managed", stat.S_IFREG | 0o644),
            ("Celeste/Content/test.bin", b"content", stat.S_IFREG | 0o644),
        ])
        extracted = root / "normal-out"
        result = module.safe_extract_zip(normal, extracted)
        tests.check(result["entries"] == 2 and (extracted / "Celeste/Celeste.exe").is_file(),
                    "normal single-wrapper ZIP extracted")

        root_zip = root / "root.zip"
        create_zip(root_zip, [("Celeste.exe", b"managed", stat.S_IFREG | 0o644)])
        root_out = root / "root-out"
        module.safe_extract_zip(root_zip, root_out)
        tests.check((root_out / "Celeste.exe").is_file(), "normal root-layout ZIP extracted")

        traversal = root / "traversal.zip"
        create_zip(traversal, [("../escape", b"bad", stat.S_IFREG | 0o644)])
        tests.check(expect_input_error(module, lambda: module.safe_extract_zip(traversal, root / "traversal-out")),
                    "parent traversal rejected")

        absolute = root / "absolute.zip"
        create_zip(absolute, [("/escape", b"bad", stat.S_IFREG | 0o644)])
        tests.check(expect_input_error(module, lambda: module.safe_extract_zip(absolute, root / "absolute-out")),
                    "absolute ZIP path rejected")

        linked = root / "link.zip"
        create_zip(linked, [("game-link", b"target", stat.S_IFLNK | 0o777)])
        tests.check(expect_input_error(module, lambda: module.safe_extract_zip(linked, root / "link-out")),
                    "ZIP symbolic link rejected")

        special = root / "special.zip"
        create_zip(special, [("device", b"bad", stat.S_IFCHR | 0o600)])
        tests.check(expect_input_error(module, lambda: module.safe_extract_zip(special, root / "special-out")),
                    "ZIP special file rejected")

        collision = root / "collision.zip"
        create_zip(collision, [
            ("Content/File", b"one", stat.S_IFREG | 0o644),
            ("content/file", b"two", stat.S_IFREG | 0o644),
        ])
        tests.check(expect_input_error(module, lambda: module.safe_extract_zip(collision, root / "collision-out")),
                    "case-colliding ZIP paths rejected")

    tests.check(module.disk_is_sufficient(25 * 1024**3, 25 * 1024**3) and
                not module.disk_is_sufficient(25 * 1024**3 - 1, 25 * 1024**3),
                "25 GiB disk threshold policy")

    with tempfile.TemporaryDirectory(prefix="stage18c-source-") as temporary:
        root = pathlib.Path(temporary)
        subprocess.run(["git", "init", "-q", str(root)], check=True)
        (root / "README").write_text("synthetic\n")
        subprocess.run(["git", "-C", str(root), "add", "README"], check=True)
        subprocess.run([
            "git", "-C", str(root), "-c", "user.name=Test", "-c", "user.email=test@example.invalid",
            "commit", "-qm", "synthetic",
        ], check=True)
        commit = subprocess.check_output(["git", "-C", str(root), "rev-parse", "HEAD"], text=True).strip()
        module.verify_source(root, commit)
        tests.check(expect_input_error(module, lambda: module.verify_source(root, "0" * 40)),
                    "source SHA mismatch rejected")

    with tempfile.TemporaryDirectory(prefix="stage18c-shell-") as temporary:
        temp = pathlib.Path(temporary)
        fake_bin = temp / "bin"
        fake_bin.mkdir()
        fake_gh = fake_bin / "gh"
        fake_gh.write_text("#!/bin/sh\ncase \"$1 $2\" in 'release view') exit ${FAKE_RELEASE_EXISTS:-1};; 'api repos/'*) exit 1;; *) exit 1;; esac\n")
        fake_gh.chmod(0o755)
        environment = {
            "PATH": str(fake_bin) + os.pathsep + os.environ["PATH"],
            "RUNNER_TEMP": str(temp),
            "GITHUB_REPOSITORY": "owner/private-builder",
        }
        allowed = run_bash(shell, "cloud_validate_cleanup_tag celeste-tvos-inputs && cloud_validate_cleanup_tag celeste-tvos-output", environment)
        tests.check(allowed.returncode == 0, "cleanup accepts only the two intended tags")
        denied = run_bash(shell, "cloud_validate_cleanup_tag unrelated-release", environment)
        tests.check(denied.returncode != 0, "cleanup rejects unrelated tag")
        absent = run_bash(shell, "cloud_ensure_output_absent", dict(environment, FAKE_RELEASE_EXISTS="1"))
        tests.check(absent.returncode == 0, "missing output Release permits build")
        present = run_bash(shell, "cloud_ensure_output_absent", dict(environment, FAKE_RELEASE_EXISTS="0"))
        tests.check(present.returncode != 0 and "already exists" in present.stderr,
                    "existing output Release is preserved and blocks overwrite")
        first = run_bash(shell, "cloud_delete_release_and_tag celeste-tvos-inputs", dict(environment, FAKE_RELEASE_EXISTS="1"))
        second = run_bash(shell, "cloud_delete_release_and_tag celeste-tvos-inputs", dict(environment, FAKE_RELEASE_EXISTS="1"))
        tests.check(first.returncode == second.returncode == 0 and first.stdout.strip() == second.stdout.strip() == "not-present",
                    "cleanup is idempotent when Release and tag are absent")

        metadata = temp / "metadata.txt"
        metadata.write_text(
            "Public source commit: 90ebb023f3043222bc67e72922ae4d68223f009c\n"
            "Celeste profile: synthetic-profile\nIPA bytes: 123\nIPA SHA-256: " + "a" * 64 + "\n"
        )
        summary = temp / "summary.md"
        rendered = run_bash(shell, f"cloud_write_success_summary {metadata}",
                            dict(environment, GITHUB_STEP_SUMMARY=str(summary)))
        text = summary.read_text() if summary.exists() else ""
        tests.check(rendered.returncode == 0 and "Build complete" in text and "unsigned" in text,
                    "success summary contains download and signing boundary")

    cache_paths = {
        "${{ runner.temp }}/celeste-tvos-cloud-source/artifacts/tvos-native/self-build",
        "${{ runner.temp }}/celeste-tvos-cloud-source/.build/tvos-host",
    }
    listed = {
        line.strip()
        for line in build_workflow.splitlines()
        if line.strip().startswith("${{ runner.temp }}/celeste-tvos-cloud-source/")
    }
    tests.check(listed == cache_paths, "cache path allow-list contains only two open-source paths")
    cache_key = run_bash(
        shell,
        f"cloud_compute_cache_key {repo}",
        {"ImageVersion": "stage18c-test-image", "RUNNER_OS": "macOS", "RUNNER_ARCH": "ARM64"},
    )
    empty_sha256 = hashlib.sha256(b"").hexdigest()
    tests.check(
        cache_key.returncode == 0
        and "stage18c-test-image" in cache_key.stdout
        and not cache_key.stdout.rstrip().endswith(empty_sha256),
        "cache key hashes tracked native inputs from the pinned source root",
    )
    tests.check("actions/upload-artifact" not in build_workflow + cleanup_workflow,
                "Actions artifacts are not used")
    tests.check("on:\n  workflow_dispatch:" in build_workflow and
                all(trigger not in build_workflow for trigger in ("pull_request:", "schedule:", "push:")),
                "Build workflow is manual-only")
    tests.check('--bundle-id "$CLOUD_BUNDLE_ID"' in build_workflow and
                '"${{ inputs.bundle_id }}"' not in build_workflow,
                "bundle identifier is passed as data rather than shell source")
    tests.check(
        '$CELESTE_SOURCE_ROOT/.build/celeste-runtime/stage18c-cloud/celeste-input.json' in build_workflow
        and '$CELESTE_SOURCE_ROOT/.build/fmod-tvos/stage18c-cloud/sdk-manifest.json' in build_workflow
        and 'celeste-tvos-cloud-state/validation' not in build_workflow,
        "validator manifests stay in their approved ignored repository roots",
    )

    print(f"PASS: {tests.count} Stage 18C deterministic cloud-builder tests")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
