using UnityEngine;
using UnityEngine.UI;

// the HUD, made in code. there's a bunch of text labels that get reused every frame,
// and some full screen images for the dimming, the white flash, the black bars and the scanlines.
// x and y are 0 to 1 across the screen (y from the top), sizes are pixels on a 1080p screen
public class Hud
{
    Font font;
    Transform root;
    Text[] labels = new Text[14];
    int labelsUsed;
    Image panel;
    Image dim;
    Image barTop;
    Image barBottom;
    Image subtitleBack;
    Image timerBack;
    Image timerFill;
    Image arrow;
    Image flash;
    RawImage scanlines;

    public Hud(Transform parent, Camera cam)
    {
        GameObject canvasObj = new GameObject("HUD");
        canvasObj.transform.SetParent(parent, false);
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; // drawn by the game camera so it shows up in screenshots
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        root = canvasObj.transform;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // the order we make them in is the order they get drawn in
        panel = MakeImage("Panel");
        dim = MakeImage("Dim");
        barTop = MakeImage("LetterboxTop");
        barBottom = MakeImage("LetterboxBottom");
        barTop.color = Color.black;
        barBottom.color = Color.black;
        subtitleBack = MakeImage("SubtitleBack");
        subtitleBack.color = new Color(0f, 0f, 0f, 0.5f);

        // lantern timer bar in the top left
        timerBack = MakeImage("TimerBack");
        PlaceTopLeft(timerBack.rectTransform, 34f, -112f, 260f, 16f);
        timerBack.color = new Color(0f, 0f, 0f, 0.55f);
        timerFill = MakeImage("TimerFill");
        PlaceTopLeft(timerFill.rectTransform, 36f, -114f, 256f, 12f);

        // the compass arrow in the bottom left. draw a triangle into a texture
        Texture2D arrowTex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float t = (y - 6f) / 52f;          // 0 at the bottom, 1 at the tip
                float halfWidth = (1f - t) * 24f;
                bool inside = t >= 0f && t <= 1f && Mathf.Abs(x - 31.5f) <= halfWidth;
                if (inside) arrowTex.SetPixel(x, y, Color.white);
                else arrowTex.SetPixel(x, y, Color.clear);
            }
        }
        arrowTex.Apply();
        arrow = MakeImage("Arrow");
        arrow.sprite = Sprite.Create(arrowTex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        RectTransform arrowRect = arrow.rectTransform;
        arrowRect.anchorMin = new Vector2(0.09f, 0.17f);
        arrowRect.anchorMax = new Vector2(0.09f, 0.17f);
        arrowRect.sizeDelta = new Vector2(64f, 64f);

        for (int i = 0; i < labels.Length; i++)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(root, false);
            Text label = labelObj.AddComponent<Text>();
            label.font = font;
            label.fontStyle = FontStyle.Bold;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            labelObj.AddComponent<Shadow>();
            labelObj.SetActive(false);
            labels[i] = label;
        }

        // scanlines like an old screen: a tiny texture repeated over the whole screen
        Texture2D scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false);
        scanTex.filterMode = FilterMode.Point;
        scanTex.wrapMode = TextureWrapMode.Repeat;
        scanTex.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
        scanTex.SetPixel(0, 1, new Color(0f, 0f, 0f, 0.2f));
        scanTex.SetPixel(0, 2, Color.clear);
        scanTex.SetPixel(0, 3, Color.clear);
        scanTex.Apply();
        GameObject scanObj = new GameObject("Scanlines");
        scanObj.transform.SetParent(root, false);
        scanlines = scanObj.AddComponent<RawImage>();
        FillScreen(scanlines.rectTransform);
        scanlines.texture = scanTex;
        scanlines.uvRect = new Rect(0f, 0f, 1f, 1080f / 8f);
        scanlines.raycastTarget = false;

        flash = MakeImage("Flash");
    }

    Image MakeImage(string name)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(root, false);
        Image img = obj.AddComponent<Image>();
        FillScreen(img.rectTransform);
        img.raycastTarget = false;
        img.enabled = false;
        return img;
    }

    void FillScreen(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void PlaceTopLeft(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);
    }

    // call this at the start of every frame, it hides everything
    public void Begin()
    {
        labelsUsed = 0;
        for (int i = 0; i < labels.Length; i++) labels[i].gameObject.SetActive(false);
        panel.enabled = false;
        dim.enabled = false;
        barTop.enabled = false;
        barBottom.enabled = false;
        subtitleBack.enabled = false;
        timerBack.enabled = false;
        timerFill.enabled = false;
        arrow.enabled = false;
    }

    public void Text(string text, float x, float y, float size, Color color)
    {
        Text(text, x, y, size, color, false, true);
    }

    public Text Text(string text, float x, float y, float size, Color color, bool leftAligned, bool shadow)
    {
        if (labelsUsed >= labels.Length) return null; // out of labels
        Text label = labels[labelsUsed];
        labelsUsed++;
        label.gameObject.SetActive(true);
        label.text = text;
        label.fontSize = Mathf.RoundToInt(size);
        label.color = color;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(x, 1f - y);
        rect.anchorMax = new Vector2(x, 1f - y);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1800f, size * 1.6f);
        if (leftAligned)
        {
            label.alignment = TextAnchor.MiddleLeft;
            rect.pivot = new Vector2(0f, 0.5f);
        }
        else
        {
            label.alignment = TextAnchor.MiddleCenter;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        Shadow textShadow = label.GetComponent<Shadow>();
        textShadow.enabled = shadow;
        textShadow.effectColor = new Color(0f, 0f, 0f, 0.6f);
        textShadow.effectDistance = new Vector2(size * 0.05f, -size * 0.05f);
        return label;
    }

    public void Lives(int left, int max)
    {
        Color color = new Color(0.5f, 0.95f, 0.3f);
        if (left <= 1) color = new Color(1f, 0.4f, 0.3f);
        Text("LIVES " + left + "/" + max, 0.92f, 0.05f, 40f, color);
    }

    // the lantern timer, fraction is 0 to 1
    public void Bar(float fraction, Color color)
    {
        timerBack.enabled = true;
        timerFill.enabled = true;
        timerFill.color = color;
        timerFill.rectTransform.sizeDelta = new Vector2(256f * Mathf.Clamp01(fraction), 12f);
    }

    // a colored strip across the screen (for the title)
    public void Panel(Color color, float top, float bottom)
    {
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f - bottom);
        rect.anchorMax = new Vector2(1f, 1f - top);
        panel.color = color;
        panel.enabled = true;
    }

    // black bars at the top and bottom like a movie
    public void Letterbox(float size)
    {
        size = Mathf.Clamp(size, 0f, 0.4f);
        barTop.enabled = size > 0.001f;
        barBottom.enabled = size > 0.001f;
        barTop.rectTransform.anchorMin = new Vector2(0f, 1f - size);
        barBottom.rectTransform.anchorMax = new Vector2(1f, size);
    }

    // a line of dialogue at the bottom. typed = how much of it is showing (0 to 1)
    public void Subtitle(string speaker, Color speakerColor, string text, float typed)
    {
        subtitleBack.enabled = true;
        subtitleBack.rectTransform.anchorMin = new Vector2(0f, 0.10f);
        subtitleBack.rectTransform.anchorMax = new Vector2(1f, 0.27f);

        int letters = Mathf.Clamp(Mathf.CeilToInt(text.Length * Mathf.Clamp01(typed)), 0, text.Length);
        Text(speaker, 0.5f, 0.765f, 30f, speakerColor);
        Text label = Text(text.Substring(0, letters), 0.5f, 0.835f, 42f, Color.white, false, true);
        if (label != null)
        {
            // long lines go onto two lines
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.rectTransform.sizeDelta = new Vector2(1500f, 120f);
        }
    }

    public void Dim(Color color)
    {
        dim.color = color;
        dim.enabled = true;
    }

    // degrees = how far to the right of straight ahead the target is
    public void Arrow(float degrees, Color color)
    {
        arrow.enabled = true;
        arrow.color = color;
        arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -degrees);
    }

    public void Scanlines(float alpha)
    {
        scanlines.color = new Color(1f, 1f, 1f, alpha);
    }

    public void Flash(float alpha, Color color)
    {
        flash.color = new Color(color.r, color.g, color.b, alpha);
        flash.enabled = alpha > 0.001f;
    }
}
