using UnityEngine;

public class LaserProjectile : MonoBehaviour
{
    Vector3 direction = Vector3.back;
    float speed = 8f;
    int damage = 20;
    float despawnZ = -1000f;

    void Awake()
    {
        // the laser has to go through the player so its a trigger
        GetComponent<Collider>().isTrigger = true;

        Rigidbody body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    // called by the spawner right after it makes the laser
    public void Launch(Vector3 newDirection, float newSpeed, int newDamage, float newDespawnZ)
    {
        direction = newDirection.normalized;
        speed = newSpeed;
        damage = newDamage;
        despawnZ = newDespawnZ;
    }

    void Update()
    {
        transform.position += direction * speed * Time.deltaTime;

        // delete the laser once its past the start of the corridor
        if (transform.position.z < despawnZ)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        PlayerHealth health = other.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
        }
    }
}
