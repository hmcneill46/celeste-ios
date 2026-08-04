#!/usr/bin/env python3
"""Remove local CodeView PDB paths from generated .NET Apple app assemblies."""

import argparse
import hashlib
import pathlib
import plistlib
import re
import struct
import subprocess
import tempfile


def fail(message):
    raise SystemExit(f"error: {message}")


def command(args, *, text=False, check=True):
    return subprocess.run(args, check=check, stdout=subprocess.PIPE,
                          stderr=subprocess.PIPE, text=text)


def rva_to_offset(blob, pe_offset, optional_size, rva):
    section_count=struct.unpack_from("<H",blob,pe_offset+6)[0]
    sections=pe_offset+24+optional_size
    for index in range(section_count):
        offset=sections+index*40
        virtual_size,virtual_address,raw_size,raw_pointer=struct.unpack_from("<IIII",blob,offset+8)
        if virtual_address <= rva < virtual_address+max(virtual_size,raw_size):
            return raw_pointer+(rva-virtual_address)
    fail("managed PE debug-directory RVA does not map to a section")


def sanitize_assembly(path, private_root):
    blob=bytearray(path.read_bytes())
    if len(blob) < 0x40 or blob[:2] != b"MZ":
        return 0
    pe_offset=struct.unpack_from("<I",blob,0x3C)[0]
    if blob[pe_offset:pe_offset+4] != b"PE\0\0":
        return 0
    optional_size=struct.unpack_from("<H",blob,pe_offset+20)[0]
    optional=pe_offset+24
    magic=struct.unpack_from("<H",blob,optional)[0]
    directory_base={0x10B:96,0x20B:112}.get(magic)
    if directory_base is None:
        fail(f"unsupported managed PE optional-header format: {path.name}")
    debug_rva,debug_size=struct.unpack_from("<II",blob,optional+directory_base+6*8)
    if not debug_rva or not debug_size:
        return 0
    debug_offset=rva_to_offset(blob,pe_offset,optional_size,debug_rva)
    changed=0
    for item in range(debug_size//28):
        entry=debug_offset+item*28
        debug_type=struct.unpack_from("<I",blob,entry+12)[0]
        data_size=struct.unpack_from("<I",blob,entry+16)[0]
        data_pointer=struct.unpack_from("<I",blob,entry+24)[0]
        if debug_type != 2 or data_size < 25 or blob[data_pointer:data_pointer+4] != b"RSDS":
            continue
        path_start=data_pointer+24
        path_end=blob.find(b"\0",path_start,data_pointer+data_size)
        if path_end < 0:
            fail(f"unterminated CodeView path: {path.name}")
        old_path=bytes(blob[path_start:path_end])
        if private_root not in old_path:
            continue
        replacement=b"linked.pdb\0"
        capacity=data_pointer+data_size-path_start
        if len(replacement)>capacity:
            fail(f"CodeView path field is unexpectedly small: {path.name}")
        blob[path_start:path_start+capacity]=replacement+b"\0"*(capacity-len(replacement))
        changed+=1
    if changed:
        path.write_bytes(blob)
    return changed


def extract_entitlements(app):
    result=command(["codesign","-d","--entitlements",":-",str(app)],check=False)
    payload=result.stdout+result.stderr
    start=payload.find(b"<?xml")
    end=payload.find(b"</plist>")
    if start < 0 or end < 0:
        fail("could not capture the existing signed entitlements")
    value=payload[start:end+8]
    plistlib.loads(value)
    return value


def identity_for_profile(app, team):
    profile=plistlib.loads(command(
        ["security","cms","-D","-i",str(app/"embedded.mobileprovision")]
    ).stdout)
    if team not in profile.get("TeamIdentifier",[]):
        fail("embedded development profile does not belong to the selected Personal Team")
    profile_hashes={hashlib.sha1(bytes(value)).hexdigest().upper()
                    for value in profile.get("DeveloperCertificates",[])}
    result=command(["security","find-identity","-v","-p","codesigning"],text=True)
    valid_hashes=set()
    for line in result.stdout.splitlines():
        match=re.search(r'^\s*\d+\)\s+([A-F0-9]{40})\s+"Apple Development:',line)
        if match: valid_hashes.add(match.group(1))
    matches=sorted(profile_hashes & valid_hashes)
    if len(matches)!=1:
        fail("expected exactly one installed Apple Development identity authorised by the embedded profile")
    return matches[0]


def scan_private_root(app, private_root):
    offenders=[]
    for path in app.rglob("*"):
        if path.is_file() and private_root in path.read_bytes():
            offenders.append(path.name)
    if offenders:
        fail("local repository path remains in: "+", ".join(sorted(set(offenders))))


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--app",required=True,type=pathlib.Path)
    parser.add_argument("--private-root",required=True,type=pathlib.Path)
    parser.add_argument("--resign-team",help="re-sign with the selected 10-character Personal Team")
    args=parser.parse_args()
    app=args.app.resolve()
    private_root=str(args.private_root.resolve()).encode()
    if not app.is_dir() or app.suffix != ".app": fail("--app must be a generated .app bundle")
    if args.resign_team and not re.fullmatch(r"[A-Z0-9]{10}",args.resign_team):
        fail("--resign-team is invalid")
    entitlements=extract_entitlements(app) if args.resign_team else None
    changed=0
    for assembly in sorted(app.glob("*.dll")):
        changed+=sanitize_assembly(assembly,private_root)
    scan_private_root(app,private_root)
    if args.resign_team:
        identity=identity_for_profile(app,args.resign_team)
        with tempfile.NamedTemporaryFile(suffix=".plist") as stream:
            stream.write(entitlements); stream.flush()
            command(["codesign","--force","--sign",identity,"--entitlements",stream.name,
                     "--generate-entitlement-der","--timestamp=none",str(app)])
        if command(["codesign","--verify","--deep","--strict",str(app)],check=False).returncode:
            fail("sanitized app failed code-sign verification")
    print(f"sanitized managed CodeView records: {changed}; local repository paths remaining: 0")


if __name__ == "__main__":
    main()
