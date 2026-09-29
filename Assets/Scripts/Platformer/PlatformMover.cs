using UnityEngine;

// Moves a platform back and forth between its start position and start + travel.
// Use a vertical travel (e.g. 0,4,0) for lifts and a horizontal one for sliders.
// Runs before the player so riders are carried using this frame's position.
[DefaultExecutionOrder(-100)]
public class PlatformMover : MonoBehaviour
{
    [SerializeField]
    [Tooltip("World-space offset from the start position to the far end of the path.")]
    Vector3 travel = new Vector3(0, 4, 0);

    [SerializeField]
    [Tooltip("Seconds for one full trip there and back.")]
    float cycleDuration = 5f;

    [SerializeField]
    [Range(0, 1)]
    [Tooltip("Where in the cycle this platform starts, so neighbouring platforms can be out of sync.")]
    float phaseOffset;

    Vector3 startPosition;

    void Awake()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float cycle = Mathf.Repeat(Time.time / cycleDuration + phaseOffset, 1f);
        // Cosine ease: 0 -> 1 -> 0, slowing down at each end so jumps on/off are easier to time.
        float blend = 0.5f - 0.5f * Mathf.Cos(cycle * 2f * Mathf.PI);
        transform.position = startPosition + travel * blend;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 from = Application.isPlaying ? startPosition : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(from, from + travel);
        Gizmos.DrawWireCube(from + travel, transform.lossyScale);
    }
}
