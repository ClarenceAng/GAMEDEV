using UnityEngine;

// the lantern: a glowing ball with a light and a short beam so you can spot it in the dark maze
public class PowerupView
{
    GridMap grid;
    Transform root;
    Transform orb;
    Light light;

    public PowerupView(Transform parent, GridMap grid, ArtSet art)
    {
        this.grid = grid;
        root = new GameObject("Lantern").transform;
        root.SetParent(parent, false);

        orb = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Orb", art.lantern, root);
        orb.localScale = new Vector3(0.5f, 0.5f, 0.5f);

        Transform beam = ShapeMaker.MakeNoShadow(PrimitiveType.Cylinder, "Beacon", art.lanternBeam, root);
        beam.localScale = new Vector3(0.09f, 1.7f, 0.09f);
        beam.localPosition = new Vector3(0f, 0.8f, 0f);

        light = ShapeMaker.MakeLight("Light", root, new Color(1f, 0.88f, 0.55f), 9f, 6f);
    }

    public void Refresh(SnakeSim sim, float look, float time)
    {
        bool show = sim.HasPowerup && look > 0.35f;
        root.gameObject.SetActive(show);
        if (!show) return;

        float bob = 0.9f + Mathf.Sin(time * 2.6f) * 0.12f;
        root.position = grid.ToWorld(sim.Powerup);
        orb.localPosition = new Vector3(0f, bob, 0f);
        light.transform.localPosition = new Vector3(0f, bob + 0.2f, 0f);
        light.intensity = 6f + Mathf.Sin(time * 11f) * 0.4f; // flicker
    }
}
