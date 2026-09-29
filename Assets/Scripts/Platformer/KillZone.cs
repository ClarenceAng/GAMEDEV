using UnityEngine;

// Trigger volume placed below the level. Anything the player falls into
// sends them back to the start.
[RequireComponent(typeof(Collider))]
public class KillZone : MonoBehaviour
{
    void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.TryGetComponent(out PlayerController _))
        {
            LevelManager.Instance.PlayerFell();
        }
    }
}
