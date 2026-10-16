using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SnakesTemptation
{
    /// <summary>
    /// The hedge maze and its floor, built once as a handful of combined meshes (16x16-cell chunks) rather than thousands
    /// of cubes. Hidden until the world flips; then each chunk rises out of the ground, outermost last, so the maze seems
    /// to grow outward from the board in the middle.
    /// </summary>
    sealed class MazeView
    {
        const int Chunk = 16;

        readonly Transform root;
        readonly List<Transform> floors = new List<Transform>();
        readonly List<Transform> walls = new List<Transform>();
        readonly List<float> delay = new List<float>();
        bool shown;

        public MazeView(Transform parent, GridMap grid, ArtSet art, MazeMap maze, RectInt retro)
        {
            root = new GameObject("Maze").transform;
            root.SetParent(parent, false);

            int n = (maze.Size + Chunk - 1) / Chunk;
            for (int cx = 0; cx < n; cx++)
                for (int cy = 0; cy < n; cy++)
                {
                    int x0 = cx * Chunk, y0 = cy * Chunk;
                    int x1 = Mathf.Min(maze.Size, x0 + Chunk), y1 = Mathf.Min(maze.Size, y0 + Chunk);

                    var floor = BuildFloor(grid, maze, retro, art, x0, y0, x1, y1);
                    var wall = BuildWalls(grid, maze, art, x0, y0, x1, y1);
                    if (floor != null) { floor.gameObject.SetActive(false); floors.Add(floor); floorDelay.Add(DelayOf(floor, maze)); }
                    if (wall != null) { wall.gameObject.SetActive(false); walls.Add(wall); delay.Add(DelayOf(wall, maze)); }
                }
        }

        readonly List<float> floorDelay = new List<float>();

        // How long a chunk waits before rising: 0 at the middle of the map, 0.85 s at the far corners.
        // Chunk meshes are in world space (the Maze root sits at the origin), so the mesh bounds locate the chunk.
        static float DelayOf(Transform chunk, MazeMap maze)
        {
            Vector3 b = chunk.GetComponent<MeshFilter>().sharedMesh.bounds.center;
            return Mathf.Clamp01(new Vector2(b.x, b.z).magnitude / (maze.Size * 0.5f)) * 0.85f;
        }

        public void Show()
        {
            shown = true;
            foreach (var t in floors) t.gameObject.SetActive(true);
            foreach (var t in walls) t.gameObject.SetActive(true);
            Refresh(0f);
        }

        /// <param name="morphT">Seconds since the world started to flip.</param>
        public void Refresh(float morphT)
        {
            if (!shown) return;
            for (int i = 0; i < walls.Count; i++)
            {
                float p = Mathf.Clamp01((morphT - delay[i]) / 0.9f);
                walls[i].localScale = new Vector3(1f, Mathf.Max(0.0001f, MathX.EaseOutBack(p)), 1f);
            }
            for (int i = 0; i < floors.Count; i++)
            {
                float p = Mathf.Clamp01((morphT - floorDelay[i] * 0.6f) / 0.6f);
                floors[i].localPosition = new Vector3(0f, -(1f - MathX.Smooth01(p)) * 1.6f, 0f);
            }
        }

        // ------------------------------------------------------------------------------------------------ meshes

        // Adds a quad whose front face looks along `normal`, whatever order the corners were given in.
        static void Quad(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f) (b, d) = (d, b);
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) n.Add(normal);
            uv.Add(UvFor(a, normal)); uv.Add(UvFor(b, normal)); uv.Add(UvFor(c, normal)); uv.Add(UvFor(d, normal));
            t.Add(i); t.Add(i + 1); t.Add(i + 2);
            t.Add(i); t.Add(i + 2); t.Add(i + 3);
        }

        // Planar mapping from world position: the same texel density on tops and on sides, no stretching, no seams between chunks.
        static Vector2 UvFor(Vector3 p, Vector3 normal)
        {
            const float scale = 0.45f;
            if (Mathf.Abs(normal.y) > 0.5f) return new Vector2(p.x * scale, p.z * scale);
            float along = Mathf.Abs(normal.x) > 0.5f ? p.z : p.x;
            return new Vector2(along * scale, p.y * scale);
        }

        static Mesh ToMesh(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int>[] submeshes)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v);
            m.SetNormals(n);
            m.SetUVs(0, uv);
            m.subMeshCount = submeshes.Length;
            for (int i = 0; i < submeshes.Length; i++) m.SetTriangles(submeshes[i], i);
            m.RecalculateBounds();
            return m;
        }

        Transform BuildFloor(GridMap grid, MazeMap maze, RectInt retro, ArtSet art, int x0, int y0, int x1, int y1)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() }; // tileA, tileB, plazaA, plazaB
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                {
                    if (maze.Wall[x, y] || retro.Contains(new Vector2Int(x, y))) continue; // the retro board has its own tiles
                    bool plaza = maze.Plaza[x, y] && !maze.Start.Contains(new Vector2Int(x, y));
                    int sub = (plaza ? 2 : 0) + (((x + y) & 1) == 0 ? 0 : 1);
                    Vector3 c = grid.ToWorld(new Vector2Int(x, y));
                    const float h = 0.485f; // a hairline gap between tiles reads as a grid
                    Quad(v, n, uv, tris[sub], c + new Vector3(-h, 0, -h), c + new Vector3(-h, 0, h), c + new Vector3(h, 0, h), c + new Vector3(h, 0, -h), Vector3.up);
                }
            if (v.Count == 0) return null;
            return MakeObject("MazeFloor", ToMesh("MazeFloor", v, n, uv, tris), new[] { art.tileA, art.tileB, art.plazaA, art.plazaB }, false);
        }

        static float WallHeight(int x, int y) => 1.55f + 0.22f * Mathf.PerlinNoise(x * 0.16f + 7.3f, y * 0.16f + 2.9f);

        Transform BuildWalls(GridMap grid, MazeMap maze, ArtSet art, int x0, int y0, int x1, int y1)
        {
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tris = new[] { new List<int>() };
            var t = tris[0];
            var dirs = new[] { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                {
                    if (!maze.Wall[x, y]) continue;
                    float h = WallHeight(x, y);
                    Vector3 c = grid.ToWorld(new Vector2Int(x, y));
                    const float s = 0.5f;
                    Quad(v, n, uv, t, c + new Vector3(-s, h, -s), c + new Vector3(-s, h, s), c + new Vector3(s, h, s), c + new Vector3(s, h, -s), Vector3.up);

                    foreach (var d in dirs)
                    {
                        var nb = new Vector2Int(x + d.x, y + d.y);
                        float from = 0f;
                        if (!maze.InMap(nb)) continue;                       // nobody sees the outside of the map
                        if (maze.Wall[nb.x, nb.y])
                        {
                            float nh = WallHeight(nb.x, nb.y);
                            if (h <= nh + 0.001f) continue;                  // hidden against a taller or equal neighbour
                            from = nh;                                        // only the step above the neighbour shows
                        }
                        Vector3 normal = new Vector3(d.x, 0, d.y);
                        Vector3 along = new Vector3(-d.y, 0, d.x);            // direction along the face
                        Vector3 o = c + normal * s;
                        Quad(v, n, uv, t, o - along * s + Vector3.up * from, o + along * s + Vector3.up * from,
                                      o + along * s + Vector3.up * h, o - along * s + Vector3.up * h, normal);
                    }
                }
            if (v.Count == 0) return null;
            return MakeObject("MazeWalls", ToMesh("MazeWalls", v, n, uv, tris), new[] { art.hedge }, true);
        }

        Transform MakeObject(string name, Mesh mesh, Material[] mats, bool castShadows)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            return go.transform;
        }
    }
}
