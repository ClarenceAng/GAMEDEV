using System.Collections.Generic;
using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>The snake: every segment is a flat dark square that jumps cell to cell like an LCD pixel.</summary>
    sealed class SnakeView
    {
        readonly GridMap grid;
        readonly ArtSet art;
        readonly Transform root;
        readonly List<Transform> seg = new List<Transform>();

        public SnakeView(Transform parent, GridMap grid, ArtSet art)
        {
            this.grid = grid;
            this.art = art;
            root = new GameObject("Snake").transform;
            root.SetParent(parent, false);
        }

        public void Refresh(SnakeSim sim)
        {
            int n = sim.Snake.Count;
            while (seg.Count < n)
            {
                var t = Prims.Make("Segment", Prims.Cube, art.retroSnake, root);
                t.localScale = new Vector3(0.86f, 0.1f, 0.86f);
                seg.Add(t);
            }
            for (int i = 0; i < seg.Count; i++)
            {
                seg[i].gameObject.SetActive(i < n);
                if (i < n) seg[i].localPosition = grid.ToWorld(sim.Snake[i], 0.05f);
            }
        }
    }
}
