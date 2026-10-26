using UnityEngine;

// all the game events in one asset so the scripts only need one reference
[CreateAssetMenu(menuName = "Snake's Temptation/Game Events", fileName = "GameEvents")]
public class GameEvents : ScriptableObject
{
    [Header("Retro board and maze")]
    public GameEvent retroStarted;
    public GameEvent pickupEaten;       // apple or seed
    public GameEvent worldFlipped;      // 3rd apple, the board turns into the maze
    public GameEvent monsterRose;
    public GameEvent chaseStarted;
    public GameEvent lanternCollected;
    public GameEvent lanternExpired;
    public GameEvent monsterRelocated;  // monster went back in the ground and came up behind you

    [Header("Deaths and lives")]
    public GameEvent snakeCrashed;
    public GameEvent snakeDevoured;
    public GameEvent snakeRespawned;

    [Header("The ending")]
    public GameEvent gameWon;
    public GameEvent endingStarted;
    public GameEvent seedPlanted;
    public GameEvent treeGrew;
    public GameEvent serpentHissed;
    public GameEvent appleBitten;

    public GameEvent[] All
    {
        get
        {
            return new GameEvent[]
            {
                retroStarted, pickupEaten, worldFlipped, monsterRose, chaseStarted, lanternCollected, lanternExpired, monsterRelocated,
                snakeCrashed, snakeDevoured, snakeRespawned, gameWon, endingStarted, seedPlanted, treeGrew, serpentHissed, appleBitten
            };
        }
    }

    // true if every event slot is filled in
    public bool IsComplete
    {
        get
        {
            GameEvent[] all = All;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) return false;
            }
            return true;
        }
    }
}
