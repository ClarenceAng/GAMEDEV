using System.Collections.Generic;
using UnityEngine;

public enum LaserPattern
{
    LowSweep,        // ankle-height beam: jump over it
    VerticalSweeper, // upright beam swinging left/right: time your pass
    Diagonal,        // slanted beam: stand under its high end
    GapGrid,         // full grid with one gap: line up with the gap
    LowAndSweeper,   // low beam plus a swinging upright beam
}

// Sits at the far end of the corridor and fires LaserWaves towards the start.
// Waves come faster and more often the further down the corridor the player gets.
// The spawner's position is the floor centre of the corridor's far end.
public class LaserSpawner : MonoBehaviour
{
    [Header("Corridor")]
    [SerializeField]
    float corridorWidth = 6f;

    [SerializeField]
    float corridorHeight = 4f;

    [SerializeField]
    [Tooltip("Waves are destroyed once they pass this world Z (behind the start).")]
    float despawnZ = -4f;

    [SerializeField]
    [Tooltip("Used to ramp difficulty: 0 at the start line, 1 at the spawner.")]
    Transform player;

    [SerializeField]
    float startLineZ = 0f;

    [Header("Timing")]
    [SerializeField]
    float startDelay = 1.5f;

    [SerializeField]
    float startInterval = 2.8f;

    [SerializeField]
    float endInterval = 1.7f;

    [SerializeField]
    float startSpeed = 4f;

    [SerializeField]
    float endSpeed = 6.5f;

    [Header("Beams")]
    [SerializeField]
    Material beamMaterial;

    [SerializeField]
    Color glowColor = new Color(1f, 0.1f, 0.1f);

    [SerializeField]
    float beamThickness = 0.06f;

    [SerializeField]
    int beamDamage = 25;

    readonly List<LaserWave> activeWaves = new List<LaserWave>();

    bool running;
    float spawnTimer;
    int waveCount;

    public float SpeedMultiplier { get; set; } = 1f;
    public bool IsRunning => running;
    public IReadOnlyList<LaserWave> ActiveWaves => activeWaves;

    // 0 at the start line, 1 at the far end.
    public float Progress
    {
        get
        {
            if (player == null)
            {
                return 0;
            }
            return Mathf.InverseLerp(startLineZ, transform.position.z, player.position.z);
        }
    }

    public void Begin()
    {
        ClearAll();
        SpeedMultiplier = 1f;
        running = true;
        spawnTimer = startDelay;
    }

    public void Stop()
    {
        running = false;
    }

    public void ClearAll()
    {
        foreach (LaserWave wave in activeWaves)
        {
            if (wave != null)
            {
                Destroy(wave.gameObject);
            }
        }
        activeWaves.Clear();
    }

    public void Despawn(LaserWave wave)
    {
        activeWaves.Remove(wave);
        Destroy(wave.gameObject);
    }

    void Update()
    {
        if (!running)
        {
            return;
        }

        // Scaled by SpeedMultiplier so slow-mo also spaces out new waves.
        spawnTimer -= Time.deltaTime * SpeedMultiplier;
        if (spawnTimer <= 0)
        {
            Spawn(PickPattern());
            spawnTimer = Mathf.Lerp(startInterval, endInterval, Progress);
        }
    }

    LaserPattern PickPattern()
    {
        float progress = Progress;
        // The first two waves are always the simplest so the player learns the idea.
        if (waveCount < 2)
        {
            return waveCount == 0 ? LaserPattern.LowSweep : LaserPattern.VerticalSweeper;
        }

        int available = 3;
        if (progress > 0.25f)
        {
            available = 4;
        }
        if (progress > 0.5f)
        {
            available = 5;
        }
        return (LaserPattern)Random.Range(0, available);
    }

    public LaserWave Spawn(LaserPattern pattern)
    {
        return Spawn(pattern, transform.position.z);
    }

    public LaserWave Spawn(LaserPattern pattern, float atZ)
    {
        waveCount++;
        GameObject waveObject = new GameObject($"Laser Wave {waveCount} ({pattern})");
        waveObject.transform.position = new Vector3(transform.position.x, transform.position.y, atZ);
        LaserWave wave = waveObject.AddComponent<LaserWave>();
        wave.Launch(this, Mathf.Lerp(startSpeed, endSpeed, Progress), despawnZ);

        float halfWidth = corridorWidth / 2;
        float span = corridorWidth - 0.1f;
        float tall = corridorHeight - 0.1f;
        float sweepAmplitude = halfWidth - 0.4f;

        switch (pattern)
        {
            case LaserPattern.LowSweep:
                CreateBeam(wave, new Vector3(0, 0.4f, 0), new Vector3(span, beamThickness, beamThickness), 0);
                break;

            case LaserPattern.VerticalSweeper:
            {
                Transform beam = CreateBeam(wave, new Vector3(0, corridorHeight / 2, 0), new Vector3(beamThickness, tall, beamThickness), 0);
                wave.AddSweeper(beam, sweepAmplitude, 3f, Random.value);
                break;
            }

            case LaserPattern.Diagonal:
            {
                // Low end 0.2 above the floor at one wall, high end 0.2 below the ceiling at the other.
                float rise = corridorHeight - 0.4f;
                float angle = Mathf.Atan2(rise, corridorWidth) * Mathf.Rad2Deg;
                float length = Mathf.Sqrt(corridorWidth * corridorWidth + rise * rise) - 0.15f;
                float sign = Random.value < 0.5f ? 1 : -1;
                CreateBeam(wave, new Vector3(0, corridorHeight / 2, 0), new Vector3(length, beamThickness, beamThickness), angle * sign);
                break;
            }

            case LaserPattern.GapGrid:
                BuildGapGrid(wave, halfWidth);
                break;

            case LaserPattern.LowAndSweeper:
            {
                CreateBeam(wave, new Vector3(0, 0.4f, 0), new Vector3(span, beamThickness, beamThickness), 0);
                Transform beam = CreateBeam(wave, new Vector3(0, corridorHeight / 2, 0), new Vector3(beamThickness, tall, beamThickness), 0);
                wave.AddSweeper(beam, sweepAmplitude, 2.6f, Random.value);
                break;
            }
        }

        AddGlow(wave.transform);
        activeWaves.Add(wave);
        return wave;
    }

    // Vertical and horizontal beams over the whole cross-section except one 1.9 m wide lane.
    void BuildGapGrid(LaserWave wave, float halfWidth)
    {
        const float gapWidth = 1.9f;
        const float spacing = 0.6f;
        float gapCentre = Random.Range(-halfWidth + 1.2f, halfWidth - 1.2f);
        float gapLeft = gapCentre - gapWidth / 2;
        float gapRight = gapCentre + gapWidth / 2;
        float tall = corridorHeight - 0.1f;

        for (float x = -halfWidth + 0.3f; x < halfWidth; x += spacing)
        {
            if (x > gapLeft && x < gapRight)
            {
                continue;
            }
            CreateBeam(wave, new Vector3(x, corridorHeight / 2, 0), new Vector3(beamThickness, tall, beamThickness), 0);
        }

        float leftSpan = gapLeft - (-halfWidth + 0.05f);
        float rightSpan = (halfWidth - 0.05f) - gapRight;
        for (float y = 0.4f; y < corridorHeight; y += 0.8f)
        {
            if (leftSpan > 0.05f)
            {
                CreateBeam(wave, new Vector3(gapLeft - leftSpan / 2, y, 0), new Vector3(leftSpan, beamThickness, beamThickness), 0);
            }
            if (rightSpan > 0.05f)
            {
                CreateBeam(wave, new Vector3(gapRight + rightSpan / 2, y, 0), new Vector3(rightSpan, beamThickness, beamThickness), 0);
            }
        }
    }

    Transform CreateBeam(LaserWave wave, Vector3 localPosition, Vector3 size, float zAngle)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Beam";
        beam.transform.SetParent(wave.transform, false);
        beam.transform.localPosition = localPosition;
        beam.transform.localRotation = Quaternion.Euler(0, 0, zAngle);
        beam.transform.localScale = size;

        MeshRenderer meshRenderer = beam.GetComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = beamMaterial;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Fatten the hitbox across the beam's thin axes so grazing a beam still counts.
        const float hitboxScale = 2.5f;
        BoxCollider hitbox = beam.GetComponent<BoxCollider>();
        hitbox.isTrigger = true;
        hitbox.size = new Vector3(
            size.x <= beamThickness ? hitboxScale : 1,
            size.y <= beamThickness ? hitboxScale : 1,
            size.z <= beamThickness ? hitboxScale : 1);

        beam.AddComponent<LaserBeam>().Configure(beamDamage);
        return beam.transform;
    }

    void AddGlow(Transform wave)
    {
        GameObject glow = new GameObject("Glow");
        glow.transform.SetParent(wave, false);
        glow.transform.localPosition = new Vector3(0, corridorHeight / 2, 0);
        Light light = glow.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = glowColor;
        light.range = 5f;
        light.intensity = 3f;
        light.shadows = LightShadows.None;
    }
}
