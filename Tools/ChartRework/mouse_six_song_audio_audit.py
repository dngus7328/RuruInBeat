#!/usr/bin/env python3
"""Audio-backed timing audit of the six shipped mouse charts.

Needs ffmpeg and numpy. Analyses original Assets/Song audio, not a rendered
beat-chart or a generated click track. Outputs timing candidates and evidence;
NEVER automatically moves notes or labels spectral peaks as exact music beats.
"""
from __future__ import annotations

import json
import subprocess
from pathlib import Path
import numpy as np
from bootbeat_tempo import beat_to_seconds

ROOT = Path(__file__).resolve().parents[2]
RATE = 11025
HOP = 256
FFT = 1024
PAIRINGS = [
    ("BOOTBEAT_MOUSE_LV6", "BOOTBEAT.wav"),
    ("ArcadeGroove_MOUSE", "ArcadeGroove.mp3"),
    ("FullBandHit_MOUSE", "Full-Band Hit.wav"),
    ("MabatakiNoSenritsu_MOUSE", "MabatakiNoSenritsu.mp3"),
    ("RhythmGameTheme_MOUSE", "RhythmGameTheme.mp3"),
    ("popn_dash", "Pop'n Dash.mp3"),
]


def read_audio(path):
    raw = subprocess.check_output(
        ["ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
         "-i", str(path), "-vn", "-ac", "1", "-ar", str(RATE),
         "-f", "s16le", "-acodec", "pcm_s16le", "pipe:1"])
    signal = np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768.0
    return signal


def onset_profile(signal):
    frames = 1 + (len(signal) - FFT) // HOP
    if frames < 100:
        raise ValueError("Audio too short for beat analysis")
    window = np.hanning(FFT).astype(np.float32)
    previous = None
    flux = np.zeros(frames, dtype=np.float32)
    energy = np.zeros(frames, dtype=np.float32)
    for i in range(frames):
        block = signal[i*HOP:i*HOP+FFT]
        spectrum = np.abs(np.fft.rfft(block * window))
        energy[i] = float(np.sqrt(np.mean(block * block)))
        if previous is not None:
            flux[i] = np.maximum(0, spectrum - previous).sum()
        previous = spectrum
    smoothed = np.convolve(flux, np.ones(17) / 17, "same")
    novelty = np.maximum(0, flux - smoothed * .55)
    # Remove volume sensitivity and dense consecutive frames.
    scale = max(1e-5, float(np.percentile(novelty, 92)))
    novelty = np.minimum(2.5, novelty / scale)
    time = (np.arange(frames) * HOP + FFT * .5) / RATE
    return time, novelty, energy


def peak_list(time, novelty):
    threshold = max(.2, float(np.percentile(novelty, 78)))
    peaks = []
    for i in range(3, len(novelty)-3):
        if novelty[i] < threshold:
            continue
        if novelty[i] >= float(np.max(novelty[i-3:i+4])):
            peaks.append((float(time[i]), float(novelty[i])))
    selected = []
    for t, w in sorted(peaks, key=lambda pair: -pair[1]):
        if all(abs(t-other_t) >= .075 for other_t, _ in selected):
            selected.append((t, w))
    return sorted(selected)


def offsets_in_section(predicted, time, novelty):
    if len(predicted) < 8:
        return None
    offsets = np.arange(-.180, .181, .005)
    scores = []
    # Average nearby transient novelty to avoid sampling an FFT frame edge.
    for offset in offsets:
        sample = np.interp(predicted + offset, time, novelty,
                           left=0.0, right=0.0)
        scores.append(float(np.mean(sample)))
    scores = np.asarray(scores)
    best = int(np.argmax(scores))
    zero = int(np.argmin(np.abs(offsets)))
    return {"bestShiftMs": round(float(offsets[best]*1000), 1),
            "scoreAtZero": round(float(scores[zero]), 4),
            "scoreAtBest": round(float(scores[best]), 4),
            "medianSearchScore": round(float(np.median(scores)), 4),
            "sharpness": round(float(scores[best] /
                               max(.0001, np.median(scores))), 2)}


def grid_evidence(chart, time, novelty):
    duration = float(time[-1])
    beats = np.arange(0, 600, .5, dtype=float)
    times = np.array([beat_to_seconds(chart, float(b)) for b in beats])
    times = times[(times >= 0.0) & (times < duration)]
    # Each beat grid gets scored in a broad, phase-independent audio envelope.
    sections = []
    for start in np.arange(0.0, duration, 24.0):
        grid = times[(times >= start) & (times < start+24.)]
        evidence = offsets_in_section(grid, time, novelty)
        sections.append({"startSec": round(float(start), 2),
                         "gridCount": len(grid), "gridEvidence": evidence})
    return sections


def audit_one(chart_id, audio_name):
    chart_path = ROOT / "Assets/Editor" / (chart_id + ".json")
    chart = json.loads(chart_path.read_text(encoding="utf-8-sig"))
    audio_path = ROOT / "Assets/Song" / audio_name
    signal = read_audio(audio_path)
    time, novelty, rms = onset_profile(signal)
    peaks = peak_list(time, novelty)
    duration = len(signal) / RATE
    note_times = np.array([
        beat_to_seconds(chart, float(note["beat"]))
        for note in chart["notes"]], dtype=float)
    tap_notes = [(note, beat_to_seconds(chart, float(note["beat"])))
                 for note in chart["notes"]]
    active = note_times[(note_times >= 0) & (note_times < duration)]
    extra_notes = int(np.sum((note_times < 0) | (note_times > duration + .1)))

    # One consistent audio peak timestamp offset can originate in the
    # transient detector, not the chart. Keep it diagnostic, never auto-apply.
    peak_time = np.array([t for t, weight in peaks], dtype=float)
    near = [float(np.min(np.abs(peak_time - hit))) * 1000
            for hit in active] if len(peak_time) else []
    windows = []
    for start in np.arange(0.0, duration, 24.0):
        subset = active[(active >= start) & (active < start + 24.)]
        windows.append({"startSec": round(float(start), 1),
                        "noteCount": len(subset),
                        "noteEvidence": offsets_in_section(subset, time, novelty)})

    result = {
        "song": audio_name, "chart": chart_id,
        "durationSec": round(duration, 3),
        "chartBpm": chart.get("bpm"),
        "chartZeroSec": chart.get("beatZeroSeconds"),
        "tempoChanges": len(chart.get("tempoChanges") or []),
        "notes": len(chart["notes"]),
        "notesBeyondAudio": extra_notes,
        "firstHitSec": round(float(np.min(note_times)), 3),
        "lastHitSec": round(float(np.max(note_times)), 3),
        "peakCount": len(peaks),
        "medianNearestStrongTransientMs": round(float(np.median(near)), 1) if near else None,
        "p90NearestStrongTransientMs": round(float(np.percentile(near, 90)), 1) if near else None,
        "noteSections": windows,
        "gridSections": grid_evidence(chart, time, novelty),
        "diagnosticOnly": "Spectral transients are not verified beats or instruments; offsets need listening. Never auto-retime from these scores.",
    }
    return result


def main():
    results = []
    for chart_id, audio_name in PAIRINGS:
        result = audit_one(chart_id, audio_name)
        results.append(result)
        print("TIMING_SONG " + json.dumps({
            k: result[k] for k in (
                "song", "chart", "durationSec", "chartBpm", "chartZeroSec",
                "tempoChanges", "notes", "notesBeyondAudio", "firstHitSec",
                "lastHitSec", "medianNearestStrongTransientMs",
                "p90NearestStrongTransientMs")
        }, ensure_ascii=False), flush=True)
        for section in result["noteSections"]:
            print("TIMING_NOTE_SECTION " + chart_id + " " +
                  json.dumps(section, ensure_ascii=False), flush=True)
        for section in result["gridSections"]:
            print("TIMING_GRID_SECTION " + chart_id + " " +
                  json.dumps(section, ensure_ascii=False), flush=True)
    output = ROOT / "mouse-six-song-audio-timing-report.json"
    output.write_text(json.dumps(results, indent=2, ensure_ascii=False),
                      encoding="utf-8")
    print("TIMING_REPORT_SAVED " + str(output), flush=True)


if __name__ == "__main__":
    main()
