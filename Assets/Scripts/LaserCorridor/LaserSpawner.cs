using System.Collections.Generic;
using UnityEngine;

public enum LaserPattern
{
    LowSweep,        // ankle-height beam: jump over it
    VerticalSweeper, // upright beam swinging left/right: time your pass
    Diagonal,        // slanted beam: stand under its high end
    GapGrid,         // full grid with one gap: line up with the gap
    LowAndSweeper,   // low beam plus a swinging upright beam
    Scissor,         // two upright beams swinging in opposite directions, crossing in the middle
    Spinner,         // rotating cross of beams in the centre: hug a wall
    MovingGap,       // grid whose gap slides side to side: follow the gap
}

// Sits at the far end of the corridor and fires LaserWaves towards the start.
// Most waves are the complicated moving patterns; single beams are used sparingly.
// Waves come faster and more often the further down the corridor the player gets.
// The spawner's position is the floor centre of the far end.
public class LaserSpawner : MonoBehaviour
{
    static readonly LaserPattern[] ComplexPatterns =
    {
        LaserPattern.LowAndSweeper,
        LaserPattern.Scissor,
        LaserPattern.Spinner,
        LaserPattern.MovingGap,
    };

    static readonly LaserPattern[] SimplePatterns =
    {
        LaserPattern.LowSweep,
        LaserPattern.VerticalSweeper,
        LaserPattern.Diagonal,
        LaserPattern.GapGrid,
    };

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
    float endInterval = 1.4f;

    [SerializeField]
    float startSpeed = 4f;

    [SerializeField]
    float endSpeed = 7.5f;

    [SerializeField]
    [Tooltip("On (re)start, waves already in flight fill the corridor beyond this distance from the start line.")]
    float prewarmClearDistance = 15f;

    [Header("Patterns")]
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("Share of waves that use a complicated moving pattern (the rest are single beams or the static grid).")]
    float complexShare = 0.8f;

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
    LaserPattern lastPattern;

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
        waveCount = 0;
        running = true;
        spawnTimer = startDelay;
        Prewarm();
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

    // Fills the corridor with waves as if the emitter had been firing for a while, so a run
    // doesn't open with a long empty walk. The nearest waves are the easiest ones.
    void Prewarm()
    {
        float spacing = startInterval * startSpeed;
        for (float z = startLineZ + prewarmClearDistance; z < transform.position.z - spacing / 2; z += spacing)
        {
            Spawn(PickPattern(), z);
        }
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

    public LaserPattern PickPattern()
    {
        // The first two waves are always the simplest so the player learns the idea.
        LaserPattern pattern;
        if (waveCount < 2)
        {
            pattern = waveCount == 0 ? LaserPattern.LowSweep : LaserPattern.VerticalSweeper;
        }
        else
        {
            LaserPattern[] group = Random.value < complexShare ? ComplexPatterns : SimplePatterns;
            // Never the same pattern twice in a row (re-picked within the same group, so the
            // complex/simple split stays exact).
            do
            {
                pattern = group[Random.Range(0, group.Length)];
            }
            while (pattern == lastPattern);
        }

        lastPattern = pattern;
        return pattern;
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
        Transform root = wave.transform;

        float halfWidth = corridorWidth / 2;
        float midHeight = corridorHeight / 2;
        float span = corridorWidth - 0.1f;
        float tall = corridorHeight - 0.1f;
        float sweepAmplitude = halfWidth - 0.4f;
        // The grid gap narrows from 1.9 m to 1.6 m (player is 1 m wide) towards the end.
        float gapWidth = Mathf.Lerp(1.9f, 1.6f, Progress);

        switch (pattern)
        {
            case LaserPattern.LowSweep:
                CreateBeam(root, new Vector3(0, 0.4f, 0), new Vector3(span, beamThickness, beamThickness), 0);
                break;

            case LaserPattern.VerticalSweeper:
            {
                Transform beam = CreateBeam(root, new Vector3(0, midHeight, 0), new Vector3(beamThickness, tall, beamThickness), 0);
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
                CreateBeam(root, new Vector3(0, midHeight, 0), new Vector3(length, beamThickness, beamThickness), angle * sign);
                break;
            }

            case LaserPattern.GapGrid:
            {
                float gapCentre = Random.Range(-halfWidth + 1.2f, halfWidth - 1.2f);
                BuildGapGrid(root, -halfWidth, halfWidth, gapCentre, gapWidth);
                break;
            }

            case LaserPattern.LowAndSweeper:
            {
                CreateBeam(root, new Vector3(0, 0.4f, 0), new Vector3(span, beamThickness, beamThickness), 0);
                Transform beam = CreateBeam(root, new Vector3(0, midHeight, 0), new Vector3(beamThickness, tall, beamThickness), 0);
                wave.AddSweeper(beam, sweepAmplitude, 2.6f, Random.value);
                break;
            }

            case LaserPattern.Scissor:
            {
                // Same sweep, half a cycle apart: they meet in the middle and at the walls together.
                float phase = Random.value;
                Transform left = CreateBeam(root, new Vector3(0, midHeight, 0), new Vector3(beamThickness, tall, beamThickness), 0);
                Transform right = CreateBeam(root, new Vector3(0, midHeight, 0), new Vector3(beamThickness, tall, beamThickness), 0);
                wave.AddSweeper(left, sweepAmplitude, 2.8f, phase);
                wave.AddSweeper(right, sweepAmplitude, 2.8f, phase + 0.5f);
                break;
            }

            case LaserPattern.Spinner:
            {
                // A cross of radius 1.6 m: only the strips along the walls are safe.
                const float radius = 1.6f;
                Transform pivot = new GameObject("Spinner").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(0, midHeight, 0);
                CreateBeam(pivot, Vector3.zero, new Vector3(radius * 2, beamThickness, beamThickness), 0);
                CreateBeam(pivot, Vector3.zero, new Vector3(radius * 2, beamThickness, beamThickness), 90);
                wave.AddSpinner(pivot, Random.value < 0.5f ? 80f : -80f);
                break;
            }

            case LaserPattern.MovingGap:
            {
                // Grid built wider than the corridor (the extra hides inside the walls) and
                // slid side to side, so the gap travels between the walls.
                const float travel = 1.3f;
                Transform grid = new GameObject("Grid").transform;
                grid.SetParent(root, false);
                BuildGapGrid(grid, -halfWidth - travel, halfWidth + travel, 0, gapWidth);
                wave.AddSweeper(grid, travel, 3.5f, Random.value);
                break;
            }
        }

        AddGlow(root);
        activeWaves.Add(wave);
        return wave;
    }

    // Vertical and horizontal beams between minX and maxX, except one gapWidth-wide lane.
    void BuildGapGrid(Transform parent, float minX, float maxX, float gapCentre, float gapWidth)
    {
        const float spacing = 0.6f;
        float gapLeft = gapCentre - gapWidth / 2;
        float gapRight = gapCentre + gapWidth / 2;
        float tall = corridorHeight - 0.1f;

        for (float x = minX + 0.3f; x < maxX; x += spacing)
        {
            if (x > gapLeft && x < gapRight)
            {
                continue;
            }
            CreateBeam(parent, new Vector3(x, corridorHeight / 2, 0), new Vector3(beamThickness, tall, beamThickness), 0);
        }

        float leftSpan = gapLeft - (minX + 0.05f);
        float rightSpan = (maxX - 0.05f) - gapRight;
        for (float y = 0.4f; y < corridorHeight; y += 0.8f)
        {
            if (leftSpan > 0.05f)
            {
                CreateBeam(parent, new Vector3(gapLeft - leftSpan / 2, y, 0), new Vector3(leftSpan, beamThickness, beamThickness), 0);
            }
            if (rightSpan > 0.05f)
            {
                CreateBeam(parent, new Vector3(gapRight + rightSpan / 2, y, 0), new Vector3(rightSpan, beamThickness, beamThickness), 0);
            }
        }
    }

    Transform CreateBeam(Transform parent, Vector3 localPosition, Vector3 size, float zAngle)
    {
        GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
        beam.name = "Beam";
        beam.transform.SetParent(parent, false);
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
