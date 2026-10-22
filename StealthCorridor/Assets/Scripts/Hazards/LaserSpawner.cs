using UnityEngine;

// The lasers from the laser corridor. Sits at the far end of the laser part of the corridor
// and fires wall to wall lasers back towards the start.
public class LaserSpawner : MonoBehaviour
{
    public GameObject laserPrefab;

    // corridor
    public float corridorWidth = 10f;
    public float travelDistance = 16f; // how far the lasers fly before they disappear

    // laser shapes
    public float diagonalChance = 0.5f;
    public Vector2 lowEndHeight = new Vector2(0f, 0.4f);   // diagonal low end (jump over it)
    public Vector2 highEndHeight = new Vector2(2.4f, 3f);  // diagonal high end (walk under it)
    public Vector2 flatHeight = new Vector2(0.3f, 0.6f);   // flat lasers, low enough to jump

    // laser movement
    public float spawnInterval = 2.5f;
    public float laserSpeed = 5f;
    public float thicknessMultiplier = 3f; // makes the lasers thicker so you can see them

    float timer;

    void Start()
    {
        // put some lasers in already so it isnt empty at the start
        float spacing = laserSpeed * spawnInterval;
        for (float z = transform.position.z - spacing; z > transform.position.z - travelDistance; z = z - spacing)
        {
            SpawnAt(z);
        }
    }

    void Update()
    {
        timer = timer - Time.deltaTime;

        if (timer <= 0f)
        {
            timer = spawnInterval;
            SpawnAt(transform.position.z);
        }
    }

    void SpawnAt(float z)
    {
        // the laser goes from one wall to the other wall
        float startY;
        float endY;

        if (Random.value < diagonalChance)
        {
            // diagonal laser spawning logic
            startY = Random.Range(lowEndHeight.x, lowEndHeight.y);
            endY = Random.Range(highEndHeight.x, highEndHeight.y);
        }
        else
        {
            // flat laser spawning logic
            startY = Random.Range(flatHeight.x, flatHeight.y);
            endY = startY;
        }

        // flip it half of the time so its not always the same side
        if (Random.value < 0.5f)
        {
            float temp = startY;
            startY = endY;
            endY = temp;
        }

        // work out how long the laser is and how much it tilts
        Vector2 beam = new Vector2(corridorWidth, endY - startY);
        float length = beam.magnitude;
        float angle = Mathf.Atan2(beam.y, beam.x) * Mathf.Rad2Deg;

        float middleY = transform.position.y + (startY + endY) / 2f;
        Vector3 position = new Vector3(transform.position.x, middleY, z);

        // the capsule is tall along its Y axis so we rotate it sideways (-90)
        Quaternion rotation = Quaternion.Euler(0f, 0f, angle - 90f);
        GameObject laser = Instantiate(laserPrefab, position, rotation, transform);

        // the capsule is 2 units tall so Y scale is half the length
        Vector3 scale = laser.transform.localScale;
        laser.transform.localScale = new Vector3(scale.x * thicknessMultiplier, length / 2f, scale.z * thicknessMultiplier);

        LaserProjectile projectile = laser.GetComponent<LaserProjectile>();
        float despawnZ = transform.position.z - travelDistance;
        projectile.Launch(Vector3.back, laserSpeed, despawnZ);
    }
}
