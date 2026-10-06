using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LaserSpawner : MonoBehaviour
{
    private Material laserMaterial;
    private Transform player;
    private Coroutine spawnRoutine;
    private readonly List<LaserObstacle> activeLasers = new List<LaserObstacle>();

    private bool paused;
    private float laserSpeedScale = 1f;
    private float intervalScale = 1f;
    private int lastPattern = -1;

    private const float SpawnZ = 147.5f;
    private const float StartZ = -146f;
    private const float GoalZ = 143.5f;
    private const float InnerWidth = 10.05f;
    private const float FloorTopY = 0.50f;
    private const float CeilingBottomY = 8.07f;

    public void Configure(Material material, Transform playerTransform)
    {
        laserMaterial = material;
        player = playerTransform;
    }

    public void StartSpawning()
    {
        StopSpawning();
        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine != null)
        {
            StopCoroutine(spawnRoutine);
            spawnRoutine = null;
        }
    }

    public void SetPaused(bool value)
    {
        paused = value;
        CleanupList();
        foreach (LaserObstacle laser in activeLasers)
        {
            if (laser != null)
                laser.SetPaused(value);
        }
    }

    public void SetSlowMode(bool active)
    {
        laserSpeedScale = active ? 0.55f : 1f;
        intervalScale = active ? 1.5f : 1f;

        CleanupList();
        foreach (LaserObstacle laser in activeLasers)
        {
            if (laser != null)
                laser.SetSpeedScale(laserSpeedScale);
        }
    }

    public void ClearLasers()
    {
        CleanupList();
        foreach (LaserObstacle laser in activeLasers)
        {
            if (laser != null)
                Destroy(laser.gameObject);
        }
        activeLasers.Clear();
    }

    private IEnumerator SpawnLoop()
    {
        // A short initial burst makes the long corridor feel dangerous sooner,
        // while every obstacle still originates from the far end as required.
        yield return new WaitForSeconds(0.55f);
        for (int i = 0; i < 3; i++)
        {
            if (!paused)
                SpawnRandomPattern();
            yield return new WaitForSeconds(0.48f);
        }

        while (true)
        {
            if (!paused)
                SpawnRandomPattern();

            float progress = GetPlayerProgress();
            float difficultyIntervalScale = Mathf.Lerp(1f, 0.68f, progress);
            float wait = Random.Range(1.12f, 1.52f) * intervalScale * difficultyIntervalScale;
            yield return new WaitForSeconds(wait);
        }
    }

    private float GetPlayerProgress()
    {
        if (player == null)
            return 0f;

        return Mathf.InverseLerp(StartZ, GoalZ, player.position.z);
    }

    private void SpawnRandomPattern()
    {
        int pattern;
        do
        {
            pattern = Random.Range(0, 12);
        }
        while (pattern == lastPattern && Random.value > 0.15f);

        lastPattern = pattern;

        GameObject root = new GameObject("LaserPattern_" + pattern);
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(0f, 0f, SpawnZ);

        float progress = GetPlayerProgress();
        float speed = Mathf.Lerp(Random.Range(17.5f, 19.5f), Random.Range(21f, 23.5f), progress);

        LaserObstacle obstacle = root.AddComponent<LaserObstacle>();
        obstacle.Configure(speed, 25);
        obstacle.SetPaused(paused);
        obstacle.SetSpeedScale(laserSpeedScale);

        switch (pattern)
        {
            // Full-width low beam: jump it.
            case 0:
                CreateHorizontalBeam(root.transform, 0.95f, 0f, obstacle);
                break;

            // Full-height vertical beams: sidestep.
            case 1:
                CreateVerticalBeam(root.transform, -2.55f, 0f, obstacle);
                break;
            case 2:
                CreateVerticalBeam(root.transform, 2.55f, 0f, obstacle);
                break;
            case 3:
                CreateVerticalBeam(root.transform, 0f, 0f, obstacle);
                break;

            // Wall-to-wall diagonals. The angle changes each spawn.
            case 4:
                CreateDiagonalBeam(root.transform, Random.Range(18f, 31f), 0f, obstacle);
                break;
            case 5:
                CreateDiagonalBeam(root.transform, -Random.Range(18f, 31f), 0f, obstacle);
                break;

            // High beam: keep moving underneath it.
            case 6:
                CreateHorizontalBeam(root.transform, 4.75f, 0f, obstacle);
                break;

            // Two full-height columns with a central escape lane.
            case 7:
                CreateVerticalBeam(root.transform, -2.75f, 0f, obstacle);
                CreateVerticalBeam(root.transform, 2.75f, 0f, obstacle);
                break;

            // Jump then immediately choose a side.
            case 8:
                CreateHorizontalBeam(root.transform, 0.92f, -2.8f, obstacle);
                CreateVerticalBeam(root.transform, Random.value > 0.5f ? -2.3f : 2.3f, 2.8f, obstacle);
                break;

            // Two opposite diagonals separated in depth.
            case 9:
                CreateDiagonalBeam(root.transform, Random.Range(20f, 29f), -2.7f, obstacle);
                CreateDiagonalBeam(root.transform, -Random.Range(20f, 29f), 2.7f, obstacle);
                break;

            // Jump, then read a diagonal.
            case 10:
                CreateHorizontalBeam(root.transform, 1.0f, -3.0f, obstacle);
                CreateDiagonalBeam(root.transform, Random.Range(20f, 30f) * (Random.value > 0.5f ? 1f : -1f), 3.0f, obstacle);
                break;

            // Double-jump rhythm.
            default:
                CreateHorizontalBeam(root.transform, 0.92f, -2.5f, obstacle);
                CreateHorizontalBeam(root.transform, 1.15f, 2.5f, obstacle);
                break;
        }

        activeLasers.Add(obstacle);
    }

    private void CreateHorizontalBeam(Transform parent, float centerY, float localZ, LaserObstacle owner)
    {
        CreateBeam(parent, new Vector3(0f, centerY, localZ), new Vector3(InnerWidth + 0.18f, 0.24f, 0.30f), 0f, owner);
    }

    private void CreateVerticalBeam(Transform parent, float centerX, float localZ, LaserObstacle owner)
    {
        float height = CeilingBottomY - FloorTopY + 0.18f;
        float centerY = (CeilingBottomY + FloorTopY) * 0.5f;
        CreateBeam(parent, new Vector3(centerX, centerY, localZ), new Vector3(0.28f, height, 0.30f), 0f, owner);
    }

    private void CreateDiagonalBeam(Transform parent, float angleDegrees, float localZ, LaserObstacle owner)
    {
        float radians = Mathf.Abs(angleDegrees) * Mathf.Deg2Rad;
        float projectedHalfHeight = (InnerWidth * 0.5f) * Mathf.Tan(radians);
        float minCenter = FloorTopY + 0.48f + projectedHalfHeight;
        float maxCenter = CeilingBottomY - 0.48f - projectedHalfHeight;
        float centerY = minCenter <= maxCenter
            ? Random.Range(minCenter, maxCenter)
            : (FloorTopY + CeilingBottomY) * 0.5f;

        // Compensate for the rotation so the beam's endpoints overlap both corridor walls.
        float length = (InnerWidth + 0.22f) / Mathf.Max(0.2f, Mathf.Cos(radians));
        CreateBeam(parent, new Vector3(0f, centerY, localZ), new Vector3(length, 0.24f, 0.30f), angleDegrees, owner);
    }

    private void CreateBeam(Transform parent, Vector3 localPosition, Vector3 scale, float zRotation, LaserObstacle owner)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "LaserBeam";
        beam.transform.SetParent(parent, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        beam.transform.localScale = scale;

        MeshRenderer renderer = beam.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = laserMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        BoxCollider collider = beam.GetComponent<BoxCollider>();
        collider.isTrigger = true;

        LaserHitbox hitbox = beam.AddComponent<LaserHitbox>();
        hitbox.Configure(owner);

        GameObject glow = new GameObject("LaserGlow");
        glow.transform.SetParent(beam.transform, false);
        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.05f, 0.03f);
        light.range = 3.0f;
        light.intensity = 3.3f;
        light.shadows = LightShadows.None;
    }

    private void CleanupList()
    {
        activeLasers.RemoveAll(item => item == null);
    }
}
