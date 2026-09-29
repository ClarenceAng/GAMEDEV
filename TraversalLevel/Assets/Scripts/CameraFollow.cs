using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField]
    Transform target;

    [SerializeField]
    Vector3 offset = new Vector3(0, 6, -9);

    [SerializeField]
    float smoothSpeed = 5f;

    // LateUpdate runs after the player has moved this frame
    void LateUpdate()
    {
        Vector3 wantedPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, wantedPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up);
    }
}
