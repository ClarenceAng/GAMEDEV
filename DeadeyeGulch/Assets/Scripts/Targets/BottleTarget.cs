using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// A glass bottle. A bullet shatters it on the spot; so does a hard knock or a fall (it tipped over and broke).
    /// Hanging bottles are kinematic and only a bullet can break them; <see cref="Restore"/> hangs a fresh one.
    /// </summary>
    public sealed class BottleTarget : KnockdownTarget
    {
        public const float Diameter = 0.17f;
        public const float BodyHeight = 0.3f;
        public const float Height = 0.46f;
        public const int Points = 75;

        const float BreakSpeed = 2.5f;
        const float TippedDot = 0.5f;
        const int ShardCount = 10;
        const float ShardLifetime = 2.5f;

        Rigidbody rb;
        Renderer[] renderers;
        Collider[] colliders;
        Material glass;

        public override Vector3 AimPoint => transform.TransformPoint(new Vector3(0f, BodyHeight * 0.5f, 0f));

        /// <summary>Builds a bottle standing with its base at <paramref name="basePos"/> (world space).</summary>
        public static BottleTarget Create(Transform parent, Vector3 basePos, ArtSet art, bool amber, bool hanging = false)
        {
            Material mat = amber ? art.bottleAmber : art.bottleGreen;
            var root = new GameObject("Bottle");
            root.transform.SetParent(parent, false);
            root.transform.position = basePos;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Shapes.Cylinder(root.transform, "Body", new Vector3(0f, BodyHeight * 0.5f, 0f), Diameter, BodyHeight, mat, Hitbox.ConvexMesh);
            Shapes.Sphere(root.transform, "Shoulder", new Vector3(0f, BodyHeight, 0f), new Vector3(Diameter, 0.1f, Diameter), mat, Hitbox.None);
            Shapes.Cylinder(root.transform, "Neck", new Vector3(0f, BodyHeight + 0.08f, 0f), 0.06f, 0.16f, mat, Hitbox.Box);
            Shapes.Cylinder(root.transform, "Cork", new Vector3(0f, Height - 0.01f, 0f), 0.05f, 0.03f, art.wood, Hitbox.None);

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.4f;
            // A hanging bottle is moved by its rope's transform, so interpolation would only drag it behind its collider.
            rb.interpolation = hanging ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
            rb.isKinematic = hanging;

            var bottle = root.AddComponent<BottleTarget>();
            bottle.points = Points;
            bottle.glass = mat;
            return bottle;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            renderers = GetComponentsInChildren<Renderer>();
            colliders = GetComponentsInChildren<Collider>();
        }

        void Start()
        {
            if (!rb.isKinematic) rb.Sleep();
        }

        public override void TakeHit(Vector3 point, Vector3 dir) => Shatter(dir);

        void OnCollisionEnter(Collision c)
        {
            if (!rb.isKinematic && c.relativeVelocity.magnitude > BreakSpeed) Shatter(-c.relativeVelocity.normalized);
        }

        void FixedUpdate()
        {
            if (IsDown || rb.isKinematic || rb.IsSleeping()) return;
            if (Vector3.Dot(transform.up, Vector3.up) < TippedDot) Shatter(Vector3.zero);
        }

        void Shatter(Vector3 dir)
        {
            if (IsDown) return;
            if (Game != null) Game.Sfx.Glass();
            Vector3 centre = AimPoint;
            for (int i = 0; i < ShardCount; i++)
            {
                var s = Shapes.Box(null, "Shard", centre + Random.insideUnitSphere * 0.08f, Vector3.one * Random.Range(0.03f, 0.06f), glass, Hitbox.Box, Random.rotation);
                Shapes.SetLayer(s, Shapes.NoHitLayer);
                var srb = s.AddComponent<Rigidbody>();
                srb.mass = 0.02f;
                srb.linearVelocity = dir * Random.Range(2f, 5f) + Random.insideUnitSphere * 2.5f + Vector3.up * 1.5f;
                srb.angularVelocity = Random.insideUnitSphere * 20f;
                Destroy(s, ShardLifetime);
            }
            Show(false);
            KnockDown();
        }

        /// <summary>Puts an unbroken bottle back (hanging bottles in Ambush).</summary>
        public void Restore()
        {
            Show(true);
            Revive();
        }

        void Show(bool on)
        {
            foreach (var r in renderers) r.enabled = on;
            foreach (var c in colliders) c.enabled = on;
            if (!on && !rb.isKinematic)
            {
                // A broken standing bottle is gone for good: freeze what is left so nothing invisible rolls around.
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }
    }
}
