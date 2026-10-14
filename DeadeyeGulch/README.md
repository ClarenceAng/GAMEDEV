# Deadeye Gulch

A first-person Western shooting gallery in two versions, made in Unity 6 (6000.6.0f1, URP). You stand at a booth with
a six-shooter and knock targets down with real, fast-moving bullets.

| Version | Scene | Targets |
|---|---|---|
| 1 · **Fence Line** | `Assets/Scenes/FenceLine.unity` | Static |
| 2 · **Ambush** | `Assets/Scenes/Ambush.unity` | Moving |

Open either scene and press Play. **Tab** on the title or results screen switches to the other version.

## How to play

Both versions share the same gun, scoring and 60-second round. Only the targets change.

- **Revolver:** 6 shots, then it reloads by itself (1.1 s). Press R to reload early. Hold right click to aim down the
  sights and zoom in for long shots.
- **Bullets** are projectiles, not instant hits. They fly at 180 m/s, push whatever they strike and leave a tracer, so you
  have to lead moving targets a little.
- **Scoring:** can 50 · bottle 75 · bandit 150 · can shot in mid-air 250 ("TRICK SHOT"). Targets 20 m or more away
  pay half again ("LONG SHOT").
- **Streak:** every 3 hits in a row raise the multiplier (x2, x3, up to x4). A miss resets it.

### Version 1: Fence Line (static targets)
28 targets standing still at four distances: bottles and cans on the fence (7 m), two can pyramids on plank tables
(11.5 m; hit them low and the whole stack comes down), bandit cut-outs on the saloon porch (about 17 m), and bottles on
rocks out in the desert (24 to 28 m). Cans are full physics objects. They count as down once they tip over or fall off
their perch, even if another can knocked them. Knock everything down before time runs out for a time bonus of 25
points per second left.

### Version 2: Ambush (moving targets)
Nothing holds still, and the round runs until the clock hits zero:
- figures pop up from behind four barrels and duck back down,
- bottles swing on ropes from a gallows beam and get rehung after you shatter them,
- a mine cart rolls across on rails carrying a figure,
- two figures pace inside the saloon and only show through the door and windows,
- cans get tossed up from behind the fence for trick shots.

About a quarter of the figures are **townsfolk**. Shooting one costs 300 points and breaks your streak.

Controls: mouse aim · left click shoot · right click zoom · R reload · WASD step along the booth · Esc pause.
Gamepad: right stick aim · RT shoot · LT zoom · X reload · left stick step · Start pause.

## Project layout

| Path | Purpose |
|---|---|
| `Assets/Scripts/KnockdownGame.cs` | Game flow (title, playing, paused, results), scoring hooks, HUD layout |
| `Assets/Scripts/Core/` | `ScoreKeeper` (points, streaks, accuracy) and `Chamber` (six-shot ammo and reload). Plain C#, unit tested |
| `Assets/Scripts/Player/` | `PlayerRig` (first-person controller), `Revolver` (view model, firing, recoil, zoom), `Bullet` (swept projectile) |
| `Assets/Scripts/Targets/` | `CanTarget`, `BottleTarget`, `CutoutTarget` on a shared `KnockdownTarget` base |
| `Assets/Scripts/Levels/` | `FenceLineLevel` (static), `AmbushLevel` (moving), `Frontier` (shared scenery built from primitives) |
| `Assets/Scripts/View/`, `Audio/` | Code-built HUD, effects, and synthesised sound (no audio files) |
| `Assets/Editor/DeadeyeSceneBuilder.cs` | Generates both scenes and all materials |
| `Assets/Tests/` | EditMode tests for the rules, PlayMode tests that play both versions end to end |

## Command line

```bash
UNITY="C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe"

# Regenerate both scenes (also in the editor: Tools > Deadeye Gulch > Build Scenes)
"$UNITY" -batchmode -quit -projectPath . -executeMethod DeadeyeSceneBuilder.BuildBatch -logFile build.log

# Run the tests (set DEADEYE_SHOT_DIR=<folder> to also save screenshots from the PlayMode run)
"$UNITY" -batchmode -runTests -testPlatform EditMode -projectPath . -testResults editmode.xml -logFile editmode.log
"$UNITY" -batchmode -runTests -testPlatform PlayMode -projectPath . -testResults playmode.xml -logFile playmode.log
```
