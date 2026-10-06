using System.Collections.Generic;
using UnityEngine;

public class LaserObstacle : MonoBehaviour
{
    private float baseSpeed;
    private float speedScale = 1f;
    private int damage;
    private bool paused;
    private readonly HashSet<int> damagedPlayers = new HashSet<int>();

    public void Configure(float speed, int damageAmount)
    {
        baseSpeed = speed;
        damage = damageAmount;
    }

    public void SetPaused(bool value)
    {
        paused = value;
    }

    public void SetSpeedScale(float value)
    {
        speedScale = Mathf.Max(0f, value);
    }

    private void Update()
    {
        if (paused)
            return;

        transform.position += Vector3.back * (baseSpeed * speedScale * Time.deltaTime);

        if (transform.position.z < -154f)
            Destroy(gameObject);
    }

    public void TryDamage(PlayerVitals vitals)
    {
        if (vitals == null || paused)
            return;

        int id = vitals.gameObject.GetInstanceID();
        if (!damagedPlayers.Add(id))
            return;

        vitals.TakeDamage(damage);
    }
}
