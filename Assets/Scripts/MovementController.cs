using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(CharacterController))]
public class MovementController : MonoBehaviour
{
    [SerializeField]
    PlayerStats stats;

    [Header("Movement")]
    [SerializeField]
    float moveSpeed = 5f;

    [SerializeField]
    float jumpHeight = 1.5f;

    [SerializeField]
    float gravity = -9.81f;

    [Header("Platform Riding")]
    [SerializeField]
    float groundCheckDistance = 0.3f;

    [SerializeField]
    LayerMask groundMask = ~0; // everything by default

    CharacterController controller;
    Vector2 moveInput;
    Vector3 verticalVelocity;
    bool jumpQueued;

  
    PlatformMover currentPlatform;
    Vector3 lastPlatformPosition;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        RidePlatformIfStandingOnOne();
        ApplyGravityAndJump();

        Vector3 horizontalMove = moveSpeed * new Vector3(moveInput.x, 0, moveInput.y);
        controller.Move((horizontalMove + verticalVelocity) * Time.deltaTime);
    }

    void ApplyGravityAndJump()
    {
        bool grounded = controller.isGrounded;

        if (grounded && verticalVelocity.y < 0f)
        {
            
            
            verticalVelocity.y = -2f;
        }

        if (jumpQueued && grounded)
        {
            verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        jumpQueued = false;

        verticalVelocity.y += gravity * Time.deltaTime;
    }

    void RidePlatformIfStandingOnOne()
    {
        PlatformMover platformBelow = null;

        float rayLength = controller.skinWidth + groundCheckDistance;
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, rayLength, groundMask))
        {
            platformBelow = hit.collider.GetComponentInParent<PlatformMover>();
        }

        if (platformBelow != null)
        {
            if (currentPlatform == platformBelow)
            {
                Vector3 platformDelta = currentPlatform.transform.position - lastPlatformPosition;
                if (platformDelta.sqrMagnitude > 0f)
                {
                    controller.Move(platformDelta);
                }
            }

            currentPlatform = platformBelow;
            lastPlatformPosition = currentPlatform.transform.position;
        }
        else
        {
            currentPlatform = null;
        }
    }

    public void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }

    public void OnJump(InputValue value)
    {
        jumpQueued = true;

        
       
        if (stats != null)
        {
            stats.Health += 10;
        }
    }
}