# Laser Corridor

A first-person take on the Resident Evil laser corridor, made in Unity 6 (6000.3.9f1, URP).

## How to play

Open `Assets/Scenes/LaserCorridor.unity` and press Play.

- **Goal:** walk from the teal start zone to the green exit at the far end of the corridor.
- **Lasers** fire from the red emitter at the far end and travel towards you. Each hit costs 25 HP.
  - *Low beam:* jump over it.
  - *Swinging upright beam:* time your pass around it.
  - *Diagonal beam:* stand under its high end.
  - *Grid:* line up with the single gap.
  - *Low beam + swinging beam:* both at once (appears in the second half).
  - Waves come faster and more often the further you get.
- **Bonus floor panels** work once per run and go dark after use:
  - Green **Medkit**: +40 HP
  - Blue **Shield**: 5 s of no damage
  - Purple **Slow-mo**: lasers move at 35% speed for 6 s
  - Yellow **Adrenaline**: 1.6× move speed for 6 s
- **Game over:** at 0 HP the run ends ("YOU DIED") and you are sent back to the start with full health and fresh panels.

Controls: WASD move · mouse look · Space jump · Shift sprint · R restart.

## Project layout

| Path | Purpose |
|---|---|
| `Assets/Scripts/LaserCorridor/` | Gameplay (`LaserCorridor` assembly): `LaserCorridorManager`, `LaserSpawner`, `LaserWave`, `LaserBeam`, `PlayerHealth`, `FloorPanel`, `CorridorGoal` |
| `Assets/Editor/LaserCorridorBuilder.cs` | Generates the scene, materials and post-processing profile |
| `Assets/Tests/PlayMode/` | PlayMode tests covering each requirement |
| `Assets/Starter Assets/` | Unity Starter Assets first-person controller |

## Command line

```bash
UNITY="C:/Program Files/Unity/Hub/Editor/6000.3.9f1/Editor/Unity.exe"

# Regenerate the scene (also available in the editor: Tools > Twilightfall > Build Laser Corridor)
"$UNITY" -batchmode -quit -projectPath . -executeMethod LaserCorridorBuilder.Build -logFile build.log

# Run the PlayMode tests
"$UNITY" -batchmode -runTests -testPlatform PlayMode -projectPath . -testResults results.xml -logFile tests.log
```
