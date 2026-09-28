using UnityEngine;


public class CheckpointTrigger : MonoBehaviour
{
    [SerializeField]
    Transform respawnAnchor; 

    void Reset()
    {
        respawnAnchor = transform;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        FallRespawn respawn = other.GetComponent<FallRespawn>();
        if (respawn == null)
        {
            return;
        }

        Vector3 point = respawnAnchor != null ? respawnAnchor.position : transform.position;
        respawn.SetRespawnPoint(point);
    }
}