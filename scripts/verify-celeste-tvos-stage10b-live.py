#!/usr/bin/env python3
"""Exercise a live Stage 10B listener using ignored, serializer-valid fixtures."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import http.client
import json
import pathlib
import re
import urllib.error
import urllib.parse
import urllib.request
import zipfile


TOKEN_RE = re.compile(r"const csrf='([0-9a-f]{64})',revision='([0-9a-f]{64})'")


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def request(url: str, cookie: str | None = None, *, data: bytes | None = None,
            headers: dict[str, str] | None = None) -> tuple[int, dict[str, str], bytes]:
    combined = dict(headers or {})
    if cookie:
        combined["Cookie"] = cookie
    method = "POST" if data is not None else "GET"
    item = urllib.request.Request(url, data=data, headers=combined, method=method)
    try:
        with urllib.request.urlopen(item, timeout=10) as response:
            return response.status, dict(response.headers.items()), response.read()
    except urllib.error.HTTPError as error:
        return error.code, dict(error.headers.items()), error.read()


def tokens(body: bytes) -> tuple[str, str]:
    match = TOKEN_RE.search(body.decode("utf-8", errors="strict"))
    if not match:
        fail("authenticated page lacks bounded session action tokens")
    return match.group(1), match.group(2)


def mutate(base: str, path: str, cookie: str, csrf: str, revision: str,
           payload: bytes | None = None) -> tuple[int, dict[str, str], bytes]:
    headers = {
        "X-Celeste-CSRF": csrf,
        "X-Celeste-Revision": revision,
    }
    if payload is not None:
        headers["Content-Type"] = "application/octet-stream"
    return request(urllib.parse.urljoin(base, path), cookie, data=payload or b"", headers=headers)


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify a live physical Stage 10B listener")
    parser.add_argument("--url", required=True)
    parser.add_argument("--code", required=True)
    parser.add_argument("--settings", type=pathlib.Path, required=True)
    parser.add_argument("--save", type=pathlib.Path, required=True)
    parser.add_argument("--output", type=pathlib.Path, required=True)
    args = parser.parse_args()

    if not re.fullmatch(r"[0-9]{6}", args.code):
        fail("access code must be six digits")
    parsed = urllib.parse.urlsplit(args.url)
    if parsed.scheme != "http" or not parsed.hostname or not parsed.port or parsed.path not in ("", "/"):
        fail("--url must be the exact numeric Save Manager root URL")
    settings = args.settings.read_bytes()
    save = args.save.read_bytes()
    if not settings or len(settings) > 65536:
        fail("Settings fixture is empty or exceeds the locked raw limit")
    if not save or len(save) > 262144:
        fail("SaveData fixture is empty or exceeds the locked raw limit")

    auth_body = urllib.parse.urlencode({"code": args.code}).encode("ascii")
    status, auth_headers, page = request(
        urllib.parse.urljoin(args.url, "/auth"),
        data=auth_body,
        headers={"Content-Type": "application/x-www-form-urlencoded"},
    )
    if status != 200 or auth_headers.get("X-Celeste-Authentication") != "accepted":
        fail(f"authentication failed with HTTP {status}")
    cookie_header = auth_headers.get("Set-Cookie", "").split(";", 1)[0]
    if not re.fullmatch(r"CelesteSaveSession=[0-9a-f]{64}", cookie_header):
        fail("authenticated session cookie is missing or malformed")
    csrf, revision = tokens(page)

    # Failures occur before any successful mutation and must not force restart.
    status, _, failure_page = mutate(args.url, "/replace/0", cookie_header, "0" * 64, revision, save)
    if status != 403:
        fail("invalid CSRF was not rejected")
    status, _, failure_page = mutate(args.url, "/replace/0", cookie_header, csrf, revision, b"not valid XML")
    if status != 422 or b"previous save was kept" not in failure_page:
        fail("malformed SaveData was not safely rejected")
    status, _, failure_page = mutate(args.url, "/replace/0", cookie_header, csrf, revision, settings)
    if status != 422:
        fail("Settings payload was accepted by a SaveData route")
    status, _, failure_page = mutate(args.url, "/replace/settings", cookie_header, csrf, revision, save)
    if status != 422:
        fail("SaveData payload was accepted by the Settings route")
    status, _, root_before = request(args.url, cookie_header)
    if status != 200 or b"Fully close and restart Celeste before continuing" in root_before:
        fail("failed mutations incorrectly forced restart-required")

    connection = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=10)
    connection.putrequest("POST", "/replace/0")
    connection.putheader("Cookie", cookie_header)
    connection.putheader("X-Celeste-CSRF", csrf)
    connection.putheader("X-Celeste-Revision", revision)
    connection.putheader("Content-Type", "application/octet-stream")
    connection.putheader("Content-Length", "262145")
    connection.endheaders()
    oversized = connection.getresponse()
    oversized.read()
    connection.close()
    if oversized.status != 413:
        fail("oversized upload was not rejected from headers")

    # A prior acceptance run may already contain this exact fixture. Make the
    # following replacement a guaranteed logical change so its former revision
    # is genuinely stale, without depending on an empty defaults namespace.
    status, _, existing_save = request(urllib.parse.urljoin(args.url, "/download/0"), cookie_header)
    if status == 200 and existing_save == save:
        status, _, page = mutate(args.url, "/delete/0", cookie_header, csrf, revision)
        if status != 200:
            fail("could not prepare a deterministic changed replacement")
        csrf, revision = tokens(page)
    elif status not in (200, 404):
        fail("could not inspect the controlled replacement target")

    old_revision = revision
    status, headers, page = mutate(args.url, "/replace/0", cookie_header, csrf, revision, save)
    if status != 200 or headers.get("X-Celeste-Mutation") not in ("committed", "unchanged"):
        fail(f"valid SaveData replacement failed with HTTP {status}")
    csrf, revision = tokens(page)
    status, download_headers, downloaded = request(urllib.parse.urljoin(args.url, "/download/0"), cookie_header)
    if status != 200 or downloaded != save:
        fail("download after replacement does not exactly match accepted bytes")
    if download_headers.get("X-Celeste-Content-SHA256") != hashlib.sha256(save).hexdigest():
        fail("download hash does not match accepted bytes")

    status, _, _ = mutate(args.url, "/replace/1", cookie_header, csrf, old_revision, save)
    if status != 409:
        fail("stale browser revision was not rejected")

    status, _, page = mutate(args.url, "/replace/settings", cookie_header, csrf, revision, settings)
    if status != 200:
        fail("valid Settings replacement failed")
    csrf, revision = tokens(page)
    status, _, downloaded_settings = request(urllib.parse.urljoin(args.url, "/download/settings"), cookie_header)
    if status != 200 or downloaded_settings != settings:
        fail("Settings replacement did not preserve exact bytes")

    # Make both concurrent targets guaranteed changes even when this fixed
    # acceptance namespace was populated by an earlier run.
    for logical_name in ("1", "2"):
        status, _, existing = request(urllib.parse.urljoin(args.url, f"/download/{logical_name}"), cookie_header)
        if status == 200 and existing == save:
            status, _, page = mutate(args.url, f"/delete/{logical_name}", cookie_header, csrf, revision)
            if status != 200:
                fail("could not prepare deterministic concurrent mutation targets")
            csrf, revision = tokens(page)
        elif status not in (200, 404):
            fail("could not inspect a controlled concurrency target")

    # Two real changes with one immutable revision: one commit, one conflict.
    def concurrent_mutation(path: str) -> tuple[int, dict[str, str], bytes]:
        return mutate(args.url, path, cookie_header, csrf, revision, save)

    with concurrent.futures.ThreadPoolExecutor(max_workers=2) as pool:
        responses = list(pool.map(concurrent_mutation, ("/replace/1", "/replace/2")))
    statuses = sorted(item[0] for item in responses)
    if statuses != [200, 409]:
        fail(f"concurrent stale mutation result was {statuses}, expected one commit and one conflict")
    success_page = next(item[2] for item in responses if item[0] == 200)
    csrf, revision = tokens(success_page)

    # Populate both controlled slots, then delete slot 2 and prove absence.
    status, _, page = mutate(args.url, "/replace/1", cookie_header, csrf, revision, save)
    if status == 409:
        status, _, current_page = request(args.url, cookie_header)
        csrf, revision = tokens(current_page)
        status, _, page = mutate(args.url, "/replace/1", cookie_header, csrf, revision, save)
    if status != 200:
        fail("second deliberate replacement in one activation failed")
    csrf, revision = tokens(page)
    status, _, page = mutate(args.url, "/delete/2", cookie_header, csrf, revision)
    if status != 200:
        fail("controlled slot deletion failed")
    csrf, revision = tokens(page)
    status, _, _ = request(urllib.parse.urljoin(args.url, "/download/2"), cookie_header)
    if status != 404:
        fail("deleted slot remained downloadable")

    status, _, page = mutate(args.url, "/reset/settings", cookie_header, csrf, revision)
    if status != 200:
        fail("Settings reset failed")
    csrf, revision = tokens(page)
    status, _, _ = request(urllib.parse.urljoin(args.url, "/download/settings"), cookie_header)
    if status != 404:
        fail("reset Settings remained downloadable")

    status, _, archive_bytes = request(urllib.parse.urljoin(args.url, "/download/all"), cookie_header)
    if status != 200:
        fail("post-mutation ZIP download failed")
    import io
    with zipfile.ZipFile(io.BytesIO(archive_bytes)) as archive:
        names = sorted(archive.namelist())
        if names != ["0.celeste", "1.celeste"]:
            fail(f"post-mutation ZIP fixed file set is wrong: {names}")
        if archive.read("0.celeste") != save or archive.read("1.celeste") != save:
            fail("post-mutation ZIP payload differs from accepted bytes")

    status, _, final_page = request(args.url, cookie_header)
    if status != 200 or b"Fully close and restart Celeste before continuing" not in final_page:
        fail("successful mutation did not leave the browser in restart-required state")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps({
        "schemaVersion": 1,
        "result": "PASS",
        "authentication": "PASS",
        "csrf": "PASS",
        "staleConflict": "PASS",
        "concurrentConflict": "PASS",
        "malformedRejected": "PASS",
        "oversizedRejectedBeforeBody": "PASS",
        "replaceExactBytes": "PASS",
        "delete": "PASS",
        "settingsReset": "PASS",
        "zipRefresh": "PASS",
        "restartRequired": "PASS",
        "settingsBytes": len(settings),
        "saveBytes": len(save),
    }, indent=2, sort_keys=True) + "\n")
    print("PASS: live writable Save Manager mutation, rollback, conflict, refresh, and restart boundaries")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
