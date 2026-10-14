using UnityEngine;

// Used for the training-stage moving targets only.
[RequireComponent(typeof(Rigidbody))]
public class MovingSnowman : MonoBehaviour
{
    [Header("Movement")]
    public float movementDistance = 1.5f;
    public float movementSpeed = 1.5f;
    public float startOffset = 0f;

    private Vector3 startingPosition;
    private Rigidbody body;
    private TargetKnockdown knockdown;

    private void Start()
    {
        startingPosition = transform.position;
        body = GetComponent<Rigidbody>();
        knockdown = GetComponent<TargetKnockdown>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (knockdown != null && knockdown.IsKnockedDown)
            return;

        float offset = Mathf.Sin(Time.fixedTime * movementSpeed + startOffset) * movementDistance;
        Vector3 position = startingPosition + Vector3.right * offset;
        body.MovePosition(position);

        Vector3 look = new Vector3(Mathf.Sign(Mathf.Cos(Time.fixedTime * movementSpeed + startOffset)), 0f, 0f);
        if (look.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(look);
            body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, Time.fixedDeltaTime * 7f));
        }
    }
}
