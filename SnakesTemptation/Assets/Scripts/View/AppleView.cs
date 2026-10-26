using UnityEngine;

// the pickup. in the retro look its a pixel art apple, in 3D its a glowing seed with a beam of light above it
public class AppleView
{
    // 9x9 pixel apple. X = dark, o = light, . = see through
    string[] pixels =
    {
        "....X....",
        "...X.XX..",
        ".XXXXXXX.",
        "XXXXXXXXX",
        "XXoXXXXXX",
        "XXoXXXXXX",
        "XXXXXXXXX",
        ".XXXXXXX.",
        "..XX.XX..",
    };

    GridMap grid;
    Transform sprite;
    Transform seed3D;
    Transform beam;
    Light glow;
    Material seedMaterial;
    Color seedEmission;

    public AppleView(Transform parent, GridMap grid, ArtSet art)
    {
        this.grid = grid;

        // draw the apple into a texture
        Texture2D tex = new Texture2D(9, 9, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Color dark = new Color32(15, 56, 15, 255);
        Color light = new Color32(139, 172, 15, 255);
        for (int row = 0; row < 9; row++)
        {
            for (int x = 0; x < 9; x++)
            {
                char c = pixels[row][x];
                Color color = Color.clear;
                if (c == 'X') color = dark;
                if (c == 'o') color = light;
                tex.SetPixel(x, 8 - row, color);
            }
        }
        tex.Apply();
        Material spriteMaterial = new Material(art.retroSprite);
        spriteMaterial.SetTexture("_BaseMap", tex);
        sprite = ShapeMaker.MakeNoShadow(PrimitiveType.Quad, "Apple2D", spriteMaterial, parent);
        sprite.rotation = Quaternion.Euler(90f, 0f, 0f); // lie flat so you can see it from above

        // the 3D seed is a squished sphere
        seed3D = new GameObject("Seed3D").transform;
        seed3D.SetParent(parent, false);
        seedMaterial = new Material(art.seed);
        seedEmission = art.seed.GetColor("_EmissionColor");
        Transform body = ShapeMaker.Make(PrimitiveType.Sphere, "Seed", seedMaterial, seed3D);
        body.localScale = new Vector3(0.5f, 0.85f, 0.32f);
        body.localRotation = Quaternion.Euler(0f, 0f, 16f);

        // thin glowing pillar so you can find the seed from far away
        beam = ShapeMaker.MakeNoShadow(PrimitiveType.Cylinder, "Beam", art.seedBeam, seed3D);
        beam.localPosition = new Vector3(0f, 1.78f, 0f);

        glow = ShapeMaker.MakeLight("Glow", seed3D, new Color(1f, 0.72f, 0.2f), 5.5f, 3.5f);
        glow.transform.localPosition = new Vector3(0f, 0.3f, 0f);
    }

    // revealed = how much the lantern is showing the seed (0 to 1). in the dark it barely glows
    public void Refresh(SnakeSim sim, float look, float time, float revealed)
    {
        bool show = sim.HasApple;
        bool is3D = look > 0.35f;
        sprite.gameObject.SetActive(show && !is3D);
        seed3D.gameObject.SetActive(show && is3D);
        if (!show) return;

        if (!is3D)
        {
            sprite.position = grid.ToWorld(sim.Apple, 0.12f);
            sprite.localScale = new Vector3(0.92f, 0.92f, 0.92f);
        }
        else
        {
            // bob up and down and spin
            seed3D.position = grid.ToWorld(sim.Apple, 0.62f + Mathf.Sin(time * 2.4f) * 0.07f);
            seed3D.rotation = Quaternion.Euler(0f, time * 80f, 0f);

            beam.gameObject.SetActive(revealed > 0.02f);
            beam.localScale = new Vector3(0.1f * revealed, 2.4f, 0.1f * revealed);
            glow.range = Mathf.Lerp(3.2f, 6f, revealed);
            glow.intensity = Mathf.Lerp(1.0f, 3.5f, revealed);
            seedMaterial.SetColor("_EmissionColor", seedEmission * Mathf.Lerp(0.25f, 1f, revealed));
        }
    }
}
