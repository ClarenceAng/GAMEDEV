using UnityEngine;

// Rotates a platform around a local axis, either spinning continuously or
// swinging back and forth like a see-saw.
// Runs before the player so riders are carried using this frame's rotation.
[DefaultExecutionOrder(-100)]
public class PlatformRotator : MonoBehaviour
{
    [SerializeField]
    Vector3 localAxis = Vector3.up;

    [SerializeField]
    [Tooltip("Continuous spin speed. Negative spins the other way. Ignored when Swing Angle is above 0.")]
    float degreesPerSecond = 30f;

    [SerializeField]
    [Tooltip("If above 0, the platform rocks between -angle and +angle instead of spinning.")]
    float swingAngle;

    [SerializeField]
    [Tooltip("Seconds for one full swing there and back.")]
    float swingDuration = 4f;

    Quaternion startRotation;

    void Awake()
    {
        startRotation = transform.localRotation;
    }

    void Update()
    {
        if (swingAngle > 0)
        {
            float angle = swingAngle * Mathf.Sin(Time.time / swingDuration * 2f * Mathf.PI);
            transform.localRotation = startRotation * Quaternion.AngleAxis(angle, localAxis);
        }
        else
        {
            transform.Rotate(localAxis, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
