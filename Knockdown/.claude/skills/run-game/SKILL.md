---
name: run-game
description: Launch the Knockdown carnival game (static cans or moving cans) from Claude, rebuilding the player first if it is out of date. Use when asked to run, play, launch or show the game.
---

# Run Knockdown

One prebuilt Windows player with both versions: `Builds/Knockdown/Knockdown.exe`. It starts on the static cans; the
STATIC / DYNAMIC plates side by side on the ground to the left of the stall switch between cans standing on shelves
and cans swinging left and right, each on its own string from the booth roof.

Launch it in a window (fullscreen would cover the user's screen):

```powershell
Start-Process "C:\Users\PC\GAMEDEV\Knockdown\Builds\Knockdown\Knockdown.exe" -ArgumentList '-screen-fullscreen 0 -screen-width 1280 -screen-height 720'
```

Controls: WASD move, mouse look, left click throw, R restart, step on a plate to switch, Alt+F4 quit.

## Rebuild when Assets/ is newer than the build

The user's Unity Editor usually has this project open, which locks it, so never run batch Unity on the live folder:

1. Copy the project somewhere short (Unity fails on paths over 260 chars; `subst` a drive letter onto the scratchpad), e.g.
   `robocopy C:\Users\PC\GAMEDEV\Knockdown K:\sandbox\Knockdown /E /XD Temp Logs UserSettings Builds .vscode`
2. Build (both scenes are in the build settings, StaticTargets first):
   `"C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe" -batchmode -nographics -quit -projectPath K:\sandbox\Knockdown -buildTarget Win64 -buildWindows64Player C:\Users\PC\GAMEDEV\Knockdown\Builds\Knockdown\Knockdown.exe -logFile K:\build.log`
3. Check the log for `Build Finished, Result: Success.`, then remove the drive letter with `subst K: /D`.

In the Editor itself: open `Assets/Knockdown/Scenes/StaticTargets.unity` and press Play.
