using UnityEngine;

// A big invisible trigger under the level. Touching it means the player fell off the map.
public class FallZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponent<PlayerMovement>().Respawn();
        }
    }
}
