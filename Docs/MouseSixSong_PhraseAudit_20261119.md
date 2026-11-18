# Six-song rhythm chart review and three-song phrase edit (2026-11-19)

This **review-only change** applies musical-phrase edits to the three newly generated MP3 charts. Original BOOTBEAT, Pop'n Dash and Full-Band Hit (including BOOTBEAT challenge) remain in main unchanged. All six songs were examined.

## Critical correction: clicks are not spatial judgements
The live `MouseModeChartPlayer.Judge` matches **input kind and timing**, not mouse cursor position. Positions determine visual reading and Pino dodging is handled separately. Prior cursor-distance stats were not a correct proof of gameplay difficulty. The on-screen body spacing test is only a visual safety check.

## Musical source and technique
- The three Suno MP3s were available locally. The low/mid/high spectral onset analysis was used as an aid to select actual accent times; **bands are not verified instrument labels**.
- The original onsets were selected using fixed numbers per 8-second block, obscuring musical dynamics. We reduced some low-salience hits in quieter phrases and added strong uncharted onset candidates at precisely recorded seconds in energetic phrases.
- Left/right actions now follow low/high spectral accents and each track has a distinct mid-band call/response. Wheel events remain untouched, avoiding forced scrolling notes.
- For added note heads, choose an unoccupied position (137px body clearance within 1.75s in 900×450), respecting central space in rain patterns.
- We have NOT claimed subjective playability or tested actual bullet trajectories. Tempo maps remain automatically estimated.

## New chart file changes
- **Cotton Pulse:** 229 → 227 notes, 9 quiet-transient cuts, 7 added sound-event accents, 37 timbre-guided click-hand changes, 8 bullet events, visual body overlap 0.
- **Oddbeat Shuffle:** 338 → 335 notes, 21 quiet-transient cuts, 18 added sound-event accents, 37 timbre-guided click-hand changes, 11 bullet events, visual body overlap 0.
- **瞬奏 -SHUNSOU-:** 409 → 398 notes, 21 quiet-transient cuts, 10 added sound-event accents, 61 timbre-guided click-hand changes, 12 bullet events, visual body overlap 0.

## Areas addressed
- **Cotton Pulse:** reduce weak notes in 0–8/48–55/112–120s, accent 80–112s.
- **Oddbeat Shuffle:** stop treating 0–16/40–56/88–96/112–120s as uniformly dense; insert percussion/bass accents near 56–64/72–88/97–112s. Turn-fan attack alternates with the existing barrage.
- **瞬奏 -SHUNSOU-:** retain high rhythmic figures at 32–48/104–112/120–136s while introducing space in the opening/soft phrases. Mid-band has a four-input response pattern in high-density sections.

## Existing-song audit
- **BOOTBEAT Lv.6 / Lv.11:** reuse known rhythm patterns but current difficulty separation depends on density and barrage events; assess music-phrase motifs and wheel tails by ear.
- **Pop'n Dash:** 12 named attack families give variety; rhythm hand placement and pattern synchronization still need verification with actual audio.
- **Full-Band Hit:** previous 818-note four-lane dataset became a 396-note mouse chart with 374 of 395 intervals around 1 beat; substantial ear-guided rhythmic restoration is needed.

## Required before main merge
Listen to the specific source event and validate attack alignment; confirm note/approach circle readability and Pino-hitbox clearance during the complete 1.75-second preview window; Unity compile/Play Mode not run. The revision is NOT a finished chart.
