using System.Collections.Generic;
using UnityEngine;

// draws the snake. in the retro look every segment is a flat square that jumps from cell to cell,
// in 3D the segments are balls that slide between cells and the head gets eyes.
// the head also carries a light, its the only light you have in the dark maze
public class SnakeView
{
    public Vector3 HeadWorld;                   // where the head is right now (between cells)
    public Vector3 HeadForward = Vector3.forward;

    GridMap grid;
    ArtSet art;
    Transform root;
    Transform headRig;
    Light torch;
    Material bodyMaterial;
    Material headMaterial;
    List<Transform> squares = new List<Transform>();        // retro segments
    List<Transform> balls = new List<Transform>();          // 3D segments
    List<Vector2Int> previous = new List<Vector2Int>();     // where the segments were before the last step
    float glow;
    Quaternion headRotation = Quaternion.identity;
    bool hasRotation;

    public SnakeView(Transform parent, GridMap grid, ArtSet art)
    {
        this.grid = grid;
        this.art = art;
        root = new GameObject("Snake").transform;
        root.SetParent(parent, false);

        // copies of the materials so we can change the glow without changing the real assets
        bodyMaterial = new Material(art.snakeBody);
        headMaterial = new Material(art.snakeHead);

        // the eyes and the light follow the head
        headRig = new GameObject("HeadRig").transform;
        headRig.SetParent(root, false);
        for (int side = -1; side <= 1; side += 2)
        {
            Transform eye = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Eye", art.eyeWhite, headRig);
            eye.localPosition = new Vector3(side * 0.26f, 0.2f, 0.3f);
            eye.localScale = new Vector3(0.3f, 0.3f, 0.3f);
            Transform pupil = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Pupil", art.pupil, eye);
            pupil.localPosition = new Vector3(0f, 0f, 0.42f);
            pupil.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        }
        // the light has shadows on so it doesn't shine through the hedges
        torch = ShapeMaker.MakeLight("Torch", headRig, Color.white, 6.5f, 3f);
        torch.transform.localPosition = new Vector3(0f, 1.5f, 0f);
        torch.shadows = LightShadows.Soft;
        torch.shadowBias = 0.05f;
        headRig.gameObject.SetActive(false);
    }

    // 0 = just the torch, 1 = the lantern is lit and the snake glows
    public void SetGlow(float amount)
    {
        glow = Mathf.Clamp01(amount);
    }

    // the snake got teleported so don't slide it there
    public void ResetMotion()
    {
        previous.Clear();
    }

    // remember where the segments were before a step so we can slide them
    public void OnStep(List<Vector2Int> before)
    {
        previous.Clear();
        previous.AddRange(before);
    }

    void AddSegment()
    {
        squares.Add(ShapeMaker.MakeNoShadow(PrimitiveType.Cube, "Segment", art.retroSnake, root));
        Material mat = bodyMaterial;
        if (balls.Count == 0) mat = headMaterial;
        balls.Add(ShapeMaker.Make(PrimitiveType.Sphere, "Segment3D", mat, root));
    }

    public void Refresh(SnakeSim sim, float progress, float look, float time, float dt)
    {
        int n = sim.Snake.Count;
        bool is3D = look > 0.35f;
        headRig.gameObject.SetActive(is3D);
        while (squares.Count < n) AddSegment();

        // the light gets bigger when the lantern is lit
        torch.range = Mathf.Lerp(6.5f, 18f, glow);
        torch.intensity = Mathf.Lerp(3.0f, 9.5f, glow) * (1f + 0.04f * Mathf.Sin(time * 13f));
        torch.color = Color.Lerp(new Color(1f, 0.80f, 0.52f), new Color(1f, 0.93f, 0.58f), glow);
        Color glowColor = new Color(0.35f, 0.9f, 0.2f) * Mathf.Lerp(0.30f, 2.0f, glow);
        bodyMaterial.SetColor("_EmissionColor", glowColor);
        headMaterial.SetColor("_EmissionColor", glowColor);

        // turn the head smoothly
        Quaternion target = Quaternion.LookRotation(new Vector3(sim.Heading.x, 0f, sim.Heading.y));
        if (!hasRotation)
        {
            headRotation = target;
            hasRotation = true;
        }
        else
        {
            headRotation = Quaternion.Slerp(headRotation, target, dt * 18f);
        }
        HeadForward = headRotation * Vector3.forward;

        float p = Mathf.Lerp(1f, progress, look); // the retro snake doesn't slide, it jumps
        for (int i = 0; i < n; i++)
        {
            Vector2Int cell = sim.Snake[i];
            Vector2Int from = cell;
            if (previous.Count > 0) from = previous[Mathf.Min(i, previous.Count - 1)];
            Vector3 pos = Vector3.Lerp(grid.ToWorld(from), grid.ToWorld(cell), p);

            squares[i].localPosition = new Vector3(pos.x, 0.05f, pos.z);
            squares[i].localScale = new Vector3(0.86f, 0.1f, 0.86f);

            // the balls get smaller towards the tail
            float size = 0.98f;
            if (i > 0) size = 0.8f * Mathf.Lerp(1f, 0.6f, i / (float)(n - 1));
            Vector3 ballPos = new Vector3(pos.x, size * 0.5f, pos.z);
            balls[i].localPosition = ballPos;
            balls[i].localScale = new Vector3(size, size, size);

            if (i == 0)
            {
                HeadWorld = ballPos;
                headRig.localPosition = ballPos;
                headRig.localRotation = headRotation;
            }
        }
        for (int i = 0; i < squares.Count; i++)
        {
            squares[i].gameObject.SetActive(i < n && !is3D);
            balls[i].gameObject.SetActive(i < n && is3D);
        }
    }
}
