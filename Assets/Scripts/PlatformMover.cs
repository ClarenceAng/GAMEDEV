using UnityEngine;



public class PlatformMover : MonoBehaviour
{
    public enum MoveAxis { Vertical, Horizontal, Both }

    [Header("Movement Settings")]
    [SerializeField]
    MoveAxis moveAxis = MoveAxis.Vertical;

    [SerializeField]
    float verticalDistance = 3f;

    [SerializeField]
    float horizontalDistance = 3f;

    [SerializeField]
    float speed = 1f;

    Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void FixedUpdate()
    {
        
        
        float wave = Mathf.Sin(Time.time * speed);

        Vector3 offset = Vector3.zero;

        if (moveAxis == MoveAxis.Vertical || moveAxis == MoveAxis.Both)
        {
            offset.y = wave * verticalDistance;
        }

        if (moveAxis == MoveAxis.Horizontal || moveAxis == MoveAxis.Both)
        {
            offset.x = wave * horizontalDistance;
        }

        transform.position = startPosition + offset;
    }
}
