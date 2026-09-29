using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Tracks the run (time, falls), respawns the player and shows the level-complete screen.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [SerializeField]
    PlayerController player;

    [SerializeField]
    Transform spawnPoint;

    [Header("UI")]
    [SerializeField]
    Text hudText;

    [SerializeField]
    GameObject completePanel;

    [SerializeField]
    Text completeText;

    int falls;
    float elapsed;
    bool isComplete;

    public int Falls => falls;
    public bool IsComplete => isComplete;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Start()
    {
        RestartLevel();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }

        if (!isComplete)
        {
            elapsed += Time.deltaTime;
        }

        if (hudText != null)
        {
            hudText.text = $"Time: {elapsed:0.0}s    Falls: {falls}\nWASD move · Space jump · R restart";
        }
    }

    public void PlayerFell()
    {
        if (isComplete)
        {
            return;
        }
        falls++;
        player.Respawn(spawnPoint.position, spawnPoint.rotation);
    }

    public void CompleteLevel()
    {
        if (isComplete)
        {
            return;
        }
        isComplete = true;
        player.SetInputEnabled(false);

        if (completePanel != null)
        {
            completePanel.SetActive(true);
        }
        if (completeText != null)
        {
            completeText.text = $"LEVEL COMPLETE!\n\nTime: {elapsed:0.0}s    Falls: {falls}\n\nPress R to play again";
        }
    }

    public void RestartLevel()
    {
        isComplete = false;
        falls = 0;
        elapsed = 0;

        if (completePanel != null)
        {
            completePanel.SetActive(false);
        }
        player.SetInputEnabled(true);
        player.Respawn(spawnPoint.position, spawnPoint.rotation);
    }
}
