#!/usr/bin/env python3
"""Rebuild Full-Band Hit tap rhythm from original WAV per-band attack events.

This does not read the original note-head timings to select tap rhythms.
It retains the game's existing wheel holds, tempo map and bullet attacks.
Candidates are explicitly sound-derived, and printed for review before
applying to the playable Unity .asset. The script does not silently write
the game chart.
"""
from __future__ import annotations
import json
import math
from pathlib import Path
import numpy as np
from bootbeat_tempo import beat_to_seconds
from fullband_music_structure_audit import (
    ROOT, CHART, BANDS, load_song, band_onset_data, detect_candidates
)

OUT = ROOT/"Reports"/"FullBandHit_sound_driven_candidate.json"


def seconds_to_beat(chart, second):
    remaining = second-float(chart["beatZeroSeconds"])
    current = 0.0
    bpm = float(chart["bpm"])
    for change in chart["tempoChanges"]:
        next_b = float(change["beat"])
        seconds_here = (next_b-current)*60/bpm
        if remaining <= seconds_here:
            return current+remaining*bpm/60
        remaining -= seconds_here
        current = next_b
        bpm = float(change["bpm"])
    return current+remaining*bpm/60


def onset_complexes(peaks):
    # Merge 4 bands within 30ms, not arbitrary long percussion fills.
    groups = []
    for p in peaks:
        t = p["time"]
        if groups and t-groups[-1][-1]["time"] < .030:
            groups[-1].append(p)
        else:
            groups.append([p])
    result = []
    for group in groups:
        strengths = {name:0.0 for name,_,_ in BANDS}
        for p in group:
            strengths[p["band"]] = max(strengths[p["band"]],p["strength"])
        if len(group)>1:
            t=sum(p["time"]*p["strength"] for p in group)/sum(
                p["strength"] for p in group)
        else:
            t=group[0]["time"]
        result.append({"sec":round(t,5),
                       "band":strengths,
                       "score":round(
                           .95*strengths[BANDS[0][0]]+
                           1.10*strengths[BANDS[1][0]]+
                           .90*strengths[BANDS[2][0]]+
                           .72*strengths[BANDS[3][0]],5)})
    return result


def slot_complexes(chart, complexes):
    result = {}
    for event in complexes:
        beat = seconds_to_beat(chart,event["sec"])
        slot = round(beat*4)/4
        if slot<0 or slot>409:
            continue
        # A sound need not sit on a beat subdivision, but an automated chart
        # should not displace it to a remote grid position.
        delta=abs(float(beat_to_seconds(chart,slot))-event["sec"])
        if delta>.039:
            continue
        quality=event["score"]/(1.+(delta/.030)**2)
        if quality<.54:
            continue
        event=dict(event,slot=round(slot,4),
                   deviationMs=round(delta*1000,2),
                   adjustedScore=round(quality,4))
        old=result.get(slot)
        if old is None or quality>old["adjustedScore"]:
            result[slot]=event
    return result


def dominant_kind(bands, prev_kind, measure_position):
    # Equalized broad frequency regions: low/body leads left,
    # mid/high leads right, mixed attacks alternate for readable play.
    low=bands[BANDS[0][0]]+0.75*bands[BANDS[1][0]]
    high=0.85*bands[BANDS[2][0]]+0.90*bands[BANDS[3][0]]
    if low>high*1.12:
        return 0
    if high>low*1.12:
        return 1
    return 1-prev_kind


def make_taps(chart, events, samples, times, rms):
    global_rms=max(1e-6,float(np.percentile(rms,75)))
    old_wheels=[dict(n) for n in chart["notes"] if n["kind"]==2]
    wheel_ranges=[(float(beat_to_seconds(chart,w["beat"])),
                   float(beat_to_seconds(chart,w["endBeat"])))
                  for w in old_wheels]
    notes=[]
    bars=[]
    prev_kind=1
    last_hit=-9.0
    for start in range(0,408,4):
        start_time=float(beat_to_seconds(chart,start))
        end_time=float(beat_to_seconds(chart,start+4))
        volume=float(np.mean(rms[(times>=start_time)&(times<end_time)]))/global_rms
        slots=[(start+i*.25, events.get(start+i*.25)) for i in range(16)]
        slots=[(b,p) for b,p in slots if p is not None]
        ranked=sorted(slots,key=lambda q:q[1]["adjustedScore"],reverse=True)
        # 190 BPM: practical mouse click cadence from real drum/instrument
        # attacks, not a 1-beat metronome: 3..8 taps per four-beat bar.
        count=5 if volume>.62 else 3
        if volume>.95:
            count=6
        if volume>1.18:
            count=7
        if volume>1.45:
            count=8
        strong=[(b,p) for b,p in ranked if p["adjustedScore"]>.65]
        picked=strong[:count]
        # Keep rhythmic anchors from strong sound evidence, not from
        # a hard-coded note each whole beat.
        picked.sort(key=lambda q:q[1]["sec"])
        filtered=[]
        for b,p in picked:
            t=p["sec"]
            if t-last_hit<.104 or (
                filtered and t-filtered[-1][1]["sec"]<.104
            ):
                continue
            # Never sprinkle taps through a wheel hold without a deliberate
            # rhythm input design: it must remain readable with wheel scroll.
            if any(head-.10<=t<=tail+.12 for head,tail in wheel_ranges):
                continue
            filtered.append((b,p))
        for b,p in filtered:
            kind=dominant_kind(p["band"],prev_kind,b-start)
            prev_kind=kind
            notes.append({
                "kind":kind,
                "beat":round(seconds_to_beat(chart,p["sec"]),6),
                "atSeconds":p["sec"],
                "slot":b,
                "bandEvidence":p["band"],
                "score":p["adjustedScore"],
                "position":{"x":.5,"y":.5},
                "endBeat":round(seconds_to_beat(chart,p["sec"]),6)+1,
                "path":[]
            })
        if filtered:
            last_hit=filtered[-1][1]["sec"]
        bars.append({
            "startBeat":start,"volume":round(volume,3),
            "strongEventSlots":len(strong),"selected":len(filtered),
            "selectedSlots":[x[0] for x in filtered],
            "selectedTimes":[x[1]["sec"] for x in filtered],
        })

    # Remove accidental close duplicates to preserved wheel start/end times.
    notes.sort(key=lambda n:n["atSeconds"])
    placed=[]
    for note in notes:
        t=note["atSeconds"]
        if placed and t-placed[-1]["atSeconds"]<.104:
            continue
        placed.append(note)
    return placed, old_wheels, bars


def assign_positions(taps,wheels,chart):
    # A note head stays visible for ~2.2s before judgement. Its center must
    # not collide with any other head in that visibility window.
    # Scene uses 92px noteSize x 1.45 scale = 133.4px.
    # Grid spacing is at least 158.4px at the smaller tested 1500x720.
    # Avoid the UI-edge clamping that made our earlier custom dots collide.
    cells=[(x,y) for y in (.17,.39,.61,.83)
                 for x in (.12,.27,.42,.57,.72,.87)]
    # Keep full wheel path in the collision model, not only the first dot.
    wheel_locations=[]
    for wheel in wheels:
        head=float(beat_to_seconds(chart,wheel["beat"]))
        end=float(beat_to_seconds(chart,wheel["endBeat"]))
        points=[(float(p["x"]),float(p["y"])) for p in wheel["path"]]
        if not points:
            points=[(float(wheel["position"]["x"]),
                     float(wheel["position"]["y"]))]
        # Linear segment samples cover the moving wheel head.
        expanded=[]
        for i,p in enumerate(points):
            expanded.append(p)
            if i+1<len(points):
                q=points[i+1]
                for k in range(1,5):
                    v=k/5.
                    expanded.append((p[0]*(1-v)+q[0]*v,
                                     p[1]*(1-v)+q[1]*v))
        wheel_locations.append((head,end,expanded))
    history=[]
    choices=[]
    for index,tap in enumerate(taps):
        t=tap["atSeconds"]
        def cost(pos):
            x,y=pos
            penalty=0.
            for prior_t,prior_x,prior_y in history:
                dt=t-prior_t
                if dt>2.5 or dt<0:
                    continue
                dx=(x-prior_x)*1500
                dy=(y-prior_y)*720
                d=math.hypot(dx,dy)
                if d<155:
                    penalty+=20000*(1-d/155)*(.25+2.2/(dt+.1))
                penalty+=.025/(max(d,10))*1.0
            for head,end,points in wheel_locations:
                # Wheel spawned before its hit and visible after its tail.
                if not (head-2.4<=t<=end+2.4):
                    continue
                nearest=min(math.hypot((x-a)*1500,(y-b)*720)
                            for a,b in points)
                if nearest<155:
                    penalty+=12000*(1-nearest/155)
            if choices and pos==choices[-1]:
                penalty+=100
            return penalty
        p=min(cells,key=cost)
        tap["position"]={"x":p[0],"y":p[1]}
        choices.append(p)
        history.append((t,*p))
    return taps


def main():
    chart=json.loads(CHART.read_text(encoding="utf-8"))
    audio=load_song()
    times,novelty,rms=band_onset_data(audio)
    peaks=detect_candidates(times,novelty)
    complexes=onset_complexes(peaks)
    events=slot_complexes(chart,complexes)
    taps,wheels,bars=make_taps(chart,events,audio,times,rms)
    assign_positions(taps,wheels,chart)
    combined=[dict(x) for x in taps]
    for w in wheels:
        combined.append({"kind":w["kind"],"beat":w["beat"],
                         "position":w["position"],"endBeat":w["endBeat"],
                         "path":w["path"],"atSeconds":round(float(
                             beat_to_seconds(chart,w["beat"])),5),
                         "preservedWheel":True})
    combined.sort(key=lambda x:(x["atSeconds"],x["kind"]))
    for index,n in enumerate(combined,1):
        n["id"]=index
    for i in range(1,len(combined)):
        assert combined[i]["atSeconds"]>=combined[i-1]["atSeconds"]
    summary={
        "kindCounts":{str(kind):sum(n["kind"]==kind for n in combined)
                      for kind in (0,1,2)},
        "musicDuration":round(len(audio)/11025,2),
        "oldNotes":len(chart["notes"]),
        "newNotes":len(combined),
        "newTap":len(taps),"preservedWheel":len(wheels),
        "eventComplexes":len(complexes),"gridOnsetEvents":len(events),
        "distinctNoteDistancesSec":[
            round(combined[i]["atSeconds"]-combined[i-1]["atSeconds"],3)
            for i in range(1,min(len(combined),30))
        ],
        "bands": [b[0] for b in BANDS],
        "warning":"Source-WAV spectral attack based, NOT manually verified musical scoring."
    }
    OUT.parent.mkdir(exist_ok=True)
    OUT.write_text(json.dumps({
        "summary":summary,"bars":bars,"notes":combined,
        "sourceAudio":"Assets/Song/Full-Band Hit.wav",
        "tempoSource":"Assets/Editor/FullBandHit_MOUSE.json",
    },indent=2,ensure_ascii=False),encoding="utf-8")
    print("RECHART_SUMMARY "+json.dumps(summary),flush=True)
    for i in range(0,len(bars),20):
        print("RECHART_BARS "+json.dumps(bars[i:i+20],separators=(",",":")),
              flush=True)
    # Full chart is exported as workflow artifact; log each chunk so it can
    # be inspected and used without undocumented binary connector behavior.
    for i in range(0,len(combined),40):
        print("RECHART_NOTES "+json.dumps(combined[i:i+40],separators=(",",":")),
              flush=True)


if __name__=="__main__":
    main()
