using System.Collections.Generic;
using UnityEngine;

// A group of beams travelling down the corridor towards the start (-Z). Individual beams can
// also sweep side to side. Speed is scaled by the spawner's SpeedMultiplier (slow-mo bonus).
[RequireComponent(typeof(Rigidbody))]
public class LaserWave : MonoBehaviour
{
    struct Sweeper
    {
        public Transform beam;
        public float centreX;
        public float amplitude;
        public float period;
        public float phase;
    }

    struct Spinner
    {
        public Transform pivot;
        public float degreesPerSecond;
    }

    readonly List<Sweeper> sweepers = new List<Sweeper>();
    readonly List<Spinner> spinners = new List<Spinner>();

    LaserSpawner spawner;
    Rigidbody body;
    float speed;
    float despawnZ;
    float age;

    public LaserSpawner Spawner => spawner;

    public void Launch(LaserSpawner owner, float travelSpeed, float despawnAtZ)
    {
        spawner = owner;
        speed = travelSpeed;
        despawnZ = despawnAtZ;
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    public void AddSweeper(Transform beam, float amplitude, float period, float phase)
    {
        sweepers.Add(new Sweeper
        {
            beam = beam,
            centreX = beam.localPosition.x,
            amplitude = amplitude,
            period = period,
            phase = phase,
        });
        UpdateSweepers();
    }

    // Rotates `pivot` around the corridor axis (Z).
    public void AddSpinner(Transform pivot, float degreesPerSecond)
    {
        spinners.Add(new Spinner { pivot = pivot, degreesPerSecond = degreesPerSecond });
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime * spawner.SpeedMultiplier;
        age += dt;

        Vector3 position = body.position;
        position.z -= speed * dt;
        body.MovePosition(position);
        UpdateSweepers();

        if (position.z < despawnZ)
        {
            spawner.Despawn(this);
        }
    }

    void UpdateSweepers()
    {
        foreach (Sweeper sweeper in sweepers)
        {
            Vector3 local = sweeper.beam.localPosition;
            local.x = sweeper.centreX + Mathf.Sin((age / sweeper.period + sweeper.phase) * Mathf.PI * 2) * sweeper.amplitude;
            sweeper.beam.localPosition = local;
        }
        foreach (Spinner spinner in spinners)
        {
            spinner.pivot.localRotation = Quaternion.Euler(0, 0, age * spinner.degreesPerSecond);
        }
    }
}
