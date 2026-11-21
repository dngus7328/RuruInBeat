#!/usr/bin/env python3
"""License-free checks for the Unity mouse rhythm-game chart assets.

This is a static data validator, NOT a claim that the song/audio timing,
mouse input or bullet collisions have been tested in Unity Play Mode.
Uses only the Python standard library on GitHub Actions.
"""
from __future__ import annotations

import json
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


def supported_attack_ids() -> set[str]:
    """Read the actual pattern IDs registered by the C# gameplay code."""
    library = (ROOT / "Assets/Scripts/MousePlay/MouseModeBulletPatternLibrary.cs"
               ).read_text(encoding="utf-8-sig")
    director = (ROOT / "Assets/MouseMode/MouseModeBulletDirector.cs"
                ).read_text(encoding="utf-8-sig")
    ids = set(KNOWN_LEGACY)
    for source, field in ((library, "Ids"), (director, "AdvancedPatternIds")):
        match = re.search(
            r"public static readonly string\[\]\s+" + field +
            r"\s*=\s*\{(.*?)\};", source, re.DOTALL)
        if not match:
            raise ValueError(f"Cannot find attack ID registration: {field}")
        ids.update(re.findall(r'"([^"]+)"', match.group(1)))
    ids.update(name for name in re.findall(r'case "([^"]+)":', director)
               if name.startswith("ex_"))
    return ids


SUPPORTED_ATTACKS = supported_attack_ids()


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



def actual_scene_note_settings() -> tuple[float, float]:
    """Use serialized Mouse.unity note diameter/lead, not a stale preview assumption."""
    scene = (ROOT / "Assets/Scene/Mouse.unity").read_text(encoding="utf-8-sig")
    def value(name: str) -> float:
        match = re.search(r"^  " + re.escape(name) + r":\s*([\d.]+)",
                          scene, re.MULTILINE)
        if not match:
            raise ValueError("Mouse.unity does not declare " + name)
        return float(match.group(1))
    return max(1.0, value("noteSize") * value("noteVisualScale")), min(
        2.2, max(0.7, value("approachTravelSeconds")))


def visible_body_overlaps(
    note_data: list[dict], bpm: float, offset: float,
    tempo: list[tuple[float, float]], diameter: float, lead: float,
    width: float, height: float,
) -> list[tuple[int, int]]:
    """Approximate runtime note-body visibility, including moving wheel heads."""
    padding = diameter * 0.62

    def ui(x: float, y: float) -> tuple[float, float]:
        return (min(width / 2 - padding, max(-width / 2 + padding,
                    (x - 0.5) * width)),
                min(height / 2 - padding, max(-height / 2 + padding,
                    (0.5 - y) * height)))

    def path(entry: dict) -> list[tuple[float, float]]:
        points = [ui(x, y) for x, y in entry["path"]] if (
            entry["kind"] == 2 and entry["path"]) else [
                ui(entry["x"], entry["y"])]
        filtered = []
        for p in points:
            if not filtered or math.dist(filtered[-1], p) > 0.5:
                filtered.append(p)
        if len(filtered) < 3:
            return filtered
        for _ in range(2):
            new = [filtered[0]]
            for a, b in zip(filtered, filtered[1:]):
                new.extend(((a[0] * .75 + b[0] * .25,
                             a[1] * .75 + b[1] * .25),
                            (a[0] * .25 + b[0] * .75,
                             a[1] * .25 + b[1] * .75)))
            new.append(filtered[-1])
            filtered = new
        return filtered

    def point(entry: dict, at: float) -> tuple[float, float]:
        points = entry["screen_path"]
        if entry["kind"] != 2 or len(points) <= 1 or at <= entry["hit"]:
            return points[0]
        total = sum(math.dist(a, b) for a, b in zip(points, points[1:]))
        remaining = total * max(0.0, min(1.0, (at - entry["hit"]) /
                                            max(.001, entry["end"] - entry["hit"])))
        for i in range(1, len(points)):
            length = math.dist(points[i - 1], points[i])
            if remaining <= length or i == len(points) - 1:
                t = max(0.0, min(1.0, remaining / max(.001, length)))
                a, b = points[i - 1], points[i]
                return a[0] * (1-t) + b[0] * t, a[1] * (1-t) + b[1] * t
            remaining -= length
        return points[-1]

    chart = []
    for note in note_data:
        start = beat_seconds(note["beat"], bpm, offset, tempo)
        end = beat_seconds(
            note["endBeat"] if note["kind"] == 2 else note["beat"],
            bpm, offset, tempo)
        chart.append({**note, "hit": start, "end": end,
                      "spawn": start - lead, "screen_path": path(note)})
    chart.sort(key=lambda n: n["hit"])
    pairs = []
    for i, a in enumerate(chart):
        for b in chart[i + 1:]:
            if b["spawn"] > a["end"] + .13:
                break
            begin = max(a["spawn"], b["spawn"])
            finish = min(a["end"] + .13, b["end"] + .13)
            if finish < begin:
                continue
            if a["kind"] != 2 and b["kind"] != 2:
                collides = math.dist(point(a, begin), point(b, begin)) < diameter
            else:
                # Wheel traversals are checked through the complete visible span.
                sample_count = max(32, math.ceil((finish - begin) / .035))
                collides = any(
                    math.dist(point(a, begin + (finish - begin) * k / sample_count),
                              point(b, begin + (finish - begin) * k / sample_count)) < diameter
                    for k in range(sample_count + 1))
            if collides:
                pairs.append((a["id"], b["id"]))
    return pairs


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
    detailed_notes: list[dict] = []
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
            detailed_notes.append({
                "id": note_id, "kind": k, "beat": b, "endBeat": e,
                "x": x, "y": y,
                "path": [(fnumber(px), fnumber(py)) for px, py in path_points],
            })

    if not hidden and not notes:
        fail(name, "no valid note entries")
    attacks = []
    exact_attacks = []
    for match in re.finditer(r"^    - beat:\s*(\S+)\s*\n\s+patternId:\s*(.*?)\s*$",
                             section(text, "attacks", None), re.MULTILINE):
        beat = fnumber(match.group(1))
        full_pattern = match.group(2).strip("'\"")
        pat = full_pattern.split("|", 1)[0].lower()
        attacks.append((beat, pat))
        exact_attacks.append((beat, full_pattern))
        if not math.isfinite(beat) or beat < 0:
            fail(name, f"attack at invalid beat {match.group(1)}")
        if not pat:
            fail(name, "attack has empty patternId")
        elif pat not in SUPPORTED_ATTACKS:
            fail(name, f"attack pattern has no registered runtime implementation: {pat}")
    if not hidden and not attacks:
        if difficulty.upper() == "MOUSE":
            fail(name, "selected MOUSE chart has no bullet attack events")
        else:
            warn(name, "legacy/non-MOUSE chart without attacks; song selector prefers MOUSE variant")

    # Input is position-independent. Only overlapping rendered note bodies
    # are a geometry failure. The preview is deliberately more compact than
    # the 1920x1080 reference Canvas, without assuming the old 900x450 canvas.
    overlap = 0
    if all(math.isfinite(rate) and rate > 0 for _, rate in changes):
        diameter, lead = actual_scene_note_settings()
        scenarios = [(1500.0, 720.0), (1792.0, 859.0)]
        for width, height in scenarios:
            conflicts = visible_body_overlaps(
                detailed_notes, bpm, offset, changes, diameter, lead,
                width, height)
            if conflicts:
                overlap += len(conflicts)
                preview = ", ".join(f"{a}/{b}" for a, b in conflicts[:5])
                issue = (f"{len(conflicts)} overlapping visible note pairs "
                         f"at {int(width)}x{int(height)} (IDs {preview})")
                if not hidden and difficulty.upper() == "MOUSE":
                    fail(name, issue)
                else:
                    warn(name, issue + "; draft/non-primary chart")

    # Reimporting old JSON must never silently discard live attack events
    # or restore the overlapping note positions that were just fixed.
    source_json = ROOT / "Assets/Editor" / (name + ".json")
    if source_json.exists():
        try:
            imported = json.loads(source_json.read_text(encoding="utf-8-sig"))
            mismatches = []
            live = {n["id"]: n for n in detailed_notes}
            source_notes = imported.get("notes", [])
            if len(source_notes) != len(detailed_notes):
                mismatches.append("note count differs")
            for entry in source_notes:
                n = live.get(entry.get("id"))
                if n is None:
                    mismatches.append("unknown source note id")
                    continue
                p = entry.get("position") or {}
                if (entry.get("kind") != n["kind"] or
                    abs(fnumber(entry.get("beat")) - n["beat"]) > .0001 or
                    abs(fnumber(p.get("x")) - n["x"]) > .00002 or
                    abs(fnumber(p.get("y")) - n["y"]) > .00002):
                    mismatches.append(f"note {n['id']} timing/kind/position differs")
                if n["kind"] == 2:
                    source_path = entry.get("path") or []
                    if (abs(fnumber(entry.get("endBeat")) - n["endBeat"]) > .0001 or
                        len(source_path) != len(n["path"])):
                        mismatches.append(f"wheel {n['id']} path/end differs")
                    else:
                        for p1, p2 in zip(source_path, n["path"]):
                            if (abs(fnumber(p1.get("x")) - p2[0]) > .0001 or
                                abs(fnumber(p1.get("y")) - p2[1]) > .0001):
                                mismatches.append(f"wheel {n['id']} path differs")
                                break
            imported_attacks = imported.get("attacks", [])
            if len(imported_attacks) != len(exact_attacks):
                mismatches.append("attack count differs")
            else:
                for i, (source, live_attack) in enumerate(
                    zip(imported_attacks, exact_attacks)):
                    if (abs(fnumber(source.get("beat")) - live_attack[0]) > .0001 or
                        source.get("patternId") != live_attack[1]):
                        mismatches.append(f"attack #{i} time/pattern differs")
                        break
            if mismatches:
                message = "JSON reimport would overwrite game data: " + ", ".join(mismatches[:5])
                if not hidden and difficulty.upper() == "MOUSE":
                    fail(name, message)
                else:
                    warn(name, message)
        except (ValueError, OSError, TypeError) as error:
            if not hidden and difficulty.upper() == "MOUSE":
                fail(name, f"source JSON cannot be read: {error}")
            else:
                warn(name, f"source JSON cannot be read: {error}")

    print(f"{name}: {len(notes)} notes / {len(attacks)} attacks / "
          f"{len(changes)} tempo markers / hidden={hidden} / "
          f"potential note-body overlaps={overlap}")
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
