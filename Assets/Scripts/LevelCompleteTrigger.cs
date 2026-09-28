using UnityEngine;
using UnityEngine.Events;


public class LevelCompleteTrigger : MonoBehaviour
{
    [SerializeField]
    UnityEvent onLevelComplete; 

    bool triggered;

    void OnTriggerEnter(Collider other)
    {
        if (triggered || !other.CompareTag("Player"))
        {
            return;
        }

        triggered = true;

        MovementController move = other.GetComponent<MovementController>();
        if (move != null)
        {
            move.enabled = true; 
        }

        Debug.Log("Level Complete!");
        onLevelComplete?.Invoke();
    }
}
