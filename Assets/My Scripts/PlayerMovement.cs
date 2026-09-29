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

    private bool jumpHeld = false;

    // The platform currently underneath the player.
    private MovingPlatform currentPlatform;


    void Start()
    {
        rb = GetComponent<Rigidbody>();
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
            movement.x * moveSpeed,
            rb.linearVelocity.y,
            movement.z * moveSpeed
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
        if (jumpHeld && IsGrounded())
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

    public void Respawn()
    {
        // Stop the player's movement.
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Make the player respawn at the starting position.
        rb.position = new Vector3(0f, 1f, 0f);

        // Reset the player's rotation.
        rb.rotation = Quaternion.identity;

        // Forget the platform we were standing on.
        currentPlatform = null;
    }
}