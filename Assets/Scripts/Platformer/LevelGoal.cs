using UnityEngine;

// Trigger volume covering the end platform. Reaching it completes the level.
[RequireComponent(typeof(Collider))]
public class LevelGoal : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController _))
        {
            LevelManager.Instance.CompleteLevel();
        }
    }
}
