# Player-view choreography, 2026-11-25

Actual active six charts: 3b5aba776b936432b1376b6149598a3e563a2277. The original scene has 133.4 reference-unit notes, 2.2s lead, and approach start scale 3.3. Whole phrases re-laid with constrained beam search; preference for upper area, sustained spatial flow, shorter rapid gaze transfers, and reduced projectile/body intersections. Existing beat, note kind, wheel path, score, attack event and tempo values preserved.

All chart data rechecked at 1500×720 and 1792×859; BOOTBEAT additionally 750×375 with both rounded and raw paths. Exact data frame sampling every 0.1s plus overlapping visibility interval checks at ≤0.025s. No note body, rail/body or wheel end/body intersections in either PC reference geometry.

- BOOTBEAT: 1287 frames; 191 heads, 7 wheels; lower field 736 frames; rapid >450px transitions 1; max visible 5.
- Cotton Pulse: 1296 frames; 209 heads, 11 wheels; lower field 22 frames; rapid >450px transitions 0; max visible 6.
- Pop'n Dash: 1206 frames; 186 heads, 11 wheels; lower field 26 frames; rapid >450px transitions 0; max visible 8.
- Oddbeat Shuffle: 1350 frames; 307 heads, 14 wheels; lower field 153 frames; rapid >450px transitions 0; max visible 10.
- SHUNSOU: 1392 frames; 374 heads, 14 wheels; lower field 269 frames; rapid >450px transitions 1; max visible 11.
- Full-Band Hit: 1297 frames; 452 heads, 17 wheels; lower field 656 frames; rapid >450px transitions 0; max visible 13.

Separate browser visual comparison used 0.1s steps over all six songs, before and after, with reconstructed shared pellet movement plus legacy rain/laser/burst/aimed motions. Positions rounded from authored source by <=0.0005 normalized units; event timing model and simplified projectile/Pino sprites are not Unity captures. Key sequences visually inspected; not all frames individually inspected by a human. The browser's preview of a limited-lookahead synthetic Pino path is not human playtesting. No claim of measured subjective enjoyment, exact scene presentation, or complete human playthrough. Runtime changes improve future ring opacity, active wheel tail cue, and lower-field projectile foreground without duplicating sprites or changing input physics.
