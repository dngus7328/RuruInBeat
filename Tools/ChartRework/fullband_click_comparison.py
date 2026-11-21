#!/usr/bin/env python3
"""Build two A/B music + audible-note timing references for Full-Band Hit.

The 'before' version uses the exact previous single-BPM chart; the 'after'
version uses the in-branch tempo map. Both preserve existing note identities,
hand assignments and count. Listen for off-beats and bad accents before merge.
"""
from pathlib import Path
import json
import subprocess
import numpy as np
from bootbeat_tempo import beat_to_seconds

ROOT=Path(__file__).resolve().parents[2]
AUDIO=ROOT/"Assets/Song/Full-Band Hit.wav"
CHART=ROOT/"Assets/Editor/FullBandHit_MOUSE.json"
RATE=22050


def sound_at(mix,t,kind):
    begin=int(round(t*RATE))
    if begin<0 or begin>=len(mix):return
    count=min(int(RATE*.052),len(mix)-begin)
    if count<1:return
    sec=np.arange(count)/RATE
    freq={0:970.,1:1320.,2:720.,3:510.}.get(kind,800.)
    pulse=(np.sin(2*np.pi*freq*sec)*np.exp(-sec/.014)*.36).astype(np.float32)
    if kind==0:
        mix[begin:begin+count,0]+=pulse
        mix[begin:begin+count,1]+=pulse*.22
    elif kind==1:
        mix[begin:begin+count,0]+=pulse*.22
        mix[begin:begin+count,1]+=pulse
    else:
        mix[begin:begin+count,:]+=pulse[:,None]*.65


def render(audio,chart,out):
    mix=audio.copy()*0.64
    for note in chart["notes"]:
        kind=int(note["kind"])
        sound_at(mix,beat_to_seconds(chart,float(note["beat"])),kind)
        if kind==2:
            sound_at(mix,beat_to_seconds(chart,float(note["endBeat"])),3)
    peak=float(np.max(np.abs(mix)))
    if peak>.99:mix*=.99/peak
    pcm=np.rint(mix*32767).clip(-32768,32767).astype("<i2")
    subprocess.run(["ffmpeg","-nostdin","-hide_banner","-loglevel","error",
         "-y","-f","s16le","-ar",str(RATE),"-ac","2","-i","pipe:0",
         "-codec:a","libmp3lame","-qscale:a","3",str(out)],
         input=pcm.tobytes(),check=True)
    print("CLICK_COMPARE "+json.dumps({"file":out.name,
        "notes":len(chart["notes"]), "bpm":chart["bpm"],
        "tempoMarkers":len(chart.get("tempoChanges") or [])}),flush=True)


def main():
    revised=json.loads(CHART.read_text(encoding="utf-8"))
    original=dict(revised)
    original["bpm"]=190.0
    original["beatZeroSeconds"]=.045484
    original["tempoChanges"]=[]
    decoded=subprocess.check_output(["ffmpeg","-nostdin","-hide_banner",
        "-loglevel","error","-i",str(AUDIO),"-ac","2","-ar",str(RATE),
        "-f","s16le","-acodec","pcm_s16le","pipe:1"])
    audio=np.frombuffer(decoded,dtype="<i2").reshape(-1,2).astype(np.float32)/32768.
    render(audio,original,ROOT/"fullband-before-clicks.mp3")
    render(audio,revised,ROOT/"fullband-after-clicks.mp3")


if __name__=="__main__":main()
