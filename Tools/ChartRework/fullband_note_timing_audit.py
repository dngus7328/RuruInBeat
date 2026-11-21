#!/usr/bin/env python3
"""Per-note audio evidence and a conservative Full-Band Hit timing proposal.

Read the original WAV, inspect transient candidates at each note hit time,
and preserve original note order / tap kind / wheel duration / attack timing.

This script does NOT call a nearby transient the intended musical note.
Matched candidates require a strong clear local attack. Uncertain notes stay
unchanged and are explicitly reported, never claimed 'audio exact'.
"""
from __future__ import annotations
import json
import math
import numpy as np
from pathlib import Path
from mouse_six_song_audio_audit import read_audio, RATE
from bootbeat_tempo import beat_to_seconds, seconds_to_beat

ROOT=Path(__file__).resolve().parents[2]
AUDIO=ROOT/"Assets/Song/Full-Band Hit.wav"
CHART=ROOT/"Assets/Editor/FullBandHit_MOUSE.json"
HOP=128
FFT=1024

def extract_onsets(samples):
    n=1+(len(samples)-FFT)//HOP
    w=np.hanning(FFT).astype(np.float32)
    freqs=np.fft.rfftfreq(FFT,1/RATE)
    masks=[
        (freqs>=45)&(freqs<280),
        (freqs>=280)&(freqs<1700),
        (freqs>=1700)&(freqs<5300),
    ]
    flux=np.zeros((3,n),dtype=np.float32)
    previous=None
    for i in range(n):
        block=samples[i*HOP:i*HOP+FFT]
        spectrum=np.log1p(25*np.abs(np.fft.rfft(block*w))).astype(np.float32)
        if previous is not None:
            delta=np.maximum(spectrum-previous,0)
            for band,mask in enumerate(masks):
                flux[band,i]=float(np.sum(delta[mask]))
        previous=spectrum

    # Whitens bands so a quiet drum is not buried under a bright synthesizer.
    novelty=np.zeros_like(flux)
    for k in range(len(masks)):
        baseline=np.convolve(flux[k],np.ones(11)/11.,mode="same")
        novelty[k]=np.maximum(0,flux[k]-baseline*.6)
        scale=max(.001,float(np.percentile(novelty[k],94)))
        novelty[k]=np.minimum(novelty[k]/scale,4.)
    combined=.40*novelty[0]+.35*novelty[1]+.25*novelty[2]
    # Peaks are returned at the center of the waveform FFT slice.
    times=(np.arange(n)*HOP+FFT*.5)/RATE

    candidates=[]
    for i in range(2,n-2):
        if combined[i]<.22:
            continue
        if combined[i] < max(combined[i-2:i+3]):
            continue
        candidates.append((float(times[i]),float(combined[i]),i))
    selected=[]
    for cand in sorted(candidates,key=lambda x:-x[1]):
        if all(abs(cand[0]-other[0]) >= .040 for other in selected):
            selected.append(cand)
    selected.sort()
    return times,combined,selected

def audit():
    chart=json.loads(CHART.read_text(encoding="utf-8"))
    samples=read_audio(AUDIO)
    times,novelty,peaks=extract_onsets(samples)
    original=[beat_to_seconds(chart,float(n["beat"])) for n in chart["notes"]]
    proposals=[]
    matched=0
    pre_gaps=[]
    post_gaps=[]
    unresolved=[]
    for index,note in enumerate(chart["notes"]):
        old=float(original[index])
        previous=float(original[index-1]) if index>0 else -1e6
        following=float(original[index+1]) if index+1<len(original) else 1e6
        # If two notes are close in time, avoid retiming across each other.
        max_shift=min(.090, max(.025,(old-previous)*.33),
                      max(.025,(following-old)*.33))
        nearby=[(t,strength) for t,strength,_ in peaks
                if abs(t-old)<=max_shift]
        # Reward sufficiently strong audio attacks without greedily snapping
        # to a loud but unrelated neighbour a full sixteenth away.
        candidates=sorted(nearby,key=lambda x:(
             -float(x[1])/(1+(abs(x[0]-old)/.055)**2),
             abs(x[0]-old)
        ))
        best=candidates[0] if candidates else None
        local=np.interp(old,times,novelty)
        if best is not None:
            new,confidence=best
            deviation=abs(new-old)
            # Avoid committing a strong 80ms deviation on weak or diffuse
            # evidence, even when a local spectral event exists.
            accept=(confidence>=.45 and deviation<=.075)
            if accept:
                matched+=1
                rounded=float(round(seconds_to_beat(chart,new),6))
                confirmed_time=beat_to_seconds(chart,rounded)
                adjusted=confirmed_time
                note_beat=rounded
                label="onset-candidate"
            else:
                adjusted=old
                note_beat=note["beat"]
                label="review"
        else:
            confidence=0
            new=None
            adjusted=old
            note_beat=note["beat"]
            label="review"

        if label=="review":
            unresolved.append({"id":note["id"],"kind":note["kind"],
                "timeSec":round(old,4),"oldBeat":note["beat"],
                "nearestCandidateSec":round(new,4) if new is not None else None,
                "candidateDistanceMs":round(abs(new-old)*1000,1) if new is not None else None,
                "confidence":round(float(confidence),3)})
        if best is not None:
            pre_gaps.append(abs(old-new)*1000)
            post_gaps.append(abs(adjusted-new)*1000)
        proposals.append({
            "id":note["id"], "kind":note["kind"],
            "originalBeat":note["beat"],"newBeat":note_beat,
            "originalTimeSec":round(old,6),
            "proposedTimeSec":round(adjusted,6),
            "shiftMs":round((adjusted-old)*1000,2),
            "confidence":round(float(confidence),3),
            "evidence":label
        })

    # Wheel releases are separate judged events, so inspect endBeat too.
    # Keep the existing hold if the track offers no clear release cue.
    wheel_end_candidates=[]
    for note in chart["notes"]:
        if int(note["kind"])!=2:
            continue
        head=beat_to_seconds(chart,float(note["beat"]))
        end=beat_to_seconds(chart,float(note["endBeat"]))
        nearby=[(t,v) for t,v,_ in peaks if abs(t-end)<=.065]
        ranked=sorted(nearby,key=lambda pair:
            -pair[1]/(1+(abs(pair[0]-end)/.055)**2))
        accepted=None
        if ranked and ranked[0][1]>=.70 and ranked[0][0]>head+.35:
            accepted=round(seconds_to_beat(chart,ranked[0][0]),6)
        wheel_end_candidates.append({
            "id":note["id"],
            "oldBeat":note["endBeat"],
            "newBeat":accepted,
            "oldSeconds":round(end,5),
            "shiftMs":round((beat_to_seconds(chart,accepted)-end)*1000,2)
                      if accepted is not None else None,
            "confidence":round(float(ranked[0][1]),3) if ranked else 0,
        })

    # A per-note proposal must keep hit order. Do not modify attack beats.
    proposed_times=[n["proposedTimeSec"] for n in proposals]
    monotonic=all(b>=a-1e-5 for a,b in zip(proposed_times,proposed_times[1:]))
    if not monotonic:
        raise RuntimeError("Audio proposal would reverse note order.")
    summary={
        "chart":"FullBandHit_MOUSE",
        "noteCount":len(proposals),
        "matchedAudioAttackCount":matched,
        "unresolvedCount":len(unresolved),
        "maxAbsShiftMs":max(round(abs(n["shiftMs"]),2) for n in proposals),
        "beforeCandidateMedianMs":round(float(np.median(pre_gaps)),2) if pre_gaps else None,
        "afterCandidateMedianMs":round(float(np.median(post_gaps)),2) if post_gaps else None,
        "longNoteHeads":sum(n["kind"]==2 for n in proposals),
        "noteOrderPreserved":monotonic,
        "warning":"Detection of transient attacks does not establish the intended note's musical sound; unresolved notes are NOT considered corrected."
    }
    output={"summary":summary,"notes":proposals,"unresolved":unresolved,
            "wheelEnds":wheel_end_candidates}
    (ROOT/"fullband-note-timing-proposal.json").write_text(
        json.dumps(output,indent=2,ensure_ascii=False),encoding="utf-8")
    print("FULLBAND_NOTE_SUMMARY "+json.dumps(summary),flush=True)
    print("FULLBAND_WHEEL_ENDS "+json.dumps(wheel_end_candidates),flush=True)
    print("FULLBAND_NOTE_CHANGES "+json.dumps([
        [n["id"],n["newBeat"],n["shiftMs"],n["confidence"]]
        for n in proposals if abs(n["shiftMs"])>0.001]),flush=True)
    for a in range(0,len(unresolved),40):
        print("FULLBAND_NOTE_UNRESOLVED "+json.dumps(unresolved[a:a+40]),flush=True)

if __name__=="__main__":
    audit()
