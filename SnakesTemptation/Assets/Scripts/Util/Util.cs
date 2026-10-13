using UnityEngine;
using UnityEngine.Rendering;

namespace SnakesTemptation
{
    static class MathX
    {
        public static float EaseOutBack(float t)
        {
            t = Mathf.Clamp01(t);
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }

    /// <summary>Maps grid cells to world space: the grid lies on the XZ plane, centred on the origin, 1 unit per cell.</summary>
    public sealed class GridMap
    {
        public readonly int W;
        public readonly int H;

        public GridMap(int w, int h)
        {
            W = w;
            H = h;
        }

        public Vector3 ToWorld(Vector2Int c, float y = 0f) => new Vector3(c.x - (W - 1) * 0.5f, y, c.y - (H - 1) * 0.5f);
    }

    /// <summary>Built-in primitive meshes and a tiny factory for renderer-only GameObjects.</summary>
    static class Prims
    {
        static Mesh cube, quad;

        public static Mesh Cube => cube != null ? cube : cube = Grab(PrimitiveType.Cube);
        public static Mesh Quad => quad != null ? quad : quad = Grab(PrimitiveType.Quad);

        static Mesh Grab(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
            return mesh;
        }

        public static Transform Make(string name, Mesh mesh, Material mat, Transform parent, bool castShadows = true, bool receiveShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = receiveShadows;
            return go.transform;
        }
    }
}
