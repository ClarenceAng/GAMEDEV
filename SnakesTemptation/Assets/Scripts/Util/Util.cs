using UnityEngine;

// turns a grid cell into a world position. the grid is flat on the ground and centered on 0,0
public class GridMap
{
    public int W;
    public int H;

    public GridMap(int w, int h)
    {
        W = w;
        H = h;
    }

    public Vector3 ToWorld(Vector2Int cell)
    {
        return ToWorld(cell, 0f);
    }

    public Vector3 ToWorld(Vector2Int cell, float y)
    {
        return new Vector3(cell.x - (W - 1) * 0.5f, y, cell.y - (H - 1) * 0.5f);
    }
}

// helper for making the cubes and spheres everything is built out of
public static class ShapeMaker
{
    public static Transform Make(PrimitiveType type, string name, Material mat, Transform parent)
    {
        GameObject obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        // we dont want colliders on the art, the game has its own colliders
        Object.DestroyImmediate(obj.GetComponent<Collider>());
        obj.transform.SetParent(parent, false);
        obj.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return obj.transform;
    }

    // same thing but it doesnt cast shadows (for glowing stuff and small details)
    public static Transform MakeNoShadow(PrimitiveType type, string name, Material mat, Transform parent)
    {
        Transform t = Make(type, name, mat, parent);
        t.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    public static Light MakeLight(string name, Transform parent, Color color, float range, float intensity)
    {
        Light light = new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(parent, false);
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        return light;
    }
}
