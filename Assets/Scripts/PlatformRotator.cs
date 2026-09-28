using UnityEngine;


public class PlatformRotator : MonoBehaviour
{
    [SerializeField]
    Vector3 rotationAxis = Vector3.up;

    [SerializeField]
    float rotationSpeed = 30f; // degrees per second

    void Update()
    {
        transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);
    }
}
