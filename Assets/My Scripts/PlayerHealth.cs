using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float regenRate = 6f;
    public float regenDelay = 3f;

    public float CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }

    private float lastDamageTime;
    private Rigidbody body;
    private PlayerMovement movement;
    private SnowballThrow thrower;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        thrower = GetComponent<SnowballThrow>();
    }

    private void Update()
    {
        if (IsDead)
            return;

        if (Time.time - lastDamageTime >= regenDelay && CurrentHealth < maxHealth)
        {
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + regenRate * Time.deltaTime);
        }
    }

    public void Damage(float amount)
    {
        if (IsDead)
            return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        lastDamageTime = Time.time;

        if (CurrentHealth <= 0f)
            Die();
    }

    private void Die()
    {
        IsDead = true;
        CurrentHealth = 0f;

        if (movement != null)
            movement.enabled = false;
        if (thrower != null)
            thrower.enabled = false;

        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
