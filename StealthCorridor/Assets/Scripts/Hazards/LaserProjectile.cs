using UnityEngine;

// Goes on each laser. Moves it towards the start and checks if the beam touches the player.
public class LaserProjectile : MonoBehaviour
{
    Vector3 direction = Vector3.back;
    float speed = 5f;
    float despawnZ = -1000f;

    StealthGame game;
    LayerMask playerLayer;

    void Start()
    {
        game = FindFirstObjectByType<StealthGame>();
        playerLayer = LayerMask.GetMask("Player");
    }

    // called by the spawner right after it makes the laser
    public void Launch(Vector3 newDirection, float newSpeed, float newDespawnZ)
    {
        direction = newDirection.normalized;
        speed = newSpeed;
        despawnZ = newDespawnZ;
    }

    void Update()
    {
        transform.position = transform.position + direction * speed * Time.deltaTime;

        // delete the laser once its past the end of the laser part
        if (transform.position.z < despawnZ)
        {
            Destroy(gameObject);
            return;
        }

        // the laser is a long capsule, so check a capsule from one end of the beam to the other
        Vector3 end1 = transform.position + transform.up * transform.localScale.y;
        Vector3 end2 = transform.position - transform.up * transform.localScale.y;
        float radius = transform.localScale.x / 2f;

        if (Physics.CheckCapsule(end1, end2, radius, playerLayer))
        {
            game.PlayerHit("a laser (Capsule check)");
        }
    }
}
