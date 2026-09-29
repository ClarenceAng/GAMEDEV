using UnityEngine;
using UnityEngine.InputSystem;

// Custom movement + jump controller built on CharacterController.
// Receives OnMove/OnJump from a PlayerInput component set to "Send Messages",
// and rides along with whatever platform it is standing on (moving or rotating).
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField]
    float moveSpeed = 6f;

    [SerializeField]
    float turnSpeed = 720f;

    [SerializeField]
    [Tooltip("Movement is relative to this transform's facing (usually the camera). World axes if empty.")]
    Transform cameraTransform;

    [Header("Jump")]
    [SerializeField]
    float jumpHeight = 1.6f;

    [SerializeField]
    float gravity = -25f;

    [SerializeField]
    float maxFallSpeed = 40f;

    [SerializeField]
    [Tooltip("Grace period after walking off a ledge during which a jump still counts.")]
    float coyoteTime = 0.1f;

    [SerializeField]
    [Tooltip("A jump pressed this long before landing is performed on landing.")]
    float jumpBufferTime = 0.15f;

    CharacterController controller;
    Vector2 moveInput;
    float verticalVelocity;
    float lastGroundedTime = float.NegativeInfinity;
    float lastJumpPressedTime = float.NegativeInfinity;
    bool inputEnabled = true;

    // Platform riding: where we stood on the platform last frame, in its local space.
    Transform ground;
    Vector3 groundLocalPosition;
    Quaternion groundLastRotation;

    public bool IsGrounded => controller.isGrounded;
    public Transform Ground => ground;

    Vector3 FeetPosition => transform.TransformPoint(controller.center) + Vector3.down * (controller.height * 0.5f);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Read before FollowGround: its Move would otherwise report us as airborne while riding a platform.
        bool grounded = controller.isGrounded;

        FollowGround();

        if (grounded)
        {
            lastGroundedTime = Time.time;
            if (verticalVelocity < 0)
            {
                // Small constant push keeps the controller snapped to the ground.
                verticalVelocity = -2f;
            }
        }

        bool canJump = Time.time - lastGroundedTime <= coyoteTime;
        bool jumpBuffered = Time.time - lastJumpPressedTime <= jumpBufferTime;
        if (inputEnabled && canJump && jumpBuffered)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            lastGroundedTime = float.NegativeInfinity;
            lastJumpPressedTime = float.NegativeInfinity;
        }

        verticalVelocity = Mathf.Max(verticalVelocity + gravity * Time.deltaTime, -maxFallSpeed);

        Vector3 moveDirection = inputEnabled ? GetMoveDirection() : Vector3.zero;
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
        }

        Vector3 velocity = moveDirection * moveSpeed + Vector3.up * verticalVelocity;
        CollisionFlags flags = controller.Move(velocity * Time.deltaTime);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0)
        {
            verticalVelocity = 0;
        }

        UpdateGround();
    }

    Vector3 GetMoveDirection()
    {
        Vector3 forward = Vector3.forward;
        Vector3 right = Vector3.right;
        if (cameraTransform != null)
        {
            forward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        }
        return Vector3.ClampMagnitude(forward * moveInput.y + right * moveInput.x, 1f);
    }

    // Move with the platform we were standing on, by however much it moved/rotated since last frame.
    void FollowGround()
    {
        if (ground == null)
        {
            return;
        }

        // Platforms move their transforms earlier this frame; make sure physics sees it before we Move.
        Physics.SyncTransforms();

        Vector3 carriedFeet = ground.TransformPoint(groundLocalPosition);
        controller.Move(carriedFeet - FeetPosition);

        Quaternion rotationDelta = ground.rotation * Quaternion.Inverse(groundLastRotation);
        Vector3 carriedForward = Vector3.ProjectOnPlane(rotationDelta * transform.forward, Vector3.up);
        if (carriedForward.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(carriedForward);
        }
    }

    // Find the collider directly beneath us and remember our position relative to it.
    void UpdateGround()
    {
        ground = null;
        if (!controller.isGrounded)
        {
            return;
        }

        Vector3 origin = transform.TransformPoint(controller.center);
        float castRadius = controller.radius * 0.9f;
        float castDistance = controller.height * 0.5f - castRadius + controller.skinWidth + 0.1f;
        if (Physics.SphereCast(origin, castRadius, Vector3.down, out RaycastHit hit, castDistance, ~0, QueryTriggerInteraction.Ignore))
        {
            ground = hit.collider.transform;
            // Anchor at the feet (on the surface) so tilting platforms don't swing the whole body sideways.
            groundLocalPosition = ground.InverseTransformPoint(FeetPosition);
            groundLastRotation = ground.rotation;
        }
    }

    public void Respawn(Vector3 position, Quaternion rotation)
    {
        // CharacterController overrides transform changes while enabled.
        controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;

        verticalVelocity = 0;
        ground = null;
        lastJumpPressedTime = float.NegativeInfinity;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            lastJumpPressedTime = Time.time;
        }
    }
}
