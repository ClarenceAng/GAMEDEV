using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeadeyeGulch
{
    /// <summary>
    /// A minimal screen-space HUD built in code (no prefabs, no TextMeshPro import): pools of labels and plain boxes, plus
    /// a dim/panel overlay. Rebuilt every frame between <see cref="Begin"/> and the draw calls.
    /// Positions are fractions of the screen measured from the top-left; sizes are in pixels of a 1080-high reference
    /// screen.
    /// </summary>
    sealed class Hud
    {
        const float RefHeight = 1080f;

        readonly Font font;
        readonly Transform root;
        readonly Image panel, dim;
        readonly List<Text> labels = new List<Text>();
        readonly List<Image> boxes = new List<Image>();
        int usedLabels, usedBoxes;

        public Hud(Transform parent, Camera cam)
        {
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);
            root = go.transform;
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; // rendered by the game camera so it also shows up in camera captures
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.1f;                      // in front of the gun model
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, RefHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            dim = MakeImage("Dim");
            panel = MakeImage("Panel");
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

        /// <summary>Start a frame: everything transient is hidden until it is set again.</summary>
        public void Begin()
        {
            usedLabels = usedBoxes = 0;
            panel.enabled = false;
            dim.enabled = false;
            foreach (var l in labels) l.gameObject.SetActive(false);
            foreach (var b in boxes) b.gameObject.SetActive(false);
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

        /// <summary>A solid rectangle centred on a screen point (plus an offset), sizes in reference pixels; +y offset is up.</summary>
        public void Box(float xFrac, float yFromTopFrac, float w, float h, Color color, Vector2 offset = default)
        {
            if (usedBoxes == boxes.Count)
            {
                var go = new GameObject("Box", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(root, false);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                boxes.Add(img);
            }
            var b = boxes[usedBoxes++];
            b.gameObject.SetActive(true);
            b.transform.SetAsLastSibling();
            b.color = color;
            var rt = b.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(xFrac, 1f - yFromTopFrac);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(w, h);
        }

        public void Text(string s, float xFrac, float yFromTopFrac, float size, Color color, TextAnchor align = TextAnchor.MiddleCenter, bool shadow = true)
        {
            if (usedLabels == labels.Count)
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
            }
            var label = labels[usedLabels++];
            label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            label.text = s;
            label.fontSize = Mathf.RoundToInt(size);
            label.color = color;
            label.alignment = align;

            float px = align == TextAnchor.MiddleLeft || align == TextAnchor.UpperLeft || align == TextAnchor.LowerLeft ? 0f
                     : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight || align == TextAnchor.LowerRight ? 1f
                     : 0.5f;
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(xFrac, 1f - yFromTopFrac);
            rt.pivot = new Vector2(px, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(1800f, size * 1.6f);

            var sh = label.GetComponent<Shadow>();
            sh.enabled = shadow;
            sh.effectColor = new Color(0.12f, 0.05f, 0.02f, 0.75f);
            sh.effectDistance = new Vector2(size * 0.06f, -size * 0.06f);
        }
    }
}
