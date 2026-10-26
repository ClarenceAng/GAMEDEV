# Snake's Temptation

**Game Jam 1: Retro Revolution.** Retro title: **Snake** (1976). Genre: **survival horror**.

## Group members

- Clarence Ang (hosts the repository)
- Mo David
- Nathaniel Irvin Adiong
- Mariella Jeanne Dellosa

## Description

Snake's Temptation begins as the classic Snake on a green Game Boy-style screen: steer the snake, eat apples to grow,
and don't hit the walls or your own tail. After the third apple the screen glitches and the board falls away into a dark
3D hedge maze, seen from just behind the snake, and an apple monster climbs out of the ground to hunt you. The snake
can never stop moving, so you have to find three glowing apple seeds in the dark while staying ahead of the monster,
which grows faster with every seed; lanterns light you up and reveal where the seeds are for 15 seconds, and you have
three lives. Collect all three seeds and you escape to a sunlit garden, where the snake plants them and, as the serpent
of Eden, tempts Eve with the fruit.

## Controls

| Action | Keyboard | Gamepad |
|---|---|---|
| Start the game | Space, Enter or any arrow key | A or Start |
| Steer on the retro board (up, down, left, right) | Arrow keys or W A S D | D-pad |
| Turn left / right in the 3D maze (relative to the snake) | ← / → or A / D | D-pad left / right |
| Skip the ending | Space or Enter | A or Start |
| Play again after a win or a game over | Space or Enter | A or Start |


## Running it

- **Unity:** 6000.6.0f1 (Unity 6), Universal Render Pipeline
- **Open it:** Unity Hub → Add → select this folder, then open `Assets/Scenes/SnakesTemptation.unity` and press Play.

## Project layout

```
Assets/
  Audio/             Sound effects (Sfx) and music loops (Music), WAV
  Input/             Input System actions
  Materials/         Grouped by subject: AppleMonster, Snake, Seed, Trees, Maze, Retro, Powerups
  Meshes/            AppleMonster meshes
  Prefabs/           AppleMonster.prefab
  Scenes/            SnakesTemptation.unity (the whole game), SampleScene.unity (Unity template)
  ScriptableObjects/ GameEvents.asset, SoundSet.asset, and the event channels (Events/)
  Scripts/           Game code (one assembly: SnakesTemptation.asmdef)
    Sim/               Game rules and maze generation: plain C#, no GameObjects
    View/              Everything you see: snake, maze, monster, trees, garden, HUD, camera
    Physics/           Collision detection in the 3D maze (colliders and the head's trigger)
    Events/            The event channels (ScriptableObjects)
    Cutscene/          The ending's dialogue
    Audio/             Plays the sounds from Assets/Audio
    Util/              Shared helpers
  Settings/          URP render pipeline assets
  Tests/             EditMode and PlayMode tests
  Textures/          AppleMonster, Maze
Packages/            Unity package list
ProjectSettings/     Unity project settings
```

## Tests

Window > General > Test Runner, then run **EditMode** and **PlayMode**.


## AI Disclosure

AI was used in this project to help in concepts that we were not familiar with such as:
- Animation
- Camera Angles
- Math Tricks
- Scene Handling
- Scene Generation with Assets
