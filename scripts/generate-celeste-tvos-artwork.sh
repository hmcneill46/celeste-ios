#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'USAGE'
Usage: scripts/generate-celeste-tvos-artwork.sh [options]

Generate a local tvOS layered strawberry icon and static Top Shelf artwork
from a lawful Celeste installation. All output stays in an ignored directory.

Options:
  --game-root DIR  Celeste 1.4.0.0 root (default: CELESTE_GAME_ROOT)
  --output DIR     Ignored asset catalog root
                   (default: .build/tvos-self-build/artwork/Assets.xcassets)
  --clean          Replace a previously generated marked output
  -h, --help       Show this help

The source files are Celeste.png and Content/Graphics/SplashScreen.png. The
script installs nothing, never changes either source, and commits no artwork.
USAGE
}

SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)"
REPO_ROOT="$(CDPATH= cd -- "$SCRIPT_DIR/.." && pwd -P)"
GAME_ROOT="${CELESTE_GAME_ROOT:-}"
OUTPUT="$REPO_ROOT/.build/tvos-self-build/artwork/Assets.xcassets"
CLEAN=0
MARKER=.celeste-tvos-artwork

repo_path() { case "$1" in /*) printf '%s\n' "$1" ;; *) printf '%s/%s\n' "$REPO_ROOT" "$1" ;; esac; }
while (($#)); do
  case "$1" in
    --game-root) [[ $# -ge 2 ]] || exit 2; GAME_ROOT="$2"; shift 2 ;;
    --output) [[ $# -ge 2 ]] || exit 2; OUTPUT="$(repo_path "$2")"; shift 2 ;;
    --clean) CLEAN=1; shift ;;
    -h|--help) usage; exit 0 ;;
    *) echo "error: unknown option: $1" >&2; usage >&2; exit 2 ;;
  esac
done

[[ -n "$GAME_ROOT" && -d "$GAME_ROOT" ]] || { echo "error: --game-root or CELESTE_GAME_ROOT is required" >&2; exit 1; }
for tool in swift xcrun git python3; do command -v "$tool" >/dev/null || { echo "error: missing required existing tool: $tool" >&2; exit 1; }; done
case "$OUTPUT" in "$REPO_ROOT/.build/tvos-self-build/"*|"$REPO_ROOT/artifacts/tvos-self-build/"*) ;; *)
  echo "error: artwork output must stay below an ignored Stage 8 directory" >&2; exit 2 ;;
esac
relative_output="${OUTPUT#$REPO_ROOT/}"
git -C "$REPO_ROOT" check-ignore --no-index -q -- "$relative_output/$MARKER" || { echo "error: artwork output is not ignored" >&2; exit 1; }

ICON_SOURCE="$GAME_ROOT/Celeste.png"
SPLASH_SOURCE="$GAME_ROOT/Content/Graphics/SplashScreen.png"
[[ -f "$ICON_SOURCE" && -r "$ICON_SOURCE" ]] || { echo "error: missing readable \$CELESTE_GAME_ROOT/Celeste.png" >&2; exit 1; }
[[ -f "$SPLASH_SOURCE" && -r "$SPLASH_SOURCE" ]] || { echo "error: missing readable \$CELESTE_GAME_ROOT/Content/Graphics/SplashScreen.png" >&2; exit 1; }

if [[ -e "$OUTPUT" ]]; then
  [[ "$CLEAN" -eq 1 && -f "$OUTPUT/$MARKER" ]] || { echo "error: output exists; rerun with --clean" >&2; exit 1; }
  rm -rf -- "$OUTPUT"
fi
mkdir -p "$REPO_ROOT/.build/tvos-self-build"
TEMP_ROOT="$(mktemp -d "$REPO_ROOT/.build/tvos-self-build/.artwork.XXXXXX")"
trap 'rm -rf -- "$TEMP_ROOT"' EXIT
RAW="$TEMP_ROOT/raw"
mkdir -p "$RAW"
swift "$SCRIPT_DIR/celeste-tvos-artwork.swift" --icon "$ICON_SOURCE" --splash "$SPLASH_SOURCE" --output "$RAW"

CATALOG="$TEMP_ROOT/Assets.xcassets"
BRAND="$CATALOG/App Icon.brandassets"
mkdir -p "$BRAND"
python3 - "$BRAND" "$RAW" <<'PY'
import json, pathlib, shutil, sys
brand, raw = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
info={"author":"celeste-tvos-builder","version":1}
def write(path, value):
    path.mkdir(parents=True, exist_ok=True)
    (path/"Contents.json").write_text(json.dumps(value, indent=2, sort_keys=True)+"\n")
def image_set(path, images):
    write(path, {"images":images,"info":info})
def stack(name, stem, scales):
    root=brand/f"{name}.imagestack"
    # Asset catalogs list image-stack layers front-to-back. The final Back
    # layer is intentionally opaque, while the strawberry Front remains alpha.
    write(root,{"info":info,"layers":[{"filename":"Front.imagestacklayer"},{"filename":"Back.imagestacklayer"}]})
    for layer in ("Back","Front"):
        layer_root=root/f"{layer}.imagestacklayer"
        write(layer_root,{"info":info,"layers":[{"filename":"Content.imageset"}]})
        records=[]
        for scale in scales:
            source=raw/f"{stem}-{scale}-{layer.lower()}.png"
            target=layer_root/"Content.imageset"/source.name
            target.parent.mkdir(parents=True,exist_ok=True)
            shutil.copyfile(source,target)
            records.append({"filename":source.name,"idiom":"tv","scale":scale})
        image_set(layer_root/"Content.imageset",records)
stack("App Icon - Small","small",["1x","2x"])
stack("App Icon - App Store","store",["1x"])
for name, stem in (("Top Shelf Image","top-shelf"),("Top Shelf Image Wide","top-shelf-wide")):
    records=[]
    for scale in ("1x","2x"):
        source=raw/f"{stem}-{scale}.png"
        target=brand/f"{name}.imageset"/source.name
        target.parent.mkdir(parents=True,exist_ok=True)
        shutil.copyfile(source,target)
        records.append({"filename":source.name,"idiom":"tv","scale":scale})
    image_set(brand/f"{name}.imageset",records)
write(brand,{"assets":[
    {"filename":"App Icon - App Store.imagestack","idiom":"tv","role":"primary-app-icon","size":"1280x768"},
    {"filename":"App Icon - Small.imagestack","idiom":"tv","role":"primary-app-icon","size":"400x240"},
    {"filename":"Top Shelf Image.imageset","idiom":"tv","role":"top-shelf-image","size":"1920x720"},
    {"filename":"Top Shelf Image Wide.imageset","idiom":"tv","role":"top-shelf-image-wide","size":"2320x720"}
],"info":info})
write(pathlib.Path(sys.argv[1]).parent,{"info":info})
PY
touch "$CATALOG/$MARKER"
mkdir -p "$(dirname -- "$OUTPUT")"
mv "$CATALOG" "$OUTPUT"

COMPILE_DIR="$TEMP_ROOT/compiled"
mkdir -p "$COMPILE_DIR"
xcrun actool "$OUTPUT" --compile "$COMPILE_DIR" --platform appletvos \
  --target-device tv --minimum-deployment-target 16.0 --app-icon "App Icon" \
  --output-partial-info-plist "$TEMP_ROOT/partial.plist" >/dev/null
[[ -f "$COMPILE_DIR/Assets.car" ]] || { echo "error: Xcode did not compile the asset catalog" >&2; exit 1; }
printf 'generated local tvOS artwork: <ARTWORK_ROOT>\n'
printf 'icon: layered black background and transparent strawberry foreground\n'
printf 'Top Shelf: centred aspect-fill at 2320x720 and 4640x1440 (plus standard variants)\n'
