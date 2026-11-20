#!/usr/bin/env python3
"""Analyze ONLY BOOTBEAT.wav for manually authored music-synced chart rework.

Prints section energy, onset strengths, and beat-grid correlations in text logs.
Does not auto-edit any chart. One pass over mono decoded music.
"""
import json
import math
import subprocess
import wave
from pathlib import Path

import numpy as np

ROOT=Path(__file__).resolve().parents[2]
AUDIO=ROOT/"Assets/Song/BOOTBEAT.wav"
BPM=134.27
RATE=11025
HOP=256
FFT=1024

def analyze():
    raw=subprocess.check_output(["ffmpeg","-nostdin","-hide_banner","-loglevel","error",
        "-i",str(AUDIO),"-ac","1","-ar",str(RATE),"-acodec","pcm_s16le","-f","s16le","pipe:1"])
    samples=np.frombuffer(raw,dtype="<i2").astype(np.float32)/32768.
    duration=len(samples)/RATE
    n=(len(samples)-FFT)//HOP+1
    if n<100: raise ValueError("audio too short")
    # Explicit frame iteration is bounded and avoids giant temporary matrices.
    window=np.hanning(FFT).astype(np.float32)
    spectra=np.empty((n,FFT//2+1),dtype=np.float32)
    rms=np.empty(n,dtype=np.float32)
    for i in range(n):
        frame=samples[i*HOP:i*HOP+FFT]
        rms[i]=np.sqrt(np.mean(frame*frame))
        spectra[i]=np.abs(np.fft.rfft(frame*window))
    frequencies=np.fft.rfftfreq(FFT,1./RATE)
    low=(frequencies>=45)&(frequencies<240)
    high=(frequencies>=1500)&(frequencies<5000)
    mid=(frequencies>=250)&(frequencies<1500)
    bass=spectra[:,low].sum(axis=1)
    treble=spectra[:,high].sum(axis=1)
    middle=spectra[:,mid].sum(axis=1)
    # Whiten positive changes to reduce false onsets on steady synth notes.
    flux=np.maximum(0,spectra[1:]-spectra[:-1]).sum(axis=1)
    flux=np.r_[0.,flux]
    baseline=np.convolve(flux,np.ones(15)/15.,mode="same")
    strength=np.maximum(0.,flux-baseline*.65)
    floor=np.percentile(strength,60)
    threshold=np.percentile(strength,83)
    candidates=[]
    for i in range(3,n-3):
        if strength[i]<max(floor*1.4,threshold):continue
        if strength[i]<max(strength[i-3:i+4]):continue
        t=(i*HOP+FFT*.5)/RATE
        candidates.append((t,float(strength[i])))
    strongest=[]
    for t,score in sorted(candidates,key=lambda x:-x[1]):
        if all(abs(t-u)>.075 for u,_ in strongest):strongest.append((t,score))
    strongest.sort()
    # Summarize timing in 16-beat phrases and 1/8, 1/16 musical subpositions.
    secperbeat=60./BPM
    maxBeat=duration/secperbeat
    out=[]
    for start in np.arange(0,maxBeat,16.):
        a=max(0,int(start*secperbeat*RATE/HOP))
        b=min(n,int((start+16)*secperbeat*RATE/HOP))
        times=[(round(t/secperbeat,3),round(score,3))
               for t,score in strongest if start<=t/secperbeat<start+16]
        out.append({"startBeat":int(start),"startSeconds":round(start*secperbeat,2),
                    "meanRMS":round(float(np.mean(rms[a:b])),4),
                    "maxRMS":round(float(np.max(rms[a:b])),4),
                    "meanBass":round(float(np.mean(bass[a:b])),2),
                    "meanMid":round(float(np.mean(middle[a:b])),2),
                    "meanHigh":round(float(np.mean(treble[a:b])),2),
                    "onsetCount":len(times),
                    "onsets":times})
    # Measure beat phase of strong percussive transitions for actual audio.
    phaseCandidates=[]
    onsets=np.array([t/secperbeat for t,_ in strongest])
    strengths=np.array([score for _,score in strongest])
    for subdivision in (1.,.5,.25):
        for phase in np.arange(0,subdivision,.025):
            dist=np.abs((onsets-phase+subdivision*.5)%subdivision-subdivision*.5)
            closeness=np.exp(-((dist/.055)**2))
            phaseCandidates.append((float(np.sum(strengths*closeness)),subdivision,float(phase)))
    phaseCandidates.sort(reverse=True)
    report={"song":"BOOTBEAT.wav","durationSec":round(duration,3),"bpm":BPM,
        "beatCount":round(maxBeat,2),"totalOnsets":len(strongest),
        "gridPhaseCandidates":[{"strength":round(a,2),"gridBeats":b,"phaseBeats":round(c,3)}
                            for a,b,c in phaseCandidates[:12]],"sections":out}
    # Approximate frequency-band attacks; instruments overlap in frequency.
    # These are evidence channels, NOT isolated kick/snare/hi-hat stems.
    band_definitions={
        "low_40_220hz":(40,220),
        "mid_220_1800hz":(220,1800),
        "high_1800_5500hz":(1800,5500),
    }
    band_strengths={}
    band_events={}
    for band_name,(lo,hi) in band_definitions.items():
        mask=(frequencies>=lo)&(frequencies<hi)
        positive=np.maximum(0,spectra[1:,mask]-spectra[:-1,mask]).sum(axis=1)
        positive=np.r_[0.,positive]
        smooth=np.convolve(positive,np.ones(15)/15.,mode="same")
        novelty=np.maximum(0,positive-smooth*0.65)
        band_strengths[band_name]=novelty
        min_peak=max(float(np.percentile(novelty,88)),float(np.percentile(novelty,60))*1.35)
        peaks=[]
        for idx in range(3,len(novelty)-3):
            if novelty[idx] < min_peak or novelty[idx] < np.max(novelty[idx-3:idx+4]):
                continue
            t=(idx*HOP+FFT*0.5)/RATE
            peaks.append((t,float(novelty[idx])))
        selected=[]
        for t,score in sorted(peaks,key=lambda x:-x[1]):
            if all(abs(t-other_t)>.080 for other_t,_ in selected):
                selected.append((t,score))
        band_events[band_name]=sorted(selected)
    # Evidence-based diagnostic, not a claim that spectral peaks equal playable beats.
    # Read the exact chart used by the editor; compare scheduled hits against audio onsets.
    chart_path=ROOT/"Assets/Editor/BOOTBEAT_MOUSE_LV6.json"
    with chart_path.open(encoding="utf-8") as f:
        chart=json.load(f)
    chart_bpm=float(chart["bpm"])
    chart_zero=float(chart.get("beatZeroSeconds",0))
    hit_times=[(int(note["id"]),chart_zero+float(note["beat"])*60/chart_bpm)
               for note in chart["notes"]]
    onset_times=np.array([t for t,_ in strongest],dtype=float)
    diagnostic=[]
    for note_id,hit in hit_times:
        if len(onset_times):
            nearest_idx=int(np.argmin(np.abs(onset_times-hit)))
            nearest=float(onset_times[nearest_idx])
            distance=round(abs(hit-nearest)*1000,1)
        else:
            nearest=None
            distance=None
        diagnostic.append({"noteId":note_id,"hitSec":round(hit,4),
                           "nearestDetectedOnsetSec":None if nearest is None else round(nearest,4),
                           "distanceMs":distance})
    distances=[x["distanceMs"] for x in diagnostic if x["distanceMs"] is not None]
    report["chartAudioDiagnostic"]={
        "noteCount":len(hit_times),"chartBeatZeroSec":chart_zero,
        "approachSec":float(chart.get("approachSeconds",0)),
        "firstHitSec":round(hit_times[0][1],4) if hit_times else None,
        "negativeSpawnCount":sum(1 for _,t in hit_times if t<float(chart.get("approachSeconds",0))),
        "nearestOnsetMedianDistanceMs":round(float(np.median(distances)),2) if distances else None,
        "nearestOnsetP90DistanceMs":round(float(np.percentile(distances,90)),2) if distances else None,
        "noteOnsetDistances":diagnostic,
        "warning":"Onset proximity is a diagnostic, NOT proof of a correct musical chart or Unity runtime sync."
    }
    band_analysis={}
    for name,events in band_events.items():
        times=np.asarray([t for t,_ in events],dtype=float)
        closest=[]
        tagged=[]
        for note_id,hit in hit_times:
            if len(times):
                idx=int(np.argmin(np.abs(times-hit)))
                gap=round(float(abs(times[idx]-hit)*1000),1)
                nearest=round(float(times[idx]),4)
            else:
                gap=None
                nearest=None
            if gap is not None:
                closest.append(gap)
            tagged.append({"noteId":note_id,"distanceMs":gap,"nearestSec":nearest})
        band_analysis[name]={
            "eventCount":len(events),
            "noteWithin45ms":sum(g<=45 for g in closest),
            "noteWithin90ms":sum(g<=90 for g in closest),
            "medianNoteNearestEventMs":round(float(np.median(closest)),1) if closest else None,
            "noteDistances":tagged,
            "first40Events":[{"timeSec":round(t,4),"strength":round(s,2)}
                             for t,s in events[:40]],
            "allBandEventTimesSec":[round(t,4) for t,_ in events]
        }
    beat_duration=60./BPM
    grid_diagnostics={}
    for unit in (1.,0.5,0.25):
        distances=[abs((hit/beat_duration + unit/2)%unit-unit/2)*beat_duration*1000
                   for _,hit in hit_times]
        grid_diagnostics[str(unit)+"beat"]={
            "medianDistanceMs":round(float(np.median(distances)),1),
            "notesOver30Ms":sum(d>30 for d in distances)
        }
    report["instrumentBandProxyAnalysis"]=band_analysis
    report["beatGridDiagnostic"]={
        "gridOriginSec":0,"bpmAssumed":BPM,
        "stats":grid_diagnostics,
        "warning":"Frequency bands overlap instruments; beat phase and meter require listening/verification."
    }
    print("AUDIO_PROFILE_BEGIN",flush=True)
    print(json.dumps(report,ensure_ascii=False,separators=(",",":")),flush=True)
    print("AUDIO_PROFILE_END",flush=True)

if __name__=="__main__":
    analyze()
