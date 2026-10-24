using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The lantern: a glowing orb with a few sparks orbiting it, a warm light and a short beacon, so it can be spotted from
    /// across the dark maze. Walking into it lights the snake up and shows where the seeds are for a while.
    /// </summary>
    sealed class PowerupView
    {
        const int Sparks = 4;

        readonly GridMap grid;
        readonly Transform root;
        readonly Transform orb;
        readonly Transform beam;
        readonly Transform[] sparks = new Transform[Sparks];
        readonly Light light;
        Vector2Int lastCell = new Vector2Int(-99, -99);
        float popStart;

        public PowerupView(Transform parent, GridMap grid, ArtSet art)
        {
            this.grid = grid;
            root = new GameObject("Lantern").transform;
            root.SetParent(parent, false);

            orb = Prims.Make("Orb", Prims.Sphere, art.lantern, root, false, false);
            orb.localScale = Vector3.one * 0.5f;
            for (int i = 0; i < Sparks; i++)
            {
                sparks[i] = Prims.Make("Spark", Prims.Sphere, art.lantern, root, false, false);
                sparks[i].localScale = Vector3.one * 0.12f;
            }
            // a short, soft beacon: tall enough to clear the hedges, much shorter than a seed's pillar
            beam = Prims.Make("Beacon", Prims.Cylinder, art.lanternBeam, root, false, false);
            beam.localScale = new Vector3(0.09f, 1.7f, 0.09f);
            beam.localPosition = new Vector3(0f, 1.7f - 0.9f, 0f);

            light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(root, false);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.88f, 0.55f);
            light.range = 9f;
            light.intensity = 6f;
            light.shadows = LightShadows.None;
        }

        public void Refresh(SnakeSim sim, float look, float time)
        {
            bool show = sim.HasPowerup && look > 0.35f;
            root.gameObject.SetActive(show);
            if (!show) return;

            if (sim.Powerup != lastCell)
            {
                lastCell = sim.Powerup;
                popStart = time;
            }
            float pop = MathX.EaseOutBack((time - popStart) / 0.35f);
            float bob = 0.9f + Mathf.Sin(time * 2.6f) * 0.12f;
            root.position = grid.ToWorld(sim.Powerup, 0f);
            root.localScale = Vector3.one * pop;
            orb.localPosition = new Vector3(0f, bob, 0f);
            for (int i = 0; i < Sparks; i++)
            {
                float a = time * 2.2f + i * Mathf.PI * 2f / Sparks;
                sparks[i].localPosition = new Vector3(Mathf.Cos(a) * 0.55f, bob + Mathf.Sin(a * 1.7f) * 0.25f, Mathf.Sin(a) * 0.55f);
            }
            light.transform.localPosition = new Vector3(0f, bob + 0.2f, 0f);
            light.intensity = 6f * (1f + 0.07f * Mathf.Sin(time * 11f) + 0.04f * Mathf.Sin(time * 27f));
        }
    }
}
