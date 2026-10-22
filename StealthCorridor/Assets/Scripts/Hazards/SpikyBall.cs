using UnityEngine;

// Goes on the pivot at the top of the rope. Swings the spiky ball from side to side like a pendulum.
// The rope and the ball are children of the pivot, so turning the pivot swings them too.
public class SpikyBall : MonoBehaviour
{
    public Transform ball;
    public float swingAngle = 48f;  // how far it swings each way (degrees)
    public float swingSpeed = 1.6f; // how fast it swings
    public float offset = 0f;       // so the balls dont all swing together
    public float hitRadius = 0.75f; // the ball and its spikes
    public LayerMask playerLayer;

    StealthGame game;

    void Start()
    {
        game = FindFirstObjectByType<StealthGame>();
    }

    void Update()
    {
        // swing left and right
        float angle = Mathf.Sin(Time.time * swingSpeed + offset) * swingAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // a ball around the spiky ball
        if (Physics.CheckSphere(ball.position, hitRadius, playerLayer))
        {
            game.PlayerCaught("a spiky ball (Sphere check)");
        }
    }
}
