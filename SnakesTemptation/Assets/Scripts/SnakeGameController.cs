using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public enum GameState { Title, Retro, Transition, Chase, Respawn, Cutscene, Won, Lost }

// SNAKE'S TEMPTATION
// it starts as a retro lcd snake game. eat 3 apples and the world turns 3D: the board opens into a big hedge maze,
// the camera goes behind the snake and an apple monster comes out of the ground and chases you.
// you can never stop. get 3 apple seeds from the plazas to escape, then the ending plays.
//
// everything happens in Advance(), which Update() calls every frame. the tests turn on manualTime
// and call Advance() themselves so they can play the game really fast
public class SnakeGameController : MonoBehaviour
{
    [Header("Assets")]
    public ArtSet art;
    public SoundSet sounds;     // Assets/ScriptableObjects/SoundSet.asset
    public GameEvents events;   // Assets/ScriptableObjects/GameEvents.asset
    public Camera cam;
    public Light sun;

    [Header("Rules")]
    public int seed = 0;                // 0 = different maze every time
    public int applesToFlip = 3;        // apples before the world turns 3D
    public int seedsToWin = ChaseTuning.SeedsToWin;
    public int livesIn3D = ChaseTuning.Lives;
    public float seedSpacing = ChaseTuning.SeedSpacing;

    [Header("Ending")]
    public bool playCutscene = true;
    public AudioClip[] cutsceneVoice;   // voice acting, one clip per line (we don't have any yet, it just shows subtitles)

    [Header("Maze")]
    public int mazeCells = ChaseTuning.MazeCells;
    public int roomCount = ChaseTuning.RoomCount;
    public int treesPerRoom = ChaseTuning.TreesPerRoom;
    public int startPlazaTrees = ChaseTuning.StartPlazaTrees;

    [Header("Speed (seconds per step, lower = faster)")]
    public float retroInterval = 0.13f;
    public float chaseInterval = ChaseTuning.SnakeInterval;
    public float monsterIntervalStart = ChaseTuning.MonsterStart;
    public float monsterIntervalMin = ChaseTuning.MonsterMin;
    public float monsterSpeedUpPerSeed = ChaseTuning.MonsterPerSeed;
    public float monsterSpeedUpPerSecond = ChaseTuning.MonsterPerSecond;

    [Header("Lantern")]
    public float lanternSeconds = ChaseTuning.PowerupDuration;
    public float firstLanternDelay = ChaseTuning.PowerupFirstDelay;
    public float lanternRespawnDelay = ChaseTuning.PowerupRespawnDelay;

    [Header("Audio")]
    [Range(0f, 1f)] public float audioVolume = 0.4f;

    public bool manualTime; // for the tests

    // game state (public so the tests can check it)
    [HideInInspector] public GameState State;
    [HideInInspector] public float StateTime;   // seconds since the state started
    [HideInInspector] public float Look;        // 0 = retro, 1 = 3D
    [HideInInspector] public float Dark;        // 0 = evening, 1 = pitch black
    [HideInInspector] public int ChaseSeeds;
    [HideInInspector] public int Lives;
    [HideInInspector] public bool Jumpscaring;
    [HideInInspector] public RectInt RetroRect;
    [HideInInspector] public RetroAudio Audio;
    public SnakeSim Sim;
    public MazeMap Maze;
    public PowerupRules Rules;
    public CollisionWorld Collisions;   // null on the retro board
    public CutscenePlan Plan;           // null until the ending starts

    // timeline of the flip, in seconds after the last apple
    const float glitchEnd = 0.8f;       // the screen glitches and shakes
    const float morphEnd = 2.6f;        // white flash, then 2D turns into 3D and the maze comes up
    const float emergeEnd = 3.7f;       // the monster climbs out
    const float lungeSeconds = 0.22f;   // how fast the monster flies at the camera
    const float jumpscareSeconds = 1.35f;

    Color lcdDark = new Color32(15, 56, 15, 255);
    Color lcdMid = new Color32(139, 172, 15, 255);
    Color dusk = new Color(0.045f, 0.035f, 0.08f);

    GridMap grid;
    Transform worldRoot;
    ArenaView arena;
    MazeView mazeView;
    AppleView appleView;
    SnakeView snakeView;
    MonsterView monsterView;
    PowerupView powerupView;
    CameraRig rig;
    Hud hud;
    GardenView garden;
    Vignette vignette;
    Bloom bloom;

    float globalTime;
    float snakeTimer;
    float monsterTimer;
    float chaseTime;
    float glow;
    float lanternBanner;
    float jumpFlash;
    float monsterRise = 1f;
    float respawnDelay;
    float hintTime;
    float danger;
    float pathDistTimer;
    float lastCutsceneTime;
    int pathDist = int.MaxValue;
    bool wasRevealed;
    bool lostToMonster;
    bool startRequested;
    bool monsterSpawned;
    Vector2Int monsterSpawnCell;

    public bool SeedRevealed
    {
        get { return Rules.Revealed; }
    }

    public float GlowLeft
    {
        get { return Rules.GlowLeft; }
    }

    public float CutsceneTime
    {
        get
        {
            if (State == GameState.Cutscene) return StateTime;
            return 0f;
        }
    }

    // how far the monster's eyes are from the camera (for the jumpscare test)
    public float MonsterFaceDistance
    {
        get
        {
            if (monsterView == null) return float.PositiveInfinity;
            return Vector3.Distance(monsterView.FaceWorld, cam.transform.position);
        }
    }

    void Awake()
    {
        if (cam == null) cam = Camera.main;

        Audio = gameObject.AddComponent<RetroAudio>();
        Audio.Load(sounds);
        Audio.Listen(events);

        rig = new CameraRig(cam);
        rig.Listen(events);

        SetupPostProcessing();
        hud = new Hud(transform, cam);
        BuildWorld();
    }

    void OnDestroy()
    {
        // the event assets stay around after the scene is gone so we have to stop listening
        if (rig != null) rig.Dispose();
    }

    void Update()
    {
        ReadInput();
        if (!manualTime) Advance(Time.deltaTime);
    }

    // tells everyone that is listening (the audio and the camera)
    void Raise(GameEvent gameEvent)
    {
        if (gameEvent != null) gameEvent.Raise();
    }

    void SetupPostProcessing()
    {
        GameObject volumeObj = new GameObject("PostFX");
        volumeObj.transform.SetParent(transform, false);
        Volume volume = volumeObj.AddComponent<Volume>();
        volume.isGlobal = true;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.22f);
        vignette.smoothness.Override(0.5f);
        vignette.color.Override(Color.black);
        bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.9f);
        bloom.intensity.Override(0f);
        bloom.scatter.Override(0.6f);
        volume.sharedProfile = profile;

        UniversalAdditionalCameraData cameraData = cam.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cam.allowHDR = true;
    }

    void BuildWorld()
    {
        if (garden != null) garden.Dispose();
        garden = null;
        Plan = null;

        int mazeSeed = seed;
        if (mazeSeed == 0) mazeSeed = System.Environment.TickCount;
        Maze = new MazeMap(mazeCells, mazeSeed, roomCount);
        Sim = new SnakeSim(Maze.Size, Maze.Size, mazeSeed);
        // the retro board is the start plaza without its outside row of cells
        RetroRect = new RectInt(Maze.Start.x + 1, Maze.Start.y + 1, Maze.Start.width - 2, Maze.Start.height - 2);
        Sim.SetPlayfield(RetroRect);
        grid = new GridMap(Maze.Size, Maze.Size);

        worldRoot = new GameObject("World").transform;
        worldRoot.SetParent(transform, false);
        arena = new ArenaView(worldRoot, grid, art, RetroRect);
        mazeView = new MazeView(worldRoot, grid, art, Maze, RetroRect);
        appleView = new AppleView(worldRoot, grid, art);
        powerupView = new PowerupView(worldRoot, grid, art);
        snakeView = new SnakeView(worldRoot, grid, art);
        monsterView = null;
        Collisions = null; // the retro board is normal snake, the maze gets colliders when it shows up

        Rules = new PowerupRules();
        Rules.Duration = lanternSeconds;
        Rules.FirstDelay = firstLanternDelay;
        Rules.RespawnDelay = lanternRespawnDelay;

        State = GameState.Title;
        StateTime = 0f;
        Look = 0f;
        Dark = 0f;
        snakeTimer = 0f;
        monsterTimer = 0f;
        chaseTime = 0f;
        ChaseSeeds = 0;
        Lives = livesIn3D;
        glow = 0f;
        lanternBanner = 0f;
        wasRevealed = false;
        Jumpscaring = false;
        lostToMonster = false;
        jumpFlash = 0f;
        monsterRise = 1f;
        hintTime = 0f;
        danger = 0f;
        pathDist = int.MaxValue;
        pathDistTimer = 0f;
        monsterSpawned = false;
        rig.Blend = 0f;
        rig.Shake = 0f;
        rig.FovKick = 0f;
        Audio.SetMuted(true); // the title screen is quiet
        Audio.StopMusic();
        RefreshViews(0f);
    }

    // throws away the world and goes back to the title screen with this seed (0 = random)
    public void Rebuild(int newSeed)
    {
        seed = newSeed;
        if (monsterView != null) monsterView.Dispose();
        worldRoot.gameObject.SetActive(false); // turn it off first so its colliders are gone right away
        Destroy(worldRoot.gameObject);
        BuildWorld();
    }

    // ================================ input

    void ReadInput()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        Vector2Int dir = Vector2Int.zero;   // for the retro game
        int turn = 0;                       // for the maze: -1 = left, 1 = right
        bool start = false;

        if (keyboard != null)
        {
            if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
            {
                dir = Vector2Int.up;
            }
            else if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            {
                dir = Vector2Int.down;
            }
            else if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                dir = Vector2Int.left;
                turn = -1;
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                dir = Vector2Int.right;
                turn = 1;
            }
            if (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame) start = true;
        }
        if (pad != null)
        {
            if (pad.dpad.up.wasPressedThisFrame)
            {
                dir = Vector2Int.up;
            }
            else if (pad.dpad.down.wasPressedThisFrame)
            {
                dir = Vector2Int.down;
            }
            else if (pad.dpad.left.wasPressedThisFrame)
            {
                dir = Vector2Int.left;
                turn = -1;
            }
            else if (pad.dpad.right.wasPressedThisFrame)
            {
                dir = Vector2Int.right;
                turn = 1;
            }
            if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) start = true;
        }

        if (State == GameState.Chase)
        {
            if (turn != 0) PressTurn(turn);
        }
        else if (dir != Vector2Int.zero)
        {
            Press(dir);
        }
        if (start) PressStart();
    }

    // arrow key in the retro game (on the title screen it also starts the game)
    public void Press(Vector2Int dir)
    {
        if (State == GameState.Title) startRequested = true;
        if (State == GameState.Title || State == GameState.Retro) Sim.QueueTurn(dir);
    }

    // left or right in the maze, from the snake's point of view
    public void PressTurn(int side)
    {
        if (State == GameState.Chase) Sim.QueueRelativeTurn(side);
    }

    public void PressStart()
    {
        startRequested = true;
    }

    // ================================ main loop

    public void Advance(float dt)
    {
        globalTime += dt;
        StateTime += dt;

        if (State == GameState.Title)
        {
            if (startRequested) StartRetro();
        }
        else if (State == GameState.Retro)
        {
            RunSnakeTimer(dt, retroInterval);
        }
        else if (State == GameState.Transition)
        {
            UpdateTransition();
        }
        else if (State == GameState.Chase)
        {
            chaseTime += dt;
            hintTime = Mathf.Max(0f, hintTime - dt);
            RunSnakeTimer(dt, chaseInterval);
            RunMonsterTimer(dt);
            if (State == GameState.Chase)
            {
                Rules.Tick(dt, Sim);
                if (wasRevealed && !Rules.Revealed) Raise(events.lanternExpired);
                wasRevealed = Rules.Revealed;
            }
        }
        else if (State == GameState.Respawn)
        {
            if (StateTime >= respawnDelay) DoRespawn();
        }
        else if (State == GameState.Cutscene)
        {
            UpdateCutscene();
        }
        else if (State == GameState.Won || State == GameState.Lost)
        {
            if (startRequested && StateTime > RestartDelay())
            {
                Rebuild(seed);
                StartRetro();
            }
        }

        startRequested = false;
        RefreshViews(dt);
    }

    void StartRetro()
    {
        State = GameState.Retro;
        StateTime = 0f;
        snakeTimer = 0f;
        Audio.SetMuted(false); // the sound starts with the game
        Raise(events.retroStarted);
    }

    // how long until space restarts the game (longer if the jumpscare has to finish)
    float RestartDelay()
    {
        if (lostToMonster) return jumpscareSeconds + 0.8f;
        return 0.8f;
    }

    float MonsterInterval()
    {
        return ChaseTuning.MonsterInterval(monsterIntervalStart, monsterIntervalMin, monsterSpeedUpPerSeed, ChaseSeeds, chaseTime, monsterSpeedUpPerSecond);
    }

    void RunSnakeTimer(float dt, float interval)
    {
        snakeTimer += dt;
        while (snakeTimer >= interval && (State == GameState.Retro || State == GameState.Chase))
        {
            snakeTimer -= interval;
            DoSnakeStep();
        }
    }

    void DoSnakeStep()
    {
        List<Vector2Int> before = new List<Vector2Int>(Sim.Snake);
        StepResult r = Sim.Step();
        if (Collisions != null) r = Collisions.Detect(r); // in the maze the physics tells us what we hit
        if (r.Moved) snakeView.OnStep(before);
        if (r.Died)
        {
            Lose();
            return;
        }
        if (r.GotPowerup)
        {
            Rules.Collect();
            wasRevealed = true;
            lanternBanner = 2.2f;
            Raise(events.lanternCollected);
        }
        if (!r.Ate) return;

        Raise(events.pickupEaten);
        if (State == GameState.Retro)
        {
            if (Sim.ApplesEaten >= applesToFlip) BeginTransition();
        }
        else
        {
            ChaseSeeds++;
            if (ChaseSeeds >= seedsToWin)
            {
                Win();
            }
            else if (Sim.ConsumeMonsterRelocation() && monsterView != null)
            {
                // the next seed was on the monster's side so it goes into the ground and comes up behind you
                monsterView.Teleport(Sim.Monster);
                monsterRise = 0f;
                monsterTimer = -ChaseTuning.RespawnGrace;
                pathDist = int.MaxValue;
                pathDistTimer = 0f;
                Raise(events.monsterRelocated);
            }
        }
    }

    void RunMonsterTimer(float dt)
    {
        monsterTimer += dt;
        float interval = MonsterInterval();
        while (monsterTimer >= interval && State == GameState.Chase)
        {
            monsterTimer -= interval;
            StepResult r = Sim.StepMonster();
            if (Collisions != null) r = Collisions.Detect(r);
            if (r.Moved)
            {
                monsterView.OnStep(Sim.Monster);
                // footsteps get louder when it's close
                int d = Sim.PathDistanceToMonster();
                Audio.Step(Mathf.Clamp01(1f - d / 18f) * 0.8f);
            }
            if (r.Died) Lose();
        }
    }

    // ================================ the flip from 2D to 3D

    void BeginTransition()
    {
        State = GameState.Transition;
        StateTime = 0f;
        snakeTimer = 0f;
        monsterSpawned = false;

        // the rules change right away (the snake is frozen during the flip), the graphics catch up over the next few seconds
        ChaseSetupResult setup = ChaseSetup.Begin(Sim, Maze, treesPerRoom, startPlazaTrees, ChaseTuning.MonsterSpawnMin, ChaseTuning.MonsterSpawnMax, seedSpacing);
        monsterSpawnCell = setup.MonsterSpawn;
        arena.PlantTrees(setup.Trees);
        mazeView.Show();
        Collisions = new CollisionWorld(worldRoot, grid, Sim);
        Sim.PhysicsContacts = true;
        Raise(events.worldFlipped);
    }

    void UpdateTransition()
    {
        Look = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(glitchEnd, morphEnd, StateTime));

        if (!monsterSpawned && StateTime >= morphEnd)
        {
            monsterSpawned = true;
            monsterView = new MonsterView(worldRoot, grid, art, monsterSpawnCell);
            Raise(events.monsterRose);
        }

        if (StateTime >= emergeEnd)
        {
            State = GameState.Chase;
            StateTime = 0f;
            Look = 1f;
            snakeTimer = 0f;
            monsterTimer = -ChaseTuning.MonsterGrace; // the monster waits a moment before it moves
            Rules.Start();
            Lives = livesIn3D;
            hintTime = 9f;
            Raise(events.chaseStarted);
        }
    }

    // ================================ dying and winning

    void Lose()
    {
        // dying in the maze costs a life. dying on the retro board is always game over
        bool inMaze = State == GameState.Chase;
        if (inMaze) Lives--;
        bool gameOver = !inMaze || Lives <= 0;

        if (gameOver) State = GameState.Lost;
        else State = GameState.Respawn;
        StateTime = 0f;

        // if the monster got you in 3D, it jumps at the camera
        Jumpscaring = Sim.Cause == DeathCause.Monster && monsterView != null && Look > 0.9f;
        lostToMonster = Jumpscaring;
        if (Jumpscaring)
        {
            monsterView.BeginLunge();
            jumpFlash = 1f;
            Raise(events.snakeDevoured);
            respawnDelay = jumpscareSeconds + 1.2f;
        }
        else
        {
            Raise(events.snakeCrashed);
            respawnDelay = 1.4f;
        }
    }

    // lost a life but there are more: back to the middle, monster goes behind you again
    void DoRespawn()
    {
        Vector2Int monsterCell = ChaseSetup.Respawn(Sim, Maze, Rules);
        snakeView.ResetMotion();
        monsterView.Teleport(monsterCell);
        monsterRise = 0f;   // it climbs out of the ground again
        rig.Snap();
        jumpFlash = 0.7f;
        glow = 0f;
        wasRevealed = false;
        pathDist = int.MaxValue;
        pathDistTimer = 0f;

        State = GameState.Chase;
        StateTime = 0f;
        snakeTimer = 0f;
        monsterTimer = -ChaseTuning.RespawnGrace;
        Raise(events.snakeRespawned);
    }

    void Win()
    {
        if (playCutscene)
        {
            BeginCutscene();
        }
        else
        {
            State = GameState.Won;
            StateTime = 0f;
            Raise(events.gameWon);
        }
    }

    // ================================ the ending

    bool HasVoice(int line)
    {
        return cutsceneVoice != null && line < cutsceneVoice.Length && cutsceneVoice[line] != null;
    }

    void BeginCutscene()
    {
        State = GameState.Cutscene;
        StateTime = 0f;
        lastCutsceneTime = -1f;

        float[] voiceSeconds = new float[CutsceneScript.Lines.Length];
        for (int i = 0; i < voiceSeconds.Length; i++)
        {
            if (HasVoice(i)) voiceSeconds[i] = cutsceneVoice[i].length;
        }
        Plan = new CutscenePlan(voiceSeconds);
        garden = new GardenView(transform, cam, art, Plan);
        worldRoot.gameObject.SetActive(false); // the maze is gone, we're somewhere else now
        Jumpscaring = false;
        glow = 0f;
        Raise(events.endingStarted);
    }

    // true the first frame after "time" happened
    bool Passed(float time)
    {
        return time > lastCutsceneTime && time <= StateTime;
    }

    void UpdateCutscene()
    {
        float t = StateTime;
        // space skips it, but not right away so you don't skip it by accident
        if (startRequested && t > 1f)
        {
            EndCutscene();
            return;
        }

        for (int i = 0; i < CutscenePlan.Trees; i++)
        {
            if (Passed(Plan.SeedAt(i) + 0.45f)) Raise(events.seedPlanted);
            if (Passed(Plan.GrowStart(i))) Raise(events.treeGrew);
        }
        if (Passed(Plan.DialogueStart - 0.1f)) Raise(events.serpentHissed);
        for (int i = 0; i < Plan.LineStart.Length; i++)
        {
            if (HasVoice(i) && Passed(Plan.LineStart[i])) Audio.PlayVoice(cutsceneVoice[i]);
        }
        if (Passed(Plan.BiteAt)) Raise(events.appleBitten);
        lastCutsceneTime = t;

        if (t >= Plan.End) EndCutscene();
    }

    public void SkipCutscene()
    {
        if (State == GameState.Cutscene) EndCutscene();
    }

    void EndCutscene()
    {
        Audio.StopVoice();
        State = GameState.Won;
        StateTime = 0f;
    }

    // ================================ graphics

    void RefreshViews(float dt)
    {
        bool inTransition = State == GameState.Transition;
        bool world3D = State == GameState.Chase || State == GameState.Respawn || State == GameState.Won || State == GameState.Lost;
        float glitch = 0f;
        float ripple = 0f;
        if (inTransition)
        {
            if (StateTime < glitchEnd) glitch = Mathf.SmoothStep(0f, 1f, StateTime / glitchEnd);
            ripple = Mathf.Max(0f, StateTime - glitchEnd);
        }

        // it gets dark when the monster comes out and stays dark (it gets lighter again if you win)
        if (inTransition) Dark = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(morphEnd - 0.3f, emergeEnd, StateTime));
        else if (State == GameState.Chase || State == GameState.Respawn || State == GameState.Lost) Dark = 1f;
        else if (State == GameState.Won) Dark = Mathf.MoveTowards(Dark, 0.15f, dt * 0.8f);
        else Dark = 0f;

        // the lantern glow turns on fast, fades out slowly, and flickers when it's about to run out
        bool lit = State == GameState.Chase && Rules.Revealed;
        float glowTarget = 0f;
        if (lit) glowTarget = 1f;
        if (lit && Rules.GlowLeft < 3f && Mathf.Sin(globalTime * 26f) > 0.55f) glowTarget = 0.3f;
        if (glowTarget > glow) glow = Mathf.MoveTowards(glow, glowTarget, dt * 6f);
        else glow = Mathf.MoveTowards(glow, glowTarget, dt * 2.5f);
        lanternBanner = Mathf.Max(0f, lanternBanner - dt);
        monsterRise = Mathf.MoveTowards(monsterRise, 1f, dt);

        Audio.Master = audioVolume;
        arena.Refresh(Look, ripple, glitch);
        if (inTransition)
        {
            mazeView.Refresh(ripple);
            arena.GrowTrees(StateTime - (glitchEnd + 0.6f));
        }
        else if (world3D)
        {
            mazeView.Refresh(10f);
            arena.GrowTrees(10f);
        }

        float interval = retroInterval;
        if (State == GameState.Chase) interval = chaseInterval;
        float snakeProgress = 1f;
        if (State == GameState.Retro || State == GameState.Chase) snakeProgress = Mathf.Clamp01(snakeTimer / interval);
        snakeView.SetGlow(glow);
        snakeView.Refresh(Sim, snakeProgress, Look, globalTime, dt);
        appleView.Refresh(Sim, Look, globalTime, glow);
        powerupView.Refresh(Sim, Look, globalTime);

        if (monsterView != null && !Jumpscaring)
        {
            float emerge = monsterRise;
            if (State == GameState.Transition) emerge = Mathf.InverseLerp(morphEnd, emergeEnd, StateTime);
            float monsterProgress = 1f;
            if (State == GameState.Chase) monsterProgress = Mathf.Clamp01(monsterTimer / MonsterInterval());
            monsterView.Refresh(monsterProgress, emerge, globalTime, dt);
        }

        // camera
        rig.Blend = Look;
        rig.Shake = glitch * 0.6f;
        rig.FollowPos = snakeView.HeadWorld;
        rig.FollowHeading = new Vector3(Sim.Heading.x, 0f, Sim.Heading.y);
        if (garden != null && State == GameState.Cutscene) garden.Pose(StateTime);
        else if (garden != null && State == GameState.Won) garden.Pose(Plan.End + StateTime);
        else rig.Tick(dt);

        // the jumpscare is placed in front of the camera so it has to happen after the camera moved
        if (Jumpscaring && monsterView != null)
        {
            monsterView.Lunge(cam.transform, StateTime / lungeSeconds, globalTime);
            if (StateTime >= jumpscareSeconds)
            {
                Jumpscaring = false;
                monsterView.EndLunge();
                monsterView.Refresh(1f, 1f, globalTime, dt);
            }
        }
        jumpFlash = Mathf.Max(0f, jumpFlash - dt / 0.4f);

        // how close is the monster through the maze (not through the hedges). checked 10 times a second
        if (State == GameState.Chase && Sim.MonsterActive)
        {
            pathDistTimer -= dt;
            if (pathDistTimer <= 0f)
            {
                pathDist = Sim.PathDistanceToMonster();
                pathDistTimer = 0.1f;
            }
        }
        else
        {
            pathDist = int.MaxValue;
        }
        danger = 0f;
        if (pathDist != int.MaxValue) danger = Mathf.Clamp01(1f - (pathDist - 2f) / 10f);

        UpdateLighting();
        UpdateHud();
    }

    void UpdateLighting()
    {
        // retro -> evening (Look), then evening -> pitch black when the monster comes out (Dark)
        Color fog = Color.Lerp(dusk, new Color(0.008f, 0.009f, 0.016f), Dark);
        cam.backgroundColor = Color.Lerp(lcdMid, fog, Look);

        RenderSettings.fog = Look > 0.01f;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = Mathf.Lerp(0f, Mathf.Lerp(0.022f, 0.055f, Dark), Look);
        RenderSettings.ambientMode = AmbientMode.Flat;
        Color duskAmbient = Color.Lerp(Color.white, new Color(0.48f, 0.45f, 0.62f), Look);
        RenderSettings.ambientLight = Color.Lerp(duskAmbient, new Color(0.035f, 0.04f, 0.075f), Dark);

        if (sun != null)
        {
            sun.color = Color.Lerp(new Color(1f, 0.68f, 0.48f), new Color(0.5f, 0.62f, 1f), Dark); // orange sunset -> blue moonlight
            sun.intensity = Mathf.Lerp(Mathf.Lerp(0f, 1.9f, Look), 0.12f, Dark);
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
        }

        // the vignette pulses red when the monster is close
        float pulse = 0.65f + 0.35f * Mathf.Sin(globalTime * Mathf.Lerp(4f, 9f, danger));
        vignette.intensity.value = Mathf.Lerp(0.22f, 0.32f, Look) + Dark * 0.12f + danger * 0.25f * pulse;
        vignette.color.value = Color.Lerp(Color.black, new Color(0.42f, 0f, 0f), danger);
        bloom.intensity.value = Mathf.Lerp(0f, 0.9f, Look) + Dark * 0.5f + glow * 0.4f;

        if (garden != null && (State == GameState.Cutscene || State == GameState.Won))
        {
            // the garden is a warm sunny afternoon
            Color sky = new Color(1.00f, 0.80f, 0.60f);
            cam.backgroundColor = sky;
            RenderSettings.fog = true;
            RenderSettings.fogColor = sky;
            RenderSettings.fogDensity = 0.0075f;
            RenderSettings.ambientLight = new Color(0.58f, 0.52f, 0.58f);
            if (sun != null)
            {
                sun.color = new Color(1f, 0.82f, 0.58f);
                sun.intensity = 1.7f;
                sun.transform.rotation = Quaternion.Euler(34f, -58f, 0f);
            }
            vignette.intensity.value = 0.26f;
            vignette.color.value = new Color(0.12f, 0.04f, 0f);
            bloom.intensity.value = 0.55f;
        }
    }

    // ================================ HUD

    void UpdateHud()
    {
        Color yellow = new Color(1f, 0.85f, 0.25f);
        Color red = new Color(0.9f, 0.15f, 0.12f);
        hud.Begin();
        hud.Scanlines(Mathf.Lerp(0.30f, 0.05f, Look));

        if (State == GameState.Title)
        {
            hud.Panel(new Color(lcdMid.r, lcdMid.g, lcdMid.b, 0.88f), 0.18f, 0.52f);
            hud.Text("SNAKE'S", 0.5f, 0.26f, 92f, lcdDark, false, false);
            hud.Text("TEMPTATION", 0.5f, 0.38f, 92f, lcdDark, false, false);
            if (globalTime % 1f < 0.65f) hud.Text("PRESS SPACE", 0.5f, 0.62f, 44f, lcdDark, false, false); // blinking
            hud.Text("WASD / ARROWS", 0.5f, 0.69f, 28f, lcdDark, false, false);
        }
        else if (State == GameState.Retro)
        {
            hud.Text("SCORE " + Sim.ApplesEaten.ToString("00"), 0.02f, 0.06f, 44f, lcdDark, true, false);
        }
        else if (State == GameState.Transition)
        {
            if (StateTime > 0.15f && StateTime < morphEnd + 0.4f)
            {
                float shake = 0f;
                if (StateTime < glitchEnd) shake = (Random.value - 0.5f) * 0.012f;
                hud.Text("YOU SHOULD NOT HAVE TASTED IT", 0.5f + shake, 0.5f - shake, 50f, new Color(0.85f, 0.1f, 0.1f));
            }
        }
        else if (State == GameState.Chase)
        {
            ChaseHud(yellow);
        }
        else if (State == GameState.Respawn)
        {
            hud.Lives(Lives, livesIn3D);
            if (Jumpscaring)
            {
                hud.Dim(new Color(0.35f, 0f, 0f, 0.28f + 0.12f * Mathf.Sin(globalTime * 40f)));
            }
            else
            {
                hud.Dim(new Color(0f, 0f, 0f, 0.55f));
                if (Sim.Cause == DeathCause.Monster) hud.Text("DEVOURED", 0.5f, 0.40f, 96f, red);
                else hud.Text("YOU CRASHED", 0.5f, 0.40f, 96f, red);
                if (Lives == 1) hud.Text("LAST LIFE", 0.5f, 0.52f, 48f, Color.white);
                else hud.Text(Lives + " LIVES LEFT", 0.5f, 0.52f, 48f, Color.white);
            }
        }
        else if (State == GameState.Lost)
        {
            if (Jumpscaring)
            {
                // the monster is in your face, no text, just red
                hud.Dim(new Color(0.35f, 0f, 0f, 0.28f + 0.12f * Mathf.Sin(globalTime * 40f)));
            }
            else
            {
                hud.Dim(new Color(0f, 0f, 0f, 0.5f));
                if (Look < 0.5f)
                {
                    // died in the retro game
                    hud.Text("GAME OVER", 0.5f, 0.42f, 120f, lcdMid);
                    hud.Text("SCORE " + Sim.ApplesEaten.ToString("00"), 0.5f, 0.55f, 44f, Color.white);
                }
                else
                {
                    hud.Lives(0, livesIn3D);
                    if (Sim.Cause == DeathCause.Monster) hud.Text("DEVOURED", 0.5f, 0.42f, 120f, red);
                    else hud.Text("YOU CRASHED", 0.5f, 0.42f, 120f, red);
                    hud.Text("SEEDS " + ChaseSeeds + "/" + seedsToWin, 0.5f, 0.55f, 44f, Color.white);
                }
                if (StateTime > RestartDelay()) hud.Text("PRESS SPACE", 0.5f, 0.66f, 38f, Color.white);
            }
        }
        else if (State == GameState.Cutscene)
        {
            float t = StateTime;
            hud.Letterbox(0.105f * Mathf.SmoothStep(0f, 1f, t / 0.9f));
            int line = Plan.LineAt(t);
            if (line >= 0)
            {
                DialogueLine l = CutsceneScript.Lines[line];
                Color nameColor = new Color(1f, 0.82f, 0.45f);
                if (l.Who == Speaker.Snake) nameColor = new Color(0.55f, 0.95f, 0.35f);
                hud.Subtitle(l.SpeakerName, nameColor, l.Text, Plan.Typed(line, t));
            }
            // title card at the start
            float card = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.0f, 1.8f, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3.4f, 4.4f, t)));
            if (card > 0.01f)
            {
                hud.Text("THREE SEEDS", 0.5f, 0.30f, 76f, new Color(1f, 1f, 1f, card));
                hud.Text("ONE GARDEN", 0.5f, 0.40f, 40f, new Color(1f, 1f, 1f, card));
            }
            if (t > 1.0f && t < Plan.End - 2f) hud.Text("SPACE: SKIP", 0.93f, 0.045f, 24f, new Color(1f, 1f, 1f, 0.55f));
        }
        else if (State == GameState.Won)
        {
            if (garden != null)
            {
                // the garden after the ending
                hud.Dim(new Color(0f, 0f, 0f, 0.22f));
                hud.Letterbox(0.105f);
                hud.Text("SHE TOOK THE BITE", 0.5f, 0.30f, 84f, new Color(1f, 0.88f, 0.45f));
                hud.Text("AND THE GARDEN WAS NEVER THE SAME", 0.5f, 0.40f, 34f, Color.white);
                if (StateTime > 0.8f) hud.Text("PRESS SPACE", 0.5f, 0.84f, 38f, Color.white);
            }
            else
            {
                hud.Dim(new Color(0f, 0f, 0f, 0.45f));
                hud.Text("YOU ESCAPED THE ORCHARD", 0.5f, 0.42f, 80f, yellow);
                hud.Text("THREE SEEDS. A NEW ORCHARD.", 0.5f, 0.53f, 34f, Color.white);
                if (StateTime > 0.8f) hud.Text("PRESS SPACE", 0.5f, 0.64f, 38f, Color.white);
            }
        }

        // white flash when the world flips
        float flip = 0f;
        if (State == GameState.Transition)
        {
            float t = StateTime - (glitchEnd - 0.15f);
            if (t < 0f) flip = 0f;
            else if (t < 0.15f) flip = t / 0.15f;
            else flip = Mathf.Clamp01(1f - (t - 0.15f) / 0.55f);
        }
        // the ending fades in from white and fades out to white
        float white = 0f;
        if (State == GameState.Cutscene) white = Plan.WhiteAt(StateTime);
        else if (State == GameState.Won && garden != null) white = 1f - Mathf.SmoothStep(0f, 1f, StateTime / 1.4f);

        float flash = Mathf.Max(flip, Mathf.Max(jumpFlash, white));
        Color flashColor = Color.white;
        if (jumpFlash > Mathf.Max(flip, white)) flashColor = new Color(1f, 0.8f, 0.75f);
        hud.Flash(flash, flashColor);
    }

    void ChaseHud(Color yellow)
    {
        hud.Text("SEEDS " + ChaseSeeds + "/" + seedsToWin, 0.02f, 0.06f, 44f, yellow, true, true);
        hud.Lives(Lives, livesIn3D);

        if (Rules.Revealed)
        {
            // the lantern timer. it blinks orange when it's almost out
            Color barColor = yellow;
            if (Rules.GlowLeft < 3f) barColor = new Color(1f, 0.45f, 0.2f, Mathf.Lerp(0.5f, 1f, Mathf.PingPong(globalTime * 6f, 1f)));
            hud.Bar(Rules.GlowLeft / Rules.Duration, barColor);
            hud.Text("LANTERN  " + Mathf.CeilToInt(Rules.GlowLeft), 0.02f, 0.15f, 28f, barColor, true, true);
        }
        if (lanternBanner > 0f)
        {
            hud.Text("THE LANTERN IS LIT", 0.5f, 0.30f, 60f, new Color(yellow.r, yellow.g, yellow.b, Mathf.Clamp01(lanternBanner * 2f)));
        }
        else if (hintTime > 0f)
        {
            hud.Text("YOU CANNOT STOP", 0.5f, 0.38f, 56f, yellow);
            hud.Text("A / D  OR  LEFT / RIGHT  TO TURN", 0.5f, 0.45f, 30f, Color.white);
            hud.Text("FOLLOW THE ARROW TO A LANTERN. ITS LIGHT SHOWS WHERE THE SEEDS ARE.", 0.5f, 0.50f, 26f, yellow);
        }

        // the compass points to the seed when the lantern is lit, otherwise to the lantern
        Vector3 forward = snakeView.HeadForward;
        forward.y = 0f;
        if (Rules.Revealed && Sim.HasApple)
        {
            Vector3 toSeed = grid.ToWorld(Sim.Apple) - snakeView.HeadWorld;
            toSeed.y = 0f;
            hud.Arrow(Vector3.SignedAngle(forward, toSeed, Vector3.up), yellow);
            hud.Text("SEED  " + SnakeSim.Manhattan(Sim.Apple, Sim.Head), 0.09f, 0.88f, 30f, yellow);
        }
        else if (Sim.HasPowerup)
        {
            Vector3 toLantern = grid.ToWorld(Sim.Powerup) - snakeView.HeadWorld;
            toLantern.y = 0f;
            Color warm = new Color(1f, 0.95f, 0.75f);
            hud.Arrow(Vector3.SignedAngle(forward, toLantern, Vector3.up), warm);
            hud.Text("LANTERN  " + SnakeSim.Manhattan(Sim.Powerup, Sim.Head), 0.09f, 0.88f, 30f, warm);
        }
        if (danger > 0.45f)
        {
            hud.Text("IT IS CLOSE", 0.5f, 0.93f, 44f, new Color(1f, 0.25f, 0.2f, Mathf.Lerp(0.5f, 1f, Mathf.PingPong(globalTime * 4f, 1f))));
        }
    }
}
