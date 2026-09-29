using UnityEngine;

// Keeps the camera at a fixed offset behind/above the target and looks at it.
public class FollowCamera : MonoBehaviour
{
    [SerializeField]
    Transform target;

    [SerializeField]
    Vector3 offset = new Vector3(0, 7, -9);

    [SerializeField]
    Vector3 lookOffset = new Vector3(0, 1, 0);

    [SerializeField]
    float smoothTime = 0.15f;

    Vector3 velocity;

    void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
        transform.LookAt(target.position + lookOffset);
    }
}
