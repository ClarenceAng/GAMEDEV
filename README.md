# Resident Evil Laser Corridor – V4

Unity version: **6000.3.9f1 (Unity 6.3 LTS)**

Open `Assets/Scenes/LaserCorridor.unity`.

## V4 changes
- Added actual reusable prefab assets for the repeated corridor section, all four bonus-pad types, and the laser beam.
- The editable Scene view uses prefab instances for the repeated corridor modules, pads, and sample lasers.
- Runtime gameplay also loads the same prefabs through `Resources/Prefabs`, with safe fallback code if an asset is missing.
- Fixed the HUD health label/bar spacing and changed the health fill to a stable fill-based bar.
- Increased desktop image quality: 4x MSAA, high-quality SMAA, full render scale, HDR/MSAA enabled, dynamic resolution disabled.
- Keeps all V3 mechanics: randomized pads, stacked speed-boost countdowns, 12 laser patterns, long corridor, 60-second timer, health/shield/slow/speed bonuses.

## Prefabs
On the first Unity import, the editor utility creates these in:

`Assets/Laser Corridor/Resources/Prefabs`

- `CorridorSection.prefab`
- `HealthPad.prefab`
- `SpeedPad.prefab`
- `ShieldPad.prefab`
- `SlowPad.prefab`
- `LaserBeam.prefab`

If you want to rebuild them manually:

`Tools > Laser Corridor > Rebuild Prefabs`

Then rebuild the editable scene if needed:

`Tools > Laser Corridor > Rebuild Editable Scene Geometry`

After Unity creates the prefabs, save the scene once (`Ctrl+S`) and include the generated prefab/material assets in your Git branch.

## Controls
- WASD: move
- Space: jump
- Mouse wheel: switch/zoom 1st/3rd person
- RMB: orbit camera in third-person
- R: run again after winning

## Image quality tip for recording
The project now enables anti-aliasing itself, but the Unity **Game** tab can still display at a low editor preview resolution. For your video, set the Game view to **1920x1080** (or another 16:9 Full HD preset) and use **Scale 1x / Fit** rather than a tiny fixed preview resolution.
