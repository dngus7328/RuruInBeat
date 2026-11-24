# Six-song mouse chart rhythm revision — 2026-11-25

Source: user-uploaded ZIP of the six original song files, compared with the active single-chart catalog on main at 815e86d25cd57c378ee0d6cd27ecec576005e9d1. All six tracks were decoded at 22,050 Hz; 4-band spectral flux, local attack strength, 8-second RMS, and harmonic/percussive separation were used as supporting evidence. A detected peak is not a verified instrument or a human-audited musical phrase. No new difficulty variants, UI, player rules, audio files, attack events, levels, or tempo maps were changed.

| Song | Heads before → after | Wheel phrases before → after | Added taps | Removed heads | Shifted heads | Button edits | Attack events |
|---|---:|---:|---:|---:|---:|---:|---:|
| BOOTBEAT | 198 → 197 | 4 → 4 | 0 | 1 | 12 | 15 | 20 |
| Cotton Pulse | 226 → 233 | 1 → 2 | 9 | 2 | 0 | 57 | 14 |
| Pop'n Dash | 200 → 207 | 4 → 4 | 7 | 0 | 2 | 1 | 19 |
| Oddbeat Shuffle | 328 → 339 | 0 → 3 | 18 | 7 | 0 | 63 | 17 |
| 瞬奏 -SHUNSOU- | 396 → 413 | 0 → 2 | 21 | 4 | 0 | 63 | 16 |
| Full-Band Hit | 468 → 479 | 11 → 11 | 11 | 0 | 0 | 37 | 15 |

- BOOTBEAT: retain beginner quarter-beat grid, four wheel phrases, phrase counts and no overlapping inputs; move selected weak accents to nearby stronger quarter-grid events and end at the last prominent cadence, before the reverb tail.
- Cotton Pulse: break long one-button runs into paired responses, add selectively supported subdivisions and a wheel phrase in a sustained recurring passage.
- Pop'n Dash: preserve bouncy rhythmic skeleton; adjust the two opening cues toward their detected attacks, add syncopations only when the subdivision and physical spacing agree.
- Oddbeat Shuffle: replace long same-button streaks with recurring responses; add selected off-beat accents and wheel phrases, including the sustained final chord instead of unsupported tail taps.
- 瞬奏: contrast quick alternating bursts with sustained wheel phrases; keep audio-derived timings of existing notes and use strong missing subdivisions in selected passages.
- Full-Band Hit: preserve eleven existing wheels and the sound-driven rhythm map; improve repeated-button runs and restore missing rhythmic accents, without wholesale density increases.

Static checks performed prior to commit: matching source time checksums; unique IDs, nonnegative and in-duration note times, valid wheel paths; exact JSON/Unity note field parity within float serialization tolerance; no overlapping note bodies at 1500×720 or 1792×859 with 133.4px note diameter and 2.2s lead, including sampled moving wheel heads. BOOTBEAT also checked existing phrase/count/grid/recovery constraints and the legacy compact-window collision model against baseline. Candidates that violate spacing or attack-boundary guards were rejected. Attack lists preserved byte-for-byte in the Unity assets and structurally in JSON.

Remaining validation: Unity Editor/Play Mode, actual cursor avoidance with bullet trajectories, hardware audio/input latency, and subjective musical playability were not measured in this environment. This revision is not a claim of commercial-grade certification or an ear-verified transcription. Preserve existing source commits for comparison.

Rejected candidates: BOOTBEAT: grid 94, grid 117, grid 151, remove 197; Cotton Pulse: none; Pop'n Dash: none; Oddbeat Shuffle: none; 瞬奏 -SHUNSOU-: fill 2869; Full-Band Hit: none.
