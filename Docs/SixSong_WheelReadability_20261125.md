# 2026-11-25 wheel phrase and visual readability pass

Source: six user-uploaded audio files. Additional wheel candidates selected from sustained harmonic energy, spectral continuity and onsets at both ends; not claimed to be human-verified instrument transcription. No additional difficulty variants. Original tempo maps, attacks and audio remain unchanged. Existing tap timings outside converted sustained phrases stay unchanged. Wheel heads and tails are independently judged; intermediate scrolling is still governed by the runtime. The conversion deliberately trades some taps for sustained gestures, not simply adds filler.

- BOOTBEAT: heads 197 → 191, wheels 4 → 7, total wheel seconds 3.9 → 7.7; body overlap 0 → 0; END overlap 1 → 0; rail/body candidates 0 → 0; notes intruding into lower-half dodge area 130 → 44; rapid eye jumps >450px 93 → 38; added head/tail intervals (centiseconds): 7163–7296, 10360–10493, 12235–12345.
- Cotton Pulse: heads 233 → 209, wheels 2 → 11, total wheel seconds 2.0 → 14.1; body overlap 0 → 0; END overlap 1 → 0; rail/body candidates 2 → 0; notes intruding into lower-half dodge area 142 → 22; rapid eye jumps >450px 56 → 2; added head/tail intervals (centiseconds): 818–944, 2327–2517, 3502–3628, 4612–4717, 5678–5804, 7380–7514, 9679–9792, 11193–11379, 12372–12475.
- Pop'n Dash: heads 207 → 186, wheels 4 → 11, total wheel seconds 3.3 → 14.7; body overlap 0 → 0; END overlap 0 → 0; rail/body candidates 1 → 0; notes intruding into lower-half dodge area 134 → 22; rapid eye jumps >450px 46 → 2; added head/tail intervals (centiseconds): 1779–1993, 2848–2999, 4458–4609, 5992–6143, 7326–7477, 8332–8483, 10093–10269.
- Oddbeat Shuffle: heads 339 → 307, wheels 3 → 14, total wheel seconds 5.2 → 19.2; body overlap 0 → 0; END overlap 1 → 0; rail/body candidates 2 → 0; notes intruding into lower-half dodge area 219 → 26; rapid eye jumps >450px 125 → 10; added head/tail intervals (centiseconds): 862–983, 2060–2208, 3206–3310, 4057–4229, 4923–5095, 6670–6774, 7621–7743, 8815–8935, 10766–10869, 11783–11903, 12730–12850.
- SHUNSOU: heads 413 → 374, wheels 2 → 14, total wheel seconds 2.2 → 17.7; body overlap 0 → 0; END overlap 1 → 0; rail/body candidates 3 → 0; notes intruding into lower-half dodge area 251 → 57; rapid eye jumps >450px 104 → 4; added head/tail intervals (centiseconds): 1089–1201, 2023–2171, 3295–3423, 4586–4697, 5588–5713, 6569–6693, 8046–8185, 9084–9286, 10288–10412, 11090–11195, 12013–12122, 12813–12936.
- Full-Band Hit: heads 479 → 452, wheels 11 → 17, total wheel seconds 7.9 → 14.5; body overlap 0 → 0; END overlap 0 → 0; rail/body candidates 0 → 0; notes intruding into lower-half dodge area 239 → 83; rapid eye jumps >450px 369 → 16; added head/tail intervals (centiseconds): 2101–2205, 4031–4159, 6539–6634, 9093–9218, 10333–10442, 12435–12531.

Geometry uses runtime 133.4 px diameter, 2.2 s lead, 0.13 s late window, clamp padding, twice-rounded wheel paths, and 1500×720 and 1792×859 stages. BOOTBEAT additionally checks compact 750×375, both smoothed and raw wheel paths. END and rail occlusion are tested separately; lower-half count includes radius. These are geometry models, not measured Unity rendering or actual player avoidance. User-configured Inspector size, approach scale and lead are unchanged.

## Cross-game pattern vocabulary

Design references: osu! visual flow and rhythmic spacing, Muse Dash two-button patterns, Rhythm Doctor recurring and syncopated patterns. Existing hit/end beats, positions and wheel paths are preserved in this input-pattern pass. No new note types or input mechanics.

### BOOTBEAT
- 16.16–19.10 s: `LRLLRR`
- 21.81–23.61 s: `LLRR`
- 30.61–31.62 s: `RLR`
- 37.14–39.84 s: `LRLRRL`
- 73.63–76.53 s: `LRLLRR`
- 88.09–89.87 s: `LLRR`
- 105.59–108.24 s: `LRRLRR`
- 118.17–119.93 s: `LRLR`

### Cotton Pulse
- 19.93–22.86 s: `LLRRLRRL`
- 36.69–38.37 s: `RRLRL`
- 42.57–45.71 s: `LLRRLLRLR`
- 61.38–63.47 s: `LRRLLR`
- 88.48–91.37 s: `RLLRLRRL`
- 120.40–123.30 s: `LRRLLRRL`

### Pop'n Dash
- 24.20–27.47 s: `LLRLRRLL`
- 40.05–40.56 s: `RLR`
- 48.35–52.13 s: `LRRLLRRLR`
- 68.48–70.74 s: `LRLRRL`
- 75.27–76.02 s: `LRRL`
- 85.58–87.85 s: `LRLRLLRR`
- 98.41–100.42 s: `RLRLLRR`
- 115.27–117.03 s: `LRRLRL`

### Oddbeat Shuffle
- 24.08–25.29 s: `RLRRL`
- 25.82–27.03 s: `LRLRR`
- 37.79–39.87 s: `LLRRLRL`
- 56.66–58.05 s: `LRRLRL`
- 72.24–73.45 s: `RLRLRLR`
- 80.02–82.09 s: `LLRRLRRL`
- 85.39–87.81 s: `RLRLLRLRLRL`
- 96.44–99.03 s: `LLRRLRLRL`
- 122.48–124.71 s: `LRRLRRLL`

### SHUNSOU
- 9.13–10.41 s: `LRLRRLR`
- 24.09–25.69 s: `RLRLLRLR`
- 27.42–28.38 s: `LRLRRL`
- 43.03–43.82 s: `LRLRL`
- 57.59–59.31 s: `LLRLRLRL`
- 67.55–68.96 s: `RLLRLRL`
- 88.05–90.54 s: `RLRLRLRLLR`
- 104.59–106.28 s: `LRLRRLRL`
- 110.13–110.59 s: `LRLR`
- 121.51–122.72 s: `LRRLRLR`

### Full-Band Hit
- 8.66–8.99 s: `RLR`
- 10.93–11.48 s: `LRLR`
- 19.09–19.56 s: `RLRL`
- 28.99–29.79 s: `LRLRL`
- 33.93–35.21 s: `LLRRLR`
- 48.11–48.91 s: `LRLRLR`
- 57.33–57.95 s: `RLRLR`
- 72.97–74.55 s: `LRLRLLRRLR`
- 80.22–80.86 s: `RLRLR`
- 88.73–90.61 s: `RRLRLRLRLR`
- 94.39–95.63 s: `LLRRLRLRL`
- 97.53–98.31 s: `LRLRL`
- 100.35–100.97 s: `RLRLR`
- 112.89–113.52 s: `LRLRL`
- 115.24–116.03 s: `LRLRLR`
- 121.66–122.61 s: `RLRLRLR`

57 explicitly selected phrases, 138 button changes. Previous geometry verification is preserved because positions and times were not altered in this pass. Unity rendering: within-body contrast badge, dim distant rings and future wheel rails, restore full opacity on wheel start. No Inspector size/lead changes. Compile and full Play Mode tests not run.

## Full-song visual choreography

- BOOTBEAT: 2 visual phrase shapes (73.63s wave, 105.59s zigzag), 3 curved wheel paths. Body/end/rail clashes: 0/0/0 at 1500×720, 0/0/0 at 1792×859. Lower field count 44 → 44, rapid >450px transitions 38 → 38.
- Cotton Pulse: 7 visual phrase shapes (19.93s zigzag, 36.69s stair, 42.57s wave, 88.48s stair, 90.15s stair, 120.40s stair, 122.06s stair), 5 curved wheel paths. Body/end/rail clashes: 0/0/0 at 1500×720, 0/0/0 at 1792×859. Lower field count 22 → 22, rapid >450px transitions 2 → 1.
- Pop'n Dash: 8 visual phrase shapes (24.20s stair, 25.97s stair, 40.05s wave, 50.87s arc, 68.48s wave, 75.27s zigzag, 86.84s wave, 115.27s return), 2 curved wheel paths. Body/end/rail clashes: 0/0/0 at 1500×720, 0/0/0 at 1792×859. Lower field count 22 → 22, rapid >450px transitions 2 → 1.
- Oddbeat Shuffle: 1 visual phrase shapes (96.44s zigzag), 3 curved wheel paths. Body/end/rail clashes: 0/0/0 at 1500×720, 0/0/0 at 1792×859. Lower field count 26 → 25, rapid >450px transitions 10 → 10.
- SHUNSOU: 7 visual phrase shapes (9.13s return, 24.09s stair, 25.05s stair, 88.05s stair, 104.59s return, 105.51s stair, 110.13s return), 3 curved wheel paths. Body/end/rail clashes: 0/0/0 at 1500×720, 0/0/0 at 1792×859. Lower field count 57 → 54, rapid >450px transitions 4 → 4.
- Full-Band Hit: 3 visual phrase shapes (8.66s stair, 10.93s zigzag, 112.89s wave), 2 curved wheel paths. Body/end/rail clashes: 0/0/0 at 1500×720, 0/0/0 at 1792×859. Lower field count 83 → 83, rapid >450px transitions 16 → 16.

Full-length HTML review: Tools/ChartRework/full_song_visual_review.html, actual authored positions and times, original audio can be selected locally. It visualizes attack cue times only, not Unity projectile simulation. All beat maps, attacks, input types and wheel start/end timings unchanged in this pass. Geometry verification is not subjective human playtesting.
