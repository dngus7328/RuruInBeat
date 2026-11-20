#!/usr/bin/env python3
"""Produce a listening reference: original BOOTBEAT with chart-judgment clicks.

This does NOT validate musicality or replace a musician listening and playing.
Clicks use the same beat->seconds timing as the current constant-tempo chart.
"""
import json
import subprocess
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]
SONG = ROOT / "Assets/Song/BOOTBEAT.wav"
CHART = ROOT / "Assets/Editor/BOOTBEAT_MOUSE_LV6.json"
OUT = ROOT / "bootbeat-chart-click-preview.mp3"
RATE = 22050

def main():
    chart = json.loads(CHART.read_text(encoding="utf-8"))
    data = subprocess.check_output([
        "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
        "-i", str(SONG), "-ac", "2", "-ar", str(RATE),
        "-acodec", "pcm_s16le", "-f", "s16le", "pipe:1"
    ])
    original = np.frombuffer(data, dtype="<i2").reshape(-1, 2).astype(np.float32) / 32768.0
    mix = original * 0.62
    bpm = float(chart["bpm"])
    beat_zero = float(chart.get("beatZeroSeconds", 0.0))
    clicks = {"left": 0, "right": 0, "wheel": 0}
    for note in chart["notes"]:
        kind = int(note["kind"])
        t = beat_zero + float(note["beat"]) * 60.0 / bpm
        play_click(mix, t, kind)
        if kind == 2:
            end = beat_zero + float(note["endBeat"]) * 60.0 / bpm
            play_click(mix, end, 3)
            clicks["wheel"] += 1
        elif kind == 0:
            clicks["left"] += 1
        elif kind == 1:
            clicks["right"] += 1
    peak = float(np.max(np.abs(mix)))
    if peak > .97:
        mix *= .97 / peak
    pcm = np.rint(mix * 32767.0).clip(-32768, 32767).astype("<i2")
    subprocess.run([
        "ffmpeg", "-nostdin", "-y", "-hide_banner", "-loglevel", "error",
        "-f", "s16le", "-ac", "2", "-ar", str(RATE), "-i", "pipe:0",
        "-codec:a", "libmp3lame", "-qscale:a", "3", str(OUT)
    ], input=pcm.tobytes(), check=True)
    print(json.dumps({
        "file": OUT.name, "durationSec": round(len(original) / RATE, 3),
        "noteCount": len(chart["notes"]), "clickCount": clicks,
        "noteTiming": "beatZeroSeconds + beat * 60 / bpm",
        "warning": "Listening aid, NOT proof that clicks follow musical rhythm or Unity input latency."
    }))

def play_click(mix, t, kind):
    start = int(round(t * RATE))
    if start < 0 or start >= len(mix):
        return
    duration = .082 if kind != 3 else .12
    n = min(int(duration * RATE), len(mix) - start)
    if n <= 0:
        return
    seconds = np.arange(n, dtype=np.float32) / RATE
    frequency = (950.0 if kind == 0 else 1280.0 if kind == 1
                 else 630.0 if kind == 2 else 420.0)
    tone = np.sin(2 * np.pi * (frequency * seconds - 120.0 * seconds**2))
    envelope = np.minimum(1.0, seconds / .002) * np.exp(-seconds / .028)
    pulse = (tone * envelope * .36).astype(np.float32)
    if kind == 0:
        mix[start:start+n, 0] += pulse
        mix[start:start+n, 1] += pulse * .25
    elif kind == 1:
        mix[start:start+n, 0] += pulse * .25
        mix[start:start+n, 1] += pulse
    else:
        mix[start:start+n, 0] += pulse * .8
        mix[start:start+n, 1] += pulse * .8

if __name__ == "__main__":
    main()
