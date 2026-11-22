# Mouse-mode art integration (2026-11-22)

Source: [image production specification](IMAGE_ASSET_SPEC_MOUSE_MODE_20261122.md)

## What the Unity project now does
- On opening Unity 2022.3, `Assets/Editor/LuluMouseArtGenerator.cs` creates **20 separate PNGs** beneath `Assets/Resources/RuruArt/` when missing. It never overwrites an existing PNG.
- New 9-sliced UI parts: `UI_Panel`, `UI_Button`, `Stability_Frame`, `Stability_Track`. The stability fill is a separate horizontal Filled sprite. Tutorial mouse guides are four independent sprites.
- Six different text-free song jackets are created and assigned only where `songSelectJacket` is null. Five song backgrounds are filled only if `backgroundImage` is null. Existing Pop'n Dash background references remain untouched.
- Mouse and Tutorial scenes get the shared horizontal stability HUD. Tutorial dialogue, steps, and buttons reuse the UI sprites. Pause gets a clickable resume menu if the existing panel is empty; result uses the shared panel and reuses the original Pino Clear/False artwork when available.
- All interactive text, score, judgement, and mouse notes remain code/TMP. The older 4-lane sprites are not used.

## Do not confuse generation with verification
The generator produces real per-asset RGBA PNG files during the next Unity Editor import. These files and the resulting importer/asset .meta changes must be included in a later Git commit if you want GitHub to store the generated binaries as well.
This change was reviewed against source code but the Unity Editor and Game View could not be run in the current environment. Before shipping, inspect 1920x1080, 1600x900, and 1366x768, and test tutorial mouse clicks, pause, result, stability fill values, and six chart jackets.

## Deliberately not changed
- Exact Lulu dialogue portrait: the 2026-11-22 original reference image is not present in this checkout, so no lookalike replacement was generated.
- Pino gameplay animations and Enemy01 source unchanged.
- Story backgrounds and an extra title key visual remain conditional on approved story/layout; no arbitrary narrative images.
- Settings slider/tab/toggle/screen scroll assets are only to be replaced after inspecting the existing sliced sprites and active settings layout.
