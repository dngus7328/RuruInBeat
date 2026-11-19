# Animated Enemy01: multi-spawn / floating / departure

- Enemy01 (user supplied 4×3 sheet) remains unchanged at `Assets/Resources/MouseEnemies/Enemy01.png`.
- Each chart **attack event** creates an independent warning cue; concurrent attacks show **separate enemies** using automatically pooled UI images. Reuse only occurs after the previous cue disappears.
- Spawn: before the earliest actual warning / first shot (currently ~0.95 s earlier than warning).
- During barrage: 6 FPS ping-pong animation, gentle 10px vertical bob, slight side sway.
- Size: up to 190 UI units (~189 on a 450 unit stage), compared with old 115.
- Departure: the **last projectile's emission**, not the disappearance of the projectile from the screen, plus 0.4 seconds. Lasers remain active until the laser actually finishes firing.
- Removed small red `WaveEmitter_A`/`WaveEmitter_B` placeholders and unused `AttackEmitter_Fallback` source graphics.
- Kept `OpenCorridor_Left/Right` indicators so existing dense barrage safety guidance remains visible.
- Did not change notes, chart attacks, bullet physics, collision, HP, or gameplay input.
- This is a runtime-script change only. All Mouse*.unity scenes using the director receive it automatically.

## Validation needed in Unity
1. Start any Mouse / Mouse_*.unity scene after Pull origin.
2. Play a barrage with one event and verify warning -> floating enemy -> last shot released -> enemy fades out.
3. During two overlapping attack events verify two Enemy01 objects are visible without covering each other.
4. Check pooling, rewind, pause, failed song, edge of play area, high and low resolution.
5. No Unity Play Mode run has occurred in this environment.
