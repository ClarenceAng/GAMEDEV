STEALTH CORRIDOR V2 — VERTICAL SECURITY (SEPARATE PROJECT VERSION)
=================================================================
See PROJECT ROOT README.md for controls, implementation, and gameplay design.

Assignment compliance
---------------------
First-person controller:   StealthFirstPersonController.cs
Moving detection hazards:  StealthPatrolSensor.cs (eight patrols)
Physics zones:             Physics.CheckSphere / CheckBox / CheckCapsule
Sight/occlusion:           Physics.Raycast from each bot toward player
Pickup layer:              HiddenIntelPickup (layer 9), ignored by bot LOS
Flashlight:                F toggles a player-mounted spotlight
Hidden objects:            StealthHiddenIntel.cs reveals only if light is ON,
                           target is in cone/range, and Raycast isn't blocked
Reuse prior level:         Modular corridor + visual LaserBeam prefabs retained
Submission:                Create a new Git branch and record an actual Unity demo

V2 adds vertical stealth without requiring ladder code:
* downward ceiling detectors and real overhead raycast-blocking cover,
* side-to-side ground scanner plus a ramp to an elevated catwalk,
* differently shaped shields, angled plates, wall-bay detailing,
* one mandatory access key on the elevated route and three normal files.

Visible scene editor preview is rebuilt automatically when possible; menu:
Tools > Laser Corridor > Rebuild Editable Scene Geometry

Designed for Unity 6000.3.9f1. Not live playtested in Unity here.
