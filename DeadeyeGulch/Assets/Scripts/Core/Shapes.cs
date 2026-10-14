using UnityEngine;

namespace DeadeyeGulch
{
    public enum Hitbox { None, Box, Primitive, ConvexMesh }

    /// <summary>
    /// Everything in the game is built from Unity primitives in code. These helpers create one part with a material,
    /// local placement and the collider it should have.
    /// </summary>
    public static class Shapes
    {
        /// <summary>Bullets ignore this layer: the player, effects and debris live on it.</summary>
        public const int NoHitLayer = 2; // built-in "Ignore Raycast"
        public const int BulletMask = ~(1 << NoHitLayer);

        public static Transform Group(Transform parent, string name, Vector3 localPos, Quaternion? localRot = null)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = localRot ?? Quaternion.identity;
            return t;
        }

        public static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, Hitbox hit = Hitbox.Box, Quaternion? localRot = null)
            => Make(PrimitiveType.Cube, parent, name, localPos, size, mat, hit, localRot);

        /// <summary>A vertical cylinder of the given diameter and height (Unity's cylinder is 1 wide and 2 tall).</summary>
        public static GameObject Cylinder(Transform parent, string name, Vector3 localPos, float diameter, float height, Material mat, Hitbox hit = Hitbox.Box, Quaternion? localRot = null)
            => Make(PrimitiveType.Cylinder, parent, name, localPos, new Vector3(diameter, height * 0.5f, diameter), mat, hit, localRot);

        public static GameObject Sphere(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, Hitbox hit = Hitbox.Primitive, Quaternion? localRot = null)
            => Make(PrimitiveType.Sphere, parent, name, localPos, size, mat, hit, localRot);

        public static GameObject Capsule(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat, Hitbox hit = Hitbox.Primitive, Quaternion? localRot = null)
            => Make(PrimitiveType.Capsule, parent, name, localPos, size, mat, hit, localRot);

        public static GameObject Quad(Transform parent, string name, Vector3 localPos, Vector2 size, Material mat, Quaternion? localRot = null)
            => Make(PrimitiveType.Quad, parent, name, localPos, new Vector3(size.x, size.y, 1f), mat, Hitbox.None, localRot);

        static GameObject Make(PrimitiveType type, Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat, Hitbox hit, Quaternion? localRot)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot ?? Quaternion.identity;
            go.transform.localScale = scale;
            if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;

            var own = go.GetComponent<Collider>();
            switch (hit)
            {
                case Hitbox.None:
                    Object.DestroyImmediate(own);
                    break;
                case Hitbox.Box when !(own is BoxCollider):
                    Object.DestroyImmediate(own);
                    go.AddComponent<BoxCollider>();
                    break;
                case Hitbox.ConvexMesh:
                    Object.DestroyImmediate(own);
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                    mc.convex = true;
                    break;
            }
            return go;
        }

        /// <summary>Puts an object and all its children on a layer.</summary>
        public static void SetLayer(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform c in go.transform) SetLayer(c.gameObject, layer);
        }

        /// <summary>Turns shadow casting off for every renderer under an object (view-model gun, effects).</summary>
        public static void NoShadows(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }
    }
}
