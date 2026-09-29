using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public enum PlatformType
    {
        Stationary,
        Horizontal,
        Vertical,
        RotateClockwise,
        RotateCounterClockwise
    }

    [Header("Platform Type")]
    public PlatformType platformType = PlatformType.Stationary;

    [Header("Movement")]
    public float movementDistance = 3f;
    public float movementSpeed = 1f;
    public float startDelay = 0f;

    [Header("Rotation")]
    public float rotationSpeed = 45f;

    // How much the platform moved during the current physics frame.
    public Vector3 MovementDelta { get; private set; }

    // How much the platform rotated during the current physics frame.
    public Quaternion RotationDelta { get; private set; }

    private Vector3 startingPosition;
    private Quaternion startingRotation;
    private float time;

    void Start()
    {
        startingPosition = transform.position;
        startingRotation = transform.rotation;

        MovementDelta = Vector3.zero;
        RotationDelta = Quaternion.identity;
    }

    void FixedUpdate()
    {
        // Reset deltas every physics frame.
        MovementDelta = Vector3.zero;
        RotationDelta = Quaternion.identity;

        time += Time.fixedDeltaTime;

        if (time < startDelay)
            return;

        Vector3 oldPosition = transform.position;
        Quaternion oldRotation = transform.rotation;

        switch (platformType)
        {
            case PlatformType.Horizontal:
                MoveHorizontal();
                break;

            case PlatformType.Vertical:
                MoveVertical();
                break;

            case PlatformType.RotateClockwise:
                Rotate(-rotationSpeed);
                break;

            case PlatformType.RotateCounterClockwise:
                Rotate(rotationSpeed);
                break;
        }

        MovementDelta = transform.position - oldPosition;
        RotationDelta = transform.rotation * Quaternion.Inverse(oldRotation);
    }

    void MoveHorizontal()
    {
        float offset =
            Mathf.Sin((time - startDelay) * movementSpeed) *
            movementDistance;

        transform.position =
            startingPosition + Vector3.right * offset;
    }

    void MoveVertical()
    {
        float offset =
            Mathf.Sin((time - startDelay) * movementSpeed) *
            movementDistance;

        transform.position =
            startingPosition + Vector3.up * offset;
    }

    void Rotate(float degreesPerSecond)
    {
        transform.Rotate(
            Vector3.up,
            degreesPerSecond * Time.fixedDeltaTime,
            Space.Self
        );
    }
}