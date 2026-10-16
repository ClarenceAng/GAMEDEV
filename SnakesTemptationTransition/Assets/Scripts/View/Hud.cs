using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SnakesTemptation
{
    /// <summary>
    /// A minimal screen-space HUD built in code (no prefabs, no TextMeshPro import): a pool of labels, a dim/panel
    /// overlay, LCD scanlines and a full-screen flash. Positions are fractions of the screen measured from the top-left;
    /// sizes are in pixels of a 1080-high reference screen.
    /// </summary>
    sealed class Hud
    {
        readonly Font font;
        readonly Transform root;
        readonly Image panel, dim, flash;
        readonly Image barTop, barBottom, subStrip;
        readonly Image arrow, barBack, barFill;
        readonly RawImage scan;
        readonly List<Text> labels = new List<Text>();
        readonly List<Image> lifeIcons = new List<Image>();
        Sprite lifeSprite;
        int used;

        public Hud(Transform parent, Camera cam)
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            root = go.transform;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // rendered by the game camera so it also shows up in camera captures
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            panel = MakeImage("Panel");
            dim = MakeImage("Dim");
            barTop = MakeImage("LetterboxTop");
            barBottom = MakeImage("LetterboxBottom");
            subStrip = MakeImage("SubtitleStrip");
            barTop.color = barBottom.color = Color.black;
            subStrip.color = new Color(0f, 0f, 0f, 0.5f);

            // Glow timer bar, top-left under the seed counter.
            barBack = MakeImage("BarBack");
            SetBox(barBack.rectTransform, new Vector2(0f, 1f), new Vector2(34f, -112f), new Vector2(260f, 16f));
            barBack.rectTransform.pivot = new Vector2(0f, 1f);
            barBack.color = new Color(0f, 0f, 0f, 0.55f);
            barFill = MakeImage("BarFill");
            SetBox(barFill.rectTransform, new Vector2(0f, 1f), new Vector2(36f, -114f), new Vector2(256f, 12f));
            barFill.rectTransform.pivot = new Vector2(0f, 1f);

            // Compass arrow toward the nearest seed (bottom centre), rotated each frame.
            var at = new Texture2D(64, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float t = (y - 6f) / 52f;                       // 0 at the base, 1 at the tip
                    float half = (1f - t) * 24f;                    // triangle half-width
                    bool inside = t >= 0f && t <= 1f && Mathf.Abs(x - 31.5f) <= half;
                    at.SetPixel(x, y, inside ? Color.white : Color.clear);
                }
            at.Apply();
            var ago = new GameObject("SeedArrow", typeof(RectTransform), typeof(Image));
            ago.transform.SetParent(root, false);
            SetBox(ago.GetComponent<RectTransform>(), new Vector2(0.09f, 0.17f), Vector2.zero, new Vector2(64f, 64f));
            arrow = ago.GetComponent<Image>();
            arrow.sprite = Sprite.Create(at, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
            arrow.raycastTarget = false;
            arrow.enabled = false;

            var scanTex = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat };
            scanTex.SetPixels(new[] { new Color(0, 0, 0, 0.55f), new Color(0, 0, 0, 0.2f), Color.clear, Color.clear });
            scanTex.Apply();
            var sgo = new GameObject("Scanlines", typeof(RectTransform), typeof(RawImage));
            sgo.transform.SetParent(root, false);
            Stretch(sgo.GetComponent<RectTransform>());
            scan = sgo.GetComponent<RawImage>();
            scan.texture = scanTex;
            scan.uvRect = new Rect(0f, 0f, 1f, 1080f / 8f); // one 8px scanline period at the reference height
            scan.raycastTarget = false;

            flash = MakeImage("Flash");
        }

        Image MakeImage(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.enabled = false;
            return img;
        }

        static void SetBox(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, anchor.y >= 0.99f ? 1f : 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        Text NextLabel()
        {
            if (used == labels.Count)
            {
                var go = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(Shadow));
                go.transform.SetParent(root, false);
                // keep flash and scanlines on top of the labels
                flash.transform.SetAsLastSibling();
                var t = go.GetComponent<Text>();
                t.font = font;
                t.fontStyle = FontStyle.Bold;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                labels.Add(t);
                scan.transform.SetAsLastSibling();
                flash.transform.SetAsLastSibling();
            }
            var label = labels[used++];
            label.gameObject.SetActive(true);
            return label;
        }

        /// <summary>Start a frame: everything transient is hidden until it is set again.</summary>
        public void Begin()
        {
            used = 0;
            panel.enabled = false;
            dim.enabled = false;
            barTop.enabled = false;
            barBottom.enabled = false;
            subStrip.enabled = false;
            arrow.enabled = false;
            barBack.enabled = false;
            barFill.enabled = false;
            foreach (var icon in lifeIcons) icon.enabled = false;
            foreach (var l in labels) l.gameObject.SetActive(false);
        }

        /// <summary>Lives as little snake heads, top-right: green for each one left, dark for each one lost.</summary>
        public void Lives(int remaining, int max)
        {
            if (lifeSprite == null)
            {
                var tex = new Texture2D(48, 48, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
                for (int y = 0; y < 48; y++)
                    for (int x = 0; x < 48; x++)
                    {
                        float body = Mathf.Clamp01(22.5f - Vector2.Distance(new Vector2(x, y), new Vector2(23.5f, 23.5f)));
                        float eyeL = Vector2.Distance(new Vector2(x, y), new Vector2(16f, 29f)), eyeR = Vector2.Distance(new Vector2(x, y), new Vector2(31f, 29f));
                        bool eye = eyeL < 5.2f || eyeR < 5.2f;
                        tex.SetPixel(x, y, new Color(eye ? 0.05f : 1f, eye ? 0.05f : 1f, eye ? 0.05f : 1f, body));
                    }
                tex.Apply();
                lifeSprite = Sprite.Create(tex, new Rect(0, 0, 48, 48), new Vector2(0.5f, 0.5f));
            }
            while (lifeIcons.Count < max)
            {
                var go = new GameObject("Life", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                var img = go.GetComponent<Image>();
                img.sprite = lifeSprite;
                img.raycastTarget = false;
                lifeIcons.Add(img);
                scan.transform.SetAsLastSibling();   // keep the scanlines and flash on top
                flash.transform.SetAsLastSibling();
            }
            for (int i = 0; i < lifeIcons.Count; i++)
            {
                var img = lifeIcons[i];
                img.enabled = i < max;
                img.color = i < remaining ? new Color(0.5f, 0.95f, 0.3f) : new Color(0.25f, 0.3f, 0.25f, 0.45f);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-34f - i * 54f, -26f);
                rt.sizeDelta = new Vector2(46f, 46f);
            }
        }

        /// <summary>A drain bar under the seed counter (the lantern's remaining glow). <paramref name="fraction"/> is 0..1.</summary>
        public void Bar(float fraction, Color color)
        {
            barBack.enabled = true;
            barFill.enabled = true;
            barFill.color = color;
            barFill.rectTransform.sizeDelta = new Vector2(256f * Mathf.Clamp01(fraction), 12f);
        }

        public void Panel(Color c, float topFrac, float bottomFrac)
        {
            var rt = panel.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f - bottomFrac);
            rt.anchorMax = new Vector2(1f, 1f - topFrac);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            panel.color = c;
            panel.enabled = true;
        }

        /// <summary>Cinematic black bars across the top and bottom, each this fraction of the screen high.</summary>
        public void Letterbox(float fraction)
        {
            fraction = Mathf.Clamp(fraction, 0f, 0.4f);
            barTop.enabled = barBottom.enabled = fraction > 0.001f;
            var t = barTop.rectTransform;
            t.anchorMin = new Vector2(0f, 1f - fraction);
            t.anchorMax = Vector2.one;
            t.offsetMin = t.offsetMax = Vector2.zero;
            var b = barBottom.rectTransform;
            b.anchorMin = Vector2.zero;
            b.anchorMax = new Vector2(1f, fraction);
            b.offsetMin = b.offsetMax = Vector2.zero;
        }

        /// <summary>A line of dialogue near the bottom: the speaker's name above it, the words typed out so far.</summary>
        public void Subtitle(string speaker, Color speakerColor, string text, float typed01)
        {
            subStrip.enabled = true;
            var r = subStrip.rectTransform;
            r.anchorMin = new Vector2(0f, 0.10f);
            r.anchorMax = new Vector2(1f, 0.27f);
            r.offsetMin = r.offsetMax = Vector2.zero;
            string wrapped = Wrap(text, 62);
            int shown = Mathf.Clamp(Mathf.CeilToInt(wrapped.Length * Mathf.Clamp01(typed01)), 0, wrapped.Length);
            Text(speaker, 0.5f, 0.765f, 30f, speakerColor);
            Text(wrapped.Substring(0, shown), 0.5f, 0.835f, 42f, Color.white);
        }

        // Breaks a line at the space nearest the middle if it is longer than maxChars.
        static string Wrap(string s, int maxChars)
        {
            if (s.Length <= maxChars) return s;
            int mid = s.Length / 2, best = -1;
            for (int d = 0; d < mid; d++)
            {
                if (mid - d >= 0 && s[mid - d] == ' ') { best = mid - d; break; }
                if (mid + d < s.Length && s[mid + d] == ' ') { best = mid + d; break; }
            }
            return best < 0 ? s : s.Substring(0, best) + "\n" + s.Substring(best + 1);
        }

        public void Dim(Color c)
        {
            dim.color = c;
            dim.enabled = true;
        }

        /// <param name="degrees">How far clockwise from straight ahead the target is (0 = ahead, 90 = to the right).</param>
        public void Arrow(float degrees, Color color)
        {
            arrow.enabled = true;
            arrow.color = color;
            arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -degrees);
        }

        public void Scanlines(float alpha) => scan.color = new Color(1f, 1f, 1f, alpha);

        public void Flash(float alpha, Color? tint = null)
        {
            Color c = tint ?? Color.white;
            flash.color = new Color(c.r, c.g, c.b, alpha);
            flash.enabled = alpha > 0.001f;
        }

        public void Text(string s, float xFrac, float yFromTopFrac, float size, Color color, bool leftAligned = false, bool shadow = true)
        {
            var t = NextLabel();
            t.text = s;
            t.fontSize = Mathf.RoundToInt(size);
            t.color = color;
            t.alignment = leftAligned ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(xFrac, 1f - yFromTopFrac);
            rt.pivot = new Vector2(leftAligned ? 0f : 0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1800f, size * 1.6f);
            var sh = t.GetComponent<Shadow>();
            sh.enabled = shadow;
            sh.effectColor = new Color(0f, 0f, 0f, 0.6f);
            sh.effectDistance = new Vector2(size * 0.05f, -size * 0.05f);
        }
    }
}
