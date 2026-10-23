using UnityEngine;

// Animated *solid* overhead cover. The roof's BoxCollider is a child of this
// kinematic Rigidbody, so each new position blocks real physics raycasts.
// The underglow is parented to the same body and follows automatically.
[RequireComponent(typeof(Rigidbody))]
public class StealthMovingRoof : MonoBehaviour
{
    [SerializeField] private Vector3 movementAxis = Vector3.right;
    [SerializeField, Min(0f)] private float halfTravel = .8f;
    [SerializeField, Min(.01f)] private float cyclesPerSecond = .19f;
    [SerializeField] private bool reverse;

    private Rigidbody roofBody;
    private Vector3 originalPosition;
    private float startTime;

    public void Initialize(Vector3 axis, float amplitude, float frequency, bool oppositeDirection)
    {
        movementAxis = axis.sqrMagnitude > .001f ? axis.normalized : Vector3.right;
        halfTravel = Mathf.Max(0f, amplitude);
        cyclesPerSecond = Mathf.Max(.01f, frequency);
        reverse = oppositeDirection;
        originalPosition = transform.position;
        startTime = Time.fixedTime;
        ConfigureRigidbody();
    }

    private void Awake()
    {
        originalPosition = transform.position;
        startTime = Time.fixedTime;
        ConfigureRigidbody();
    }

    private void ConfigureRigidbody()
    {
        if (roofBody == null) roofBody = GetComponent<Rigidbody>();
        roofBody.useGravity = false;
        roofBody.isKinematic = true;
        roofBody.interpolation = RigidbodyInterpolation.Interpolate;
        roofBody.collisionDetectionMode = CollisionDetectionMode.Discrete;
    }

    private void FixedUpdate()
    {
        if (roofBody == null) return;
        float elapsed = Mathf.Max(0f, Time.fixedTime - startTime);
        float sign = reverse ? -1f : 1f;
        float offset = Mathf.Sin(elapsed * Mathf.PI * 2f * cyclesPerSecond) * halfTravel * sign;
        roofBody.MovePosition(originalPosition + movementAxis * offset);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 axis = movementAxis.sqrMagnitude > .001f ? movementAxis.normalized : Vector3.right;
        Vector3 center = Application.isPlaying ? originalPosition : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(center - axis * halfTravel, center + axis * halfTravel);
        Gizmos.DrawWireSphere(center - axis * halfTravel, .12f);
        Gizmos.DrawWireSphere(center + axis * halfTravel, .12f);
    }
}
