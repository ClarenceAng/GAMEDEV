using UnityEngine;
using UnityEngine.InputSystem;

// Dedicated FPS controller. Reuses the original player's Rigidbody and BoxCollider.
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public class StealthFirstPersonController : MonoBehaviour
{
    [Header("FPS Movement")]
    public float walkSpeed = 5.7f;
    public float crouchSpeed = 2.8f;
    public float sprintSpeed = 8.3f;
    public float lookSensitivity = 0.13f;
    public float jumpHeight = 1.1f;
    public bool MovementEnabled { get; set; } = true;
    public bool IsCrouching { get; private set; }
    public bool IsSprinting { get; private set; }
    public Transform Eyes => viewCamera != null ? viewCamera.transform : transform;

    private Rigidbody body;
    private BoxCollider hitbox;
    private Camera viewCamera;
    private Vector2 moveInput;
    private float pitch;
    private float yaw;
    private bool requestedJump;
    private float eyeHeight = 2.05f;

    public void Initialize(Camera camera, Vector3 start)
    {
        viewCamera = camera;
        PlayerMovement oldMovement = GetComponent<PlayerMovement>();
        if (oldMovement != null) oldMovement.enabled = false;
        PlayerCamera oldCamera = camera.GetComponent<PlayerCamera>();
        if (oldCamera != null) oldCamera.enabled = false;

        // The previous snowman model remains in the project but cannot block the FPS view.
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        foreach (Collider c in GetComponentsInChildren<Collider>(true))
            if (c.gameObject != gameObject) c.enabled = false;

        body = GetComponent<Rigidbody>();
        hitbox = GetComponent<BoxCollider>();
        body.useGravity = true;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.linearDamping = 3f;
        hitbox.size = new Vector3(0.65f, 1.5f, 0.65f);
        hitbox.center = new Vector3(0f, 1.25f, 0f);
        viewCamera.transform.SetParent(null);
        viewCamera.nearClipPlane = 0.065f;
        viewCamera.fieldOfView = 74f;
        Teleport(start);
        SetMouseCaptured(true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard == null) return;

        if (keyboard.escapeKey.wasPressedThisFrame) SetMouseCaptured(false);
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetMouseCaptured(true);

        if (MovementEnabled && Cursor.lockState == CursorLockMode.Locked && mouse != null)
        {
            Vector2 delta = mouse.delta.ReadValue();
            yaw += delta.x * lookSensitivity;
            pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -78f, 78f);
        }

        IsCrouching = MovementEnabled && (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed);
        moveInput = MovementEnabled ? new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)) : Vector2.zero;
        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        IsSprinting = MovementEnabled && !IsCrouching && keyboard.leftShiftKey.isPressed && moveInput.y > 0.1f;
        if (MovementEnabled && keyboard.spaceKey.wasPressedThisFrame) requestedJump = true;

        // Keep the standing player's collider foot at y=0.5, matching the reused floor.
        float newHeight = IsCrouching ? 0.95f : 1.5f;
        hitbox.size = new Vector3(0.65f, newHeight, 0.65f);
        hitbox.center = new Vector3(0f, 0.5f + newHeight * 0.5f, 0f);
        eyeHeight = Mathf.Lerp(eyeHeight, IsCrouching ? 1.33f : 2.05f, Time.deltaTime * 12f);
    }

    private void FixedUpdate()
    {
        if (body == null) return;
        Quaternion newRotation = Quaternion.Euler(0f, yaw, 0f);
        body.MoveRotation(newRotation);
        float speed = IsCrouching ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
        Vector3 requested = newRotation * new Vector3(moveInput.x, 0f, moveInput.y) * speed;
        body.linearVelocity = new Vector3(requested.x, body.linearVelocity.y, requested.z);
        if (requestedJump && !IsCrouching && Grounded())
        {
            body.linearVelocity = new Vector3(body.linearVelocity.x,
                Mathf.Sqrt(2f * Physics.gravity.magnitude * jumpHeight), body.linearVelocity.z);
        }
        requestedJump = false;
    }

    private bool Grounded()
    {
        // Exclude our own collider/layer from the sphere check.
        int mask = ~(1 << gameObject.layer);
        return Physics.CheckSphere(transform.position + Vector3.up * 0.47f,
            0.17f, mask, QueryTriggerInteraction.Ignore);
    }

    private void LateUpdate()
    {
        if (viewCamera == null) return;
        viewCamera.transform.position = transform.position + Vector3.up * eyeHeight;
        viewCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    public void Teleport(Vector3 pos)
    {
        if (body == null) body = GetComponent<Rigidbody>();
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.position = pos;
        transform.position = pos;
        yaw = 0f;
        pitch = 0f;
        moveInput = Vector2.zero;
        IsSprinting = false;
        IsCrouching = false;
        requestedJump = false;
        eyeHeight = 2.05f;
        Physics.SyncTransforms();
    }

    public void SetMouseCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.47f, 0.17f);
    }
}
