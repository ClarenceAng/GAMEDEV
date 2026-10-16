using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// The retro board: the floor, a checkerboard of flat LCD pixels (one cube per cell) and a ring of dark border squares
    /// around it.
    /// </summary>
    sealed class ArenaView
    {
        public ArenaView(Transform parent, GridMap grid, ArtSet art)
        {
            var root = new GameObject("Arena").transform;
            root.SetParent(parent, false);

            var ground = Prims.Make("Ground", Prims.Cube, art.retroBase, root, false, true);
            ground.localPosition = new Vector3(0f, -0.16f, 0f);
            ground.localScale = new Vector3(grid.W + 14f, 0.2f, grid.H + 14f);

            for (int x = 0; x < grid.W; x++)
                for (int y = 0; y < grid.H; y++)
                {
                    var tile = Prims.Make("Tile", Prims.Cube, ((x + y) & 1) == 0 ? art.retroTileA : art.retroTileB, root, false, true);
                    tile.localPosition = grid.ToWorld(new Vector2Int(x, y), -0.05f);
                    tile.localScale = new Vector3(0.9f, 0.1f, 0.9f);
                }

            // One ring of cells around the board: the LCD border.
            for (int x = -1; x <= grid.W; x++)
                for (int y = -1; y <= grid.H; y++)
                {
                    if (x >= 0 && y >= 0 && x < grid.W && y < grid.H) continue;
                    var border = Prims.Make("Border", Prims.Cube, art.retroBorder, root);
                    border.localPosition = grid.ToWorld(new Vector2Int(x, y));
                    border.localScale = new Vector3(0.9f, 0.1f, 0.9f);
                }
        }
    }
}
