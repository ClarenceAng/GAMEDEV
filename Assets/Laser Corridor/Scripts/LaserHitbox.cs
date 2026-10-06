using UnityEngine;

public class LaserHitbox : MonoBehaviour
{
    private LaserObstacle owner;

    public void Configure(LaserObstacle obstacle)
    {
        owner = obstacle;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        PlayerVitals vitals = other.GetComponent<PlayerVitals>();
        if (vitals != null && owner != null)
            owner.TryDamage(vitals);
    }
}
