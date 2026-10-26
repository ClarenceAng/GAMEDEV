using UnityEngine;

namespace SnakesTemptation
{
    /// <summary>
    /// Collision detection in the 3D world, done by Unity physics. The hedges and trees are static box colliders (merged
    /// into rectangles, not one per cell); the seed, the lantern and the apple monster are trigger boxes; and the snake's
    /// head is a <see cref="HeadSensor"/>, a kinematic trigger sphere.
    ///
    /// After every move, <see cref="Detect"/> puts the colliders on their cells and runs one physics step itself
    /// (Physics.Simulate: the controller switches physics to script mode, so this is deterministic and independent of the
    /// frame rate). The head's OnTriggerEnter reports what it ran into, and that is applied to the rules.
    /// </summary>
    public sealed class CollisionWorld
    {
        const float Height = 0.5f;      // every collider sits on the same plane, half a cell up
        const float HeadRadius = 0.3f;  // under half a cell: the head can only touch what is on its own cell
        const float TriggerSize = 0.8f;

        readonly SnakeSim sim;
        readonly GridMap grid;
        readonly Transform root, seed, lantern, monster;
        readonly HeadSensor head;
        Transform obstacles;
        int obstacleCells = -1;

        /// <summary>The snake's head in the physics world.</summary>
        public HeadSensor Head => head;
        /// <summary>How many box colliders the hedges and trees take.</summary>
        public int ObstacleColliders { get; private set; }

        public CollisionWorld(Transform parent, GridMap grid, SnakeSim sim)
        {
            this.sim = sim;
            this.grid = grid;
            root = new GameObject("Colliders").transform;
            root.SetParent(parent, false);

            var headGo = new GameObject("Head");
            headGo.transform.SetParent(root, false);
            var body = headGo.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var sphere = headGo.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = HeadRadius;
            head = headGo.AddComponent<HeadSensor>();

            seed = Trigger("Seed", Contact.Seed);
            lantern = Trigger("Lantern", Contact.Lantern);
            monster = Trigger("Monster", Contact.Monster);
            Sync();
        }

        /// <summary>
        /// Runs collision detection for a move the sim has just made, and applies what the head ran into: a hedge or tree
        /// or the monster kills it (the most serious contact wins), a seed is eaten, a lantern is taken.
        /// </summary>
        public StepResult Detect(StepResult r)
        {
            if (r.Died || sim.Dead) return r;
            Sync();
            head.Touched.Clear();
            Physics.SyncTransforms();
            Physics.Simulate(Time.fixedDeltaTime);

            var touched = head.Touched;
            if (touched.Contains(Contact.Obstacle)) return Crashed(r, DeathCause.Obstacle);
            if (touched.Contains(Contact.Monster)) return Crashed(r, DeathCause.Monster);
            if (touched.Contains(Contact.Seed) && sim.EatApple()) r.Ate = true;
            if (touched.Contains(Contact.Lantern) && sim.TakePowerup()) r.GotPowerup = true;
            return r;
        }

        StepResult Crashed(StepResult r, DeathCause cause)
        {
            var crash = sim.Crash(cause);
            r.Died = crash.Died;
            r.Cause = crash.Cause;
            return r;
        }

        // Moves every collider to where the sim says it is. The obstacles are rebuilt only when the blocked cells change.
        void Sync()
        {
            if (sim.Blocked.Count != obstacleCells) BuildObstacles();
            Put(head.transform, sim.Head, true);
            Put(seed, sim.Apple, sim.HasApple);
            Put(lantern, sim.Powerup, sim.HasPowerup);
            Put(monster, sim.Monster, sim.MonsterActive);
        }

        void Put(Transform t, Vector2Int cell, bool present)
        {
            if (t.gameObject.activeSelf != present) t.gameObject.SetActive(present);
            if (present) t.localPosition = grid.ToWorld(cell, Height);
        }

        Transform Trigger(string name, Contact kind)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = Vector3.one * TriggerSize;
            go.AddComponent<Touchable>().kind = kind;
            go.SetActive(false);
            return go.transform;
        }

        // Greedy rectangles: each box grows right as far as it can, then down while the whole row below is blocked too.
        // The maze's long hedge lines become single colliders.
        void BuildObstacles()
        {
            if (obstacles != null)
            {
                obstacles.gameObject.SetActive(false); // leaves the physics scene now, not at the end of the frame
                if (Application.isPlaying) Object.Destroy(obstacles.gameObject);
                else Object.DestroyImmediate(obstacles.gameObject);
            }
            obstacles = new GameObject("Obstacles").transform;
            obstacles.SetParent(root, false);
            ObstacleColliders = 0;

            var taken = new bool[grid.W, grid.H];
            bool Free(int x, int y) => !taken[x, y] && sim.Blocked.Contains(new Vector2Int(x, y));
            for (int y = 0; y < grid.H; y++)
                for (int x = 0; x < grid.W; x++)
                {
                    if (!Free(x, y)) continue;
                    int w = 1;
                    while (x + w < grid.W && Free(x + w, y)) w++;
                    int h = 1;
                    while (y + h < grid.H && RowFree(x, y + h, w)) h++;
                    for (int i = 0; i < w; i++)
                        for (int j = 0; j < h; j++) taken[x + i, y + j] = true;

                    var go = new GameObject("Obstacle");
                    go.transform.SetParent(obstacles, false);
                    go.transform.localPosition = grid.ToWorld(new Vector2Int(x, y), Height) + new Vector3((w - 1) * 0.5f, 0f, (h - 1) * 0.5f);
                    go.AddComponent<BoxCollider>().size = new Vector3(w, 1f, h);
                    go.AddComponent<Touchable>().kind = Contact.Obstacle;
                    ObstacleColliders++;
                }
            obstacleCells = sim.Blocked.Count;

            bool RowFree(int x0, int y, int w)
            {
                for (int i = 0; i < w; i++) if (!Free(x0 + i, y)) return false;
                return true;
            }
        }
    }
}
