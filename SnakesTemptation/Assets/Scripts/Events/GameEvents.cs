using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// Every gameplay moment the game broadcasts, as one asset (Assets/ScriptableObjects/GameEvents.asset) so the controller
    /// and the listeners (<see cref="RetroAudio"/>, <see cref="CameraRig"/>) are wired up with a single reference.
    /// </summary>
    [CreateAssetMenu(menuName = "Snake's Temptation/Game Events", fileName = "GameEvents")]
    public sealed class GameEvents : ScriptableObject
    {
        [Header("Retro board and maze")]
        public GameEvent retroStarted;      // the game begins on the retro board
        public GameEvent pickupEaten;       // an apple (retro) or a seed (maze) was eaten
        public GameEvent worldFlipped;      // the third apple: the board turns into the 3D maze
        public GameEvent monsterRose;       // the apple monster climbs out of the ground
        public GameEvent chaseStarted;      // the chase in the dark maze begins
        public GameEvent lanternCollected;  // the snake lights up and the seeds show
        public GameEvent lanternExpired;    // the glow ran out
        public GameEvent monsterRelocated;  // the next seed was behind the monster, so it re-emerged behind the snake

        [Header("Deaths and lives")]
        public GameEvent snakeCrashed;      // into a wall, a hedge, a tree (or its own body on the retro board)
        public GameEvent snakeDevoured;     // the apple monster reached the head
        public GameEvent snakeRespawned;    // back in the centre for another life

        [Header("The ending")]
        public GameEvent gameWon;           // the win screen without the cutscene
        public GameEvent endingStarted;     // the garden cutscene begins
        public GameEvent seedPlanted;
        public GameEvent treeGrew;
        public GameEvent serpentHissed;
        public GameEvent appleBitten;

        public GameEvent[] All => new[]
        {
            retroStarted, pickupEaten, worldFlipped, monsterRose, chaseStarted, lanternCollected, lanternExpired, monsterRelocated,
            snakeCrashed, snakeDevoured, snakeRespawned, gameWon, endingStarted, seedPlanted, treeGrew, serpentHissed, appleBitten,
        };

        public bool IsComplete => System.Array.TrueForAll(All, e => e != null);
    }
}
