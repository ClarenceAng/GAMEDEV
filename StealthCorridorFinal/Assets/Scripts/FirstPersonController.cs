using UnityEngine;

/// First-person controller: mouse look + WASD walking + Space to jump + hold Shift to sneak.
/// Built on Unity's CharacterController. Esc frees the mouse, clicking the window locks it again.
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    public Transform cameraPivot;          // the camera object (pitch / look up-down is applied here)

    public float walkSpeed = 3.5f;
    public float sneakSpeed = 1.6f;
    public float jumpHeight = 0.9f;
    public float gravity = -20f;
    public float lookSensitivity = 2f;
    public float maxLookAngle = 80f;

    /// The game changes this for the speed-boost panel. 1 = normal.
    public float SpeedMultiplier = 1f;
    /// The game turns this off while "YOU DIED" / "SPOTTED" / "YOU ESCAPED" is on screen.
    public bool InputEnabled = true;

    public bool IsSneaking { get; private set; }
    public bool IsMoving { get; private set; }

    CharacterController controller;
    float yaw;
    float pitch;
    float verticalVelocity;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Start()
    {
        SetCursorLocked(true);
    }

    /// Puts the player somewhere new. The CharacterController must be off while we move it by hand.
    public void Teleport(Vector3 position, float facingDegrees)
    {
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;

        yaw = facingDegrees;
        pitch = 0f;
        verticalVelocity = 0f;
        ApplyLook();
    }

    void Update()
    {
        HandleCursor();

        bool cursorIsLocked = Cursor.lockState == CursorLockMode.Locked;
        if (!InputEnabled || !cursorIsLocked)
        {
            IsMoving = false;
            IsSneaking = false;
            return;
        }

        Look();
        Move();
    }

    void HandleCursor()
    {
        if (GameInput.CancelPressed()) SetCursorLocked(false);
        else if (GameInput.ClickPressed() && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void Look()
    {
        Vector2 look = GameInput.Look();
        yaw += look.x * lookSensitivity;
        pitch = Mathf.Clamp(pitch - look.y * lookSensitivity, -maxLookAngle, maxLookAngle);
        ApplyLook();
    }

    void ApplyLook()
    {
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        cameraPivot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 input = Vector2.ClampMagnitude(GameInput.Move(), 1f);
        IsSneaking = GameInput.SneakHeld();
        IsMoving = input.sqrMagnitude > 0.01f;

        float speed = (IsSneaking ? sneakSpeed : walkSpeed) * SpeedMultiplier;
        Vector3 horizontalVelocity = (transform.right * input.x + transform.forward * input.y) * speed;

        if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;           // stay stuck to the floor
        if (controller.isGrounded && GameInput.JumpPressed()) verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        verticalVelocity += gravity * Time.deltaTime;

        Vector3 velocity = horizontalVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }
}
