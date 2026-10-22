using UnityEngine;

// An arrow made out of a cylinder and some cubes. It flies forward (the blue arrow in the scene view),
// then jumps back to where it started and flies again.
// Every arrow uses one of the physics checks to see if it touched the player.
public class Arrow : MonoBehaviour
{
    public enum CheckType { Sphere, Box, Capsule, Raycast }

    public CheckType checkType = CheckType.Sphere;
    public float speed = 5f;
    public float distance = 12f;   // how far it flies before it goes back to the start
    public float startOffset = 0f; // how far along it starts, so the arrows dont all fly together
    public float length = 1.2f;    // how long the arrow is
    public float thickness = 0.15f; // how fat the hit area is
    public LayerMask playerLayer;

    StealthGame game;
    Vector3 startPosition;
    float travelled;

    void Start()
    {
        game = FindFirstObjectByType<StealthGame>();
        startPosition = transform.position;

        travelled = startOffset;
        transform.position = startPosition + transform.forward * travelled;
    }

    void Update()
    {
        // fly forward
        float move = speed * Time.deltaTime;
        transform.position = transform.position + transform.forward * move;
        travelled = travelled + move;

        // go back to the start and fly again
        if (travelled >= distance)
        {
            transform.position = startPosition;
            travelled = 0f;
        }

        if (TouchingPlayer())
        {
            game.PlayerHit("an arrow");
        }
    }

    bool TouchingPlayer()
    {
        // the front and back of the arrow
        Vector3 tip = transform.position + transform.forward * (length / 2f);
        Vector3 tail = transform.position - transform.forward * (length / 2f);

        if (checkType == CheckType.Sphere)
        {
            // a small ball around the tip
            return Physics.CheckSphere(tip, thickness * 2f, playerLayer);
        }
        else if (checkType == CheckType.Box)
        {
            // a box around the whole arrow
            Vector3 halfSize = new Vector3(thickness, thickness, length / 2f);
            return Physics.CheckBox(transform.position, halfSize, transform.rotation, playerLayer);
        }
        else if (checkType == CheckType.Capsule)
        {
            // a capsule from the back of the arrow to the tip
            return Physics.CheckCapsule(tail, tip, thickness, playerLayer);
        }
        else
        {
            // a line from the back of the arrow to the tip
            return Physics.Raycast(tail, transform.forward, length, playerLayer);
        }
    }
}
