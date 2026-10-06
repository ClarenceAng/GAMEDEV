using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSpeed = 10f;

    [Header("Jump")]
    public float jumpHeight = 6f;
    public float riseGravity = 60f;
    public float apexGravity = 180f;
    public float fallGravity = 60f;

    [Header("Apex")]
    public float apexStartVelocity = 10f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.25f;

    [Header("Camera")]
    public Transform cameraTransform;

    private Rigidbody rb;
    private float originalMoveSpeed;
    private float speedMultiplier = 1f;
    private bool controlsEnabled = true;
    private Transform respawnPoint;

    private bool jumpHeld = false;

    // The platform currently underneath the player.
    private MovingPlatform currentPlatform;


    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalMoveSpeed = moveSpeed;
    }


    void Update()
    {
        // Check whether Space is currently being held.
        if (Keyboard.current != null)
        {
            jumpHeld = Keyboard.current.spaceKey.isPressed;
        }
    }


    void FixedUpdate()
    {
        HandlePlatformMovement();
        HandleMovement();
        HandleJump();
        HandleGravity();
    }


    void HandlePlatformMovement()
    {
        if (currentPlatform == null)
            return;

        // Normal platform movement.
        Vector3 newPosition =
            rb.position + currentPlatform.MovementDelta;

        // If the platform is rotating, move the player around the platform's center as well.
        if (currentPlatform.RotationDelta != Quaternion.identity)
        {
            Vector3 offsetFromPlatform =
                rb.position - currentPlatform.transform.position;

            Vector3 rotatedOffset =
                currentPlatform.RotationDelta * offsetFromPlatform;

            newPosition =
                currentPlatform.transform.position + rotatedOffset;
        }

        // Move the player to the platform's new position.
        rb.MovePosition(newPosition);

        // Rotate the player with the platform.
        if (currentPlatform.RotationDelta != Quaternion.identity)
        {
            rb.MoveRotation(
                currentPlatform.RotationDelta * rb.rotation
            );
        }
    }


    void HandleMovement()
    {
        if (!controlsEnabled)
        {
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
            return;
        }

        Vector2 input = Vector2.zero;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed)
                input.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                input.y -= 1f;

            if (Keyboard.current.aKey.isPressed)
                input.x -= 1f;

            if (Keyboard.current.dKey.isPressed)
                input.x += 1f;
        }

        if (input.magnitude > 1f)
            input.Normalize();


        Vector3 cameraForward = cameraTransform.forward;
        Vector3 cameraRight = cameraTransform.right;

        // Ignore the camera's up/down angle.
        cameraForward.y = 0f;
        cameraRight.y = 0f;

        cameraForward.Normalize();
        cameraRight.Normalize();


        Vector3 movement =
            cameraForward * input.y +
            cameraRight * input.x;

        movement = Vector3.ClampMagnitude(movement, 1f);


        // Move the player.
        rb.linearVelocity = new Vector3(
            movement.x * originalMoveSpeed * speedMultiplier,
            rb.linearVelocity.y,
            movement.z * originalMoveSpeed * speedMultiplier
        );


        // Turn the snowman toward the direction it's moving.
        if (movement.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(movement);

            rb.MoveRotation(
                Quaternion.Slerp(
                    rb.rotation,
                    targetRotation,
                    rotationSpeed * Time.fixedDeltaTime
                )
            );
        }
    }


    void HandleJump()
    {
        // If Space is being held and the player is grounded, jump immediately.
        if (controlsEnabled && jumpHeld && IsGrounded())
        {
            // Calculate the upward velocity needed to reach the desired jump height.
            float jumpVelocity =
                Mathf.Sqrt(2f * riseGravity * jumpHeight);

            rb.linearVelocity = new Vector3(
                rb.linearVelocity.x,
                jumpVelocity,
                rb.linearVelocity.z
            );

            // Stop being carried by the platform when jumping.
            currentPlatform = null;
        }
    }


    void HandleGravity()
    {
        if (!IsGrounded())
        {
            float currentGravity;

            // When the player is moving upward normally, use the normal rise gravity.
            if (rb.linearVelocity.y > apexStartVelocity)
            {
                currentGravity = riseGravity;
            }

            // When the player gets close to the top of the jump, use stronger gravity to reduce the hang time.
            else if (rb.linearVelocity.y > 0f)
            {
                currentGravity = apexGravity;
            }

            // When falling, use fall gravity.
            else
            {
                currentGravity = fallGravity;
            }

            rb.linearVelocity +=
                Vector3.down *
                currentGravity *
                Time.fixedDeltaTime;
        }
    }


    bool IsGrounded()
    {
        if (groundCheck == null)
            return false;

        return Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            groundCheckDistance
        );
    }


    private void OnCollisionEnter(Collision collision)
    {
        MovingPlatform platform =
            collision.gameObject.GetComponent<MovingPlatform>();

        if (platform != null)
        {
            currentPlatform = platform;
        }
    }


    private void OnCollisionStay(Collision collision)
    {
        MovingPlatform platform =
            collision.gameObject.GetComponent<MovingPlatform>();

        if (platform != null)
        {
            currentPlatform = platform;
        }
    }


    private void OnCollisionExit(Collision collision)
    {
        MovingPlatform platform =
            collision.gameObject.GetComponent<MovingPlatform>();

        if (platform != null &&
            currentPlatform == platform)
        {
            currentPlatform = null;
        }
    }

    public void SetRespawnPoint(Transform point)
    {
        respawnPoint = point;
    }

    public void SetControlsEnabled(bool enabled)
    {
        controlsEnabled = enabled;
        if (!enabled && rb != null)
            rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
    }

    public void SetBaseMoveSpeed(float speed)
    {
        moveSpeed = Mathf.Max(0.1f, speed);
        originalMoveSpeed = moveSpeed;
    }

    public void ConfigureJump(float height, float rise, float apex, float fall, float apexStart)
    {
        jumpHeight = Mathf.Max(0.5f, height);
        riseGravity = Mathf.Max(1f, rise);
        apexGravity = Mathf.Max(1f, apex);
        fallGravity = Mathf.Max(1f, fall);
        apexStartVelocity = Mathf.Max(0f, apexStart);
    }

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = Mathf.Max(0.1f, multiplier);
    }

    public void ResetMovementModifiers()
    {
        speedMultiplier = 1f;
        controlsEnabled = true;
    }

    public void Respawn()
    {
        // Stop the player's movement.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Use a referenced spawn Transform when the current level provides one.
        // The old hard-coded position remains as a fallback for the original traversal scene.
        rb.position = respawnPoint != null ? respawnPoint.position : new Vector3(0f, 1f, 0f);

        // Reset the player's rotation.
        rb.rotation = respawnPoint != null ? respawnPoint.rotation : Quaternion.identity;

        // Forget the platform we were standing on.
        currentPlatform = null;
    }
}