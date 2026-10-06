# Resident Evil Laser Corridor – V3

Unity version: **6000.3.9f1 (Unity 6.3 LTS)**

Open `Assets/Scenes/LaserCorridor.unity`.

## V3 changes
- Speed-pad arrows now clearly point toward the goal (+Z).
- Timed bonus text counts down every second and fades at 0s.
- Multiple speed boosts stack independently, each with its own countdown line.
- Two speed pads are present and all bonus-pad positions shuffle each attempt.
- Vertical lasers run from floor to ceiling.
- Horizontal and diagonal lasers overlap the corridor walls so there are no visible gaps at the ends.
- 12 more varied laser patterns, including randomized diagonal angles and multi-beam combinations.
- Harder spawn rate / laser speed while keeping the 60-second round.
- The corridor geometry is now generated in **Edit Mode** and automatically saved into `LaserCorridor.unity`, so the corridor is visible and inspectable immediately when the scene is opened. Expand `LASER_CORRIDOR_SCENE_GEOMETRY` in the Hierarchy during a video demo.

## Controls
- WASD: move
- Space: jump
- Mouse wheel: switch/zoom 1st/3rd person
- RMB: orbit camera in third-person
- R: run again after winning

## Scene-view demo
When `LaserCorridor.unity` is opened, an editor utility automatically creates and saves organized scene geometry under:

`LASER_CORRIDOR_SCENE_GEOMETRY`

It contains structure, lighting, decoration, pads, gameplay markers, and representative laser shapes as ordinary Unity GameObjects that can be clicked and inspected in the Scene/Hierarchy views.

At Play time, the gameplay bootstrap replaces this editor geometry with the live collision-enabled version, so the actual gameplay remains reliable.

If the editable scene geometry ever goes missing, use:

`Tools > Laser Corridor > Rebuild Editable Scene Geometry`
