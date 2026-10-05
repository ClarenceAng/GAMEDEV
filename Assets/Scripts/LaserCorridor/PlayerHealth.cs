using System;
using UnityEngine;

// Health pool for the player. Lasers call TakeDamage; a short invulnerability window after
// each hit stops a single beam from draining several hits at once. A shield (from a bonus
// panel) blocks all damage while it lasts.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField]
    int maxHealth = 100;

    [SerializeField]
    [Tooltip("Seconds of invulnerability after taking a hit.")]
    float hitInvulnerability = 0.75f;

    int current;
    float invulnerableUntil;
    float shieldUntil;

    public int Current => current;
    public int Max => maxHealth;
    public bool IsDead => current <= 0;
    public bool IsShielded => Time.time < shieldUntil;
    public float ShieldRemaining => Mathf.Max(0, shieldUntil - Time.time);

    public event Action<int> Damaged;
    public event Action Died;

    void Awake()
    {
        current = maxHealth;
    }

    // Returns true if the damage was applied (not blocked by a shield or the hit window).
    public bool TakeDamage(int amount)
    {
        if (IsDead || IsShielded || Time.time < invulnerableUntil)
        {
            return false;
        }

        current = Mathf.Max(0, current - amount);
        invulnerableUntil = Time.time + hitInvulnerability;
        Damaged?.Invoke(amount);

        if (current == 0)
        {
            Died?.Invoke();
        }
        return true;
    }

    public void Heal(int amount)
    {
        if (IsDead)
        {
            return;
        }
        current = Mathf.Min(maxHealth, current + amount);
    }

    public void GrantShield(float seconds)
    {
        shieldUntil = Mathf.Max(shieldUntil, Time.time + seconds);
    }

    public void ResetHealth()
    {
        current = maxHealth;
        invulnerableUntil = 0;
        shieldUntil = 0;
    }
}
