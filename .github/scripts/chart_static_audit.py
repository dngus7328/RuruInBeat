#!/usr/bin/env python3
"""License-free checks for the Unity mouse rhythm-game chart assets.

This is a static data validator, NOT a claim that the song/audio timing,
mouse input or bullet collisions have been tested in Unity Play Mode.
Uses only the Python standard library on GitHub Actions.
"""
from __future__ import annotations

import math
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CHARTS = ROOT / "Assets/MouseMode/ChartSystem/Charts"
CATALOG = ROOT / "Assets/Resources/SongSelectChartCatalog.asset"
AUDIO = ROOT / "Assets/Song"
VALID_KIND = {0, 1, 2}
KNOWN_LEGACY = {"basic", "left", "right", "split", "spread", "fan", "rain", "aim",
                "laser", "burst"}
ERRORS: list[str] = []
WARNINGS: list[str] = []


def fail(chart: str, message: str) -> None:
    ERRORS.append(f"{chart}: {message}")


def warn(chart: str, message: str) -> None:
    WARNINGS.append(f"{chart}: {message}")


def scalar(content: str, key: str, default: str = "") -> str:
    match = re.search(r"^  " + re.escape(key) + r":\s*(.*?)\s*$",
                      content, re.MULTILINE)
    return match.group(1) if match else default


def data_scalar(content: str, key: str, default: str = "") -> str:
    match = re.search(r"^    " + re.escape(key) + r":\s*(.*?)\s*$",
                      content, re.MULTILINE)
    return match.group(1) if match else default


def section(content: str, start: str, stop: str | None) -> str:
    prefix = "    " + start + ":"
    idx = content.find(prefix)
    if idx < 0:
        return ""
    begin = content.find("\n", idx)
    if begin < 0:
        return ""
    end = content.find("\n    " + stop + ":", begin) if stop else -1
    return content[begin + 1:end if end >= 0 else None]


def fnumber(text: str, default: float = float("nan")) -> float:
    try:
        number = float(text)
        return number if math.isfinite(number) else default
    except (TypeError, ValueError):
        return default


def meta_guid(path: Path) -> str | None:
    candidate = Path(str(path) + ".meta")
    if not candidate.exists():
        return None
    match = re.search(r"^guid:\s*([a-f0-9]{32})\s*$",
                      candidate.read_text(encoding="utf-8-sig"), re.MULTILINE)
    return match.group(1) if match else None


def beat_seconds(beat: float, bpm: float, offset: float,
                 changes: list[tuple[float, float]]) -> float:
    seconds, previous, current_bpm = offset, 0.0, bpm
    for change_beat, next_bpm in changes:
        if change_beat >= beat:
            break
        seconds += (change_beat - previous) * 60.0 / current_bpm
        previous, current_bpm = change_beat, next_bpm
    return seconds + (beat - previous) * 60.0 / current_bpm


def inspect_chart(path: Path, valid_audio_guids: set[str]) -> tuple[str | None, bool]:
    name = path.stem
    text = path.read_text(encoding="utf-8-sig")
    hidden = scalar(text, "hideFromSongSelect", "0") == "1"
    level = fnumber(data_scalar(text, "level", "1"))
    bpm = fnumber(data_scalar(text, "bpm"))
    offset = fnumber(data_scalar(text, "beatZeroSeconds", "0"))
    approach = fnumber(data_scalar(text, "approachSeconds"))
    difficulty = data_scalar(text, "difficulty", "NORMAL").strip("'\"")
    song_guid_match = re.search(r"^  song:\s*\{[^\n]*guid:\s*([a-f0-9]{32})", text, re.MULTILINE)
    song_guid = song_guid_match.group(1) if song_guid_match else None

    if not hidden and (song_guid is None or song_guid not in valid_audio_guids):
        fail(name, "missing or unresolved AudioClip GUID")
    if not hidden and (level < 1 or level > 15):
        fail(name, f"level outside [1,15]: {level}")
    if not math.isfinite(bpm) or bpm <= 0:
        fail(name, f"invalid BPM: {bpm}")
        return song_guid, hidden
    if not math.isfinite(offset):
        fail(name, "invalid beatZeroSeconds")
        return song_guid, hidden
    if not math.isfinite(approach) or approach <= 0:
        fail(name, "invalid approachSeconds")

    changes: list[tuple[float, float]] = []
    for match in re.finditer(r"^    - beat:\s*(\S+)\s*\n\s+bpm:\s*(\S+)",
                             section(text, "tempoChanges", "approachSeconds"), re.MULTILINE):
        changes.append((fnumber(match.group(1)), fnumber(match.group(2))))
    for i, (at, rate) in enumerate(changes):
        if not all(map(math.isfinite, (at, rate))) or at <= 0 or rate <= 0:
            fail(name, f"invalid tempo marker {i}")
        if i and at <= changes[i - 1][0]:
            fail(name, f"tempo markers not strictly increasing at index {i}")

    entries = section(text, "notes", "attacks")
    notes = []
    ids: set[int] = set()
    for match in re.finditer(r"^    - id:\s*(\d+)\s*\n(.*?)(?=^    - id:|\Z)",
                             entries, re.MULTILINE | re.DOTALL):
        note_id = int(match.group(1))
        block = match.group(2)
        kind = re.search(r"^      kind:\s*(\d+)", block, re.MULTILINE)
        beat = re.search(r"^      beat:\s*(\S+)", block, re.MULTILINE)
        end = re.search(r"^      endBeat:\s*(\S+)", block, re.MULTILINE)
        position = re.search(r"^      position:\s*\{x:\s*([^,]+), y:\s*([^}]+)\}", block, re.MULTILINE)
        if note_id in ids:
            fail(name, f"duplicate note id {note_id}")
        ids.add(note_id)
        k = int(kind.group(1)) if kind else -1
        b = fnumber(beat.group(1) if beat else "")
        e = fnumber(end.group(1) if end else "")
        x = fnumber(position.group(1) if position else "")
        y = fnumber(position.group(2) if position else "")
        if k not in VALID_KIND:
            fail(name, f"note {note_id}: unknown kind {k}")
        if not math.isfinite(b) or b < 0:
            fail(name, f"note {note_id}: invalid beat")
        if not all(map(math.isfinite, (x, y))) or not (0 <= x <= 1 and 0 <= y <= 1):
            fail(name, f"note {note_id}: position outside play area")
        path_points = re.findall(r"^      - \{x:\s*([^,]+), y:\s*([^}]+)\}", block, re.MULTILINE)
        if k == 2:
            if not math.isfinite(e) or e <= b:
                fail(name, f"wheel {note_id}: endBeat must be after beat")
            if len(path_points) < 2:
                fail(name, f"wheel {note_id}: requires at least two path points")
            for px, py in path_points:
                ax, ay = fnumber(px), fnumber(py)
                if not math.isfinite(ax) or not math.isfinite(ay) or not (0 <= ax <= 1 and 0 <= ay <= 1):
                    fail(name, f"wheel {note_id}: path outside play area")
        if math.isfinite(b) and math.isfinite(x) and math.isfinite(y):
            notes.append((note_id, b, x, y))

    if not hidden and not notes:
        fail(name, "no valid note entries")
    attacks = []
    for match in re.finditer(r"^    - beat:\s*(\S+)\s*\n\s+patternId:\s*(.*?)\s*$",
                             section(text, "attacks", None), re.MULTILINE):
        beat = fnumber(match.group(1))
        pat = match.group(2).strip("'\"").split("|", 1)[0].lower()
        attacks.append((beat, pat))
        if not math.isfinite(beat) or beat < 0:
            fail(name, f"attack at invalid beat {match.group(1)}")
        if not pat:
            fail(name, "attack has empty patternId")
        elif not (pat in KNOWN_LEGACY or pat.startswith(("adv_", "ex_", "pop_"))):
            warn(name, f"unknown attack pattern: {pat}")
    if not hidden and not attacks:
        if difficulty.upper() == "MOUSE":
            fail(name, "selected MOUSE chart has no bullet attack events")
        else:
            warn(name, "legacy/non-MOUSE chart without attacks; song selector prefers MOUSE variant")

    # The current editor diagnostic uses a 900x450 note-area model and a
    # 1.75s display window. These are *readability warnings*, not proof that
    # a chart is impossible or defective.
    overlap = fast = 0
    if all(math.isfinite(rate) and rate > 0 for _, rate in changes):
        ordered = sorted((beat_seconds(b, bpm, offset, changes), x, y)
                         for _, b, x, y in notes)
        for i, (sec, x, y) in enumerate(ordered):
            if i:
                prev_sec, px, py = ordered[i - 1]
                dt = sec - prev_sec
                dist = math.hypot((x - px) * 900, (y - py) * 450)
                if dt > 0 and dist / dt > 1200:
                    fast += 1
            for s2, x2, y2 in ordered[i + 1:]:
                if s2 - sec >= 1.75:
                    break
                if math.hypot((x2 - x) * 900, (y2 - y) * 450) < 137:
                    overlap += 1
    if not hidden and overlap:
        warn(name, f"{overlap} potential overlapping approach displays (not invalid notes)")
    if not hidden and fast:
        warn(name, f"{fast} high mouse-travel-speed intervals (needs gameplay QA)")

    print(f"{name}: {len(notes)} notes / {len(attacks)} attacks / "
          f"{len(changes)} tempo markers / hidden={hidden} / "
          f"overlap flags={overlap} / travel flags={fast}")
    return song_guid, hidden


def main() -> int:
    if not CATALOG.exists():
        fail("catalog", "missing Resources chart catalog")
        return 1
    if not CHARTS.exists():
        fail("charts", "missing chart asset folder")
        return 1

    audio_guids = {g for p in AUDIO.rglob("*") if p.is_file()
                   and p.suffix.lower() in {".wav", ".mp3", ".ogg", ".aiff"}
                   if (g := meta_guid(p))}
    playable_guids: set[str] = set()
    for path in sorted(CHARTS.glob("*.asset")):
        guid = meta_guid(path)
        if guid is None:
            fail(path.name, "Unity .meta GUID missing")
        _, hidden = inspect_chart(path, audio_guids)
        if not hidden and guid is not None:
            playable_guids.add(guid)

    catalog_text = CATALOG.read_text(encoding="utf-8-sig")
    catalog_guids = re.findall(r"^  - \{[^\n]*guid:\s*([a-f0-9]{32})",
                               catalog_text, re.MULTILINE)
    if len(catalog_guids) != len(set(catalog_guids)):
        fail("catalog", "duplicate chart GUID")
    for missing in sorted(playable_guids - set(catalog_guids)):
        fail("catalog", f"visible chart absent from catalog GUID={missing}")
    for stale in sorted(set(catalog_guids) - playable_guids):
        fail("catalog", f"nonplayable/stale GUID in catalog={stale}")

    print(f"\nCatalog: {len(catalog_guids)} references; "
          f"{len(playable_guids)} visible chart assets; "
          f"{len(audio_guids)} audio clips with Unity GUIDs")
    for w in WARNINGS:
        print("WARNING:", w)
    for e in ERRORS:
        print("ERROR:", e, file=sys.stderr)
    print(f"\nStatic chart audit: {len(ERRORS)} errors, "
          f"{len(WARNINGS)} advisory warnings.")
    return 1 if ERRORS else 0


if __name__ == "__main__":
    sys.exit(main())
