using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField]
    Vector3 moveOffset = new Vector3(0, 3, 0);

    [SerializeField]
    float secondsPerTrip = 2.5f;

    Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        float t = Mathf.PingPong(Time.time / secondsPerTrip, 1f);
        t = Mathf.SmoothStep(0f, 1f, t);

        transform.position = startPosition + moveOffset * t;
    }
}
