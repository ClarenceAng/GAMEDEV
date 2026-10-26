using UnityEngine;

// the hedges and the floor of the maze. one cube for each hedge cell and one tile for each floor cell.
// its hidden until the world flips, then the whole thing rises up out of the ground
public class MazeView
{
    Transform root;
    bool shown;

    public MazeView(Transform parent, GridMap grid, ArtSet art, MazeMap maze, RectInt retro)
    {
        root = new GameObject("Maze").transform;
        root.SetParent(parent, false);

        for (int x = 0; x < maze.Size; x++)
        {
            for (int y = 0; y < maze.Size; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                Vector3 pos = grid.ToWorld(cell);

                if (maze.Wall[x, y])
                {
                    // hedges are a little bit different heights so they look more natural
                    float height = Random.Range(1.55f, 1.77f);
                    Transform hedge = ShapeMaker.Make(PrimitiveType.Cube, "Hedge", art.hedge, root);
                    hedge.localPosition = pos + new Vector3(0f, height / 2f, 0f);
                    hedge.localScale = new Vector3(1f, height, 1f);
                }
                else if (!retro.Contains(cell)) // the retro board has its own tiles (ArenaView)
                {
                    Transform tile = ShapeMaker.MakeNoShadow(PrimitiveType.Quad, "Floor", FloorMaterial(art, maze, x, y), root);
                    tile.localPosition = pos;
                    tile.localRotation = Quaternion.Euler(90f, 0f, 0f); // lie flat
                    tile.localScale = new Vector3(0.97f, 0.97f, 1f);     // small gap so you can see the grid
                }
            }
        }
        root.gameObject.SetActive(false);
    }

    Material FloorMaterial(ArtSet art, MazeMap maze, int x, int y)
    {
        bool even = (x + y) % 2 == 0;
        bool plaza = maze.Plaza[x, y] && !maze.Start.Contains(new Vector2Int(x, y));
        if (plaza)
        {
            if (even) return art.plazaA;
            return art.plazaB;
        }
        if (even) return art.tileA;
        return art.tileB;
    }

    public void Show()
    {
        shown = true;
        root.gameObject.SetActive(true);
        Refresh(0f);
    }

    // morphTime = seconds since the world started flipping
    public void Refresh(float morphTime)
    {
        if (!shown) return;
        float t = Mathf.SmoothStep(0f, 1f, morphTime / 1.5f);
        root.localPosition = new Vector3(0f, Mathf.Lerp(-2f, 0f, t), 0f);
    }
}
