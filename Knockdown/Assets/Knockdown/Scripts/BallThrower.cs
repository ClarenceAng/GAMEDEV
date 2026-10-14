using UnityEngine;
using UnityEngine.InputSystem;

// Goes on the player. Left click throws a ball where you are looking.
public class BallThrower : MonoBehaviour
{
    public Rigidbody ballPrefab;
    public BoothGame game;
    public float throwSpeed = 25f;
    public float lift = 0.8f; // throws the ball a little bit up so it lands where you aim

    void Update()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (game.balls > 0 && game.gameOver == false)
            {
                ThrowBall();
            }
        }
    }

    void ThrowBall()
    {
        game.balls = game.balls - 1;

        Transform cam = Camera.main.transform;
        Rigidbody ball = Instantiate(ballPrefab, cam.position + cam.forward * 0.6f, Quaternion.identity);
        ball.linearVelocity = cam.forward * throwSpeed + Vector3.up * lift;

        // so the ball doesn't hit the player
        foreach (Collider playerCollider in GetComponentsInChildren<Collider>())
        {
            Physics.IgnoreCollision(ball.GetComponent<Collider>(), playerCollider);
        }

        Destroy(ball.gameObject, 5f);
    }
}
