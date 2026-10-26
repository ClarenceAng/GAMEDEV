using System.Collections.Generic;
using UnityEngine;

// materials for the garden. they're copies of materials the game already has, with new colors
public class GardenMaterials
{
    public Material grass, grassLight, dirt, skin, hair, dress, leafGreen, leafDark, trunk, apple, appleHero, flesh, stem, mouth, tongue, sun;
    public Material[] flowers;
    List<Material> made = new List<Material>();

    public GardenMaterials(ArtSet art)
    {
        Material lit = art.tileA;
        Material glowing = art.seed;
        grass = Make(lit, new Color(0.34f, 0.56f, 0.20f), 0.08f);
        grassLight = Make(lit, new Color(0.47f, 0.66f, 0.26f), 0.08f);
        dirt = Make(lit, new Color(0.30f, 0.19f, 0.10f), 0.05f);
        skin = Make(lit, new Color(0.80f, 0.55f, 0.42f), 0.2f);
        hair = Make(lit, new Color(0.19f, 0.075f, 0.035f), 0.5f);
        dress = Make(lit, new Color(0.97f, 0.93f, 0.80f), 0.15f);
        leafGreen = Make(lit, new Color(0.23f, 0.55f, 0.17f), 0.12f);
        leafDark = Make(lit, new Color(0.12f, 0.33f, 0.12f), 0.1f);
        trunk = Make(art.trunk, new Color(0.36f, 0.22f, 0.12f), 0.08f);
        apple = MakeGlowing(glowing, new Color(0.70f, 0.04f, 0.04f), 0.5f, new Color(0.16f, 0.01f, 0.005f));
        appleHero = MakeGlowing(glowing, new Color(0.78f, 0.045f, 0.04f), 0.55f, new Color(0.30f, 0.02f, 0.01f));
        flesh = Make(lit, new Color(0.98f, 0.93f, 0.72f), 0.3f);
        stem = Make(lit, new Color(0.28f, 0.2f, 0.08f), 0.1f);
        mouth = Make(lit, new Color(0.45f, 0.04f, 0.06f), 0.4f);
        tongue = Make(lit, new Color(0.85f, 0.10f, 0.14f), 0.5f);
        sun = Make(art.retroBase, new Color(1.6f, 1.2f, 0.55f), 0f);
        flowers = new Material[4];
        flowers[0] = Make(lit, new Color(0.98f, 0.97f, 0.92f), 0.3f);
        flowers[1] = Make(lit, new Color(0.98f, 0.55f, 0.70f), 0.3f);
        flowers[2] = Make(lit, new Color(1.00f, 0.84f, 0.25f), 0.3f);
        flowers[3] = Make(lit, new Color(0.62f, 0.50f, 0.95f), 0.3f);
    }

    Material Make(Material template, Color color, float smoothness)
    {
        Material m = new Material(template);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetTexture("_BaseMap", null);
        m.SetColor("_EmissionColor", Color.black);
        made.Add(m);
        return m;
    }

    Material MakeGlowing(Material template, Color color, float smoothness, Color emission)
    {
        Material m = Make(template, color, smoothness);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        return m;
    }

    public void Dispose()
    {
        foreach (Material m in made)
        {
            if (m != null) Object.Destroy(m);
        }
        made.Clear();
    }
}

// a path made of points. you can ask where you are after walking a certain distance along it
public class GardenPath
{
    public float Length;
    List<Vector3> points = new List<Vector3>();
    List<float> distances = new List<float>();

    public void Add(Vector3 p)
    {
        if (points.Count > 0)
        {
            float d = Vector3.Distance(p, points[points.Count - 1]);
            if (d < 0.0001f) return;
            Length += d;
        }
        points.Add(p);
        distances.Add(Length);
    }

    public Vector3 At(float distance)
    {
        if (points.Count == 0) return Vector3.zero;
        if (distance <= 0f) return points[0];
        if (distance >= Length) return points[points.Count - 1];
        for (int i = 1; i < points.Count; i++)
        {
            if (distances[i] >= distance)
            {
                float k = (distance - distances[i - 1]) / (distances[i] - distances[i - 1]);
                return Vector3.Lerp(points[i - 1], points[i], k);
            }
        }
        return points[points.Count - 1];
    }

    // which way the path goes at this distance
    public Vector3 Direction(float distance)
    {
        Vector3 d = At(distance + 0.3f) - At(distance - 0.3f);
        if (d.sqrMagnitude < 0.00001f) return Vector3.forward;
        return d.normalized;
    }
}

// an apple tree that grows: first the trunk, then the leaves, then the apples
public class GardenTree
{
    public Transform Root;
    Transform trunk;
    List<Transform> leaves = new List<Transform>();
    List<Vector3> leafSizes = new List<Vector3>();
    List<Transform> apples = new List<Transform>();
    float height;
    float radius;

    public GardenTree(Transform parent, GardenMaterials m, Vector3 pos, float height, float trunkRadius, int leafCount, int appleCount)
    {
        this.height = height;
        radius = trunkRadius;
        Root = new GameObject("Tree").transform;
        Root.SetParent(parent, false);
        Root.localPosition = pos;
        trunk = ShapeMaker.Make(PrimitiveType.Cylinder, "Trunk", m.trunk, Root);

        float k = height / 5.2f;
        for (int i = 0; i < leafCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Sqrt(Random.value) * 1.75f * k;
            float y = height * Random.Range(0.80f, 1.02f) - r * 0.18f;
            float size = Random.Range(1.15f, 1.8f) * k;
            Material mat = m.leafGreen;
            if (i % 3 == 0) mat = m.leafDark;
            Transform leaf = ShapeMaker.Make(PrimitiveType.Sphere, "Leaves", mat, Root);
            leaf.localPosition = new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
            leaves.Add(leaf);
            leafSizes.Add(new Vector3(size, size * 0.82f, size));
        }
        for (int i = 0; i < appleCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(0.55f, 1.75f) * k;
            Transform a = ShapeMaker.Make(PrimitiveType.Sphere, "Apple", m.apple, Root);
            a.localPosition = new Vector3(Mathf.Cos(angle) * r, height * Random.Range(0.70f, 0.86f), Mathf.Sin(angle) * r);
            apples.Add(a);
        }
        Grow(0f);
    }

    // 0 = not there, 1 = fully grown
    public void Grow(float amount)
    {
        amount = Mathf.Clamp01(amount);
        Root.gameObject.SetActive(amount > 0.001f);

        float h = Mathf.SmoothStep(0f, 1f, amount / 0.7f); // the trunk goes first
        float r = radius * (0.3f + 0.7f * h);
        trunk.localScale = new Vector3(r * 2f, Mathf.Max(0.001f, height * 0.5f * h), r * 2f);
        trunk.localPosition = new Vector3(0f, height * 0.5f * h, 0f);

        float leafGrow = Mathf.SmoothStep(0f, 1f, (amount - 0.35f) / 0.55f);
        for (int i = 0; i < leaves.Count; i++)
        {
            leaves[i].localScale = leafSizes[i] * leafGrow;
            leaves[i].gameObject.SetActive(leafGrow > 0.001f);
        }

        float appleGrow = Mathf.SmoothStep(0f, 1f, (amount - 0.82f) / 0.16f);
        for (int i = 0; i < apples.Count; i++)
        {
            apples[i].localScale = Vector3.one * 0.2f * appleGrow;
            apples[i].gameObject.SetActive(appleGrow > 0.001f);
        }
    }
}

// the snake in the ending. its a line of balls that follow the head along a path
// (so it can slither over the grass and then climb up the tree)
public class CutsceneSnake
{
    public Vector3 HeadPos;

    int segmentCount = 30;
    float spacing = 0.26f;
    Transform root;
    Transform[] segments;
    Transform head;
    Transform mouth;
    Transform tongue;

    public CutsceneSnake(Transform parent, ArtSet art, GardenMaterials m)
    {
        root = new GameObject("CutsceneSnake").transform;
        root.SetParent(parent, false);
        segments = new Transform[segmentCount];
        for (int i = 1; i < segmentCount; i++)
        {
            Material mat = art.snakeBody;
            if (i % 5 == 0) mat = art.snakeHead; // stripes
            segments[i] = ShapeMaker.Make(PrimitiveType.Sphere, "Segment", mat, root);
            float size = Radius(i) * 2f;
            segments[i].localScale = new Vector3(size, size, size);
        }

        head = new GameObject("Head").transform;
        head.SetParent(root, false);
        head.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        segments[0] = head;
        Transform skull = ShapeMaker.Make(PrimitiveType.Sphere, "Skull", art.snakeHead, head);
        skull.localScale = new Vector3(0.67f, 0.59f, 0.8f);
        Transform snout = ShapeMaker.Make(PrimitiveType.Sphere, "Snout", art.snakeHead, head);
        snout.localScale = new Vector3(0.52f, 0.34f, 0.55f);
        snout.localPosition = new Vector3(0f, -0.04f, 0.33f);

        mouth = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Mouth", m.mouth, head);
        mouth.localPosition = new Vector3(0f, -0.14f, 0.45f);

        for (int side = -1; side <= 1; side += 2)
        {
            Transform eye = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Eye", art.eyeWhite, head);
            eye.localPosition = new Vector3(side * 0.23f, 0.15f, 0.25f);
            eye.localScale = new Vector3(0.21f, 0.21f, 0.21f);
            Transform pupil = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Pupil", art.pupil, eye);
            pupil.localPosition = new Vector3(0f, 0f, 0.43f);
            pupil.localScale = new Vector3(0.32f, 0.85f, 0.4f); // snake eyes are slits
        }

        tongue = ShapeMaker.MakeNoShadow(PrimitiveType.Cube, "Tongue", m.tongue, head);
    }

    float Radius(int i)
    {
        if (i == 0) return 0.32f;
        return 0.8f * Mathf.Lerp(0.31f, 0.095f, (i - 1) / (float)(segmentCount - 2));
    }

    public Vector3 MouthWorld
    {
        get { return head.TransformPoint(new Vector3(0f, -0.1f, 0.42f)); }
    }

    // headDistance = where the head is on the path, groundEnd = where the grass ends and the climbing starts,
    // wave = how much it wiggles, lift = how high the head rears up, pitchDown = degrees the head looks down
    public void Pose(GardenPath path, float headDistance, float groundEnd, float time, float wave, float lift, float pitchDown,
                     Vector3 lookAt, float lookWeight, float mouthOpen, float tongueOut)
    {
        for (int i = 0; i < segmentCount; i++)
        {
            float s = headDistance - i * spacing;
            Vector3 p = path.At(s);
            Vector3 dir = path.Direction(s);

            // on the grass the balls sit on the ground, on the tree they're on the path
            float onGround = 1f - Mathf.Clamp01((s - groundEnd) / 1.5f);
            p.y += Radius(i) * onGround;

            // wiggle side to side
            Vector3 side = Vector3.Cross(Vector3.up, dir).normalized;
            p += side * Mathf.Sin(s * 2.3f - time * 6.2f) * 0.2f * wave * onGround * Mathf.Min(1f, i / 4f);

            // the front of the body follows the head up
            if (i < 5)
            {
                float w = 1f - i / 5f;
                p += Vector3.up * lift * w * w;
            }
            segments[i].localPosition = p;
            if (i == 0) HeadPos = p;
        }

        // point the head along the path, or at what it's looking at
        Vector3 forward = path.Direction(headDistance - 0.15f);
        if (lookWeight > 0f)
        {
            forward = Vector3.Slerp(forward, (lookAt - HeadPos).normalized, Mathf.Clamp01(lookWeight));
        }
        head.localRotation = Quaternion.LookRotation(forward) * Quaternion.Euler(pitchDown, 0f, 0f);

        mouth.localScale = new Vector3(0.3f, 0.05f + 0.15f * Mathf.Clamp01(mouthOpen), 0.2f);

        // the tongue flicks in and out
        float t = Mathf.Clamp01(tongueOut);
        tongue.gameObject.SetActive(t > 0.02f);
        tongue.localScale = new Vector3(0.05f, 0.025f, 0.4f * t);
        tongue.localPosition = new Vector3(0f, -0.1f, 0.5f + 0.2f * t);
    }
}
