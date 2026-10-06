RESIDENT EVIL LASER CORRIDOR (Unity) - minimal version

OPEN: Unity Hub > Add project from disk > this folder (Unity 2021 LTS or newer, Built-in or URP).
      Open Assets/Scenes/LaserCorridor.unity and press Play.
      (If the scene won't load: make an empty scene, add an empty GameObject, attach LaserCorridorGame.)

CONTROLS: WASD or arrow keys.

RULES
- Lasers spawn at the end of the corridor and move toward you. Each is a wall with a gap: line up with the gap.
- Touching a laser costs 20 HP.
- Floor panels (once per run): green = heal +30, blue = speed boost for 5s.
- Reach the green exit door at the far end to win.
- At 0 HP: "YOU DIED", then you are reset to the start (health, panels and lasers too).

TWEAK: select the LaserCorridorGame object and edit values in the Inspector
(corridor size, laser speed/gap/damage, spawn interval, bonus amounts).

The whole game is one script: Assets/Scripts/LaserCorridorGame.cs
