using UnityEngine;

// The same target component works for both icicles and snowmen.
[RequireComponent(typeof(Rigidbody))]
public class TargetKnockdown : MonoBehaviour
{
    [Header("Knockdown")]
    public float extraTorque = 2.2f;
    public float settleDelay = 0.65f;
    public float settleVelocity = 0.18f;

    public bool IsKnockedDown { get; private set; }
    private Rigidbody body;
    private float stillTime;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        if (!IsKnockedDown)
            return;

        // When the target has already landed and almost stopped moving,
        // freeze it so it does not spin or roll forever.
        if (body.linearVelocity.sqrMagnitude < settleVelocity * settleVelocity &&
            body.angularVelocity.sqrMagnitude < settleVelocity * settleVelocity)
        {
            stillTime += Time.fixedDeltaTime;
            if (stillTime >= settleDelay)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.constraints = RigidbodyConstraints.FreezeAll;
                enabled = false;
            }
        }
        else
        {
            stillTime = 0f;
        }
    }

    public void KnockDown(Vector3 direction, Vector3 point, float force)
    {
        if (IsKnockedDown)
            return;

        IsKnockedDown = true;
        stillTime = 0f;

        MovingSnowman movement = GetComponent<MovingSnowman>();
        if (movement != null)
            movement.enabled = false;

        EnemySnowman enemy = GetComponent<EnemySnowman>();
        if (enemy != null)
            enemy.enabled = false;

        body.isKinematic = false;
        body.useGravity = true;
        body.constraints = RigidbodyConstraints.None;
        body.linearDamping = 1.4f;
        body.angularDamping = 2.8f;
        body.maxAngularVelocity = 6f;
        body.AddForceAtPosition(direction * force, point, ForceMode.Impulse);

        Vector3 axis = Vector3.Cross(Vector3.up, direction);
        if (axis.sqrMagnitude < 0.01f)
            axis = transform.right;
        body.AddTorque(axis.normalized * extraTorque, ForceMode.Impulse);

        if (SnowballGame.instance != null)
            SnowballGame.instance.TargetHit();
    }
}
