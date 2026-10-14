using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>
    /// A real, fast-moving projectile (not an instant hitscan). Each frame it casts a ray along the stretch it is about to
    /// cover, so even at 180 m/s it can't skip through a thin can between frames. It pushes whatever rigidbody it strikes
    /// and reports exactly one outcome to the game: a hit (with the target) or a miss.
    ///
    /// The bullet really travels down the crosshair line from the eye, so it lands exactly where you aimed at any range
    /// (leading a moving target included). Only what you see starts at the gun's muzzle: the drawn slug and tracer are
    /// offset towards the muzzle and slide onto the true path over the first few metres.
    /// </summary>
    public sealed class Bullet : MonoBehaviour
    {
        const float Lifetime = 1.5f;
        const float Radius = 0.025f;
        const float ConvergeDistance = 6f;

        KnockdownGame game;
        Vector3 position, velocity, muzzleOffset;
        float impulse, age, travelled;
        bool done;

        /// <param name="origin">Start of the true path (the camera).</param>
        /// <param name="muzzle">Where the shot appears to come from.</param>
        public static Bullet Fire(KnockdownGame game, Vector3 origin, Vector3 dir, Vector3 muzzle, float speed, float impulse)
        {
            var go = new GameObject("Bullet");
            go.layer = Shapes.NoHitLayer;
            go.transform.SetPositionAndRotation(muzzle, Quaternion.LookRotation(dir));

            var slug = Shapes.Capsule(go.transform, "Slug", Vector3.zero, new Vector3(0.03f, 0.12f, 0.03f), game.art.tracer, Hitbox.None, Quaternion.Euler(90f, 0f, 0f));
            Shapes.SetLayer(slug, Shapes.NoHitLayer);

            var trail = go.AddComponent<TrailRenderer>();
            trail.sharedMaterial = game.art.tracer;
            trail.time = 0.04f;
            trail.startWidth = 0.035f;
            trail.endWidth = 0f;
            trail.minVertexDistance = 0.2f;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            Shapes.NoShadows(go);

            var b = go.AddComponent<Bullet>();
            b.game = game;
            b.position = origin;
            b.muzzleOffset = muzzle - origin;
            b.velocity = dir.normalized * speed;
            b.impulse = impulse;
            return b;
        }

        void Update()
        {
            if (done) return;
            float dt = Time.deltaTime;
            age += dt;
            Vector3 step = velocity * dt;

            // Moving targets are driven through their transforms; make sure their colliders are where they are drawn.
            Physics.SyncTransforms();
            if (Physics.SphereCast(position, Radius, velocity, out RaycastHit hit, step.magnitude, Shapes.BulletMask, QueryTriggerInteraction.Ignore))
            {
                Impact(hit);
                return;
            }

            position += step;
            travelled += step.magnitude;
            transform.position = position + muzzleOffset * Mathf.Clamp01(1f - travelled / ConvergeDistance);
            if (age > Lifetime) Finish(null, Vector3.zero);
        }

        void Impact(RaycastHit hit)
        {
            transform.position = hit.point;
            Vector3 dir = velocity.normalized;
            Rigidbody rb = hit.rigidbody;
            if (rb != null && !rb.isKinematic) rb.AddForceAtPosition(dir * impulse, hit.point, ForceMode.Impulse);

            var target = hit.collider.GetComponentInParent<KnockdownTarget>();
            if (target == null) Fx.Puff(game.art.dust, hit.point, hit.normal);
            Finish(target, hit.point);
        }

        void Finish(KnockdownTarget target, Vector3 point)
        {
            done = true;
            game.OnBulletLanded(target, point, velocity.normalized);
            // Let the tracer fade out instead of vanishing.
            foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.enabled = false;
            Destroy(gameObject, 0.1f);
        }
    }
}
