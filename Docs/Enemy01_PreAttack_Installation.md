# Enemy01 early-barrage visual

## Implementation
- The 12-frame 4x3 transparent PNG supplied by the user is used without redrawing.
- Pino-like idle motion: 6FPS, ping-pong frames.
- The enemy appears before the first actual warning or projectile in each chart attack. It stays visible for its attack group and disappears between bouts.
- A restrained animation/recoil cues impending attacks; existing projectiles, chart beats, damage, Pino hitboxes and song timing are unchanged.
- All mouse scenes use MouseModeBulletDirector. It creates Enemy01 automatically so no per-scene component is required.
- No gameplay enemy HP, collisions or boss mechanics added; it is a **visual warning enemy** only.

## Required PNG (binary image supplied separately)
Place the **original** uploaded sprite sheet at:
`Assets/Resources/MouseEnemies/Enemy01.png`

The 2048×1536 image has 4 columns and 3 rows of 512×512 cells. Runtime slices it automatically in top-left to bottom-right order. Use the attached installation ZIP if the PNG is not in the GitHub branch; no Sprite Editor slicing is needed.

## Validation
1. Pull updated main and copy the PNG to the path above. Unity should import as Texture2D; the script calls Resources.Load<Texture2D>().
2. Open any `Mouse_*.unity` scene and Play. During active attack sequences watch `BulletLayer/Enemy01_AnimatedPreAttack` appear before existing barrage warnings.
3. Confirm the 12-frame animation, warning-before-first-projectile, stage pause/resume, multi-event attacks, and visual layering behind note symbols.
4. If the console says `[Enemy01] Missing sprite sheet`, verify file spelling and Resources folder.
5. Test actual play: no Unity runtime compile or collision verification performed outside the user's editor.

There are no new chart events. Existing notes and attacks stay intact.
