using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(TargetKnockdown))]
public class EnemySnowman : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Rigidbody snowballPrefab;

    [Header("Movement")]
    public Vector3 roamCenter;
    public Vector2 roamSize = new Vector2(10f, 10f);
    public float moveSpeed = 2f;
    public float stoppingDistance = 0.7f;

    [Header("Attack")]
    public float attackRange = 18f;
    public float throwSpeed = 20f;
    public float cooldownMin = 2.2f;
    public float cooldownMax = 3.8f;
    public float damage = 12f;

    private Vector3 roamTarget;
    private Rigidbody body;
    private TargetKnockdown knockdown;
    private float nextThrowTime;

    private void Start()
    {
        body = GetComponent<Rigidbody>();
        knockdown = GetComponent<TargetKnockdown>();
        body.isKinematic = true;
        body.useGravity = false;
        ChooseNewRoamTarget();
        nextThrowTime = Time.time + Random.Range(0.5f, 1.8f);
    }

    private void FixedUpdate()
    {
        if (player == null || knockdown.IsKnockedDown)
            return;

        Vector3 flatTarget = roamTarget;
        flatTarget.y = transform.position.y;
        Vector3 delta = flatTarget - transform.position;
        delta.y = 0f;

        if (delta.magnitude <= stoppingDistance)
        {
            ChooseNewRoamTarget();
            delta = roamTarget - transform.position;
            delta.y = 0f;
        }

        Vector3 step = delta.normalized * moveSpeed * Time.fixedDeltaTime;
        if (step.sqrMagnitude > delta.sqrMagnitude)
            step = delta;

        body.MovePosition(body.position + step);

        Vector3 look = player.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.1f)
        {
            Quaternion rotation = Quaternion.LookRotation(look.normalized);
            body.MoveRotation(Quaternion.Slerp(body.rotation, rotation, Time.fixedDeltaTime * 4f));
        }
    }

    private void Update()
    {
        if (player == null || snowballPrefab == null || knockdown.IsKnockedDown)
            return;

        PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
            return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > attackRange)
            return;

        if (Time.time >= nextThrowTime)
        {
            ThrowSnowball();
            nextThrowTime = Time.time + Random.Range(cooldownMin, cooldownMax);
        }
    }

    private void ChooseNewRoamTarget()
    {
        roamTarget = roamCenter + new Vector3(
            Random.Range(-roamSize.x * 0.5f, roamSize.x * 0.5f),
            0f,
            Random.Range(-roamSize.y * 0.5f, roamSize.y * 0.5f));
    }

    private void ThrowSnowball()
    {
        Vector3 spawnPoint = transform.position + Vector3.up * 1.35f + transform.forward * 0.55f;
        Vector3 targetPoint = player.position + Vector3.up * 1.0f;
        Vector3 toTarget = targetPoint - spawnPoint;
        float flightTime = Mathf.Clamp(toTarget.magnitude / Mathf.Max(1f, throwSpeed), 0.2f, 1.7f);
        Vector3 adjustedTarget = targetPoint + Vector3.up * (0.5f * Mathf.Abs(Physics.gravity.y) * flightTime * flightTime * 0.75f);
        Vector3 direction = (adjustedTarget - spawnPoint).normalized;

        Rigidbody ball = Object.Instantiate(snowballPrefab, spawnPoint, Quaternion.LookRotation(direction));
        ball.linearVelocity = direction * throwSpeed;

        Snowball snowball = ball.GetComponent<Snowball>();
        if (snowball != null)
        {
            snowball.fromEnemy = true;
            snowball.damage = damage;
        }

        Collider ownCollider = GetComponent<Collider>();
        Collider ballCollider = ball.GetComponent<Collider>();
        if (ownCollider != null && ballCollider != null)
            Physics.IgnoreCollision(ballCollider, ownCollider);
    }
}
