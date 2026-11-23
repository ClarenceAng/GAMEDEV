using UnityEngine;

namespace Pandamonium
{
    /// <summary>
    /// The visual for one note: a lane-coloured puck that slides along its lane. Pooled and reused by
    /// <see cref="NoteSpawner"/>; holds no game logic (judging lives in <see cref="HitJudge"/>).
    /// </summary>
    public class RhythmNote : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public enum NoteState { Approaching, Hit, Missed }

        [SerializeField] Renderer body;
        [SerializeField] Color missedColor = new(0.32f, 0.32f, 0.32f);
        [Tooltip("Height of the note above the lane origin.")]
        [SerializeField] float rideHeight = 0.19f;
        [Tooltip("Seconds the hit 'pop' lasts before the note is recycled.")]
        [SerializeField, Min(0.01f)] float hitPopDuration = 0.12f;
        [Tooltip("Past the hit line, notes sink and shrink away over this distance.")]
        [SerializeField, Min(0.1f)] float sinkDistance = 1.7f;

        MaterialPropertyBlock block;
        Vector3 baseScale;
        float hitTimer;

        public NoteData Data { get; private set; }
        public NoteState State { get; private set; }

        /// <summary>Distance past the hit line after which the note has fully sunk away.</summary>
        public float SinkDistance => sinkDistance;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseScale = transform.localScale;
        }

        public void Activate(NoteData data, Color laneColor, Transform lane)
        {
            Data = data;
            State = NoteState.Approaching;
            hitTimer = 0f;
            transform.SetParent(lane, false);
            transform.localRotation = Quaternion.identity;
            transform.localScale = baseScale;
            SetColor(laneColor);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Places the note <paramref name="z"/> units before the hit line (negative = past it). Past
        /// the line the note dips under the highway, so it never slides over the keycaps.
        /// </summary>
        public void SetDistance(float z)
        {
            float sink = z < 0f ? Mathf.Clamp01(-z / sinkDistance) : 0f;
            transform.localPosition = new Vector3(0f, rideHeight - 0.25f * sink, z);
            transform.localScale = baseScale * (1f - sink);
        }

        public void MarkHit()
        {
            State = NoteState.Hit;
            hitTimer = 0f;
        }

        public void MarkMissed()
        {
            State = NoteState.Missed;
            SetColor(missedColor);
        }

        /// <summary>Plays the hit pop (widen and flatten). Returns true when finished.</summary>
        public bool AdvanceHitPop(float deltaTime)
        {
            hitTimer += deltaTime;
            float k = Mathf.Clamp01(hitTimer / hitPopDuration);
            transform.localScale = Vector3.Scale(baseScale, new Vector3(1f + 0.6f * k, 1f - k, 1f + 0.6f * k));
            return k >= 1f;
        }

        void SetColor(Color color)
        {
            body.GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);
            body.SetPropertyBlock(block);
        }
    }
}
