using UnityEngine;

public class TargetMover : MonoBehaviour
{
    public float angle = 15f;  // how far it swings each way (degrees)
    public float speed = 2.5f; // how fast it swings
    public float offset = 0f;  // so the cans don't all swing together

    void Update()
    {
        float z = Mathf.Sin(Time.time * speed + offset) * angle;
        transform.rotation = Quaternion.Euler(0f, 0f, z);
    }
}
