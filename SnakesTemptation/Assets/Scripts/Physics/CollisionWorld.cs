using UnityEngine;

// collision detection for the 3D maze, using unity physics.
// hedges and trees are box colliders, the seed, lantern and monster are triggers,
// and the snake head is a small trigger sphere with a HeadSensor on it.
// after every move we put everything in place and step the physics once ourselves
// (Physics.Simulate, the game sets physics to script mode)
public class CollisionWorld
{
    SnakeSim sim;
    GridMap grid;
    Transform root;
    Transform obstacles;
    Transform seed;
    Transform lantern;
    Transform monster;
    bool[,] used;
    int lastBlockedCount = -1;

    float height = 0.5f; // all the colliders sit half a cell off the ground

    public HeadSensor Head;
    public int ObstacleColliders; // how many boxes the hedges and trees use

    public CollisionWorld(Transform parent, GridMap grid, SnakeSim sim)
    {
        this.sim = sim;
        this.grid = grid;
        root = new GameObject("Colliders").transform;
        root.SetParent(parent, false);

        // the snake head
        GameObject headObj = new GameObject("Head");
        headObj.transform.SetParent(root, false);
        Rigidbody body = headObj.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        SphereCollider sphere = headObj.AddComponent<SphereCollider>();
        sphere.isTrigger = true;
        sphere.radius = 0.3f; // less than half a cell so it can only touch stuff on its own cell
        Head = headObj.AddComponent<HeadSensor>();

        seed = MakeTrigger("Seed", Contact.Seed);
        lantern = MakeTrigger("Lantern", Contact.Lantern);
        monster = MakeTrigger("Monster", Contact.Monster);
        MoveColliders();
    }

    // call this right after the sim moved. it adds what the head ran into to the result
    public StepResult Detect(StepResult result)
    {
        if (result.Died || sim.Dead) return result;

        MoveColliders();
        Head.Touched.Clear();
        Physics.SyncTransforms();
        Physics.Simulate(Time.fixedDeltaTime);

        // hitting a hedge or the monster is worse than anything else so check those first
        if (Head.Touched.Contains(Contact.Obstacle)) return Crash(result, DeathCause.Obstacle);
        if (Head.Touched.Contains(Contact.Monster)) return Crash(result, DeathCause.Monster);

        if (Head.Touched.Contains(Contact.Seed))
        {
            if (sim.EatApple()) result.Ate = true;
        }
        if (Head.Touched.Contains(Contact.Lantern))
        {
            if (sim.TakePowerup()) result.GotPowerup = true;
        }
        return result;
    }

    StepResult Crash(StepResult result, DeathCause cause)
    {
        StepResult crash = sim.Crash(cause);
        result.Died = crash.Died;
        result.Cause = crash.Cause;
        return result;
    }

    // puts every collider where the sim says things are
    void MoveColliders()
    {
        // only rebuild the hedge boxes when the walls changed
        if (sim.Blocked.Count != lastBlockedCount) BuildObstacles();

        PutOnCell(Head.transform, sim.Head, true);
        PutOnCell(seed, sim.Apple, sim.HasApple);
        PutOnCell(lantern, sim.Powerup, sim.HasPowerup);
        PutOnCell(monster, sim.Monster, sim.MonsterActive);
    }

    void PutOnCell(Transform t, Vector2Int cell, bool show)
    {
        if (t.gameObject.activeSelf != show) t.gameObject.SetActive(show);
        if (show) t.localPosition = grid.ToWorld(cell, height);
    }

    Transform MakeTrigger(string name, Contact kind)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        BoxCollider box = obj.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(0.8f, 0.8f, 0.8f);
        obj.AddComponent<Touchable>().kind = kind;
        obj.SetActive(false);
        return obj.transform;
    }

    // one box per wall cell would be thousands of colliders, so walls next to each other get joined into rectangles.
    // each box goes right as far as it can, then down as long as the whole row under it is wall too
    void BuildObstacles()
    {
        if (obstacles != null)
        {
            obstacles.gameObject.SetActive(false); // turn it off first so it leaves the physics right away
            Object.Destroy(obstacles.gameObject);
        }
        obstacles = new GameObject("Obstacles").transform;
        obstacles.SetParent(root, false);
        ObstacleColliders = 0;

        used = new bool[grid.W, grid.H];
        for (int y = 0; y < grid.H; y++)
        {
            for (int x = 0; x < grid.W; x++)
            {
                if (!IsUnusedWall(x, y)) continue;

                int w = 1;
                while (x + w < grid.W && IsUnusedWall(x + w, y)) w++;

                int h = 1;
                while (y + h < grid.H && IsUnusedRow(x, y + h, w)) h++;

                for (int i = 0; i < w; i++)
                {
                    for (int j = 0; j < h; j++)
                    {
                        used[x + i, y + j] = true;
                    }
                }

                GameObject box = new GameObject("Obstacle");
                box.transform.SetParent(obstacles, false);
                box.transform.localPosition = grid.ToWorld(new Vector2Int(x, y), height) + new Vector3((w - 1) * 0.5f, 0f, (h - 1) * 0.5f);
                box.AddComponent<BoxCollider>().size = new Vector3(w, 1f, h);
                box.AddComponent<Touchable>().kind = Contact.Obstacle;
                ObstacleColliders++;
            }
        }
        lastBlockedCount = sim.Blocked.Count;
    }

    bool IsUnusedWall(int x, int y)
    {
        return !used[x, y] && sim.Blocked.Contains(new Vector2Int(x, y));
    }

    bool IsUnusedRow(int x, int y, int w)
    {
        for (int i = 0; i < w; i++)
        {
            if (!IsUnusedWall(x + i, y)) return false;
        }
        return true;
    }
}
