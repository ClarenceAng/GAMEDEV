using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class Snowball : MonoBehaviour
{
    public float lifetime = 5f;
    public float knockForce = 5.5f;
    public bool fromEnemy = false;
    public float damage = 12f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (fromEnemy)
        {
            PlayerHealth player = collision.collider.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.Damage(damage);
            }
        }
        else
        {
            TargetKnockdown target = collision.collider.GetComponentInParent<TargetKnockdown>();
            if (target != null && collision.contactCount > 0)
            {
                Rigidbody body = GetComponent<Rigidbody>();
                Vector3 direction = body.linearVelocity.sqrMagnitude > 0.01f
                    ? body.linearVelocity.normalized
                    : transform.forward;

                target.KnockDown(direction, collision.GetContact(0).point, knockForce);
            }
        }

        Destroy(gameObject, 0.08f);
    }
}
