using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LaserCorridorHUD : MonoBehaviour
{
    private sealed class BonusNotice
    {
        public TMP_Text Text;
        public string Label;
        public Color Color;
        public float EffectRemaining;
        public float FadeRemaining = 0.45f;
        public bool ShowTimer;
        public bool Fading;
        public int LastDisplayedSecond = int.MinValue;
    }

    private LaserCorridorGameManager manager;
    private PlayerVitals vitals;

    private TMP_Text healthText;
    private TMP_Text timerText;
    private TMP_Text centerText;
    private Image healthFill;
    private TMP_FontAsset font;
    private RectTransform bonusStack;
    private readonly List<BonusNotice> bonusNotices = new List<BonusNotice>();

    public void Configure(LaserCorridorGameManager gameManager, PlayerVitals playerVitals)
    {
        manager = gameManager;
        vitals = playerVitals;
        BuildUI();

        manager.TimeChanged += OnTimeChanged;
        manager.CenterMessageChanged += OnCenterMessage;
        manager.TimedBonusStarted += OnTimedBonusStarted;
        manager.BonusToastRequested += OnBonusToastRequested;
        manager.BonusNotificationsCleared += ClearBonusNotices;
        vitals.HealthChanged += OnHealthChanged;
        vitals.ShieldChanged += OnShieldChanged;

        OnTimeChanged(manager.TimeRemaining);
        OnHealthChanged(vitals.CurrentHealth, vitals.MaxHealth);
    }

    private void OnDestroy()
    {
        if (manager != null)
        {
            manager.TimeChanged -= OnTimeChanged;
            manager.CenterMessageChanged -= OnCenterMessage;
            manager.TimedBonusStarted -= OnTimedBonusStarted;
            manager.BonusToastRequested -= OnBonusToastRequested;
            manager.BonusNotificationsCleared -= ClearBonusNotices;
        }

        if (vitals != null)
        {
            vitals.HealthChanged -= OnHealthChanged;
            vitals.ShieldChanged -= OnShieldChanged;
        }
    }

    private void Update()
    {
        bool removedAny = false;

        for (int i = bonusNotices.Count - 1; i >= 0; i--)
        {
            BonusNotice notice = bonusNotices[i];
            if (notice == null || notice.Text == null)
            {
                bonusNotices.RemoveAt(i);
                removedAny = true;
                continue;
            }

            if (!notice.Fading)
            {
                notice.EffectRemaining -= Time.deltaTime;

                if (notice.ShowTimer)
                {
                    int seconds = Mathf.Max(0, Mathf.CeilToInt(notice.EffectRemaining));
                    if (seconds != notice.LastDisplayedSecond)
                    {
                        notice.LastDisplayedSecond = seconds;
                        notice.Text.text = $"{notice.Label}  {seconds}s";
                    }
                }

                if (notice.EffectRemaining <= 0f)
                {
                    notice.Fading = true;
                    notice.FadeRemaining = 0.45f;
                }
            }
            else
            {
                notice.FadeRemaining -= Time.deltaTime;
                Color c = notice.Color;
                c.a = Mathf.Clamp01(notice.FadeRemaining / 0.45f);
                notice.Text.color = c;

                if (notice.FadeRemaining <= 0f)
                {
                    Destroy(notice.Text.gameObject);
                    bonusNotices.RemoveAt(i);
                    removedAny = true;
                }
            }
        }

        if (removedAny)
            RepositionBonusNotices();
    }

    private void BuildUI()
    {
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = transform as RectTransform;
        if (root == null)
            root = gameObject.AddComponent<RectTransform>();

        GameObject topPanel = MakePanel("TopPanel", root, new Color(0.015f, 0.02f, 0.03f, 0.78f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(880f, 74f));

        healthText = MakeText("HealthText", topPanel.transform, "HEALTH 100", 26, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(38f, 10f), new Vector2(250f, 44f));

        GameObject healthBg = MakePanel("HealthBarBG", topPanel.transform, new Color(0.05f, 0.06f, 0.07f, 1f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(285f, 0f), new Vector2(260f, 20f));
        GameObject fill = MakePanel("HealthFill", healthBg.transform, new Color(0.15f, 0.95f, 0.35f, 1f), new Vector2(0f, 0f), new Vector2(0f, 1f), Vector2.zero, Vector2.zero);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        healthFill = fill.GetComponent<Image>();

        timerText = MakeText("TimerText", topPanel.transform, "TIME 60.0", 34, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-34f, 0f), new Vector2(240f, 50f));

        centerText = MakeText("CenterMessage", root, string.Empty, 54, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 220f));
        centerText.fontStyle = FontStyles.Bold;
        centerText.color = new Color(0.95f, 0.98f, 1f, 1f);

        GameObject stack = new GameObject("BonusNotificationStack", typeof(RectTransform));
        stack.transform.SetParent(root, false);
        bonusStack = stack.GetComponent<RectTransform>();
        bonusStack.anchorMin = new Vector2(0.5f, 0.86f);
        bonusStack.anchorMax = new Vector2(0.5f, 0.86f);
        bonusStack.pivot = new Vector2(0.5f, 1f);
        bonusStack.anchoredPosition = Vector2.zero;
        bonusStack.sizeDelta = new Vector2(850f, 240f);

        TMP_Text controls = MakeText(
            "Controls",
            root,
            "WASD Move   •   SPACE Jump   •   Mouse Wheel: 1st/3rd Person   •   RMB: Orbit in 3rd Person",
            20,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f),
            new Vector2(1150f, 44f));
        controls.color = new Color(0.78f, 0.82f, 0.88f, 0.9f);
    }

    private void OnHealthChanged(int current, int max)
    {
        if (healthText != null)
            healthText.text = $"HEALTH {current}";

        if (healthFill != null)
        {
            float ratio = max > 0 ? (float)current / max : 0f;
            RectTransform rect = healthFill.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);

            healthFill.color = ratio > 0.55f
                ? new Color(0.15f, 0.95f, 0.35f, 1f)
                : ratio > 0.25f
                    ? new Color(1f, 0.72f, 0.12f, 1f)
                    : new Color(1f, 0.18f, 0.12f, 1f);
        }
    }

    private void OnTimeChanged(float value)
    {
        if (timerText != null)
        {
            timerText.text = $"TIME {Mathf.Max(0f, value):00.0}";
            timerText.color = value <= 10f
                ? new Color(1f, 0.25f, 0.18f, 1f)
                : Color.white;
        }
    }

    private void OnCenterMessage(string message)
    {
        if (centerText != null)
            centerText.text = message;
    }

    private void OnTimedBonusStarted(string label, float duration, Color color)
    {
        AddBonusNotice(label, duration, color, true);
    }

    private void OnBonusToastRequested(string label, Color color, float duration)
    {
        AddBonusNotice(label, duration, color, false);
    }

    private void AddBonusNotice(string label, float duration, Color color, bool showTimer)
    {
        if (bonusStack == null)
            return;

        TMP_Text text = MakeText(
            "BonusNotice",
            bonusStack,
            showTimer ? $"{label}  {Mathf.CeilToInt(duration)}s" : label,
            27,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(800f, 38f));
        text.fontStyle = FontStyles.Bold;
        text.color = color;
        text.outlineWidth = 0.18f;
        text.outlineColor = new Color(0f, 0f, 0f, 0.85f);

        BonusNotice notice = new BonusNotice
        {
            Text = text,
            Label = label,
            Color = color,
            EffectRemaining = duration,
            ShowTimer = showTimer,
            LastDisplayedSecond = showTimer ? Mathf.CeilToInt(duration) : int.MinValue
        };

        bonusNotices.Add(notice);
        RepositionBonusNotices();
    }

    private void ClearBonusNotices()
    {
        foreach (BonusNotice notice in bonusNotices)
        {
            if (notice != null && notice.Text != null)
                Destroy(notice.Text.gameObject);
        }
        bonusNotices.Clear();
    }

    private void RepositionBonusNotices()
    {
        const float spacing = 38f;
        for (int i = 0; i < bonusNotices.Count; i++)
        {
            BonusNotice notice = bonusNotices[i];
            if (notice != null && notice.Text != null)
                notice.Text.rectTransform.anchoredPosition = new Vector2(0f, -i * spacing);
        }
    }

    private void OnShieldChanged(bool active)
    {
        if (healthText != null)
            healthText.color = active ? new Color(0.25f, 0.85f, 1f, 1f) : Color.white;
    }

    private GameObject MakePanel(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.GetComponent<Image>().color = color;
        return go;
    }

    private TMP_Text MakeText(string name, Transform parent, string value, int size, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMin.x > 0.75f ? new Vector2(1f, 0.5f) : anchorMin.x < 0.25f ? new Vector2(0f, 0.5f) : new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;

        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        if (font != null)
            text.font = font;
        return text;
    }
}
