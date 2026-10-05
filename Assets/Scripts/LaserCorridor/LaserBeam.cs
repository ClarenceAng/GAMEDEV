using UnityEngine;

// One damaging beam inside a LaserWave. The wave's kinematic Rigidbody makes this trigger
// report overlaps with the player's CharacterController even while the player stands still.
[RequireComponent(typeof(BoxCollider))]
public class LaserBeam : MonoBehaviour
{
    [SerializeField]
    int damage = 25;

    public void Configure(int beamDamage)
    {
        damage = beamDamage;
    }

    void OnTriggerEnter(Collider other)
    {
        TryHit(other);
    }

    // Stay as well as Enter so a player still inside the beam is hit again once the
    // post-hit invulnerability window runs out.
    void OnTriggerStay(Collider other)
    {
        TryHit(other);
    }

    void TryHit(Collider other)
    {
        if (other.TryGetComponent(out PlayerHealth health))
        {
            health.TakeDamage(damage);
        }
    }
}
