using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Runs the laser corridor: starts/stops the laser spawner, applies floor-panel bonuses,
// handles death (game over screen, then back to the start) and the level-complete screen.
public class LaserCorridorManager : MonoBehaviour
{
    public enum State
    {
        Playing,
        Dead,
        Complete,
    }

    public static LaserCorridorManager Instance { get; private set; }

    // HUD palette: green for good news, reds for danger, blue only for the shield.
    static readonly Color GoodTextColor = new Color(0.45f, 0.85f, 0.5f);
    static readonly Color DangerTextColor = new Color(1f, 0.23f, 0.19f);

    [SerializeField]
    PlayerHealth player;

    [SerializeField]
    FirstPersonController controller;

    [SerializeField]
    Transform spawnPoint;

    [SerializeField]
    LaserSpawner spawner;

    [SerializeField]
    FloorPanel[] panels;

    [Header("Bonuses")]
    [SerializeField]
    int medkitHeal = 40;

    [SerializeField]
    float shieldDuration = 5f;

    [SerializeField]
    float slowDuration = 6f;

    [SerializeField]
    [Range(0.1f, 1f)]
    float slowFactor = 0.35f;

    [SerializeField]
    float speedBoostDuration = 6f;

    [SerializeField]
    float speedBoostMultiplier = 1.6f;

    [Header("Game Over")]
    [SerializeField]
    float deathScreenDuration = 2.5f;

    [Header("UI")]
    [SerializeField]
    RectTransform healthFill;

    [SerializeField]
    Image healthFillImage;

    [SerializeField]
    Text healthText;

    [SerializeField]
    Text statsText;

    [SerializeField]
    Text effectsText;

    [SerializeField]
    Text pickupText;

    [SerializeField]
    Image screenTint;

    [SerializeField]
    GameObject messagePanel;

    [SerializeField]
    Text messageText;

    State state;
    int deaths;
    float elapsed;
    float bestTime = -1;
    float deadUntil;
    float slowUntil;
    float speedUntil;
    float pickupUntil;
    float damageFlash;
    float baseMoveSpeed;
    float baseSprintSpeed;
    CharacterController characterController;

    public State CurrentState => state;
    public int Deaths => deaths;
    public PlayerHealth Player => player;
    public LaserSpawner Spawner => spawner;
    public bool IsSpeedBoosted => Time.time < speedUntil;
    public bool IsSlowed => Time.time < slowUntil;

    void Awake()
    {
        Instance = this;
        characterController = player.GetComponent<CharacterController>();
        baseMoveSpeed = controller.MoveSpeed;
        baseSprintSpeed = controller.SprintSpeed;
        player.Damaged += OnPlayerDamaged;
        player.Died += OnPlayerDied;
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
        if (player != null)
        {
            player.Damaged -= OnPlayerDamaged;
            player.Died -= OnPlayerDied;
        }
    }

    void Start()
    {
        StartRun();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            deaths = 0;
            StartRun();
        }

        if (state == State.Dead && Time.time >= deadUntil)
        {
            StartRun();
        }

        if (state == State.Playing)
        {
            elapsed += Time.deltaTime;
        }

        UpdateTimedBonuses();
        UpdateHud();
    }

    // Puts the player back at the start with full health and restarts the lasers.
    public void StartRun()
    {
        state = State.Playing;
        elapsed = 0;
        damageFlash = 0;
        pickupUntil = 0;
        slowUntil = 0;
        speedUntil = 0;
        UpdateTimedBonuses();

        player.ResetHealth();
        Teleport(spawnPoint.position, spawnPoint.rotation);
        controller.enabled = true;

        foreach (FloorPanel panel in panels)
        {
            panel.ResetPanel();
        }

        spawner.Begin();
        messagePanel.SetActive(false);
    }

    public void Teleport(Vector3 position, Quaternion rotation)
    {
        // CharacterController overrides transform changes while enabled.
        characterController.enabled = false;
        player.transform.SetPositionAndRotation(position, rotation);
        characterController.enabled = true;
    }

    // Returns false if the bonus could not be used right now (e.g. during the death screen).
    public bool ApplyBonus(BonusType bonus)
    {
        if (state != State.Playing)
        {
            return false;
        }

        switch (bonus)
        {
            case BonusType.Medkit:
                player.Heal(medkitHeal);
                ShowPickup($"MEDKIT  +{medkitHeal} HP");
                break;
            case BonusType.Shield:
                player.GrantShield(shieldDuration);
                ShowPickup($"SHIELD  {shieldDuration:0}s invulnerability");
                break;
            case BonusType.TimeSlow:
                slowUntil = Time.time + slowDuration;
                ShowPickup($"SLOW-MO  lasers slowed for {slowDuration:0}s");
                break;
            case BonusType.SpeedBoost:
                speedUntil = Time.time + speedBoostDuration;
                ShowPickup($"ADRENALINE  speed up for {speedBoostDuration:0}s");
                break;
        }
        UpdateTimedBonuses();
        return true;
    }

    public void CompleteLevel()
    {
        if (state != State.Playing)
        {
            return;
        }

        state = State.Complete;
        spawner.Stop();
        spawner.ClearAll();
        controller.enabled = false;

        bool newBest = bestTime < 0 || elapsed < bestTime;
        if (newBest)
        {
            bestTime = elapsed;
        }

        messagePanel.SetActive(true);
        messageText.color = GoodTextColor;
        messageText.text = $"CORRIDOR CLEARED\n\n<size=40>Time {elapsed:0.0}s{(newBest ? "  (best!)" : "")}    Health {player.Current}/{player.Max}    Deaths {deaths}</size>\n\n<size=32>Press R to run it again</size>";
    }

    void OnPlayerDamaged(int amount)
    {
        damageFlash = 1;
    }

    void OnPlayerDied()
    {
        if (state != State.Playing)
        {
            return;
        }

        state = State.Dead;
        deaths++;
        deadUntil = Time.time + deathScreenDuration;
        spawner.Stop();
        controller.enabled = false;

        messagePanel.SetActive(true);
        messageText.color = DangerTextColor;
        messageText.text = "YOU DIED\n\n<size=36>Returning to the start...</size>";
    }

    void UpdateTimedBonuses()
    {
        spawner.SpeedMultiplier = IsSlowed ? slowFactor : 1f;

        float speedScale = IsSpeedBoosted ? speedBoostMultiplier : 1f;
        controller.MoveSpeed = baseMoveSpeed * speedScale;
        controller.SprintSpeed = baseSprintSpeed * speedScale;
    }

    void ShowPickup(string message)
    {
        pickupText.text = message;
        pickupText.color = GoodTextColor;
        pickupUntil = Time.time + 2.5f;
    }

    void UpdateHud()
    {
        float fraction = (float)player.Current / player.Max;
        healthFill.anchorMax = new Vector2(fraction, 1);
        healthFillImage.color = player.IsShielded
            ? new Color(0.25f, 0.45f, 0.85f)
            : Color.Lerp(new Color(0.35f, 0.03f, 0.03f), new Color(0.6f, 0.08f, 0.08f), fraction);

        // Resident Evil style condition readout.
        string condition = fraction > 0.6f ? "<color=#73D980>FINE</color>"
            : fraction > 0.3f ? "<color=#C86464>CAUTION</color>"
            : "<color=#FF3B30>DANGER</color>";
        healthText.text = $"{condition}   {player.Current} / {player.Max}";

        string best = bestTime < 0 ? "--" : $"{bestTime:0.0}s";
        statsText.text = $"Time {elapsed:0.0}s    Best {best}    Deaths {deaths}    Progress {spawner.Progress * 100:0}%";

        string effects = "";
        if (player.IsShielded)
        {
            effects += $"<color=#73D980>SHIELD {player.ShieldRemaining:0.0}s</color>   ";
        }
        if (IsSlowed)
        {
            effects += $"<color=#73D980>SLOW-MO {slowUntil - Time.time:0.0}s</color>   ";
        }
        if (IsSpeedBoosted)
        {
            effects += $"<color=#73D980>ADRENALINE {speedUntil - Time.time:0.0}s</color>";
        }
        effectsText.text = effects;

        pickupText.enabled = Time.time < pickupUntil;

        // Red flash on hit, steady blue tint while shielded.
        damageFlash = Mathf.MoveTowards(damageFlash, 0, Time.deltaTime * 2.5f);
        if (damageFlash > 0)
        {
            screenTint.color = new Color(1f, 0f, 0f, 0.35f * damageFlash);
        }
        else if (player.IsShielded)
        {
            screenTint.color = new Color(0.3f, 0.6f, 1f, 0.12f);
        }
        else
        {
            screenTint.color = Color.clear;
        }
    }
}
