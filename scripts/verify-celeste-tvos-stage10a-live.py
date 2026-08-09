#!/usr/bin/env python3
"""Stress a physical Stage 10A listener using ignored automation credentials."""

from __future__ import annotations

import argparse
import concurrent.futures
import hashlib
import http.client
import io
import json
import pathlib
import re
import urllib.parse
import zipfile


def fail(message: str) -> None:
    raise SystemExit(f"error: {message}")


def main() -> int:
    parser = argparse.ArgumentParser(description="Verify a live physical Stage 10A listener")
    parser.add_argument("--url", required=True)
    parser.add_argument("--code", required=True)
    parser.add_argument("--output", required=True, type=pathlib.Path)
    parser.add_argument("--downloads", required=True, type=pathlib.Path)
    args = parser.parse_args()

    parsed = urllib.parse.urlsplit(args.url)
    if parsed.scheme != "http" or not parsed.hostname or not parsed.port or parsed.path != "/" or parsed.query or parsed.fragment:
        fail("automation URL is not the expected numeric HTTP listener root")
    if not re.fullmatch(r"[0-9]{6}", args.code):
        fail("automation access code has an unexpected shape")
    args.downloads.mkdir(parents=True, exist_ok=True)

    def request(method: str, path: str, *, body: bytes | None = None, cookie: str | None = None) -> tuple[int, dict[str, str], bytes]:
        headers = {"Host": parsed.hostname, "Connection": "close"}
        if cookie:
            headers["Cookie"] = cookie
        if body is not None:
            headers["Content-Type"] = "application/x-www-form-urlencoded"
            headers["Content-Length"] = str(len(body))
        connection = http.client.HTTPConnection(parsed.hostname, parsed.port, timeout=12)
        try:
            connection.request(method, path, body=body, headers=headers)
            response = connection.getresponse()
            payload = response.read()
            return response.status, {name.lower(): value for name, value in response.getheaders()}, payload
        finally:
            connection.close()

    status, _, _ = request("GET", "/")
    if status != 200:
        fail(f"unauthenticated root returned HTTP {status}")
    wrong = "000000" if args.code != "000000" else "000001"
    status, _, _ = request("POST", "/auth", body=f"code={wrong}".encode("ascii"))
    if status != 401:
        fail(f"wrong access code returned HTTP {status}")
    status, headers, page = request("POST", "/auth", body=f"code={args.code}".encode("ascii"))
    if status != 200 or headers.get("x-celeste-authentication") != "accepted" or b"Download all files" not in page:
        fail("correct access code did not return the authenticated page directly")
    cookie = headers.get("set-cookie", "").split(";", 1)[0]
    if not cookie.startswith("CelesteSaveSession="):
        fail("authenticated response did not set the temporary session cookie")

    status, _, _ = request("HEAD", "/", cookie=cookie)
    if status != 200:
        fail(f"authenticated HEAD root returned HTTP {status}")

    routes = {
        "settings": ("/download/settings", "settings.celeste"),
        "0": ("/download/0", "0.celeste"),
        "1": ("/download/1", "1.celeste"),
        "2": ("/download/2", "2.celeste"),
    }
    payloads: dict[str, bytes] = {}
    for logical, (path, filename) in routes.items():
        status, response_headers, payload = request("GET", path, cookie=cookie)
        if status == 404:
            continue
        if status != 200 or response_headers.get("x-celeste-logical-name") != logical:
            fail(f"{logical} download returned an invalid response")
        expected_hash = response_headers.get("x-celeste-content-sha256")
        actual_hash = hashlib.sha256(payload).hexdigest()
        if expected_hash != actual_hash:
            fail(f"{logical} download hash differs from the response evidence")
        payloads[logical] = payload
        (args.downloads / filename).write_bytes(payload)

    # Repeated sequential and four-wide concurrent downloads prove that native
    # NWConnection objects are retired and the bounded connection gate reopens.
    for _ in range(3):
        for logical, (path, _) in routes.items():
            if logical not in payloads:
                continue
            status, _, payload = request("GET", path, cookie=cookie)
            if status != 200 or payload != payloads[logical]:
                fail(f"repeated {logical} download failed")

    work = [(logical, routes[logical][0]) for _ in range(4) for logical in payloads]
    def concurrent_download(item: tuple[str, str]) -> bool:
        logical, path = item
        status, _, payload = request("GET", path, cookie=cookie)
        return status == 200 and payload == payloads[logical]

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        if not all(pool.map(concurrent_download, work)):
            fail("a concurrent repeated download failed")

    status, archive_headers, archive_payload = request("GET", "/download/all", cookie=cookie)
    if status != 200 or archive_headers.get("content-type") != "application/zip":
        fail("download-all archive failed")
    with zipfile.ZipFile(io.BytesIO(archive_payload)) as archive:
        archived = {item.filename: archive.read(item) for item in archive.infolist()}
    expected_archive = {routes[name][1]: payload for name, payload in payloads.items()}
    if archived != expected_archive:
        fail("download-all archive does not exactly contain the exported logical payloads")
    (args.downloads / "Celeste-saves.zip").write_bytes(archive_payload)

    summary = {
        "schemaVersion": 1,
        "result": "PASS",
        "logicalFiles": sorted(payloads),
        "sequentialDownloadCount": len(payloads) * 4,
        "concurrentDownloadCount": len(work),
        "archiveEntries": sorted(archived),
        "hashes": {name: hashlib.sha256(payload).hexdigest() for name, payload in sorted(payloads.items())},
    }
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(summary, indent=2, sort_keys=True) + "\n")
    print(f"PASS: physical listener served {summary['sequentialDownloadCount']} sequential, {len(work)} concurrent, and one archive download")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
