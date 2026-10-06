LASER CORRIDOR V4
Unity 6000.3.9f1

Open Assets/Scenes/LaserCorridor.unity.

V4 adds reusable prefab assets for:
- CorridorSection
- HealthPad
- SpeedPad
- ShieldPad
- SlowPad
- LaserBeam

They are automatically generated under:
Assets/Laser Corridor/Resources/Prefabs

Manual editor tools:
Tools > Laser Corridor > Rebuild Prefabs
Tools > Laser Corridor > Rebuild Editable Scene Geometry

The Scene-view corridor now uses prefab instances for the repeated modules, bonus pads, and example lasers. Runtime gameplay also loads the prefab assets, with fallback code for safety.

HUD health alignment was fixed. Desktop rendering now uses 4x MSAA + high-quality SMAA and full render scale.

Controls:
WASD move
Space jump
Mouse wheel first/third person
RMB orbit in third person
R restart after win
