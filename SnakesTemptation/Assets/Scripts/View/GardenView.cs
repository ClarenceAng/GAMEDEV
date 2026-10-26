using UnityEngine;

// the ending: a sunny garden where the snake plants the 3 seeds, the trees grow, and eve walks up to the big tree.
// everything here only depends on the time t. Pose(t) puts everything where it should be at that moment,
// so it plays the same at any frame rate
public class GardenView
{
    // where everything is (x = right, z = away from the camera)
    Vector3[] spots = { new Vector3(-5.6f, 0f, 1.7f), new Vector3(5.6f, 0f, 1.7f), new Vector3(0f, 0f, 3.7f) };
    Vector3[] stops = { new Vector3(-5.6f, 0f, 0.55f), new Vector3(5.6f, 0f, 0.55f), new Vector3(-0.35f, 0f, 2.35f) }; // where the snake stops to plant
    float[] heights = { 5.2f, 5.0f, 6.6f };
    Vector3 bigTree = new Vector3(0f, 0f, 3.7f);
    Vector3 hangHead = new Vector3(-0.72f, 1.95f, 3.30f);   // where the snake's head ends up, looking at eve
    Vector3 eveMark = new Vector3(-1.50f, 0f, 2.62f);       // where eve stops
    Vector3 appleSpot = new Vector3(-0.95f, 1.90f, 2.55f);  // the apple on the low branch

    Camera cam;
    CutscenePlan plan;
    GardenMaterials mats;
    Transform root;
    GardenTree[] trees = new GardenTree[3];
    Transform[] mounds = new Transform[3];
    Transform seedFlight;
    CutsceneSnake snake;
    EveView eve;
    Transform apple;
    Transform bite;
    Transform branch;
    Light appleLight;

    GardenPath path = new GardenPath();     // the snake's path
    GardenPath evePath = new GardenPath();
    float[] stopDistance = new float[3];    // how far along the path each planting stop is
    float groundEnd;                        // where the grass ends and the climb starts
    float climbTop;
    float[] keyTimes;                       // when the snake is where (for HeadDistance)
    float[] keyDistances;

    public GardenView(Transform parent, Camera cam, ArtSet art, CutscenePlan plan)
    {
        this.cam = cam;
        this.plan = plan;
        mats = new GardenMaterials(art);
        root = new GameObject("Garden").transform;
        root.SetParent(parent, false);

        BuildGround();

        // the 3 trees and the dirt mounds the seeds go in
        for (int i = 0; i < 3; i++)
        {
            if (i == 2) trees[i] = new GardenTree(root, mats, spots[i], heights[i], 0.46f, 12, 6); // the big one
            else trees[i] = new GardenTree(root, mats, spots[i], heights[i], 0.34f, 9, 8);
            mounds[i] = ShapeMaker.Make(PrimitiveType.Sphere, "Mound", mats.dirt, root);
            mounds[i].localPosition = spots[i] + Vector3.up * 0.02f;
            mounds[i].localScale = Vector3.zero;
        }
        seedFlight = ShapeMaker.Make(PrimitiveType.Sphere, "Seed", art.seed, root);
        seedFlight.localScale = new Vector3(0.15f, 0.26f, 0.1f);
        seedFlight.gameObject.SetActive(false);

        // the low branch on the big tree with THE apple on it
        Vector3 branchStart = bigTree + new Vector3(-0.30f, 2.55f, -0.30f);
        Vector3 branchEnd = appleSpot + new Vector3(-0.05f, 0.34f, 0.05f);
        branch = ShapeMaker.Make(PrimitiveType.Cylinder, "Branch", mats.trunk, root);
        branch.localPosition = (branchStart + branchEnd) / 2f;
        branch.up = branchEnd - branchStart;
        branch.localScale = new Vector3(0.1f, Vector3.Distance(branchStart, branchEnd) / 2f, 0.1f);

        apple = ShapeMaker.Make(PrimitiveType.Sphere, "TemptingApple", mats.appleHero, root);
        apple.localScale = new Vector3(0.27f, 0.27f, 0.27f);
        Transform stem = ShapeMaker.MakeNoShadow(PrimitiveType.Cylinder, "Stem", mats.stem, apple);
        stem.localPosition = new Vector3(0f, 0.52f, 0f);
        stem.localScale = new Vector3(0.07f, 0.14f, 0.07f);
        Transform leaf = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Leaf", mats.leafGreen, apple);
        leaf.localPosition = new Vector3(0.17f, 0.62f, 0f);
        leaf.localScale = new Vector3(0.3f, 0.06f, 0.16f);
        leaf.localRotation = Quaternion.Euler(0f, 0f, 25f);
        bite = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Bite", mats.flesh, apple);
        bite.localPosition = new Vector3(0.42f, 0.05f, 0f);
        bite.localScale = Vector3.zero;
        appleLight = ShapeMaker.MakeLight("AppleLight", root, new Color(1f, 0.55f, 0.25f), 5f, 0f);

        // the snake's path: over the grass to each planting stop...
        path.Add(new Vector3(-21f, 0f, -1.2f));
        path.Add(new Vector3(-13f, 0f, 0.1f));
        path.Add(new Vector3(-8f, 0f, 0.6f));
        path.Add(stops[0]);
        stopDistance[0] = path.Length;
        path.Add(new Vector3(-4f, 0f, 0.1f));
        path.Add(new Vector3(-2.8f, 0f, -0.4f));
        path.Add(new Vector3(0f, 0f, -0.6f));
        path.Add(new Vector3(1.6f, 0f, -0.5f));
        path.Add(new Vector3(3.5f, 0f, -0.1f));
        path.Add(stops[1]);
        stopDistance[1] = path.Length;
        path.Add(new Vector3(6.0f, 0f, 1.1f));
        path.Add(new Vector3(5.2f, 0f, 1.5f));
        path.Add(new Vector3(3.6f, 0f, 1.6f));
        path.Add(new Vector3(1.4f, 0f, 1.8f));
        path.Add(stops[2]);
        stopDistance[2] = path.Length;

        // ...then round and round up the big tree...
        Vector3 treeBase = bigTree + new Vector3(0f, 0f, -0.72f);
        path.Add(Vector3.Lerp(stops[2], treeBase, 0.5f));
        path.Add(treeBase);
        groundEnd = path.Length;
        for (int k = 1; k <= 140; k++)
        {
            float f = k / 140f;
            float angle = (-90f + 630f * f) * Mathf.Deg2Rad;
            path.Add(new Vector3(bigTree.x + Mathf.Cos(angle) * 0.72f, 0.1f + 3.3f * f, bigTree.z + Mathf.Sin(angle) * 0.72f));
        }
        // ...and out along a branch to hang down in front of eve
        Vector3 top = path.At(path.Length);
        for (int k = 1; k <= 36; k++)
        {
            float f = k / 36f;
            path.Add(Vector3.Lerp(top, hangHead, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 0.4f);
        }
        climbTop = path.Length;

        // when the snake is where on the path. it stops at each planting spot for a bit
        keyTimes = new float[] { 0f, plan.Arrive[0], plan.DepartAt(0), plan.Arrive[1], plan.DepartAt(1), plan.Arrive[2], plan.ClimbStart, plan.ClimbStart + 0.9f, plan.ClimbEnd };
        keyDistances = new float[] { 0f, stopDistance[0], stopDistance[0], stopDistance[1], stopDistance[1], stopDistance[2], stopDistance[2], groundEnd, climbTop };

        evePath.Add(new Vector3(11f, 0f, -2.0f));
        evePath.Add(new Vector3(5f, 0f, -1.6f));
        evePath.Add(new Vector3(1.5f, 0f, -1.0f));
        evePath.Add(new Vector3(-0.5f, 0f, -0.2f));
        evePath.Add(new Vector3(-1.3f, 0f, 0.6f));
        evePath.Add(new Vector3(-1.5f, 0f, 1.4f));
        evePath.Add(eveMark);

        snake = new CutsceneSnake(root, art, mats);
        eve = new EveView(root, mats);

        Pose(0f);
    }

    void BuildGround()
    {
        Transform grass = ShapeMaker.MakeNoShadow(PrimitiveType.Cylinder, "Ground", mats.grass, root);
        grass.localScale = new Vector3(130f, 0.1f, 130f);
        grass.localPosition = new Vector3(0f, -0.1f, 0f);
        Transform clearing = ShapeMaker.MakeNoShadow(PrimitiveType.Cylinder, "Clearing", mats.grassLight, root);
        clearing.localScale = new Vector3(17f, 0.012f, 11f);
        clearing.localPosition = new Vector3(0f, 0.006f, 1.8f);

        // just a big glowing ball in the sky, the real light is the sun light in the scene
        Transform sun = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "SunDisc", mats.sun, root);
        sun.localPosition = new Vector3(-30f, 15f, 85f);
        sun.localScale = new Vector3(16f, 16f, 16f);

        // flowers
        for (int i = 0; i < 110; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(3f, 29f);
            Vector3 p = new Vector3(Mathf.Cos(angle) * r * 1.3f, 0f, 2f + Mathf.Sin(angle) * r);
            bool inClearing = Mathf.Abs(p.x) < 9f && p.z > -3.5f && p.z < 6f;
            if (inClearing && Random.value < 0.7f) continue; // fewer flowers in the middle
            Transform flowerStem = ShapeMaker.MakeNoShadow(PrimitiveType.Cylinder, "Stem", mats.leafGreen, root);
            flowerStem.localPosition = p + Vector3.up * 0.1f;
            flowerStem.localScale = new Vector3(0.025f, 0.1f, 0.025f);
            Transform flower = ShapeMaker.MakeNoShadow(PrimitiveType.Sphere, "Flower", mats.flowers[Random.Range(0, 4)], root);
            flower.localPosition = p + Vector3.up * 0.22f;
            flower.localScale = new Vector3(0.14f, 0.09f, 0.14f);
        }

        // bushes
        for (int i = 0; i < 16; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(9f, 23f);
            Vector3 p = new Vector3(Mathf.Cos(angle) * r * 1.3f, 0f, 2f + Mathf.Sin(angle) * r);
            if (Mathf.Abs(p.x) < 8f && p.z > -4f && p.z < 6f) continue;
            for (int k = 0; k < 3; k++)
            {
                Material mat = mats.leafGreen;
                if (k == 1) mat = mats.leafDark;
                Transform bush = ShapeMaker.Make(PrimitiveType.Sphere, "Bush", mat, root);
                float size = Random.Range(0.9f, 1.7f);
                bush.localPosition = p + new Vector3((k - 1) * 0.7f, size * 0.35f, Random.Range(0f, 0.5f));
                bush.localScale = new Vector3(size, size * 0.75f, size);
            }
        }

        // normal trees in a circle around the clearing
        for (int i = 0; i < 34; i++)
        {
            float angle = (i + Random.Range(0f, 0.7f)) / 34f * Mathf.PI * 2f;
            float r = Random.Range(20f, 36f);
            Vector3 p = new Vector3(Mathf.Cos(angle) * r * 1.35f, 0f, 2f + Mathf.Sin(angle) * r);
            if (p.z < -9f && Mathf.Abs(p.x) < 14f) continue; // keep the camera side clear
            GardenTree tree = new GardenTree(root, mats, p, Random.Range(5.5f, 8.5f), 0.34f, 6, 0);
            tree.Grow(1f);
        }
    }

    // smooth 0 to 1 between time a and time b
    float Smooth(float a, float b, float t)
    {
        return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, t));
    }

    // how far along its path the snake's head is at time t
    float HeadDistance(float t)
    {
        if (t <= keyTimes[0]) return keyDistances[0];
        for (int i = 1; i < keyTimes.Length; i++)
        {
            if (t <= keyTimes[i])
            {
                float k = Mathf.InverseLerp(keyTimes[i - 1], keyTimes[i], t);
                return Mathf.Lerp(keyDistances[i - 1], keyDistances[i], Mathf.SmoothStep(0f, 1f, k));
            }
        }
        return keyDistances[keyDistances.Length - 1];
    }

    public void Pose(float t)
    {
        // ---- trees and mounds
        for (int i = 0; i < 3; i++)
        {
            trees[i].Grow((t - plan.GrowStart(i)) / plan.GrowSeconds(i));
            float mound = Mathf.Clamp01((t - (plan.SeedAt(i) + 0.45f)) / 0.5f);
            mounds[i].localScale = new Vector3(0.95f, 0.26f, 0.95f) * mound;
        }

        // ---- the snake
        float headDistance = HeadDistance(t);
        float lift = 0f;
        float pitch = 0f;
        for (int i = 0; i < 3; i++)
        {
            // at each spot the head goes up, then down to spit out the seed
            float tt = t - plan.Arrive[i];
            if (tt > 0f && tt < CutscenePlan.PlantDwell + 0.2f)
            {
                lift = 0.65f * Smooth(0.15f, 0.55f, tt) * (1f - Smooth(0.6f, 0.95f, tt));
                pitch = 48f * Smooth(0.55f, 0.95f, tt) * (1f - Smooth(1.25f, 1.85f, tt));
            }
        }
        bool moving = Mathf.Abs(HeadDistance(t + 0.05f) - HeadDistance(t - 0.05f)) > 0.004f;
        float wave = 0.4f;
        if (moving) wave = 1f;

        int line = plan.LineAt(t);
        bool snakeTalks = line >= 0 && CutsceneScript.Lines[line].Who == Speaker.Snake;
        bool eveTalks = line >= 0 && CutsceneScript.Lines[line].Who == Speaker.Eve;

        float lookWeight = Smooth(plan.ClimbEnd - 1.5f, plan.ClimbEnd + 0.5f, t);
        float mouthOpen = 0.05f;
        if (snakeTalks) mouthOpen = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(t * 13f));
        float tongue = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 2.3f)), 6f);
        if (snakeTalks) tongue = Mathf.Max(tongue, Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * 5.1f)), 4f) * 0.8f);
        if (t > plan.BiteAt)
        {
            // after eve bites the snake grins with its tongue out
            mouthOpen = 0f;
            tongue = Mathf.Max(tongue, Smooth(plan.BiteAt, plan.BiteAt + 0.5f, t));
        }
        snake.Pose(path, headDistance, groundEnd, t, wave, lift, pitch, eve.FaceWorld, lookWeight, mouthOpen, tongue);

        // ---- the seed flying from the snake's mouth into the ground
        seedFlight.gameObject.SetActive(false);
        for (int i = 0; i < 3; i++)
        {
            float f = (t - plan.SeedAt(i)) / 0.5f;
            if (f < 0f || f > 1f) continue;
            Vector3 from = snake.MouthWorld;
            Vector3 to = spots[i] + Vector3.up * 0.15f;
            seedFlight.gameObject.SetActive(true);
            seedFlight.localPosition = Vector3.Lerp(from, to, f) + Vector3.up * Mathf.Sin(f * Mathf.PI) * 0.55f;
            seedFlight.localRotation = Quaternion.Euler(f * 540f, 0f, 20f);
        }

        // ---- eve walks in
        float walkProgress = Mathf.Clamp01((t - plan.EveEnter) / (plan.EveArrive - plan.EveEnter));
        float walked = evePath.Length * Mathf.SmoothStep(0f, 1f, walkProgress);
        Vector3 evePos = evePath.At(walked);
        bool walking = t > plan.EveEnter && t < plan.EveArrive;
        Vector3 dir = evePath.Direction(walked);
        float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
        yaw = Mathf.LerpAngle(yaw, 90f, Smooth(plan.EveArrive - 1.4f, plan.EveArrive + 0.3f, t)); // turn to face the tree
        eve.SetVisible(t > plan.EveEnter - 0.05f);

        // her right hand reaches for the apple, takes it, and brings it to her mouth
        Vector3 handAtApple = appleSpot + new Vector3(-0.16f, -0.05f, -0.1f);
        Vector3 handAtMouth = eve.MouthWorld + new Vector3(0.1f, -0.03f, -0.02f);
        float reach = Smooth(plan.ReachAt, plan.GrabAt, t);
        float toMouth = Smooth(plan.GrabAt + 0.1f, plan.BiteAt - 0.05f, t);
        Vector3 hand = Vector3.Lerp(handAtApple, handAtMouth, toMouth);
        float talk = 0f;
        if (eveTalks) talk = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 12f));
        Vector3 eveLookAt = snake.HeadPos;
        if (line < 0) eveLookAt = Vector3.Lerp(snake.HeadPos, appleSpot, 0.7f); // between lines she looks at the apple
        float eveLookWeight = Smooth(plan.EveArrive - 0.6f, plan.EveArrive + 0.6f, t) * 0.9f;
        eve.Pose(evePos, yaw, walked * 3.6f, walking, t, talk, eveLookAt, eveLookWeight, hand, reach);

        // ---- the apple hangs on the branch, then she takes it, then she bites it
        if (t >= plan.GrabAt) apple.position = eve.RightHandWorld + new Vector3(0f, 0.1f, 0f);
        else apple.localPosition = appleSpot + Vector3.up * Mathf.Sin(t * 1.8f) * 0.02f;
        bite.localScale = Vector3.one * 0.55f * Mathf.Clamp01((t - plan.BiteAt) / 0.2f);
        bool appleVisible = trees[2].Root.gameObject.activeSelf && t >= plan.GrowEnd(2) - 1.4f;
        apple.gameObject.SetActive(appleVisible);
        branch.gameObject.SetActive(appleVisible);

        float glow = Smooth(plan.GrowEnd(2), plan.EveArrive + 2f, t);
        float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3.4f);
        appleLight.transform.position = apple.position + new Vector3(-0.2f, 0.1f, -0.5f);
        appleLight.intensity = 0f;
        if (appleVisible) appleLight.intensity = (0.5f + 2.2f * glow) * pulse;
        if (t > plan.BiteAt && t < plan.BiteAt + 0.4f) appleLight.intensity += 3f * (1f - (t - plan.BiteAt) / 0.4f); // flash when she bites

        PlaceCamera(t);
    }

    // the camera shots, one after the other
    void PlaceCamera(float t)
    {
        if (t < plan.Arrive[0] - 0.9f)
        {
            // following the snake as it comes in
            SetCamera(new Vector3(-12.5f + t * 0.45f, 2.3f, -8f), new Vector3(-8f, 0.8f, 0.8f), 42f);
        }
        else if (t < plan.Arrive[0] + 1.7f)
        {
            // planting the first seed
            float d = t - plan.Arrive[0];
            SetCamera(spots[0] + new Vector3(3.6f, 1.9f, -5.3f + d * 0.12f), spots[0] + new Vector3(0f, 0.8f, -0.2f), 40f);
        }
        else if (t < plan.Arrive[1] - 0.6f)
        {
            // watching the first tree grow
            float d = t - plan.Arrive[0];
            SetCamera(new Vector3(spots[0].x - 2.2f, 2.7f, -8.2f + d * 0.1f), new Vector3(spots[0].x, 2.2f, spots[0].z), 46f);
        }
        else if (t < plan.Arrive[1] + 1.7f)
        {
            float d = t - plan.Arrive[1];
            SetCamera(spots[1] + new Vector3(-3.6f, 1.9f, -5.3f + d * 0.12f), spots[1] + new Vector3(0f, 0.8f, -0.2f), 40f);
        }
        else if (t < plan.Arrive[2] - 0.7f)
        {
            float d = t - plan.Arrive[1];
            SetCamera(new Vector3(spots[1].x + 2.2f, 2.7f, -8.2f + d * 0.1f), new Vector3(spots[1].x, 2.2f, spots[1].z), 46f);
        }
        else if (t < plan.DepartAt(2) - 0.2f)
        {
            // the last seed
            float d = t - plan.Arrive[2];
            SetCamera(new Vector3(0.9f, 1.9f, -3.9f + d * 0.1f), new Vector3(-0.2f, 0.85f, 2.6f), 44f);
        }
        else if (t < plan.DepartAt(2) + 3.6f)
        {
            // the camera goes up and back while the big tree grows
            float k = Smooth(plan.DepartAt(2) - 0.2f, plan.DepartAt(2) + 3.4f, t);
            Vector3 pos = Vector3.Lerp(new Vector3(0.5f, 1.8f, -6.5f), new Vector3(1f, 4.6f, -15.5f), k);
            SetCamera(pos, new Vector3(0f, Mathf.Lerp(1.6f, 3.2f, k), 3.4f), Mathf.Lerp(48f, 54f, k));
        }
        else if (t < plan.LineStart[0] - 0.15f)
        {
            // eve walks in
            SetCamera(new Vector3(0.6f - (t - plan.EveEnter) * 0.05f, 2.8f, -11f), new Vector3(-0.3f, 2.0f, 2.6f), 46f);
        }
        else if (t < plan.DialogueEnd + 0.35f)
        {
            // the talking: a wide shot, then close ups of whoever is talking
            int shotLine = 0;
            for (int i = 0; i < plan.LineStart.Length; i++)
            {
                if (t >= plan.LineStart[i] - 0.15f) shotLine = i;
            }
            float d = t - plan.DialogueStart;
            if (shotLine == 0 || shotLine == 6)
            {
                SetCamera(new Vector3(-0.2f + d * 0.02f, 2.15f, -3.6f), new Vector3(-1.0f, 1.95f, 3.0f), 38f);
            }
            else if (CutsceneScript.Lines[shotLine].Who == Speaker.Snake)
            {
                SetCamera(new Vector3(-0.15f + d * 0.004f, 2.05f, 0.55f), snake.HeadPos, 30f);
            }
            else
            {
                SetCamera(new Vector3(-1.05f + d * 0.004f, 1.95f, 0.45f), eve.FaceWorld + new Vector3(0.35f, -0.03f, 0f), 30f);
            }
        }
        else if (t < plan.BiteAt - 0.1f)
        {
            // her hand and the apple
            Vector3 look = Vector3.Lerp(appleSpot, eve.FaceWorld, Smooth(plan.GrabAt, plan.BiteAt, t));
            SetCamera(new Vector3(-0.1f, 2.0f, 0.5f), look, 33f);
        }
        else if (t < plan.End - 0.6f)
        {
            // she bites, then we see the snake grin
            Vector3 look = Vector3.Lerp(eve.FaceWorld, snake.HeadPos, Mathf.SmoothStep(0f, 1f, (t - plan.BiteAt) / 0.9f));
            SetCamera(new Vector3(-0.5f + (t - plan.BiteAt) * 0.1f, 2.1f, 0.4f), look, 30f);
        }
        else
        {
            // pull back at the end
            float d = t - plan.End;
            SetCamera(new Vector3(0.8f, 3.4f + d * 0.12f, -9.5f - d * 0.3f), new Vector3(-0.5f, 2.0f, 3.2f), 46f);
        }
    }

    void SetCamera(Vector3 pos, Vector3 lookAt, float fov)
    {
        cam.transform.position = pos;
        cam.transform.LookAt(lookAt);
        cam.fieldOfView = fov;
    }

    public void Dispose()
    {
        if (root != null) Object.Destroy(root.gameObject);
        mats.Dispose();
    }
}
