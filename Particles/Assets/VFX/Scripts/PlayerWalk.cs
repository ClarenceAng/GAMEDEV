using UnityEngine;
using UnityEngine.InputSystem;

// Walk around with WASD and look around with the mouse.
public class PlayerWalk : MonoBehaviour
{
    // the camera is a child of the player, at eye height
    public Transform cam;
    public float speed = 5f;
    public float mouseSpeed = 0.1f;

    CharacterController controller;
    float lookUp = 0f;
    float fall = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();

        // hide the mouse and keep it in the game window
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // look around: turn the body left and right, tilt the camera up and down
        Vector2 mouse = Mouse.current.delta.ReadValue();
        transform.Rotate(0, mouse.x * mouseSpeed, 0);
        lookUp = lookUp - mouse.y * mouseSpeed;
        lookUp = Mathf.Clamp(lookUp, -80f, 80f);
        cam.localEulerAngles = new Vector3(lookUp, 0, 0);

        // walk with WASD
        Vector3 move = Vector3.zero;
        if (Keyboard.current.wKey.isPressed)
        {
            move = move + transform.forward;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            move = move - transform.forward;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            move = move + transform.right;
        }
        if (Keyboard.current.aKey.isPressed)
        {
            move = move - transform.right;
        }

        // simple gravity so the player stays on the ground
        if (controller.isGrounded)
        {
            fall = -1f;
        }
        else
        {
            fall = fall - 9.8f * Time.deltaTime;
        }

        Vector3 velocity = move.normalized * speed;
        velocity.y = fall;
        controller.Move(velocity * Time.deltaTime);
    }
}
