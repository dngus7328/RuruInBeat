#!/usr/bin/env python3
"""Constrained tempo-map candidate for Full-Band Hit using original audio.

Treat this as a listening candidate, never as a verified human beat track.
Consecutive beat durations vary by at most ~8 ms from 190 BPM. Track a
continuous phase through the whole song (not independent offset guesses).
Print proposed tempo markers; do not modify working Unity assets.
"""
from pathlib import Path
import json
import numpy as np

from bootbeat_tempo import beat_to_seconds
from mouse_six_song_audio_audit import read_audio, onset_profile, peak_list, RATE

ROOT = Path(__file__).resolve().parents[2]
CHART = ROOT / "Assets/Editor/FullBandHit_MOUSE.json"
AUDIO = ROOT / "Assets/Song/Full-Band Hit.wav"
BASE_BPM = 190.0
STEP = 0.004
N = 151
PHASE = (np.arange(N) - (N//2)) * STEP
RATES = np.array([-2, -1, 0, 1, 2], dtype=int)
RATES_DELTA = RATES * STEP


def fit_tempo(chart, audio_times, strength):
    maxbeat = int(max(float(n["beat"]) for n in chart["notes"]))+1
    beats = np.arange(0, maxbeat+1, dtype=float)
    start = float(chart["beatZeroSeconds"])
    reference_times = start + beats * 60.0 / BASE_BPM
    # Original-audio transient strength is not the same as musical meter.
    # Use only local energy near each expected beat and a strong smoothness
    # constraint; no arbitrary per-note peak snapping.
    pool = np.maximum.reduce([
        strength, np.r_[strength[0],strength[:-1]],
        np.r_[strength[1:], strength[-1]]
    ])
    emissions = np.array([
        np.interp(t+PHASE, audio_times, pool, left=0.0, right=0.0)
        for t in reference_times
    ])
    node_reward = np.minimum(2.0, emissions)
    # A moderate prior prevents picking an unrelated off-beat at the start.
    prior = -.7 * (PHASE/.095)**2
    prev = np.tile(prior[:,None], (1,len(RATES))) + node_reward[0,:,None]
    prev -= .04 * np.abs(RATES)[None,:]
    previous_rate = []
    for i in range(1,len(beats)):
        next_scores = np.full_like(prev,-1e12)
        next_rate_pointers = np.full(prev.shape,-1,dtype=np.int16)
        for ri,step in enumerate(RATES):
            old = np.arange(N)-step
            valid = (old>=0)&(old<N)
            ids = np.clip(old,0,N-1)
            transitions = prev[ids,:] - .16*np.abs(
                np.arange(len(RATES))[None,:]-ri)
            br = np.argmax(transitions,axis=1)
            val = transitions[np.arange(N),br]
            val[~valid]=-1e12
            next_scores[:,ri] = val + node_reward[i,:] - .020*abs(step)
            next_rate_pointers[:,ri] = br
        prev = next_scores
        previous_rate.append(next_rate_pointers)

    ph,rate = np.unravel_index(np.argmax(prev),prev.shape)
    phase_index = np.zeros(len(beats),dtype=int)
    phase_index[-1]=ph
    for i in range(len(beats)-1,0,-1):
        pr=int(previous_rate[i-1][ph,rate])
        ph=int(ph-RATES[rate])
        phase_index[i-1]=ph
        rate=pr
    offset = PHASE[phase_index]
    adjusted = reference_times + offset

    # A 4-/8-beat linearized map is maintainable in the Unity editor and
    # keeps ordinary note beats intact. Each marker marks NEXT segment BPM.
    markers = []
    interval = 8
    points = list(range(0, int(beats[-1])+1, interval))
    if points[-1]!=int(beats[-1]): points.append(int(beats[-1]))
    for k in range(len(points)-1):
        a,b=points[k],points[k+1]
        bpm=60*(b-a)/(adjusted[b]-adjusted[a])
        if k>0: markers.append({"beat":a,"bpm":round(float(bpm),6)})
    bpm_start=60*(points[1]-points[0])/(adjusted[points[1]]-adjusted[points[0]])
    revised=dict(chart)
    revised["beatZeroSeconds"]=round(float(adjusted[0]),6)
    revised["bpm"]=round(float(bpm_start),6)
    revised["tempoChanges"]=markers
    # Evaluate map approximation separately from unconstrained 4ms path.
    new_t=np.array([beat_to_seconds(revised,float(b)) for b in beats])
    old_t=np.array([beat_to_seconds(chart,float(b)) for b in beats])
    old_score=np.mean(np.interp(old_t,audio_times,pool,left=0,right=0))
    new_score=np.mean(np.interp(new_t,audio_times,pool,left=0,right=0))
    peaks=peak_list(audio_times,strength)
    peak_t=np.array([t for t,_ in peaks])
    def near_stats(times):
        d=np.array([np.min(np.abs(peak_t-t)) for t in times])*1000
        return {"medianMs":round(float(np.median(d)),1),
                "p90Ms":round(float(np.percentile(d,90)),1),
                "within30ms":int(np.sum(d<=30))}
    old_notes=np.array([beat_to_seconds(chart,float(n["beat"])) for n in chart["notes"]])
    new_notes=np.array([beat_to_seconds(revised,float(n["beat"])) for n in chart["notes"]])
    measures={
      "oldBpm":chart["bpm"],"newStartBpm":revised["bpm"],
      "newZeroSec":revised["beatZeroSeconds"],"markers":len(markers),
      "maxAbsShiftMs":round(float(np.max(np.abs(new_notes-old_notes)))*1000,1),
      "medianShiftMs":round(float(np.median(new_notes-old_notes))*1000,1),
      "originalGridStrength":round(float(old_score),4),
      "candidateGridStrength":round(float(new_score),4),
      "originalNotes":near_stats(old_notes),
      "candidateNotes":near_stats(new_notes),
      "bpmRange": [round(float(min([revised["bpm"]]+[m["bpm"] for m in markers])),3),
                   round(float(max([revised["bpm"]]+[m["bpm"] for m in markers])),3)],
      "warning":"Audio-derived hypothesis, NOT a verified tempo map; audible review required before applying."
    }
    print("FULLBAND_FIT " + json.dumps(measures),flush=True)
    for i in range(0,len(markers),8):
        print("FULLBAND_MARKERS " + json.dumps(markers[i:i+8]),flush=True)
    (ROOT/"fullband-tempo-candidate.json").write_text(
        json.dumps(revised,indent=2,ensure_ascii=False),encoding="utf-8")
    return measures


def main():
    chart=json.loads(CHART.read_text(encoding="utf-8"))
    original=read_audio(AUDIO)
    times,strength,_=onset_profile(original)
    fit_tempo(chart,times,strength)


if __name__=="__main__":
    main()
