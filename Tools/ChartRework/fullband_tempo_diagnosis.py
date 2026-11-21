#!/usr/bin/env python3
"""Look for a real Full-Band Hit tempo discrepancy before editing timing.

Reports candidate BPM/phase by 16-second audio regions. This is a diagnostic,
not proof of song meter; spectral transients can reflect offbeats or pickups.
"""
from pathlib import Path
import json
import numpy as np

from mouse_six_song_audio_audit import read_audio, onset_profile, peak_list, RATE
from bootbeat_tempo import beat_to_seconds

ROOT = Path(__file__).resolve().parents[2]
BPM0 = 190.0
AUDIO = ROOT / "Assets/Song/Full-Band Hit.wav"
CHART = ROOT / "Assets/Editor/FullBandHit_MOUSE.json"


def value_at(t, waveform_times, novelty):
    return np.interp(t, waveform_times, novelty, left=0, right=0)


def probe(t, x, novelty, beats, bpm, center, shifts):
    # Center-relative adjustment avoids aliasing due to huge absolute beat IDs.
    adjusted = t + (beats-center)*(60.0/bpm-60.0/BPM0)
    return np.array([float(np.mean(value_at(adjusted+shift, x, novelty)))
                     for shift in shifts])


def audit():
    chart = json.loads(CHART.read_text(encoding="utf-8"))
    samples = read_audio(AUDIO)
    x, novelty, rms = onset_profile(samples)
    # Max-pool three neighbouring frames so fractional FFT positions do not
    # introduce an arbitrary systematic 10-20ms penalty.
    smooth = np.maximum.reduce(
        [novelty, np.r_[novelty[0], novelty[:-1]],
         np.r_[novelty[1:], novelty[-1]]])
    note_beats = np.asarray([float(n["beat"]) for n in chart["notes"]])
    note_times = np.asarray([beat_to_seconds(chart, float(b)) for b in note_beats])
    bpm_grid = np.arange(180.0, 201.01, .2)
    shift_grid = np.arange(-.160, .161, .008)
    windows = []
    for start in np.arange(0, len(samples)/RATE, 16.):
        mask = (note_times >= start) & (note_times < start+16.)
        selected_beats = note_beats[mask]
        selected_times = note_times[mask]
        if len(selected_beats)<15:
            continue
        center = float(np.mean(selected_beats))
        candidates = []
        for bpm in bpm_grid:
            scores = probe(selected_times, x, smooth, selected_beats,
                           float(bpm), center, shift_grid)
            ix = int(np.argmax(scores))
            candidates.append((float(scores[ix]),float(bpm),float(shift_grid[ix])))
        best = max(candidates)
        original = probe(selected_times, x, smooth, selected_beats,
                         BPM0, center, np.array([0.0]))[0]
        # A better musical score at the same BPM indicates constant phase
        # shift, while a BPM change indicates systematic drift.
        phase_only = max(v for v in candidates if abs(v[1]-BPM0)<.001)
        ranking = sorted(candidates, reverse=True)[:4]
        report = {
            "startSec": round(float(start),1),
            "noteCount":len(selected_times),
            "bestBpm":round(best[1],2),
            "bestShiftMsAtCenter":round(best[2]*1000,1),
            "baseAt190Score":round(float(original),3),
            "bestScore":round(best[0],3),
            "phaseOnlyScoreAt190":round(phase_only[0],3),
            "improvementFactor":round(best[0]/max(.0001,float(original)),2),
            "top4":[{"bpm":round(b,2),"ms":round(sh*1000,1),
                     "score":round(v,3)} for v,b,sh in ranking]
        }
        windows.append(report)
        print("FULLBAND_WINDOW "+json.dumps(report),flush=True)
    output = ROOT/"fullband-tempo-diagnosis.json"
    output.write_text(json.dumps(windows,indent=2),encoding="utf-8")
    print("FULLBAND_REPORT_SAVED "+str(output),flush=True)


if __name__=="__main__":
    audit()
