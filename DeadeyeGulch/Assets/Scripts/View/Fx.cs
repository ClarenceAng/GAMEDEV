using UnityEngine;

namespace DeadeyeGulch
{
    /// <summary>Short-lived effects made from primitives.</summary>
    public static class Fx
    {
        /// <summary>A little cloud of dust where a bullet strikes the ground or a wall.</summary>
        public static void Puff(Material mat, Vector3 point, Vector3 normal)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 drift = (normal * 0.8f + Random.insideUnitSphere * 0.5f + Vector3.up * 0.4f) * Random.Range(0.4f, 0.9f);
                var go = Shapes.Sphere(null, "Dust", point + normal * 0.05f, Vector3.one * 0.06f, mat, Hitbox.None);
                Shapes.SetLayer(go, Shapes.NoHitLayer);
                Shapes.NoShadows(go);
                go.AddComponent<FxFade>().Init(drift, Random.Range(0.25f, 0.45f), Random.Range(0.3f, 0.5f));
            }
        }
    }

    /// <summary>Drifts, grows and shrinks away, then destroys itself.</summary>
    public sealed class FxFade : MonoBehaviour
    {
        Vector3 velocity;
        float life, age, size;

        public void Init(Vector3 velocity, float maxSize, float life)
        {
            this.velocity = velocity;
            this.life = life;
            size = maxSize;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = age / life;
            if (k >= 1f) { Destroy(gameObject); return; }
            transform.position += velocity * Time.deltaTime;
            velocity *= 1f - Time.deltaTime * 3f;
            transform.localScale = Vector3.one * (size * Mathf.Sin(Mathf.Sqrt(k) * Mathf.PI));
        }
    }
}
