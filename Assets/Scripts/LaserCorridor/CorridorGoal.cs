using UnityEngine;

// Trigger volume at the far end of the corridor; entering it clears the level.
[RequireComponent(typeof(BoxCollider))]
public class CorridorGoal : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (LaserCorridorManager.Instance != null && other.TryGetComponent(out PlayerHealth _))
        {
            LaserCorridorManager.Instance.CompleteLevel();
        }
    }
}
