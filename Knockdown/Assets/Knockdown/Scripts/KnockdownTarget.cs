using UnityEngine;

// Goes on every can. A can is down when it tips over or falls.
public class KnockdownTarget : MonoBehaviour
{
    Rigidbody rb;
    float startHeight;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        startHeight = transform.position.y;
    }

    public bool IsDown()
    {
        bool tippedOver = transform.up.y < 0.5f;
        bool fell = transform.position.y < startHeight - 0.2f;
        return tippedOver || fell;
    }

    // Cans on the shelves just get knocked over by the physics.
    // Cans on a string are kinematic so they can't fall. When a ball hits one,
    // it comes off its string and falls straight down.
    void OnCollisionEnter(Collision collision)
    {
        if (rb.isKinematic == false)
        {
            return;
        }

        // only a ball can knock a can off its string, not another can
        if (collision.gameObject.GetComponent<KnockdownTarget>() != null)
        {
            return;
        }

        rb.isKinematic = false;
        transform.parent = null; // stop swinging with the string
        collision.rigidbody.linearVelocity = Vector3.zero; // stop the ball too, so it can't bounce into other cans
    }
}
