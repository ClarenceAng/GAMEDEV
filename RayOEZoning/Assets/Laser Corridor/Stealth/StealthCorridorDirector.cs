using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Installs the stealth assignment on top of the original, prefab-based corridor.
// Everything is built from the preexisting LaserCorridor scene at Play time.
public class StealthCorridorDirector : MonoBehaviour
{
    public const int RequiredIntel = 3;
    public int CollectedIntel { get; private set; }
    public int TotalIntel => Mathf.Max(0, allIntel.Count - 1);
    public bool GantryKeyCollected { get; private set; }
    public float Suspicion { get; private set; }
    public bool IsCaught { get; private set; }
    public bool HasEscaped { get; private set; }
    public bool FlashlightOn => flashlight != null && flashlight.enabled;
    public string ActiveDetector { get; private set; } = "NONE";
    public string StatusMessage => Time.time < noticeUntil ? notice : "";
    public StealthHiddenIntel Collectable { get; private set; }

    private Transform player;
    private Transform startingPoint;
    private Transform levelRoot;
    private StealthFirstPersonController controller;
    private Light flashlight;
    // Dedicated event-based Input Action: works with F alone, even when standing still.
    private InputAction flashlightAction;
    private readonly List<StealthPatrolSensor> sensors = new List<StealthPatrolSensor>();
    private readonly List<StealthHiddenIntel> allIntel = new List<StealthHiddenIntel>();
    private StealthCorridorHUD hud;
    private float noticeUntil;
    private string notice;

    // Bind gameplay to the actual objects saved inside LaserCorridor.unity.
    // We no longer delete the scene and construct a duplicate at Play time.
    public void InitializeScene(GameObject playerGO, Camera camera, Transform sceneLevel)
    {
        player = playerGO.transform;
        levelRoot = sceneLevel;
        startingPoint = sceneLevel.Find("Player Start");
        if (startingPoint == null)
        {
            Debug.LogError("Stealth Corridor: Player Start was not found in the saved scene.");
            return;
        }

        const int stealthPlayerLayer = 8;
        playerGO.layer = stealthPlayerLayer;
        controller = playerGO.GetComponent<StealthFirstPersonController>();
        if (controller == null) controller = playerGO.AddComponent<StealthFirstPersonController>();
        controller.Initialize(camera, startingPoint.position);

        Transform flashlightObject = camera.transform.Find("Flashlight");
        if (flashlightObject == null)
        {
            Debug.LogError("Stealth Corridor: Flashlight is missing. Rebuild the saved scene using Tools > Stealth Corridor.");
            return;
        }
        flashlight = flashlightObject.GetComponent<Light>();
        if (flashlight == null)
        {
            Debug.LogError("Stealth Corridor: Flashlight object needs a Spot Light component.");
            return;
        }
        flashlight.enabled = false;
        BindFlashlightKey();

        // Scene objects are real, editable instances. Only their game-state
        // references are wired when Play begins.
        Transform bots = sceneLevel.Find("Security Bots");
        if (bots != null)
        {
            for (int i = 0; i < StealthV2Layout.Guards.Length; i++)
            {
                Transform bot = bots.Find("Security Bot " + (i + 1));
                if (bot == null) { Debug.LogWarning("Missing Security Bot " + (i + 1)); continue; }
                StealthPatrolSensor sensor = bot.GetComponent<StealthPatrolSensor>();
                if (sensor == null) { Debug.LogWarning("Missing detection script on " + bot.name); continue; }
                Light sight = bot.Find("Search Light")?.GetComponent<Light>();
                Renderer eye = bot.Find("Lens")?.GetComponent<Renderer>();
                sensor.Initialize(player, StealthV2Layout.Guards[i], stealthPlayerLayer, i, sight, eye);
                sensors.Add(sensor);
            }
        }

        Transform items = sceneLevel.Find("Hidden Items");
        if (items != null)
        {
            int intelNumber = 0;
            for (int i = 0; i < StealthV2Layout.Intels.Length; i++)
            {
                StealthV2Layout.Intel spec = StealthV2Layout.Intels[i];
                Transform item = items.Find(spec.gantryKey ? "Access Key" : "Intel " + (++intelNumber));
                if (item == null) { Debug.LogWarning("Missing hidden item " + (i + 1)); continue; }
                StealthHiddenIntel intel = item.GetComponent<StealthHiddenIntel>();
                if (intel == null) { Debug.LogWarning("Missing collectible script on " + item.name); continue; }
                intel.Initialize(spec.name, camera.transform, player, flashlight, this, spec.gantryKey);
                allIntel.Add(intel);
            }
        }

        // Exit script is now its own Unity MonoBehaviour file. Recover gracefully
        // even if an older V6.2 scene still contains a missing-script component.
        Transform exitFolder = sceneLevel.Find("Exit");
        if (exitFolder == null)
        {
            GameObject folder = new GameObject("Exit");
            folder.transform.SetParent(sceneLevel, false);
            exitFolder = folder.transform;
        }
        Transform exitTransform = exitFolder.Find("Exit Trigger");
        if (exitTransform == null)
        {
            GameObject exitObject = new GameObject("Exit Trigger");
            exitObject.transform.SetParent(exitFolder, false);
            exitTransform = exitObject.transform;
            exitTransform.position = new Vector3(0f, 1.75f, 143.5f);
        }
        BoxCollider exitCollider = exitTransform.GetComponent<BoxCollider>();
        if (exitCollider == null) exitCollider = exitTransform.gameObject.AddComponent<BoxCollider>();
        exitCollider.enabled = true;
        exitCollider.isTrigger = true;
        exitCollider.size = new Vector3(8.5f, 3.4f, 1.5f);
        StealthExitZone goal = exitTransform.GetComponent<StealthExitZone>();
        if (goal == null) goal = exitTransform.gameObject.AddComponent<StealthExitZone>();
        goal.Initialize(this);

        Physics.SyncTransforms();
        GameObject hudObject = new GameObject("HUD", typeof(RectTransform));
        hud = hudObject.AddComponent<StealthCorridorHUD>();
        hud.Initialize(this);
        ShowNotice("SECURITY ACTIVE  |  Use F to reveal hidden intel.", 5f);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
        {
            Restart();
            return;
        }
        Collectable = null;
        foreach (StealthHiddenIntel i in allIntel)
            if (i != null && i.CanCollect) { Collectable = i; break; }

        if (IsCaught || HasEscaped) return;
        bool found = false;
        string detector = "NONE";
        foreach (StealthPatrolSensor scanner in sensors)
        {
            if (scanner != null && scanner.SeesPlayer)
            {
                found = true;
                detector = scanner.PatrolLabel + " / " + scanner.ZoneLabel + " + RAYCAST";
                break;
            }
        }
        ActiveDetector = detector;
        if (found)
        {
            // Running and waving an active flashlight in a search cone
            // are riskier than moving cautiously while the light is off.
            float riskPerSecond = controller.IsSprinting ? 110f : 70f;
            if (FlashlightOn) riskPerSecond *= 1.45f;
            Suspicion += Time.deltaTime * riskPerSecond;
        }
        else
            Suspicion -= Time.deltaTime * 46f;
        Suspicion = Mathf.Clamp(Suspicion, 0f, 100f);
        if (Suspicion >= 100f) StartCoroutine(CaughtSequence());
    }

    private void BindFlashlightKey()
    {
        // InputAction fires from the Input System event stream. Polling
        // wasPressedThisFrame in the director missed some isolated F presses
        // on the user's editor setup, especially when not moving.
        flashlightAction = new InputAction("Flashlight (F)", InputActionType.Button, "<Keyboard>/f");
        flashlightAction.performed += OnFlashlightPressed;
        flashlightAction.Enable();
    }

    private void OnFlashlightPressed(InputAction.CallbackContext context)
    {
        if (flashlight == null || IsCaught || HasEscaped) return;
        flashlight.enabled = !flashlight.enabled;
        ShowNotice(flashlight.enabled
            ? "FLASHLIGHT ON - scan for hidden intel"
            : "FLASHLIGHT OFF - stay concealed", 1.8f);
    }

    private void OnEnable()
    {
        if (flashlightAction != null) flashlightAction.Enable();
    }

    private void OnDisable()
    {
        if (flashlightAction != null) flashlightAction.Disable();
    }

    private void OnDestroy()
    {
        if (flashlightAction == null) return;
        flashlightAction.performed -= OnFlashlightPressed;
        flashlightAction.Disable();
        flashlightAction.Dispose();
        flashlightAction = null;
    }

    public void Collect(StealthHiddenIntel intel)
    {
        if (intel == null || intel.Collected || IsCaught || HasEscaped || !intel.CanCollect) return;
        intel.MarkCollected();
        if (intel.IsGantryKey)
        {
            GantryKeyCollected = true;
            ShowNotice("GANTRY ACCESS KEY ACQUIRED - security gate override enabled!", 3.5f);
        }
        else
        {
            CollectedIntel++;
            ShowNotice("INTEL RECOVERED: " + intel.IntelName + "   (" + CollectedIntel + "/" + RequiredIntel + ")", 3f);
        }
    }

    public void ReachedExit()
    {
        if (IsCaught || HasEscaped) return;
        if (CollectedIntel < RequiredIntel || !GantryKeyCollected)
        {
            if (!GantryKeyCollected)
            {
                ShowNotice("EXIT LOCKED - get the GANTRY KEY via the elevated right-hand route.", 4f);
                return;
            }
            ShowNotice("EXIT LOCKED - recover " + (RequiredIntel - CollectedIntel) + " more hidden intel.", 3f);
            return;
        }
        HasEscaped = true;
        Suspicion = 0f;
        controller.MovementEnabled = false;
        ShowNotice("MISSION COMPLETE! Press R to replay.", 3600f);
    }

    private IEnumerator CaughtSequence()
    {
        IsCaught = true;
        ActiveDetector = "DETECTED";
        controller.MovementEnabled = false;
        ShowNotice("SECURITY BREACH - CAUGHT!", 2.2f);
        yield return new WaitForSeconds(1.6f);
        if (!HasEscaped)
        {
            controller.Teleport(startingPoint.position);
            controller.MovementEnabled = true;
            Suspicion = 0f;
            ActiveDetector = "NONE";
            if (flashlight != null) flashlight.enabled = false;
            IsCaught = false;
            ShowNotice("RESET TO ENTRANCE - use cover and crouch (CTRL).", 3f);
        }
    }

    private void Restart()
    {
        StopAllCoroutines();
        HasEscaped = false;
        IsCaught = false;
        Suspicion = 0f;
        CollectedIntel = 0;
        GantryKeyCollected = false;
        ActiveDetector = "NONE";
        if (flashlight != null) flashlight.enabled = false;
        foreach (StealthHiddenIntel intel in allIntel) if (intel != null) intel.ResetIntel();
        controller.Teleport(startingPoint.position);
        controller.MovementEnabled = true;
        controller.SetMouseCaptured(true);
        ShowNotice("NEW ATTEMPT - find intel with F, evade sensors.", 3f);
    }

    public void ShowNotice(string message, float seconds)
    {
        notice = message;
        noticeUntil = Time.time + seconds;
    }


}
