# Mouse chart readability audit (2026-11-21)

This is **not a play-test result**. It is a read-only investigation of all 9 currently stored `.asset` files in `Assets/MouseMode/ChartSystem/Charts`, including hidden charts. It does not infer audio synchronization from the JSON timing map alone.

## How to run from Unity

1. Pull `main` using GitHub Desktop and open the Unity project.
2. In the Unity menu choose **Tools > Ruru In Beat > Audit All Mouse Charts** (outside Play Mode).
3. Open `Reports/MouseChartAuditSummary.csv` and `Reports/MouseChartAuditDetails.csv`. The second file reports note IDs and song seconds.
4. Re-run after editing any chart. All source assets, notes, attacks, and gameplay settings remain unchanged by this command.

The audit reads the serialized note size, visual scale, approach duration and judgement window from `Assets/Scene/Mouse.unity`. It then evaluates two explicitly labeled *screen-size scenarios*: 900x450 and 750x375. It matches runtime normalized-to-UI clamping and the two-pass Chaikin-smoothed wheel path. A note remains visible from its spawn until its hit or wheel end plus the GOOD judgement window. Pairs are sampled at 25 time points (24 intervals); they can still miss very brief transient collisions. The code also reports *quick same-area transitions* separately from simultaneous visible body overlaps.

**Important limitation:** Actual resolved RectTransform size can vary from these virtual sizes, and several song-specific scenes use different Inspector settings. These reports cannot prove actual frame rendering, human reading comfort, bullet collision, music timing, or how fun a chart is. They are used to prioritize manual recharting and Unity Play Mode verification, **not** to silently move authored notes or declare charts complete.

## Initial baseline (independent 13-point static approximation)

The preliminary model uses the `Mouse.unity` Inspector values: visual note diameter 133.4, approach 2.2 s, GOOD window 0.13 s, virtual 900x450 display. Counts may differ slightly from the Unity audit because its sampling is denser.

| Chart asset | Notes | Visible body-overlap candidate pairs | Attacks |
| --- | ---: | ---: | ---: |
| BOOTBEAT_MOUSE_LV6 | 198 | 0 | 35 |
| BOOTBEAT_EASY | 164 | 103 | 0 |
| BOOTBEAT_MOUSE_LV11 (hidden) | 271 | 106 | 18 |
| popn_dash | 200 | 108 | 22 |
| RhythmGameTheme_MOUSE | 227 | 160 | 14 |
| ArcadeGroove_MOUSE | 329 | 199 | 17 |
| FullBandHit_MOUSE | 394 | 296 | 15 |
| MabatakiNoSenritsu_MOUSE | 397 | 442 | 16 |
| NewMouseChart 2 (hidden) | 200 | 49 | 22 |

There are thousands of close future-note/earlier-note combinations outside BOOTBEAT. Counts describe visual overlap opportunities, *not automatically invalid music patterns*. In particular, intentionally rapid patterns and wheel rails need human review before repositioning.

## Next validation layers required before any 'complete' claim

- Unity **Play Mode**: inspect actual canvas bounds, each song's audio sync, approach-circle visibility, the wheel path and input timings.
- Bullet movement: simulate collision and escape routes under real position, movement speed, invulnerability and attack schedules; compare with human play.
- Rechart high-risk sections while preserving rhythm and intended phrases; do not just shrink sprites or randomize positions.
- After each chart change, rerun this audit and the existing EditMode tests.

No chart coordinate, music file, difficulty, attack schedule, inspector size, or note judgement window was changed in the audit commit.
