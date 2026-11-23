#!/usr/bin/env python3
"""Export self-contained Unity sprites from user-authored PNG sprite sheets.

Original source files and their .meta GUIDs are never modified. Only assets in
Assets/Resources/RuruArt/OriginalParts/ are written.
"""
from __future__ import annotations

import re
import uuid
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Assets/Image"
OUTPUT = ROOT / "Assets/Resources/RuruArt/OriginalParts"
OUTPUT.mkdir(parents=True, exist_ok=True)

# Each entry identifies the source's Unity sprite index, not an invented drawing.
SHEETS = [
    ("TitleUI/버튼.png", [
        ("UI/TitleButton_Selected", 0, (75, 34, 75, 34)),
        ("UI/TitleButton_Unselected", 1, (75, 34, 75, 34)),
    ]),
    ("TitleUI/슬라이더.png", [
        ("UI/SettingsSlider_ActiveBar", 0, (28, 0, 28, 0)),
        ("UI/SettingsSlider_Track", 1, (28, 0, 28, 0)),
        ("UI/SettingsSlider_HandleDefault", 2, (0, 0, 0, 0)),
        ("UI/SettingsSlider_HandleHover", 3, (0, 0, 0, 0)),
        ("UI/SettingsSlider_HandlePressed", 4, (0, 0, 0, 0)),
        ("UI/SettingsSlider_HandleDisabled", 5, (0, 0, 0, 0)),
    ]),
    ("TitleUI/슬라이더게이지.png", [
        ("UI/SettingsSlider_FillLong", 0, (45, 0, 45, 0)),
        ("UI/SettingsSlider_DarkLong", 1, (45, 0, 45, 0)),
    ]),
    ("피노_Clear.png", [
        (f"Characters/Pino_Clear_{i}", i, (0, 0, 0, 0)) for i in range(4)
    ]),
    ("피노_False.png", [
        (f"Characters/Pino_Failed_{i}", i, (0, 0, 0, 0)) for i in range(4)
    ]),
]

SPRITE_PATTERN = re.compile(
    r'(?m)^    - serializedVersion: 2\s*'
    r'^      name: .+\s*'
    r'^      rect:\s*'
    r'^        serializedVersion: 2\s*'
    r'^        x: ([0-9.]+)\s*'
    r'^        y: ([0-9.]+)\s*'
    r'^        width: ([0-9.]+)\s*'
    r'^        height: ([0-9.]+)'
)

def sprite_meta(path: str, border: tuple[int, int, int, int]) -> str:
    # Stable GUID/ID, independent of build machine or checkout directory.
    guid = uuid.uuid5(uuid.NAMESPACE_URL, f"ruru-original-parts/{path}").hex
    sprite_id = uuid.uuid5(uuid.NAMESPACE_OID, guid).hex
    x, y, z, w = border
    return f"""fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  alphaUsage: 1
  alphaIsTransparency: 1
  textureType: 8
  textureShape: 1
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: {x}, y: {y}, z: {z}, w: {w}}}
  spriteGenerateFallbackPhysicsShape: 0
  maxTextureSize: 4096
  textureFormat: 1
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    physicsShape: []
    bones: []
    spriteID: {sprite_id}
    internalID: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def folder_meta(name: str) -> str:
    guid = uuid.uuid5(uuid.NAMESPACE_URL, f"ruru-original-parts/folder/{name}").hex
    return f"""fileFormatVersion: 2
guid: {guid}
folderAsset: yes
DefaultImporter:
  externalObjects: {{}}
  userData:
  assetBundleName:
  assetBundleVariant:
"""

def write_meta(path: Path, content: str) -> None:
    path.write_text(content, encoding="utf-8", newline="\n")

def main() -> None:
    for path in (OUTPUT, OUTPUT / "UI", OUTPUT / "Characters"):
        path.mkdir(parents=True, exist_ok=True)
        write_meta(path.with_suffix(".meta"), folder_meta(path.relative_to(ROOT).as_posix()))
    completed = 0
    for source_rel, parts in SHEETS:
        source = SOURCE / source_rel
        meta = source.with_name(source.name + ".meta")
        if not source.is_file() or not meta.is_file():
            raise FileNotFoundError(f"Required original missing: {source_rel}")
        sprites = [tuple(round(float(v)) for v in match)
                   for match in SPRITE_PATTERN.findall(meta.read_text(encoding="utf-8"))]
        if len(sprites) < len(parts):
            raise ValueError(f"Sprite rectangles missing: {source_rel}, found {len(sprites)}")
        with Image.open(source) as atlas:
            atlas = atlas.convert("RGBA")
            for target_rel, index, border in parts:
                x, y, w, h = sprites[index]
                box = (x, atlas.height - y - h, x + w, atlas.height - y)
                if min(box) < 0 or box[2] > atlas.width or box[3] > atlas.height:
                    raise ValueError(f"Invalid crop: {source_rel} #{index} {box}")
                frame = atlas.crop(box)
                if frame.getchannel("A").getextrema()[1] == 0:
                    raise ValueError(f"Empty alpha: {source_rel} #{index}")
                dest = OUTPUT / f"{target_rel}.png"
                dest.parent.mkdir(parents=True, exist_ok=True)
                frame.save(dest, format="PNG", optimize=True)
                write_meta(dest.with_name(dest.name + ".meta"),
                           sprite_meta(dest.relative_to(ROOT).as_posix(), border))
                print(f"[art] {dest.relative_to(ROOT)} ({w}x{h})")
                completed += 1
    assert completed == 18, f"Unexpected number of exports: {completed}"

if __name__ == "__main__":
    main()
