using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LaserCorridorGameManager : MonoBehaviour
{
    public static LaserCorridorGameManager Instance { get; private set; }

    public event Action<float> TimeChanged;
    public event Action<string> CenterMessageChanged;
    public event Action<string, float, Color> TimedBonusStarted;
    public event Action<string, Color, float> BonusToastRequested;
    public event Action BonusNotificationsCleared;

    public float TimeRemaining { get; private set; }
    public bool IsRunning { get; private set; }

    private const float RoundDuration = 60f;
    private const float SpeedBoostDuration = 6f;
    private const float SpeedBoostPerStack = 0.45f;

    private PlayerMovement movement;
    private PlayerVitals vitals;
    private Transform startPoint;
    private LaserSpawner spawner;
    private BonusPad[] pads;

    private Coroutine shieldRoutine;
    private Coroutine slowRoutine;
    private readonly List<Coroutine> speedRoutines = new List<Coroutine>();
    private Coroutine resetRoutine;
    private int activeSpeedBoosts;

    private bool ending;
    private bool won;

    private void Awake()
    {
        Instance = this;
    }

    public void Configure(PlayerMovement playerMovement, PlayerVitals playerVitals, Transform spawnPoint, LaserSpawner laserSpawner)
    {
        movement = playerMovement;
        vitals = playerVitals;
        startPoint = spawnPoint;
        spawner = laserSpawner;

        vitals.Died += HandlePlayerDeath;
    }

    private void OnDestroy()
    {
        if (vitals != null)
            vitals.Died -= HandlePlayerDeath;
    }

    public void SetPads(BonusPad[] bonusPads)
    {
        pads = bonusPads;
    }

    public void BeginGame()
    {
        ResetAttemptState();
        StartCoroutine(BeginSequence());
    }

    private void Update()
    {
        if (IsRunning)
        {
            TimeRemaining -= Time.deltaTime;
            TimeChanged?.Invoke(Mathf.Max(0f, TimeRemaining));

            if (TimeRemaining <= 0f && !ending)
                BeginFailure("TIME UP");
        }

        if (won && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            won = false;
            BeginGame();
        }
    }

    private IEnumerator BeginSequence()
    {
        movement.SetControlsEnabled(false);
        CenterMessageChanged?.Invoke("GET READY");
        yield return new WaitForSeconds(1.0f);
        CenterMessageChanged?.Invoke("3");
        yield return new WaitForSeconds(0.55f);
        CenterMessageChanged?.Invoke("2");
        yield return new WaitForSeconds(0.55f);
        CenterMessageChanged?.Invoke("1");
        yield return new WaitForSeconds(0.55f);
        CenterMessageChanged?.Invoke("GO!");

        TimeRemaining = RoundDuration;
        TimeChanged?.Invoke(TimeRemaining);
        ending = false;
        won = false;
        IsRunning = true;
        movement.SetControlsEnabled(true);
        spawner.SetPaused(false);
        spawner.StartSpawning();

        yield return new WaitForSeconds(0.7f);
        if (IsRunning)
            CenterMessageChanged?.Invoke(string.Empty);
    }

    private void HandlePlayerDeath()
    {
        if (IsRunning && !ending)
            BeginFailure("GAME OVER");
    }

    private void BeginFailure(string message)
    {
        if (ending)
            return;

        ending = true;
        IsRunning = false;
        movement.SetControlsEnabled(false);
        spawner.SetPaused(true);
        spawner.StopSpawning();
        CenterMessageChanged?.Invoke(message);
        BonusNotificationsCleared?.Invoke();

        if (resetRoutine != null)
            StopCoroutine(resetRoutine);
        resetRoutine = StartCoroutine(ResetAfterDelay());
    }

    private IEnumerator ResetAfterDelay()
    {
        yield return new WaitForSeconds(2f);
        BeginGame();
    }

    public void PlayerReachedGoal()
    {
        if (!IsRunning || ending)
            return;

        ending = true;
        won = true;
        IsRunning = false;
        movement.SetControlsEnabled(false);
        spawner.SetPaused(true);
        spawner.StopSpawning();
        CenterMessageChanged?.Invoke("ESCAPE SUCCESSFUL\nPress R to run again");
        BonusNotificationsCleared?.Invoke();
    }

    public bool ApplyBonus(BonusPad.BonusType type)
    {
        if (!IsRunning || ending)
            return false;

        switch (type)
        {
            case BonusPad.BonusType.Heal:
                vitals.Heal(35);
                BonusToastRequested?.Invoke("+35 HEALTH", new Color(0.2f, 1f, 0.35f), 1.35f);
                return true;

            case BonusPad.BonusType.Shield:
                if (shieldRoutine != null)
                    StopCoroutine(shieldRoutine);
                shieldRoutine = StartCoroutine(ShieldBonus());
                return true;

            case BonusPad.BonusType.LaserSlowdown:
                if (slowRoutine != null)
                    StopCoroutine(slowRoutine);
                slowRoutine = StartCoroutine(SlowBonus());
                return true;

            case BonusPad.BonusType.SpeedBoost:
                activeSpeedBoosts++;
                ApplySpeedBoostStack();
                TimedBonusStarted?.Invoke("SPEED BOOST", SpeedBoostDuration, new Color(0.25f, 0.95f, 1f));
                Coroutine routine = StartCoroutine(SpeedBonusInstance());
                speedRoutines.Add(routine);
                return true;
        }

        return false;
    }

    private IEnumerator ShieldBonus()
    {
        const float duration = 5f;
        vitals.SetShield(true);
        TimedBonusStarted?.Invoke("SHIELD", duration, new Color(0.25f, 0.85f, 1f));
        yield return new WaitForSeconds(duration);
        vitals.SetShield(false);
        shieldRoutine = null;
    }

    private IEnumerator SlowBonus()
    {
        const float duration = 7f;
        spawner.SetSlowMode(true);
        TimedBonusStarted?.Invoke("LASERS SLOWED", duration, new Color(1f, 0.68f, 0.15f));
        yield return new WaitForSeconds(duration);
        spawner.SetSlowMode(false);
        slowRoutine = null;
    }

    private IEnumerator SpeedBonusInstance()
    {
        yield return new WaitForSeconds(SpeedBoostDuration);
        activeSpeedBoosts = Mathf.Max(0, activeSpeedBoosts - 1);
        ApplySpeedBoostStack();
    }

    private void ApplySpeedBoostStack()
    {
        float multiplier = 1f + activeSpeedBoosts * SpeedBoostPerStack;
        movement.SetSpeedMultiplier(multiplier);
    }

    private void ResetAttemptState()
    {
        StopTimedBonusRoutines();

        IsRunning = false;
        ending = false;
        won = false;
        TimeRemaining = RoundDuration;

        spawner.StopSpawning();
        spawner.SetPaused(true);
        spawner.SetSlowMode(false);
        spawner.ClearLasers();

        movement.ResetMovementModifiers();
        movement.SetControlsEnabled(false);
        movement.SetRespawnPoint(startPoint);
        movement.Respawn();

        vitals.ResetVitals();

        if (pads != null)
        {
            foreach (BonusPad pad in pads)
            {
                if (pad != null)
                    pad.ResetPad();
            }

            RandomizePadPositions();
        }

        TimeChanged?.Invoke(TimeRemaining);
        BonusNotificationsCleared?.Invoke();
    }

    private void RandomizePadPositions()
    {
        if (pads == null || pads.Length == 0)
            return;

        // Five slots across the long corridor. Two of the pads are speed boosts,
        // so some runs can naturally produce overlapping speed-boost timers.
        float[] zSlots = { -112f, -62f, -8f, 48f, 105f };

        for (int i = zSlots.Length - 1; i > 0; i--)
        {
            int swap = UnityEngine.Random.Range(0, i + 1);
            float temp = zSlots[i];
            zSlots[i] = zSlots[swap];
            zSlots[swap] = temp;
        }

        float[] lanes = { -2.3f, 0f, 2.3f };
        int count = Mathf.Min(pads.Length, zSlots.Length);

        for (int i = 0; i < count; i++)
        {
            if (pads[i] == null)
                continue;

            float x = lanes[UnityEngine.Random.Range(0, lanes.Length)];
            float z = zSlots[i] + UnityEngine.Random.Range(-4f, 4f);
            pads[i].SetRunPosition(new Vector3(x, 0.58f, z));
        }
    }

    private void StopTimedBonusRoutines()
    {
        if (shieldRoutine != null) StopCoroutine(shieldRoutine);
        if (slowRoutine != null) StopCoroutine(slowRoutine);

        foreach (Coroutine routine in speedRoutines)
        {
            if (routine != null)
                StopCoroutine(routine);
        }
        speedRoutines.Clear();

        shieldRoutine = null;
        slowRoutine = null;
        activeSpeedBoosts = 0;

        if (vitals != null)
            vitals.SetShield(false);
        if (movement != null)
            movement.SetSpeedMultiplier(1f);
        if (spawner != null)
            spawner.SetSlowMode(false);
    }
}
