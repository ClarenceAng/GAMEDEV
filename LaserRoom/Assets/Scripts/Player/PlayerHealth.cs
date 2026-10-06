using System.Collections;
using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public int currentHealth;
    public float invulnerabilitySeconds = 0.5f; // so u dont get hit by the same laser
    public Transform respawnPoint;
    public float goalResetDelay = 3f; // seconds after reaching the end before we go back to the start

    CharacterController controller;
    PlayerPowerUps powerUps;
    Vector3 startPosition;
    Quaternion startRotation;
    float invulnerableUntil;

    GameHUD hud;
    LaserSpawner spawner;
    bool reachedGoal = false;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        powerUps = GetComponent<PlayerPowerUps>();

        if (respawnPoint != null)
        {
            startPosition = respawnPoint.position;
            startRotation = respawnPoint.rotation;
        }
        else
        {
            startPosition = transform.position;
            startRotation = transform.rotation;
        }

        currentHealth = maxHealth;
    }

    void Start()
    {
        hud = FindFirstObjectByType<GameHUD>();
        spawner = FindFirstObjectByType<LaserSpawner>();
    }

    // the goal is just a trigger box in the scene, the player checks for it by name
    void OnTriggerEnter(Collider other)
    {
        if (other.name == "GoalZone" && !reachedGoal)
        {
            reachedGoal = true;
            if (hud != null)
            {
                hud.ShowMessage("You reached the end!", 3f);
            }
            StartCoroutine(ResetAfterGoal());
        }
    }

    // wait a few seconds then send the player back to the start
    IEnumerator ResetAfterGoal()
    {
        yield return new WaitForSeconds(goalResetDelay);
        Respawn();
        reachedGoal = false;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || Time.time < invulnerableUntil)
        {
            return;
        }

        currentHealth = currentHealth - amount;
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }
        invulnerableUntil = Time.time + invulnerabilitySeconds;

        if (currentHealth == 0)
        {
            if (hud != null)
            {
                hud.ShowMessage("You died - you're back to the start!", 2.5f);
            }
            Respawn();
        }
    }

    // returns false if health is already full
    public bool Heal(int amount)
    {
        if (amount <= 0 || currentHealth >= maxHealth)
        {
            return false;
        }

        currentHealth = currentHealth + amount;
        if (currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }
        return true;
    }

    public void Respawn()
    {
        // the character controller has to be turned off or it moves the player back
        controller.enabled = false;
        transform.SetPositionAndRotation(startPosition, startRotation);
        controller.enabled = true;

        currentHealth = maxHealth;
        invulnerableUntil = Time.time + invulnerabilitySeconds;

        // reset the power ups (and bring the panels back) and the lasers
        if (powerUps != null)
        {
            powerUps.ResetPowerUps();
        }
        if (spawner != null)
        {
            spawner.ClearLasers();
        }
    }
}
