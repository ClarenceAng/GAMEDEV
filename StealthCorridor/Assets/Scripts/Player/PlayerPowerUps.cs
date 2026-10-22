using UnityEngine;
using StarterAssets;

// The power ups from the laser corridor: speed, jump boost and +20 HP.
public class PlayerPowerUps : MonoBehaviour
{
    // power up settings
    public float speedMultiplier = 1.5f;
    public float jumpMultiplier = 2f;
    public float powerUpSeconds = 10f;
    public int healAmount = 20;

    FirstPersonController controller;
    StealthGame game;

    // the normal values, so we can put them back after the power up ends
    float baseMoveSpeed;
    float baseSprintSpeed;
    float baseJumpHeight;

    float speedEndTime;
    float jumpEndTime;
    bool speedActive = false;
    bool jumpActive = false;

    void Awake()
    {
        controller = GetComponent<FirstPersonController>();
        baseMoveSpeed = controller.MoveSpeed;
        baseSprintSpeed = controller.SprintSpeed;
        baseJumpHeight = controller.JumpHeight;
    }

    void Start()
    {
        game = FindFirstObjectByType<StealthGame>();
    }

    void Update()
    {
        // turn the power ups off when the time runs out
        if (speedActive && Time.time >= speedEndTime)
        {
            ClearSpeed();
        }
        if (jumpActive && Time.time >= jumpEndTime)
        {
            ClearJump();
        }
    }

    // the panels on the floor dont have a script, we check their name when we walk into them
    void OnTriggerEnter(Collider other)
    {
        bool used = false;

        if (other.name.StartsWith("PowerUp_Speed"))
        {
            ApplySpeed();
            used = true;
        }
        else if (other.name.StartsWith("PowerUp_JumpBoost"))
        {
            ApplyJump();
            used = true;
        }
        else if (other.name.StartsWith("PowerUp_Health"))
        {
            // Heal gives back false if health is already full, then the panel stays
            used = game.Heal(healAmount);
        }

        // used panels disappear (they come back when the level starts again)
        if (used)
        {
            other.gameObject.SetActive(false);
        }
    }

    void ApplySpeed()
    {
        controller.MoveSpeed = baseMoveSpeed * speedMultiplier;
        controller.SprintSpeed = baseSprintSpeed * speedMultiplier;
        speedEndTime = Time.time + powerUpSeconds;
        speedActive = true;
    }

    void ApplyJump()
    {
        controller.JumpHeight = baseJumpHeight * jumpMultiplier;
        jumpEndTime = Time.time + powerUpSeconds;
        jumpActive = true;
    }

    void ClearSpeed()
    {
        controller.MoveSpeed = baseMoveSpeed;
        controller.SprintSpeed = baseSprintSpeed;
        speedActive = false;
    }

    void ClearJump()
    {
        controller.JumpHeight = baseJumpHeight;
        jumpActive = false;
    }
}
