using System.Collections.Generic;
using UnityEngine;

// the retro board: the floor tiles, the border, and later the trees.
// in the retro look the tiles are flat lcd pixels. when the world flips a wave goes across the tiles,
// the border sinks into the ground and trees grow
public class ArenaView
{
    GridMap grid;
    ArtSet art;
    RectInt rect;               // the board, in map cells
    Transform root;
    Transform ground;
    Transform[,] tiles;
    List<Transform> border = new List<Transform>();
    List<Vector2Int> borderCells = new List<Vector2Int>();
    List<Transform> trees = new List<Transform>();
    List<float> treeDelay = new List<float>();
    bool lit;
    bool glitched;

    public ArenaView(Transform parent, GridMap grid, ArtSet art, RectInt rect)
    {
        this.grid = grid;
        this.art = art;
        this.rect = rect;
        root = new GameObject("Arena").transform;
        root.SetParent(parent, false);

        ground = ShapeMaker.MakeNoShadow(PrimitiveType.Cube, "Ground", art.retroBase, root);

        tiles = new Transform[rect.width, rect.height];
        for (int i = 0; i < rect.width; i++)
        {
            for (int j = 0; j < rect.height; j++)
            {
                tiles[i, j] = ShapeMaker.MakeNoShadow(PrimitiveType.Cube, "Tile", TileMaterial(i, j), root);
            }
        }

        // a ring of border squares around the board
        for (int x = rect.xMin - 1; x <= rect.xMax; x++)
        {
            for (int y = rect.yMin - 1; y <= rect.yMax; y++)
            {
                Vector2Int cell = new Vector2Int(x, y);
                if (rect.Contains(cell)) continue;
                border.Add(ShapeMaker.Make(PrimitiveType.Cube, "Border", art.retroBorder, root));
                borderCells.Add(cell);
            }
        }

        Layout(0f, 0f);
    }

    // checkerboard
    Material TileMaterial(int i, int j)
    {
        bool even = (i + j) % 2 == 0;
        if (lit)
        {
            if (even) return art.tileA;
            return art.tileB;
        }
        if (even) return art.retroTileA;
        return art.retroTileB;
    }

    public void Refresh(float look, float rippleTime, float glitch)
    {
        bool nowLit = look > 0.35f;
        if (nowLit != lit)
        {
            lit = nowLit;
            if (lit) ground.GetComponent<MeshRenderer>().sharedMaterial = art.ground;
            else ground.GetComponent<MeshRenderer>().sharedMaterial = art.retroBase;
            ResetTileColors();
        }
        Layout(look, rippleTime);
        Glitch(glitch);
    }

    void Layout(float look, float rippleTime)
    {
        float size = Mathf.Lerp(0.90f, 0.975f, look);
        float thickness = Mathf.Lerp(0.10f, 1.0f, look);
        Vector2 middle = new Vector2((rect.width - 1) * 0.5f, (rect.height - 1) * 0.5f);

        for (int i = 0; i < rect.width; i++)
        {
            for (int j = 0; j < rect.height; j++)
            {
                // a bump that moves out from the middle
                float bump = 0f;
                if (rippleTime > 0f)
                {
                    float d = Vector2.Distance(new Vector2(i, j), middle);
                    float e = (d - rippleTime * 13f) / 2.2f;
                    bump = Mathf.Exp(-e * e) * 0.55f;
                }
                Vector3 pos = grid.ToWorld(new Vector2Int(rect.xMin + i, rect.yMin + j));
                pos.y = -thickness * 0.5f + bump;
                tiles[i, j].localPosition = pos;
                tiles[i, j].localScale = new Vector3(size, thickness, size);
            }
        }

        // the border sinks away, thats where the board opens up into the maze
        float h = Mathf.Lerp(0.10f, 0f, Mathf.InverseLerp(0.15f, 0.55f, look));
        bool showBorder = h > 0.015f;
        for (int i = 0; i < border.Count; i++)
        {
            border[i].gameObject.SetActive(showBorder);
            if (!showBorder) continue;
            Vector3 pos = grid.ToWorld(borderCells[i]);
            pos.y = -0.05f + h * 0.5f;
            border[i].localPosition = pos;
            border[i].localScale = new Vector3(0.9f, h, 0.9f);
        }

        ground.localPosition = new Vector3(0f, Mathf.Lerp(-0.06f, -1.05f, look) - 0.1f, 0f);
        ground.localScale = new Vector3(grid.W + 14f, 0.2f, grid.H + 14f);
    }

    // right before the flip random tiles flash dark like the screen is breaking
    void Glitch(float amount)
    {
        if (amount <= 0f)
        {
            if (glitched)
            {
                ResetTileColors();
                glitched = false;
            }
            return;
        }
        ResetTileColors();
        int count = (int)(amount * 70f);
        for (int k = 0; k < count; k++)
        {
            Transform tile = tiles[Random.Range(0, rect.width), Random.Range(0, rect.height)];
            if (Random.value < 0.5f) tile.GetComponent<MeshRenderer>().sharedMaterial = art.retroSnake;
            else tile.GetComponent<MeshRenderer>().sharedMaterial = art.retroBorder;
        }
        glitched = true;
    }

    void ResetTileColors()
    {
        for (int i = 0; i < rect.width; i++)
        {
            for (int j = 0; j < rect.height; j++)
            {
                tiles[i, j].GetComponent<MeshRenderer>().sharedMaterial = TileMaterial(i, j);
            }
        }
    }

    public void PlantTrees(List<Vector2Int> cells)
    {
        foreach (Vector2Int cell in cells)
        {
            Transform tree = new GameObject("Tree").transform;
            tree.SetParent(root, false);
            tree.localPosition = grid.ToWorld(cell);
            tree.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            Transform trunk = ShapeMaker.Make(PrimitiveType.Cylinder, "Trunk", art.trunk, tree);
            trunk.localPosition = new Vector3(0f, 0.45f, 0f);
            trunk.localScale = new Vector3(0.24f, 0.45f, 0.24f);
            Transform leaves = ShapeMaker.Make(PrimitiveType.Sphere, "Leaves", art.foliage, tree);
            leaves.localPosition = new Vector3(0f, 1.25f, 0f);
            leaves.localScale = new Vector3(1.15f, 1.0f, 1.15f);
            Transform top = ShapeMaker.Make(PrimitiveType.Sphere, "LeavesTop", art.foliage, tree);
            top.localPosition = new Vector3(0.1f, 1.85f, 0.05f);
            top.localScale = new Vector3(0.75f, 0.75f, 0.75f);

            tree.localScale = Vector3.zero;
            trees.Add(tree);
            treeDelay.Add(Random.Range(0f, 0.45f));
        }
    }

    public void GrowTrees(float time)
    {
        for (int i = 0; i < trees.Count; i++)
        {
            float grow = Mathf.Clamp01((time - treeDelay[i]) / 0.5f);
            trees[i].localScale = Vector3.one * Mathf.SmoothStep(0f, 1f, grow);
        }
    }
}
