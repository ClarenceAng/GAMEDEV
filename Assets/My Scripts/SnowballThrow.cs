using UnityEngine;
using UnityEngine.InputSystem;

public class SnowballThrow : MonoBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Rigidbody snowballPrefab;
    public Transform snowballSpawn;

    [Header("Throw")]
    public float throwSpeed = 36f;
    public float cooldown = 0.32f;

    private float nextThrowTime;

    private void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame || Time.time < nextThrowTime)
            return;

        if (playerCamera == null || snowballPrefab == null || snowballSpawn == null)
            return;

        ThrowSnowball();
        nextThrowTime = Time.time + cooldown;
    }

    private void ThrowSnowball()
    {
        Vector3 spawnPoint = snowballSpawn.position;
        Ray viewRay = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Vector3 aimPoint = viewRay.GetPoint(55f);

        RaycastHit[] hits = Physics.RaycastAll(viewRay, 100f, ~0, QueryTriggerInteraction.Ignore);
        float closest = float.PositiveInfinity;
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                aimPoint = hit.point;
            }
        }

        float flightTime = Vector3.Distance(spawnPoint, aimPoint) / Mathf.Max(1f, throwSpeed);
        Vector3 adjustedAim = aimPoint + Vector3.up *
            (0.5f * Mathf.Abs(Physics.gravity.y) * flightTime * flightTime * 0.85f);
        Vector3 direction = (adjustedAim - spawnPoint).normalized;
        if (direction.sqrMagnitude < 0.1f)
            direction = playerCamera.transform.forward;

        Rigidbody ball = Instantiate(snowballPrefab, spawnPoint, Quaternion.LookRotation(direction));
        ball.linearVelocity = direction * throwSpeed;
        Snowball snowball = ball.GetComponent<Snowball>();
        if (snowball != null)
            snowball.fromEnemy = false;

        Collider ownCollider = GetComponent<Collider>();
        Collider ballCollider = ball.GetComponent<Collider>();
        if (ownCollider != null && ballCollider != null)
            Physics.IgnoreCollision(ballCollider, ownCollider);
    }
}
