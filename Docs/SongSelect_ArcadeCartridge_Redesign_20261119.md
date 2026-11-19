# Song Select Arcade – actual redesign (2026-11-19)

## What changed
- The **actual serialized Unity scene** `Assets/Scene/SongSelectArcadeScene.unity` has been rearranged; this is not just a label swap.
- Left shelf: six staggered and tilted cartridge spines with cut-corner geometry, raised edges, printed label pockets and insertion pins. Every row is still a working Button and the chart catalog still sets its content.
- Right: an oversized irregular package shell, distinctly separate jacket/image layer, raised label bar, information dashboard and active difficulty/preview buttons.
- Rear background and bottom controls use matte lilac, dusty pink and plum; no glow, chrome, premade frames or new texture assets.
- The original `SongSelectScene.unity` remains unchanged.

## Motion
- `SongSelectArcadeMotion` is attached to the Arcade scene Canvas.
- On entry, six cartridges slide into their slots in a short stagger.
- Selected cartridge advances, scales gently and tilts; hover receives smaller movement.
- Selecting a new track creates a brief case/jacket/title exchange recoil.
- Difficulty changes pulse the level readout briefly.
- Runs using unscaled delta time. Disable on the Canvas through `enableMotion` if desired.

## Existing game logic preserved
- `SongSelectMenuUI` continues to handle chart discovery, mouse wheel, F2 sorting, mouse chart difficulty, audio preview, and launch of shared `Mouse` scene.
- No new gameplay key bindings were introduced.
- `MouseModeChartAsset.songSelectJacket` replaces the jacket display area when art is available.
- Return to title, loading and preview references still point at their original scene objects.
- A new shape renderer subclasses `Image`; the existing Button, tint and references are retained.

## Verify inside Unity
1. Pull branch `feat/cartridge-song-select-redesign-20261119`, open `Assets/Scene/SongSelectArcadeScene.unity`.
2. Allow script imports, inspect the left cartridges in **Scene** and **Game** views; open Play Mode.
3. Check click / mouse wheel to switch tracks, preview, difficulty arrows, Back and Load & Play.
4. Switch resolution and look for text overlap or clipped geometry (designed for 1920×1080 CanvasScaler).
5. Verify with a track jacket Sprite and without a Sprite.
6. Confirm Console has no warnings/errors before merging.

**Note:** serialized scene structural checks were completed, but the Unity Editor was not run from this environment, so visual/compile results require local verification.
