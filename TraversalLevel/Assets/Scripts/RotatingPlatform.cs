using UnityEngine;

// Spins the platform around. (0, 30, 0) = 30 degrees per second around the Y axis.
public class RotatingPlatform : MonoBehaviour
{
    [SerializeField]
    Vector3 rotationSpeed = new Vector3(0, 30, 0);

    void Update()
    {
        transform.Rotate(rotationSpeed * Time.deltaTime);
    }
}
