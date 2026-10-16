using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SnakesTemptation
{
    /// <summary>
    /// A minimal screen-space HUD built in code (no prefabs, no TextMeshPro import): a pool of labels, a dim/panel
    /// overlay and LCD scanlines. Positions are fractions of the screen measured from the top-left; sizes are in pixels of
    /// a 1080-high reference screen.
    /// </summary>
    sealed class Hud
    {
        readonly Font font;
        readonly Transform root;
        readonly Image panel, dim;
        readonly RawImage scan;
        readonly List<Text> labels = new List<Text>();
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
                var t = go.GetComponent<Text>();
                t.font = font;
                t.fontStyle = FontStyle.Bold;
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                t.verticalOverflow = VerticalWrapMode.Overflow;
                t.raycastTarget = false;
                labels.Add(t);
                scan.transform.SetAsLastSibling(); // keep the scanlines on top of the labels
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
            foreach (var l in labels) l.gameObject.SetActive(false);
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

        public void Dim(Color c)
        {
            dim.color = c;
            dim.enabled = true;
        }

        public void Scanlines(float alpha) => scan.color = new Color(1f, 1f, 1f, alpha);

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
