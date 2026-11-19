#!/usr/bin/env python3
"""Analyze actual song dynamics and attack-onset candidates from checked-in audio.

This intentionally does NOT rewrite notes or claim onsets equal intended hit cues.
It provides the audio evidence needed to author musical rhythm and barrage sections.
Requires ffmpeg, available on ubuntu-latest. Python standard library only.
"""
import array
import json
import math
import re
import statistics
import subprocess
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
CHART_DIR=ROOT/"Assets/MouseMode/ChartSystem/Charts"
SONG_DIR=ROOT/"Assets/Song"
SAMPLE_RATE=8000
HOP=160

def guid_for(path):
    meta=Path(str(path)+".meta")
    if not meta.exists(): return None
    m=re.search(r"^guid:\s*([a-f0-9]{32})$",meta.read_text(encoding="utf-8-sig"),re.M)
    return m.group(1) if m else None

def audio_envelope(path):
    raw=subprocess.check_output([
        "ffmpeg","-nostdin","-hide_banner","-loglevel","error",
        "-i",str(path),"-ac","1","-ar",str(SAMPLE_RATE),
        "-f","s16le","-acodec","pcm_s16le","pipe:1"])
    data=array.array("h")
    data.frombytes(raw[:len(raw)//2*2])
    if sys.byteorder!="little": data.byteswap()
    rms=[];hf=[]
    for p in range(0,len(data)-HOP,HOP):
        chunk=data[p:p+HOP]
        rms.append(math.sqrt(sum(v*v for v in chunk)/len(chunk)))
        hf.append(sum(abs(chunk[i]-chunk[i-1]) for i in range(1,len(chunk))) / (len(chunk)-1))
    return rms,hf,len(data)/SAMPLE_RATE

def audio_onsets(rms,hf):
    # Adaptive local energy and high-frequency novelty: a low-cost diagnostic,
    # not a substitution for listening, and not directly a note generator.
    novelty=[]
    for i in range(len(rms)):
        a=max(0,i-16)
        base=sum(rms[a:i])/max(1,i-a)
        hb=sum(hf[a:i])/max(1,i-a)
        strength=max(0.,rms[i]-base)*.70 + max(0.,hf[i]-hb)*.30
        novelty.append(strength)
    positives=sorted(x for x in novelty if x>1)
    if not positives: return []
    threshold=positives[int(.73*(len(positives)-1))]
    candidates=[]
    for i in range(2,len(novelty)-2):
        score=novelty[i]
        if score<threshold or score<=novelty[i-1] or score<novelty[i+1]: continue
        # Strong local transient against background, ignoring very quiet noise.
        candidates.append((i*HOP/SAMPLE_RATE,round(score,2)))
    # Local nonmaximum suppression: avoid treating a single drum as 3 hits.
    selected=[]
    for time,score in sorted(candidates,key=lambda x:-x[1]):
        if all(abs(time-t)>.10 for t,_ in selected):
            selected.append((time,score))
    return sorted(selected)

def chart_info(path):
    text=path.read_text(encoding="utf-8-sig")
    tag=lambda key: re.search(r"^    "+key+r": ([\d.-]+)",text,re.M)
    bpm=float(tag("bpm").group(1))
    offset=float(tag("beatZeroSeconds").group(1))
    tempo_match=re.search(r"^    tempoChanges:(.*?)(?=^    approachSeconds:)",text,re.M|re.S)
    tempo_text=tempo_match.group(1) if tempo_match else ""
    changes=[(float(x),float(y)) for x,y in re.findall(
        r"^    - beat: ([\d.-]+)\n      bpm: ([\d.-]+)",tempo_text,re.M)]
    audio=re.search(r"^  song: \{[^}]+guid: ([a-f0-9]{32})",text,re.M)
    notes=[float(x) for x in re.findall(r"^      beat: ([\d.-]+)",text,re.M)]
    attacks=[float(x) for x in re.findall(r"^    - beat: ([\d.-]+)\n      patternId:",text,re.M)]
    return dict(bpm=bpm,offset=offset,changes=changes,guid=audio.group(1) if audio else "",notes=notes,attacks=attacks)

def beat_from_seconds(seconds,chart):
    prev=0.
    tempo=chart["bpm"]
    remaining=seconds-chart["offset"]
    for beat,nextTempo in chart["changes"]:
        duration=(beat-prev)*60./tempo
        if remaining<duration: return prev+remaining*tempo/60.
        remaining-=duration
        prev=beat
        tempo=nextTempo
    return prev+remaining*tempo/60.

def audit():
    audio={guid_for(p):p for p in SONG_DIR.iterdir() if p.suffix.lower() in {".wav",".mp3",".ogg"}}
    results={}
    cache={}
    for path in sorted(CHART_DIR.glob("*.asset")):
        info=chart_info(path)
        source=audio.get(info["guid"])
        if source is None: continue
        if source not in cache:
            cache[source]=(*audio_envelope(source),)
        rms,hf,duration=cache[source]
        onsets=audio_onsets(rms,hf)
        beatOnsets=[(round(beat_from_seconds(t,info),4),score)
                    for t,score in onsets if 0<=beat_from_seconds(t,info)<=max(info["notes"]+[0])+5]
        blocks=[]
        for start in range(0,int(max(info["notes"]+[0]))+16,16):
            minTime=0.
            # Select data using the actual tempo map, including variable BPM.
            relevant=[(i,v) for i,v in enumerate(rms)
                      if start<=beat_from_seconds(i*HOP/SAMPLE_RATE,info)<start+16]
            avg=round(sum(v for _,v in relevant)/max(1,len(relevant)),2)
            peak=round(max((v for _,v in relevant),default=0.),2)
            blocks.append(dict(beat=start,rms=avg,peak=peak,
                onsets=sum(start<=b<start+16 for b,_ in beatOnsets),
                notes=sum(start<=b<start+16 for b in info["notes"]),
                attacks=sum(start<=b<start+16 for b in info["attacks"])))
        result={
            "audio":source.name,"durationSeconds":round(duration,3),
            "bpm":info["bpm"],"offset":info["offset"],"tempoChanges":len(info["changes"]),
            "onsets":beatOnsets, "blocks":blocks,
            "totalNotes":len(info["notes"]),"totalAttacks":len(info["attacks"])
        }
        results[path.stem]=result
        print("CHART",path.stem,source.name,"duration",round(duration,1),
              "onsets",len(beatOnsets),"notes",len(info["notes"]),
              "attacks",len(info["attacks"]),flush=True)
    print("AUDIT_JSON_START",flush=True)
    print(json.dumps(results,ensure_ascii=False,separators=(",",":")),flush=True)
    print("AUDIT_JSON_END",flush=True)

if __name__=="__main__":
    audit()
