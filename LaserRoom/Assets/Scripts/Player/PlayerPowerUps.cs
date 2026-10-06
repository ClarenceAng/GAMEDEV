using System.Collections.Generic;
using UnityEngine;
using StarterAssets;

public class PlayerPowerUps : MonoBehaviour
{
    // power up settings
    public float speedMultiplier = 1.5f;
    public float jumpMultiplier = 2f;
    public float powerUpSeconds = 10f;
    public int healAmount = 20;

    ThirdPersonController controller;
    PlayerHealth health;

    // the normal values, so we can put them back after the power up ends
    float baseMoveSpeed;
    float baseSprintSpeed;
    float baseJumpHeight;

    float speedEndTime;
    float jumpEndTime;
    bool speedActive = false;
    bool jumpActive = false;

    // panels we already stepped on, they come back when we respawn
    List<GameObject> usedPanels = new List<GameObject>();

    void Awake()
    {
        controller = GetComponent<ThirdPersonController>();
        health = GetComponent<PlayerHealth>();
        baseMoveSpeed = controller.MoveSpeed;
        baseSprintSpeed = controller.SprintSpeed;
        baseJumpHeight = controller.JumpHeight;
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
            used = health.Heal(healAmount);
        }

        if (used)
        {
            other.gameObject.SetActive(false);
            usedPanels.Add(other.gameObject);
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

    public void ResetPowerUps()
    {
        ClearSpeed();
        ClearJump();

        foreach (GameObject panel in usedPanels)
        {
            panel.SetActive(true);
        }
        usedPanels.Clear();
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
