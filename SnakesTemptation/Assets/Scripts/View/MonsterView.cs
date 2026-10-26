using UnityEngine;

// the apple monster. the prefab doesn't have animations so all the movement is done here:
// it hops from cell to cell, turns where its going, sways, and climbs out of the ground when it spawns
public class MonsterView
{
    GridMap grid;
    Transform root;
    Light aura;
    Vector2Int from;
    Vector2Int to;
    Quaternion rotation = Quaternion.identity;
    bool lunging;
    Vector3 lungeStart;

    float faceHeight = 1.3f; // how high its eyes are

    public MonsterView(Transform parent, GridMap grid, ArtSet art, Vector2Int cell)
    {
        this.grid = grid;
        from = cell;
        to = cell;
        root = new GameObject("Monster").transform;
        root.SetParent(parent, false);
        Transform model = Object.Instantiate(art.monsterPrefab, root).transform;
        model.localScale = new Vector3(1.1f, 1.1f, 1.1f);

        aura = ShapeMaker.MakeLight("Aura", root, new Color(1f, 0.85f, 0.2f), 9f, 7f);
        aura.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
    }

    // put it on a cell without walking there
    public void Teleport(Vector2Int cell)
    {
        from = cell;
        to = cell;
    }

    public void OnStep(Vector2Int newCell)
    {
        from = to;
        to = newCell;
    }

    // where its eyes are in the world
    public Vector3 FaceWorld
    {
        get { return root.position + root.rotation * (Vector3.up * faceHeight * root.localScale.y); }
    }

    public void BeginLunge()
    {
        lunging = true;
        lungeStart = FaceWorld;
    }

    // the jumpscare: its face flies at the camera and stays right in front of it, shaking.
    // t goes from 0 to 1 (and stays at 1 after)
    public void Lunge(Transform camera, float t, float time)
    {
        if (!lunging) return;
        float e = Mathf.SmoothStep(0f, 1f, t * 1.5f);
        Vector3 camPos = camera.position;
        Vector3 end = camPos + camera.forward * 2.5f + camera.up * 0.12f;
        Vector3 face = Vector3.Lerp(lungeStart, end, e);
        face += Random.insideUnitSphere * (0.02f + 0.05f * e); // shaking
        float size = Mathf.Lerp(1f, 1.9f, e);

        Quaternion look = Quaternion.LookRotation(camPos - face, Vector3.up);
        root.rotation = look;
        root.localScale = new Vector3(size, size, size);
        root.position = face - look * (Vector3.up * faceHeight * size); // so the face is where we want it, not the feet

        // put the light in front of the face so you can see it
        aura.transform.position = face + (camPos - face).normalized * (1.3f * size) + Vector3.up * 0.4f;
        aura.color = Color.Lerp(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.55f, 0.18f), e);
        aura.intensity = Mathf.Lerp(7f, 55f, e) * (0.8f + 0.2f * Mathf.Sin(time * 53f));
        aura.range = Mathf.Lerp(9f, 16f, e);
    }

    public void EndLunge()
    {
        lunging = false;
        aura.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);
        aura.color = new Color(1f, 0.85f, 0.2f);
        aura.intensity = 7f;
        aura.range = 9f;
    }

    // progress = how far between the two cells, emerge = 0 in the ground to 1 all the way out
    public void Refresh(float progress, float emerge, float time, float dt)
    {
        if (lunging) return;
        Vector3 a = grid.ToWorld(from);
        Vector3 b = grid.ToWorld(to);
        Vector3 pos = Vector3.Lerp(a, b, progress);

        // face the way its going
        Vector3 dir = b - a;
        if (dir.sqrMagnitude > 0.01f)
        {
            rotation = Quaternion.Slerp(rotation, Quaternion.LookRotation(dir), dt * 10f);
        }

        float hop = Mathf.Sin(progress * Mathf.PI) * 0.14f;
        float sink = (1f - Mathf.Clamp01(emerge)) * 1.8f;
        root.position = pos + new Vector3(0f, hop - sink, 0f);
        root.rotation = rotation * Quaternion.Euler(8f, 0f, Mathf.Sin(time * 5f) * 4f); // leans forward and sways
        float size = Mathf.Clamp01(emerge * 1.5f);
        root.localScale = new Vector3(size, size, size);
    }

    public void Dispose()
    {
        if (root != null) Object.Destroy(root.gameObject);
    }
}
