#!/usr/bin/env python3
"""Temporarily install the sound-driven chart for CI validation.

Run after fullband_music_driven_rechart.py. The checkout is ephemeral; this
script itself does not push to GitHub. To ship a chart, review it and make
matching .json/.asset changes in the authoritative repository.
"""
from __future__ import annotations
import json
from pathlib import Path
from bootbeat_tempo import beat_to_seconds
from fullband_music_structure_audit import ROOT

CANDIDATE=ROOT/"Reports/FullBandHit_sound_driven_candidate.json"
JSON=ROOT/"Assets/Editor/FullBandHit_MOUSE.json"
ASSET=ROOT/"Assets/MouseMode/ChartSystem/Charts/FullBandHit_MOUSE.asset"

def number(value):
    s=("%0.6f"%float(value)).rstrip("0").rstrip(".")
    return s if s else "0"

def clean(note):
    return {
        "id":int(note["id"]),
        "kind":int(note["kind"]),
        "beat":round(float(note["beat"]),6),
        "position": {
            "x":float(note["position"]["x"]),
            "y":float(note["position"]["y"]),
        },
        "endBeat":round(float(note["endBeat"]),6),
        "path":[{"x":float(v["x"]),"y":float(v["y"])}
                for v in note.get("path",[])],
    }

def note_yaml(note):
    out=[
        "    - id: "+str(note["id"]),
        "      kind: "+str(note["kind"]),
        "      beat: "+number(note["beat"]),
        "      position: {x: "+number(note["position"]["x"])+
           ", y: "+number(note["position"]["y"])+"}",
        "      endBeat: "+number(note["endBeat"])
    ]
    path=note["path"]
    if not path:
        out.append("      path: []")
    else:
        out.append("      path: ")
        for point in path:
            out.append("      - {x: "+number(point["x"])+
                       ", y: "+number(point["y"])+"}")
    return "\n".join(out)+"\n"

def main():
    result=json.loads(CANDIDATE.read_text(encoding="utf-8"))
    old=json.loads(JSON.read_text(encoding="utf-8"))
    notes=[clean(n) for n in result["notes"]]
    assert len(notes)==result["summary"]["newNotes"],"note count"
    assert len(notes)>=400 and len(notes)<=700, "unexpected chart size"
    assert sum(n["kind"]==2 for n in notes)==11, "wheel holds changed"
    assert len({n["id"] for n in notes})==len(notes), "duplicate note IDs"
    times=[]
    for n in notes:
        at=float(beat_to_seconds(old,n["beat"]))
        assert at>=.0, ("early note",n["id"])
        assert at<=129.0, ("late note",n["id"],at)
        if n["kind"]!=2:
            source=result["notes"][n["id"]-1]["atSeconds"]
            assert abs(at-source)<.002, ("audio mismatch",n["id"],at,source)
        else:
            assert n["endBeat"]>n["beat"]
            assert len(n["path"])>=2
        times.append(at)
    assert all(right>=left for left,right in zip(times,times[1:])), "notes not sorted"
    new=dict(old,notes=notes)
    old_asset=ASSET.read_text(encoding="utf-8")
    header="  notes:\n"
    trailer="  attacks: "
    i=old_asset.index(header)
    j=old_asset.index(trailer,i+len(header))
    serialized=header+"".join(note_yaml(n) for n in notes)
    updated=old_asset[:i]+serialized+old_asset[j:]
    JSON.write_text(json.dumps(new,ensure_ascii=False,indent=2)+"\n",
                    encoding="utf-8")
    ASSET.write_text(updated,encoding="utf-8")
    print("RECHART_INSTALL "+json.dumps({
        "oldNotes":len(old["notes"]),"newNotes":len(notes),
        "wheelHolds":sum(n["kind"]==2 for n in notes),
        "firstTimeSec":round(times[0],5),
        "lastTimeSec":round(times[-1],5),
        "noteJsonMatchesYaml":updated.count("    - id: ")==len(notes)
    }),flush=True)

if __name__=="__main__":
    main()
