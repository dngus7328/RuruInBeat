# Anime Arcade / Cartridge Song Select Prototype (2026-11-19)

This is a **second, fully editable scene**, not a destructive rewrite of the user-authored song select.

## Open in Unity
- New screen: `Assets/Scene/SongSelectArcadeScene.unity`
- Original, unchanged: `Assets/Scene/SongSelectScene.unity`
- Title's START GAME now opens the new variant. Return to the original by changing the TitleMenuController Song Select Scene Name back to `SongSelectScene`.

## Layout direction
- Inspired by the **feel of anime-styled PC rhythm games**, not a copy of any existing commercial game.
- Left: asymmetrical stack of six cartridge-like song rows, subtle alternating tilt and spines, thick manga selection.
- Right: a large tilted game-card package with matte pastel backing, big song title, level and controls.
- Bottom: cartridge insertion/start button, smaller back key, mouse control hint.
- Flat ink, warm pink, desaturated purple, no glossy/neon filters.
- All objects use Unity Canvas, Image and TextMeshPro. No graphic asset purchase or PNG required.
- Original functional Button/Image/TMP references and SongSelectMenuUI script remain intact.
- Song counts and levels still come from the editor-created chart .asset catalog, not a hardcoded six-song array.
- Newly added visual-only objects are all named `Arcade_*` so the user can individually replace or delete them.
- The design is a **placeholder arrangement**. The per-chart `songSelectJacket` Sprite can be changed later to real art.

## Safeguards
- Existing SongSelectScene.unity file was **not modified**.
- Existing UI positions in the original scene are preserved; the variant contains separate serialized RectTransforms that the user can edit freely.
- Runtime menu script does not reset the variant scene's existing positions.
- The one-time SongSelectFirstTimeLayoutBuilder runs only for the original blank scene, so it will not regenerate the new variant.
- TitleScene has only one serialized scene-name string changed; no Title scene RectTransform or UI layout change.
- The build includes the new variant without deleting or reordering previous scene entries.
- After the user starts modifying the variant, future updates should only replace sprites, colors and behavior — **never regenerate or reposition that scene**.

Unity editor/Play Mode visual and compilation verification still required.
