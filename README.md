# Laser Corridor

A first-person take on the Resident Evil laser corridor, made in Unity 6 (6000.3.9f1, URP).

## How to play

Open `Assets/Scenes/LaserCorridor.unity` and press Play.

- **Goal:** walk from the blue start zone to the blue exit at the far end of the 116 m corridor.
- **Lasers** fire from the red emitter at the far end and travel towards you. Each hit costs 25 HP.
  - *Low beam:* jump over it.
  - *Swinging upright beam:* time your pass around it.
  - *Diagonal beam:* stand under its high end.
  - *Grid:* line up with the single gap.
  - *Low beam + swinging beam:* both at once.
  - *Scissor:* two upright beams swinging in opposite directions; slip through as they cross.
  - *Spinner:* a rotating cross in the centre; hug a wall to get past.
  - *Moving gap:* a grid whose gap slides side to side; follow it.
  - 80% of waves are the moving multi-beam patterns (low + swinging, scissor, spinner, moving gap); single beams and the static grid make up the rest. The same pattern never comes twice in a row.
  - Waves come faster and more often the further you get, and the grid gap narrows near the end.
  - A run starts with waves already in flight, so there's no long empty walk at the start.
- **Look:** red for danger (lasers, emergency lights), blue for anything that helps you. The HUD shows Resident Evil style FINE / CAUTION / DANGER health.
- **Bonus floor panels** glow blue, work once per run and go dark after use. The icon on each tells you what it does:
  - Cross, **Medkit**: +40 HP
  - Square, **Shield**: 5 s of no damage
  - Pause bars, **Slow-mo**: lasers move at 35% speed for 6 s
  - Chevrons, **Adrenaline**: 1.6× move speed for 6 s
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
