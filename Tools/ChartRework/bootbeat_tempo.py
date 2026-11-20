"""Unity MouseModeChartTiming-equivalent musical tempo conversion for tooling."""

def beat_to_seconds(chart, beat):
    base_bpm = max(1.0, float(chart["bpm"]))
    zero = float(chart.get("beatZeroSeconds", 0))
    changes = sorted(chart.get("tempoChanges") or [], key=lambda c: float(c["beat"]))
    if beat <= 0 or not changes:
        return zero + float(beat) * 60.0 / base_bpm
    seconds = zero
    previous_beat = 0.0
    bpm = base_bpm
    for change in changes:
        at = float(change["beat"])
        new_bpm = float(change["bpm"])
        if at <= previous_beat or new_bpm <= 0:
            continue
        if at >= beat:
            break
        seconds += (at - previous_beat) * 60.0 / bpm
        previous_beat = at
        bpm = new_bpm
    return seconds + (float(beat) - previous_beat) * 60.0 / bpm

def seconds_to_beat(chart, seconds):
    bpm = max(1.0, float(chart["bpm"]))
    remaining = float(seconds) - float(chart.get("beatZeroSeconds", 0))
    changes = sorted(chart.get("tempoChanges") or [], key=lambda c: float(c["beat"]))
    if remaining <= 0 or not changes:
        return remaining * bpm / 60.0
    previous_beat = 0.0
    for change in changes:
        at = float(change["beat"])
        new_bpm = float(change["bpm"])
        if at <= previous_beat or new_bpm <= 0:
            continue
        segment_seconds = (at - previous_beat) * 60.0 / bpm
        if remaining <= segment_seconds:
            return previous_beat + remaining * bpm / 60.0
        remaining -= segment_seconds
        previous_beat = at
        bpm = new_bpm
    return previous_beat + remaining * bpm / 60.0
