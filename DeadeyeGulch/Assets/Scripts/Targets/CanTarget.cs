using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// A tin can that real physics knocks about. It counts as down once it tips over, drops or slides off its perch,
    /// whatever caused it: a bullet, or another can falling on it.
    ///
    /// A tossed can (Ambush) is different: it only scores if a bullet catches it in the air, and once it lands it is dead.
    /// </summary>
    public sealed class CanTarget : KnockdownTarget
    {
        public const float Diameter = 0.2f;
        public const float Height = 0.3f;
        public const int Points = 50;
        public const int TrickShotPoints = 250;

        const float TippedDot = 0.6f;      // up-vector dot below this = on its side (about 53 degrees)
        const float DropDistance = 0.2f;
        const float SlideDistance = 0.3f;
        const float LandedLifetime = 1f;

        Rigidbody rb;
        Vector3 home;
        bool tossed, landed;
        float landedAge;

        public bool Tossed => tossed;
        public bool Landed => landed;
        public Rigidbody Body => rb;

        public override bool IsLive => !IsDown && !(tossed && landed);
        // The collider follows the physics pose, which runs up to one step ahead of the interpolated mesh.
        public override Vector3 AimPoint => rb != null ? rb.position : transform.position;
        public override Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;

        /// <summary>Builds a can standing with its base at <paramref name="basePos"/> (world space).</summary>
        public static CanTarget Create(Transform parent, Vector3 basePos, ArtSet art, Quaternion? rot = null)
        {
            var go = Shapes.Cylinder(parent, "Can", Vector3.zero, Diameter, Height, art.can, Hitbox.ConvexMesh).gameObject;
            go.transform.position = basePos + Vector3.up * (Height * 0.5f);
            go.transform.rotation = rot ?? Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            // The band is a child of a scaled cylinder, so its local size is relative to the can (1 = can size).
            Shapes.Cylinder(go.transform, "Band", Vector3.zero, 1.02f, 1.2f, art.canBand, Hitbox.None);
            Shapes.Cylinder(go.transform, "Lid", new Vector3(0f, 0.99f, 0f), 0.9f, 0.04f, art.iron, Hitbox.None);

            var rb = go.AddComponent<Rigidbody>();
            rb.mass = 0.35f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.4f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            var can = go.AddComponent<CanTarget>();
            can.points = Points;
            return can;
        }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            home = transform.position;
        }

        void Start()
        {
            home = transform.position;
            if (!tossed) rb.Sleep(); // stacked cans start at rest instead of jittering into place
        }

        /// <summary>Throws the can into the air as a trick-shot target.</summary>
        public void Toss(Vector3 velocity, Vector3 spin)
        {
            tossed = true;
            points = TrickShotPoints;
            bonusLabel = "TRICK SHOT";
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.linearVelocity = velocity;
            rb.angularVelocity = spin;
        }

        public override void TakeHit(Vector3 point, Vector3 dir)
        {
            if (Game != null) Game.Sfx.Clang();
            if (tossed && !landed) KnockDown();
        }

        void FixedUpdate()
        {
            if (IsDown || tossed || rb.IsSleeping()) return;
            Vector3 p = transform.position;
            Vector3 flat = p - home;
            flat.y = 0f;
            bool tipped = Vector3.Dot(transform.up, Vector3.up) < TippedDot;
            bool dropped = p.y < home.y - DropDistance;
            bool slid = flat.magnitude > SlideDistance;
            if (tipped || dropped || slid) KnockDown();
        }

        protected override void Update()
        {
            base.Update();
            if (!landed) return;
            landedAge += Time.deltaTime;
            if (landedAge > LandedLifetime) Destroy(gameObject);
        }

        void OnCollisionEnter(Collision c)
        {
            if (tossed && !landed) landed = true;
        }
    }
}
