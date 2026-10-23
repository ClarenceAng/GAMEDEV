using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Compact FPS HUD. Each panel owns its children so margins and alignment are
// consistent even when the Game view changes aspect ratio or Canvas scale.
public class StealthCorridorHUD : MonoBehaviour
{
    private StealthCorridorDirector director;
    private TextMeshProUGUI mission;
    private TextMeshProUGUI dangerLabel;
    private TextMeshProUGUI dangerPercent;
    private TextMeshProUGUI dangerSource;
    private TextMeshProUGUI status;
    private TextMeshProUGUI hint;
    private TextMeshProUGUI crosshair;
    private Image dangerFill;
    private RectTransform dangerFillRect;
    private const float DetectionBarMaxWidth = 436f;

    public void Initialize(StealthCorridorDirector owner)
    {
        director = owner;
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();
        RectTransform root = (RectTransform)transform;

        // Top-left panel: five lines at 22px with an even 15px top/bottom inset.
        // V4's 216px background left a large unused black strip below FLASHLIGHT.
        RectTransform missionPanel = Panel("Mission Panel", root, new Vector2(0f, 1f),
            new Vector2(296f, -101f), new Vector2(560f, 166f),
            new Color(.005f, .018f, .025f, .83f));
        mission = Text("Mission Information", missionPanel, new Vector2(0f, 1f),
            new Vector2(15f, -14f), new Vector2(530f, 145f),
            22f, TextAlignmentOptions.TopLeft);

        // Top-right panel: explicit left/right columns avoid the V4 double-right
        // alignment; the 100% text can never overlap the scanner label or gauge.
        RectTransform detectionPanel = Panel("Sensor Panel", root, new Vector2(1f, 1f),
            new Vector2(-261f, -82f), new Vector2(474f, 128f),
            new Color(.005f, .018f, .025f, .83f));
        dangerLabel = Text("Detection State", detectionPanel, new Vector2(0f, 1f),
            new Vector2(18f, -14f), new Vector2(320f, 31f),
            23f, TextAlignmentOptions.TopLeft);
        dangerLabel.fontStyle = FontStyles.Bold;
        dangerPercent = Text("Detection Percent", detectionPanel, new Vector2(1f, 1f),
            new Vector2(-18f, -14f), new Vector2(104f, 31f),
            23f, TextAlignmentOptions.TopRight);
        dangerPercent.fontStyle = FontStyles.Bold;
        dangerSource = Text("Detection Source", detectionPanel, new Vector2(0f, 1f),
            new Vector2(18f, -53f), new Vector2(438f, 26f),
            16.5f, TextAlignmentOptions.TopLeft);
        dangerSource.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform bar = Panel("Detection Gauge", detectionPanel,
            new Vector2(0f, 1f), new Vector2(238f, -100f),
            new Vector2(442f, 16f), new Color(.14f, .16f, .18f, 1f));
        RectTransform fill = Panel("Detection Gauge Fill", bar,
            new Vector2(0f, .5f), new Vector2(0f, 0f),
            new Vector2(DetectionBarMaxWidth, 12f), new Color(1f, .66f, .12f));
        fill.pivot = new Vector2(0f, .5f);
        fill.anchoredPosition = new Vector2(3f, 0f);
        dangerFillRect = fill;
        dangerFill = fill.GetComponent<Image>();

        status = Text("Center Notification", root, new Vector2(.5f, .73f),
            Vector2.zero, new Vector2(1100f, 110f), 32f, TextAlignmentOptions.Center);
        status.fontStyle = FontStyles.Bold;
        status.outlineWidth = .21f;
        status.outlineColor = new Color(0f, 0f, 0f, .88f);

        crosshair = Text("Crosshair", root, new Vector2(.5f, .5f),
            Vector2.zero, new Vector2(64f, 64f), 34f, TextAlignmentOptions.Center);
        crosshair.text = "+";
        crosshair.color = new Color(.82f, 1f, 1f, .88f);

        Panel("Bottom Panel", root, new Vector2(.5f, 0f),
            new Vector2(0f, 45f), new Vector2(1430f, 68f),
            new Color(.005f, .018f, .025f, .76f));
        hint = Text("Controls", root, new Vector2(.5f, 0f),
            new Vector2(0f, 45f), new Vector2(1380f, 58f),
            22f, TextAlignmentOptions.Center);
    }

    private void Update()
    {
        if (director == null) return;
        mission.text = "<b>SECURITY INFILTRATION</b>\n" +
            "<color=#5FE8EA>HIDDEN INTEL</color>  " + director.CollectedIntel + "/" +
            StealthCorridorDirector.RequiredIntel + " required  (" + director.TotalIntel + " available)\n" +
            "<color=#B7D5DC>GANTRY KEY</color>  " + (director.GantryKeyCollected ? "<color=#7BFAF3>ACQUIRED</color>" : "MISSING") + "\n" +
            "<color=#B7D5DC>EXIT</color>  " + (director.CollectedIntel >= StealthCorridorDirector.RequiredIntel && director.GantryKeyCollected ? "UNLOCKED" : "LOCKED") + "\n" +
            "<color=#B7D5DC>FLASHLIGHT</color>  " + (director.FlashlightOn ? "<color=#7BFAF3>ON</color>" : "OFF");

        float percent = Mathf.Clamp(director.Suspicion, 0f, 100f);
        bool spotted = director.ActiveDetector != "NONE";
        dangerLabel.text = spotted ? "DETECTED" : "HIDDEN / CLEAR";
        dangerLabel.color = spotted ? new Color(1f, .36f, .25f) : new Color(.55f, .95f, .76f);
        dangerPercent.text = Mathf.RoundToInt(percent) + "%";
        dangerPercent.color = spotted ? new Color(1f, .36f, .25f) : new Color(.85f, .98f, 1f);
        dangerSource.text = director.IsCaught ? "SECURITY BREACH - restarting" :
            spotted ? director.ActiveDetector : "Sphere / Box / Capsule + Raycast";
        dangerFillRect.sizeDelta = new Vector2(DetectionBarMaxWidth * percent / 100f, 12f);
        dangerFill.color = percent > 65f ? new Color(1f, .18f, .12f) : new Color(1f, .64f, .12f);

        status.text = director.StatusMessage;
        status.color = director.IsCaught ? new Color(1f, .34f, .22f) : new Color(.6f, 1f, .94f);

        if (director.Collectable != null && !director.IsCaught && !director.HasEscaped)
        {
            hint.text = "<color=#83FCF6><b>[E] PICK UP " + director.Collectable.IntelName + "</b></color>";
            crosshair.text = "◆";
        }
        else
        {
            hint.text = "WASD Move     Mouse Look     Shift Sprint     Ctrl/C Crouch     " +
                "<color=#84EFFF>F Flashlight</color>     E Collect     Space Jump     R Restart     Esc Cursor";
            crosshair.text = "+";
        }
    }

    private static RectTransform Panel(string label, RectTransform parent, Vector2 anchor,
        Vector2 center, Vector2 size, Color color)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(Image));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = center;
        rect.sizeDelta = size;
        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return rect;
    }

    private static TextMeshProUGUI Text(string label, RectTransform parent,
        Vector2 anchor, Vector2 pos, Vector2 size, float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(label, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor.x > .95f ? new Vector2(1f, 1f) :
            anchor.x < .05f ? new Vector2(0f, 1f) : new Vector2(.5f, .5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
        TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.fontSize = fontSize;
        tmp.color = new Color(.89f, .97f, 1f);
        tmp.alignment = alignment;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        if (font != null) tmp.font = font;
        return tmp;
    }
}
