using System;
using UnityEngine;

public class PlayerVitals : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool ShieldActive { get; private set; }

    public event Action<int, int> HealthChanged;
    public event Action<bool> ShieldChanged;
    public event Action Died;

    private void Awake()
    {
        CurrentHealth = maxHealth;
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || CurrentHealth <= 0)
            return;

        if (ShieldActive)
            return;

        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (CurrentHealth <= 0)
            Died?.Invoke();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || CurrentHealth <= 0)
            return;

        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void SetShield(bool active)
    {
        if (ShieldActive == active)
            return;

        ShieldActive = active;
        ShieldChanged?.Invoke(ShieldActive);
    }

    public void ResetVitals()
    {
        CurrentHealth = maxHealth;
        ShieldActive = false;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        ShieldChanged?.Invoke(false);
    }

    public void BroadcastCurrentState()
    {
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        ShieldChanged?.Invoke(ShieldActive);
    }
}
