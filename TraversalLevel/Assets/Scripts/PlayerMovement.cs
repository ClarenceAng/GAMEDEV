using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(100)] 
public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    float moveSpeed = 6f;

    [SerializeField]
    float jumpHeight = 1.5f;

    [SerializeField]
    float gravity = -20f;

    CharacterController controller;
    Vector2 moveInput;
    bool jumpPressed;
    float verticalVelocity;
    Vector3 startPosition;

    Transform currentPlatform;
    Vector3 positionOnPlatform;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        startPosition = transform.position;
    }

    void Update()
    {
        Physics.SyncTransforms();

        // 1. ride along with the platform we are standing on
        Vector3 platformMovement = Vector3.zero;
        if (currentPlatform != null)
        {
            Vector3 newPosition = currentPlatform.TransformPoint(positionOnPlatform);
            platformMovement = newPosition - transform.position;
        }

        // 2. gravity and jumping
        if (controller.isGrounded && verticalVelocity < 0)
        {
            verticalVelocity = -2f; // a small push down keeps us stuck to the ground
        }

        if (jumpPressed && controller.isGrounded)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        jumpPressed = false;

        verticalVelocity += gravity * Time.deltaTime;

        // 3. walking
        Vector3 velocity = new Vector3(moveInput.x, 0, moveInput.y) * moveSpeed;
        velocity.y = verticalVelocity;

        currentPlatform = null; // OnControllerColliderHit sets this again if we are still on something
        controller.Move(velocity * Time.deltaTime + platformMovement);

        if (currentPlatform != null)
        {
            positionOnPlatform = currentPlatform.InverseTransformPoint(transform.position);
        }
    }

    // Called by the CharacterController when it touches a collider during Move()
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (hit.normal.y > 0.5f) 
        {
            currentPlatform = hit.transform;
        }
    }

    // Sends the player back to where they started (called by FallZone)
    public void Respawn()
    {
        controller.enabled = false; 
        transform.position = startPosition;
        controller.enabled = true;

        verticalVelocity = 0f;
        currentPlatform = null;
    }

    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    void OnJump(InputValue value)
    {
        if (value.isPressed)
        {
            jumpPressed = true;
        }
    }
}
