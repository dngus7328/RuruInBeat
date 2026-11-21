#!/usr/bin/env python3
"""Evidence-based, non-destructive musical audit of Full-Band Hit.

The original WAV, not the chart itself, supplies the evidence. This produces:
  * per-note hit-time and corresponding low/body/mid/high attack candidates
  * 4-bar summaries of the musical attack density versus chart-note density
  * strong attacks missing a nearby note (CANDIDATES, not mandated new notes)

Frequency bands do NOT identify isolated kick/snare/instrument stems. A strong
spectral attack is NOT automatically a good rhythm-game note. Manual listening
and Play Mode verification are still mandatory. This tool never edits beats.
"""
from __future__ import annotations

import csv
import json
import math
import subprocess
from pathlib import Path

import numpy as np

from bootbeat_tempo import beat_to_seconds

ROOT = Path(__file__).resolve().parents[2]
SONG = ROOT / "Assets/Song/Full-Band Hit.wav"
CHART = ROOT / "Assets/Editor/FullBandHit_MOUSE.json"
RATE = 11025
FFT = 1024
HOP = 128
BANDS = [
    ("low_45_220Hz", 45, 220),
    ("body_220_950Hz", 220, 950),
    ("mid_950_2600Hz", 950, 2600),
    ("high_2600_5400Hz", 2600, 5400),
]


def load_song():
    raw = subprocess.check_output([
        "ffmpeg", "-nostdin", "-hide_banner", "-loglevel", "error",
        "-i", str(SONG), "-vn", "-ac", "1", "-ar", str(RATE),
        "-acodec", "pcm_s16le", "-f", "s16le", "pipe:1"
    ])
    return np.frombuffer(raw, dtype="<i2").astype(np.float32) / 32768.0


def band_onset_data(signal):
    frames = (len(signal) - FFT) // HOP + 1
    frequency = np.fft.rfftfreq(FFT, 1.0/RATE)
    masks = [(frequency >= lo) & (frequency < hi)
             for _,lo,hi in BANDS]
    window = np.hanning(FFT).astype(np.float32)
    flux = np.zeros((len(BANDS), frames), dtype=np.float32)
    loud = np.zeros(frames, dtype=np.float32)
    previous = None
    for i in range(frames):
        block = signal[i*HOP:i*HOP+FFT]
        loud[i] = float(np.sqrt(np.mean(block*block)))
        # Positive log spectral changes reduce sustained-instrument leakage.
        spectrum = np.log1p(30.0*np.abs(np.fft.rfft(block*window)))
        if previous is not None:
            difference = np.maximum(spectrum-previous, 0.0)
            for k, mask in enumerate(masks):
                flux[k,i] = float(difference[mask].sum())
        previous = spectrum
    novelty = np.empty_like(flux)
    for k in range(len(BANDS)):
        local = np.convolve(flux[k], np.ones(19)/19.0, mode="same")
        flux_delta = np.maximum(0.0, flux[k]-.65*local)
        scale = max(1e-6, float(np.percentile(flux_delta, 94)))
        novelty[k] = np.minimum(flux_delta/scale, 4.0)
    times = (np.arange(frames)*HOP + FFT*.5)/RATE
    return times, novelty, loud


def detect_candidates(times, novelty):
    # Retain individual band peaks, which are often lost in a single global
    # onset envelope (bass + snare + hats can occur on different subdivisions).
    candidates = []
    for k, (name, _, _) in enumerate(BANDS):
        strength = novelty[k]
        threshold = max(.52, float(np.percentile(strength, 75)))
        found = []
        for i in range(3, len(strength)-3):
            if strength[i] < threshold:
                continue
            if strength[i] >= float(np.max(strength[i-3:i+4])):
                found.append((float(times[i]), float(strength[i])))
        selected = []
        for t, level in sorted(found, key=lambda v:-v[1]):
            if all(abs(t-previous[0]) >= .062 for previous in selected):
                selected.append((t, level))
        for t, level in sorted(selected):
            candidates.append({"time":round(t,5), "band":name,
                               "strength":round(level,4)})
    return sorted(candidates, key=lambda x:(x["time"],-x["strength"]))


def nearest_note_events(note_time, candidates):
    around = [v for v in candidates if abs(v["time"]-note_time)<=.10]
    result=[]
    for name, _, _ in BANDS:
        matching=[v for v in around if v["band"]==name]
        if not matching:
            result.append(None)
            continue
        # Prefer a clear attack very near the note, not just loudest nearby.
        best=max(matching,key=lambda x:
                 x["strength"]/(1+(abs(x["time"]-note_time)/.045)**2))
        result.append({"onsetSeconds":best["time"],
                       "distanceMs":round(1000*(best["time"]-note_time),2),
                       "strength":best["strength"]})
    return result


def main():
    song = load_song()
    chart = json.loads(CHART.read_text(encoding="utf-8"))
    times, novelty, rms = band_onset_data(song)
    candidates = detect_candidates(times, novelty)
    notes = chart["notes"]
    rows = []
    for i, note in enumerate(notes):
        t = float(beat_to_seconds(chart, float(note["beat"])))
        by_band = nearest_note_events(t,candidates)
        matches=[(BANDS[j][0],x) for j,x in enumerate(by_band) if x is not None]
        close=[(band,x) for band,x in matches if abs(x["distanceMs"])<=35]
        # Distinguish absent evidence from mismatches with the strongest band.
        lead = max(close if close else matches, key=lambda pair:
                   pair[1]["strength"]/(1+(abs(pair[1]["distanceMs"])/30)**2)
                  ) if matches else None
        status=("attack-within-35ms" if close else
                "attack-only-outside-35ms" if matches else "no-attack-within-100ms")
        audio_rms=float(np.interp(t,times,rms,left=0,right=0))
        reference_rms=max(1e-8,float(np.percentile(rms,75)))
        row={
            "id":note["id"],"kind":int(note["kind"]),
            "rmsAtHit":round(audio_rms,6),
            "rmsRatioToSongP75":round(audio_rms/reference_rms,4),
            "beat":note["beat"],"hitSeconds":round(t,6),
            "status":status,
            "bestBand":lead[0] if lead else "",
            "bestOffsetMs":lead[1]["distanceMs"] if lead else "",
            "bestStrength":lead[1]["strength"] if lead else "",
            "allBands":json.dumps(dict(zip((b[0] for b in BANDS),by_band)),
                                  ensure_ascii=False,separators=(",",":")),
            "wheelEndSeconds":(
               round(beat_to_seconds(chart,float(note["endBeat"])),6)
               if int(note["kind"])==2 else ""
            )
        }
        rows.append(row)

    # For each 16-beat phrase, show the audible transient pattern and the
    # note grid side by side; do not call it a chorus/verse without listening.
    sections=[]
    max_beat=max(float(n["beat"]) for n in notes)
    for start_beat in range(0,int(max_beat)+1,16):
        start_t=beat_to_seconds(chart,float(start_beat))
        end_t=beat_to_seconds(chart,float(start_beat+16))
        actual=[x for x in candidates if start_t<=x["time"]<end_t]
        section_notes=[n for n in rows if start_t<=n["hitSeconds"]<end_t]
        local=[x for x in section_notes if x["status"]=="attack-within-35ms"]
        # Strong uncharted attacks are only LISTENING CANDIDATES.
        gaps=[]
        for attack in actual:
            if attack["strength"] < 1.2:
                continue
            if any(abs(attack["time"]-n["hitSeconds"])<=.055
                   for n in section_notes):
                continue
            gaps.append(attack)
        gaps.sort(key=lambda x:-x["strength"])
        per_band={name:sum(x["band"]==name for x in actual)
                  for name,_,_ in BANDS}
        report={
            "startBeat":start_beat,
            "startSeconds":round(start_t,3),
            "endSeconds":round(end_t,3),
            "noteCount":len(section_notes),
            "notesNearAnAttack":len(local),
            "bandAttackCounts":per_band,
            "notedStrongAttacksToReview":gaps[:12],
            "notesToReview":[
                {"id":n["id"],"timeSeconds":n["hitSeconds"],
                 "offsetMs":n["bestOffsetMs"],"status":n["status"]}
                for n in section_notes if n["status"]!="attack-within-35ms"
            ],
        }
        sections.append(report)

    outdir=ROOT/"Reports"
    outdir.mkdir(exist_ok=True)
    csv_path=outdir/"FullBandHit_note_musical_evidence.csv"
    with csv_path.open("w",encoding="utf-8-sig",newline="") as f:
        writer=csv.DictWriter(f,fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    json_path=outdir/"FullBandHit_phrase_musical_evidence.json"
    json_path.write_text(json.dumps({
        "song":"Full-Band Hit.wav","chart":"FullBandHit_MOUSE",
        "musicDurationSeconds":round(len(song)/RATE,3),
        "noteCount":len(notes),
        "bandDefinitionsHz":BANDS,
        "warning":"Frequency-band onset candidates DO NOT establish the intended musical note or verify exact timing. Review by ear in music+click references.",
        "sections":sections
    },ensure_ascii=False,indent=2),encoding="utf-8")

    # Wheel ends may follow a sustained sound's release, not a new attack.
    # Assess the waveform envelope separately from the note-head onset test.
    wheel_releases=[]
    for note in notes:
        if int(note["kind"])!=2:
            continue
        end=float(beat_to_seconds(chart,float(note["endBeat"])))
        before=rms[(times>=end-.16)&(times<end-.035)]
        after=rms[(times>end+.035)&(times<=end+.16)]
        before_level=float(np.mean(before)) if len(before) else 0.0
        after_level=float(np.mean(after)) if len(after) else 0.0
        onset=nearest_note_events(end,candidates)
        close=[(BANDS[k][0],c) for k,c in enumerate(onset)
               if c is not None and abs(c["distanceMs"])<=35]
        wheel_releases.append({
            "id":note["id"],"endBeat":note["endBeat"],
            "endSeconds":round(end,6),
            "rmsBefore":round(before_level,5),
            "rmsAfter":round(after_level,5),
            "afterOverBefore":round(after_level/max(before_level,.00001),3),
            "hasBandOnsetWithin35Ms":bool(close),
            "onsetBands":close,
            "warning":"Whole-mix RMS drop/attack does not prove this wheel release matches a specific instrument."
        })
    wheel_path=outdir/"FullBandHit_wheel_release_evidence.json"
    wheel_path.write_text(json.dumps(wheel_releases,ensure_ascii=False,indent=2),
                          encoding="utf-8")
    print("FULLBAND_WHEEL_RELEASE "+json.dumps(wheel_releases,
          ensure_ascii=False),flush=True)

    summary={
        "durationSec":round(len(song)/RATE,3),
        "notes":len(rows),
        "nearAttack":sum(n["status"]=="attack-within-35ms" for n in rows),
        "outside35ms":sum(n["status"]=="attack-only-outside-35ms" for n in rows),
        "noAttackWithin100ms":sum(n["status"]=="no-attack-within-100ms" for n in rows),
        "strongUnchartedAttackCandidates":sum(
            len(section["notedStrongAttacksToReview"]) for section in sections),
        "warning":"Not a proof of correct musical chart; timbral attribution needs listening."
    }
    print("FULLBAND_ENDING_NOTES "+json.dumps([{
        "id":n["id"],"hitSeconds":n["hitSeconds"],"status":n["status"],
        "rms":n["rmsAtHit"],"rmsRelative":n["rmsRatioToSongP75"],
        "band":n["bestBand"],"offsetMs":n["bestOffsetMs"]
    } for n in rows[-22:]],ensure_ascii=False),flush=True)
    print("FULLBAND_MUSIC_REVIEW "+json.dumps([{
        "id":n["id"],"kind":n["kind"],"beat":n["beat"],
        "hitSec":n["hitSeconds"],"band":n["bestBand"],
        "strength":n["bestStrength"],"offsetMs":n["bestOffsetMs"],
        "status":n["status"],"rmsRelative":n["rmsRatioToSongP75"]
    } for n in rows if n["status"]!="attack-within-35ms"],
       ensure_ascii=False),flush=True)
    print("FULLBAND_MUSIC_SUMMARY "+json.dumps(summary),flush=True)
    for section in sections:
        print("FULLBAND_MUSIC_SECTION "+json.dumps({
          "startBeat":section["startBeat"],
          "noteCount":section["noteCount"],
          "nearAttack":section["notesNearAnAttack"],
          "bandAttacks":section["bandAttackCounts"],
          "noteReview":section["notesToReview"],
          "strongGaps":section["notedStrongAttacksToReview"]
        },separators=(",",":")),flush=True)
    print("FULLBAND_MUSIC_FILES "+json.dumps([str(csv_path),str(json_path)]),
          flush=True)


if __name__=="__main__":
    main()
