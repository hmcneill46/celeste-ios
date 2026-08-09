#!/usr/bin/env python3
"""Verify a Stage 8A app or signing-ready unsigned tvOS IPA."""

import argparse
import datetime
import hashlib
import json
import os
import pathlib
import plistlib
import re
import shutil
import subprocess
import sys
import tempfile

EXPECTED_BANKS = {
    "Content/FMOD/Desktop/Master Bank.bank",
    "Content/FMOD/Desktop/Master Bank.strings.bank",
    "Content/FMOD/Desktop/music.bank",
    "Content/FMOD/Desktop/sfx.bank",
    "Content/FMOD/Desktop/ui.bank",
    "Content/FMOD/Desktop/dlc_music.bank",
    "Content/FMOD/Desktop/dlc_sfx.bank",
}
def run(*args, check=True):
    return subprocess.run(args, check=check, stdout=subprocess.PIPE, stderr=subprocess.PIPE, text=True)

def sha256(path):
    digest=hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b""):
            digest.update(chunk)
    return digest.hexdigest()

def fail(message):
    raise SystemExit(f"error: {message}")

def verify_app(app, signed, repo=None):
    info_path=app/"Info.plist"
    if not info_path.is_file(): fail("application Info.plist is missing")
    info=plistlib.loads(info_path.read_bytes())
    if info.get("CFBundleDisplayName") != "Celeste" or info.get("CFBundleName") != "Celeste":
        fail("application display name is not Celeste")
    if str(info.get("MinimumOSVersion")) != "16.0": fail("minimum tvOS version is not 16.0")
    if 3 not in info.get("UIDeviceFamily", []): fail("bundle is not declared for Apple TV")
    icons=info.get("CFBundleIcons", {})
    if icons.get("CFBundlePrimaryIcon") != "App Icon - Small": fail("compiled layered app icon metadata is missing")
    shelf=info.get("TVTopShelfImage", {})
    if shelf.get("TVTopShelfPrimaryImage") != "Top Shelf Image" or shelf.get("TVTopShelfPrimaryImageWide") != "Top Shelf Image Wide":
        fail("compiled Top Shelf metadata is missing")
    if not (app/"Assets.car").is_file(): fail("compiled Assets.car is missing")
    privacy_path=app/"PrivacyInfo.xcprivacy"
    if not privacy_path.is_file(): fail("privacy manifest is missing")
    privacy=plistlib.loads(privacy_path.read_bytes())
    reasons=privacy.get("NSPrivacyAccessedAPITypes",[])
    expected={"NSPrivacyAccessedAPIType":"NSPrivacyAccessedAPICategoryUserDefaults","NSPrivacyAccessedAPITypeReasons":["CA92.1"]}
    if expected not in reasons: fail("Stage 6 UserDefaults CA92.1 privacy declaration is missing")
    asset_info=json.loads(run("xcrun","assetutil","--info",str(app/"Assets.car")).stdout)
    names={item.get("Name") for item in asset_info}
    for name in ("App Icon - Small","Top Shelf Image","Top Shelf Image Wide"):
        if name not in names: fail(f"compiled asset is missing: {name}")
    executable=app/info["CFBundleExecutable"]
    if not executable.is_file(): fail("bundle executable is missing")
    archs=run("lipo","-archs",str(executable)).stdout.split()
    if archs != ["arm64"]: fail(f"expected only arm64 executable, found {archs}")
    build=run("xcrun","vtool","-show-build",str(executable)).stdout
    if not re.search(r"platform\s+TVOS$",build,re.M): fail("executable is not a tvOS binary")
    if not re.search(r"minos\s+16\.0",build): fail("executable minimum platform is not tvOS 16.0")
    for aot in ("Celeste.aotdata.arm64","FNA.aotdata.arm64","CelesteTvOSRuntimeHost.aotdata.arm64"):
        if not (app/aot).is_file(): fail(f"full-AOT evidence file is missing: {aot}")
    exports=run("nm","-gjU",str(executable)).stdout
    for symbol in ("_FMOD_System_GetVersion","_FMOD_Studio_System_Initialize","_FMOD_SDL_Register"):
        if symbol not in exports: fail(f"real FMOD export is missing: {symbol}")
    banks={str(path.relative_to(app)).replace(os.sep,"/") for path in app.glob("Content/FMOD/**/*.bank")}
    if banks != EXPECTED_BANKS: fail(f"bank set differs from the exact seven files: {sorted(banks)}")
    for pattern in ("*.dylib","*.so","*.a","*.h"):
        if list(app.rglob(pattern)): fail(f"loose forbidden native/development file found: {pattern}")
    # Full-AOT .NET Apple bundles still carry trimmed ECMA metadata assemblies;
    # reject a loose native/desktop DLL, while accepting only managed metadata.
    for dll in app.rglob("*.dll"):
        description=run("file",str(dll)).stdout
        if "Mono/.Net assembly" not in description:
            fail(f"loose non-managed DLL found: {dll.name}")
    if repo:
        private_root=str(repo.resolve()).encode()
        for candidate in app.rglob("*"):
            if not candidate.is_file(): continue
            with candidate.open("rb") as stream:
                if private_root in stream.read():
                    fail(f"bundle embeds the local repository path in {candidate.name}")
    entitlement=run("codesign","-d","--entitlements",":-",str(app),check=False)
    entitlement_text=entitlement.stdout+entitlement.stderr
    for paid in ("com.apple.developer.user-management","com.apple.developer.icloud","com.apple.security.application-groups"):
        if paid in entitlement_text: fail(f"forbidden entitlement present: {paid}")
    if signed:
        if run("codesign","--verify","--deep","--strict",str(app),check=False).returncode != 0:
            fail("signed app does not pass codesign verification")
        profile_path=app/"embedded.mobileprovision"
        if not profile_path.is_file(): fail("signed app has no embedded development profile")
        try:
            profile=plistlib.loads(run("security","cms","-D","-i",str(profile_path)).stdout.encode())
        except Exception as exc:
            fail(f"embedded development profile could not be decoded: {exc}")
        signed_result=subprocess.run(
            ["codesign","-d","--entitlements",":-",str(app)],
            check=False,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        entitlement_bytes=signed_result.stdout+signed_result.stderr
        start=entitlement_bytes.find(b"<?xml")
        end=entitlement_bytes.find(b"</plist>")
        if start < 0 or end < 0: fail("signed executable entitlements could not be decoded")
        signed_entitlements=plistlib.loads(entitlement_bytes[start:end+8])
        profile_entitlements=profile.get("Entitlements",{})
        signed_app_id=signed_entitlements.get("application-identifier","")
        if not signed_app_id.endswith("."+info.get("CFBundleIdentifier","")):
            fail("signed application identifier does not match the bundle identifier")
        if profile_entitlements.get("application-identifier") != signed_app_id:
            fail("embedded profile application identifier does not match the signed executable")
        if profile_entitlements.get("com.apple.developer.team-identifier") != signed_entitlements.get("com.apple.developer.team-identifier"):
            fail("embedded profile team does not match the signed executable")
        if not profile_entitlements.get("get-task-allow") or not profile.get("ProvisionedDevices"):
            fail("embedded profile is not a device development profile")
        expiration=profile.get("ExpirationDate")
        now=datetime.datetime.now(datetime.timezone.utc).replace(tzinfo=None)
        if not isinstance(expiration,datetime.datetime) or expiration <= now:
            fail("embedded development profile is expired")
    else:
        if (app/"embedded.mobileprovision").exists(): fail("unsigned app contains an embedded provisioning profile")
        if (app/"_CodeSignature").exists(): fail("unsigned app contains a CodeResources signature")
        if run("codesign","--verify",str(app),check=False).returncode == 0:
            fail("unsigned app still has a usable code signature")
    return {"displayName":"Celeste","architecture":"arm64","platform":"TVOS","minimumOS":"16.0","banks":7,"signed":signed,"appBytes":sum(p.stat().st_size for p in app.rglob("*") if p.is_file())}

def verify_repo(repo):
    if not (repo/".git").exists(): fail("--repo-root is not a Git checkout")
    entitlements=(repo/"tvos/CelesteTvOSRuntimeHost/Entitlements.plist").read_text()
    project=(repo/"tvos/CelesteTvOSRuntimeHost/CelesteTvOSRuntimeHost.csproj").read_text()
    info=plistlib.loads((repo/"tvos/CelesteTvOSRuntimeHost/Info.plist").read_bytes())
    store=(repo/"tvos/CelesteTvOSRuntimeHost/Stage6PersistenceStore.cs").read_text()
    ignore=(repo/".gitignore").read_text()
    if info.get("CFBundleDisplayName")!="Celeste" or info.get("CFBundleName")!="Celeste": fail("tracked product name is not Celeste")
    for paid in ("com.apple.developer.user-management","com.apple.developer.icloud","com.apple.security.application-groups"):
        if paid in entitlements: fail(f"tracked forbidden entitlement present: {paid}")
    for required in ("<UseInterpreter>false</UseInterpreter>","<TrimMode Condition=\"'$(Configuration)' == 'Release'\">full</TrimMode>","CelesteBrandingEnabled"):
        if required not in project: fail(f"tracked release requirement is missing: {required}")
    for required in (
        "NSUserDefaults.StandardUserDefaults",
        '"CelesteTvOS.Persistence.v1"',
        "FormatVersion = 2",
        "ZLibStream",
        "CompressionLevel.SmallestSize",
        '"compressed-hash-mismatch"',
        '"uncompressed-hash-mismatch"',
    ):
        if required not in store: fail(f"Stage 6 standard-defaults persistence is missing: {required}")
    for required in (".build/tvos-self-build/","artifacts/tvos-self-build/","dist/"):
        if required not in ignore: fail(f"generated Stage 8 path is not ignored: {required}")
    tracked=run("git","-C",str(repo),"ls-files").stdout.splitlines()
    for path in tracked:
        if path.startswith((".build/tvos-self-build/","artifacts/tvos-self-build/","dist/")):
            fail(f"generated Stage 8 output is tracked: {path}")
        if pathlib.PurePosixPath(path).name in ("Celeste.png","SplashScreen.png"):
            fail(f"user-owned artwork is tracked: {path}")
    candidates=run("git","-C",str(repo),"status","--porcelain","-z").stdout.split("\0")
    for record in candidates:
        if not record: continue
        path=record[3:] if len(record)>3 else ""
        if path.endswith((".ipa",".mobileprovision",".p12",".cer",".png",".bank")) or ".app/" in path:
            fail(f"proprietary/signing/generated candidate is visible to Git: {path}")
    return {"repoIsolation":True,"userManagement":False,"stage6StandardUserDefaults":True,"generatedArtworkTracked":False}

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--app",type=pathlib.Path)
    parser.add_argument("--ipa",type=pathlib.Path)
    parser.add_argument("--signed",action="store_true")
    parser.add_argument("--output",type=pathlib.Path)
    parser.add_argument("--repo-root",type=pathlib.Path)
    args=parser.parse_args()
    if bool(args.app) == bool(args.ipa): fail("pass exactly one of --app or --ipa")
    temp=None
    try:
        if args.ipa:
            if args.signed: fail("IPA lane verifies signing-ready unsigned output only")
            if not args.ipa.is_file(): fail("IPA is missing")
            temp=pathlib.Path(tempfile.mkdtemp(prefix="celeste-stage8a-ipa."))
            run("ditto","-x","-k",str(args.ipa),str(temp))
            apps=list((temp/"Payload").glob("*.app")) if (temp/"Payload").is_dir() else []
            if len(apps) != 1 or apps[0].name != "Celeste.app": fail("IPA must contain exactly Payload/Celeste.app")
            result=verify_app(apps[0],False,args.repo_root)
            result.update({"ipaBytes":args.ipa.stat().st_size,"ipaSha256":sha256(args.ipa),"payload":"Payload/Celeste.app"})
        else:
            if not args.app.is_dir(): fail("app bundle is missing")
            result=verify_app(args.app,args.signed,args.repo_root)
        if args.repo_root:
            result.update(verify_repo(args.repo_root.resolve()))
        result["schemaVersion"]=1
        if args.output:
            args.output.parent.mkdir(parents=True,exist_ok=True)
            args.output.write_text(json.dumps(result,indent=2,sort_keys=True)+"\n")
        print(json.dumps(result,sort_keys=True))
    finally:
        if temp: shutil.rmtree(temp)

if __name__ == "__main__": main()
