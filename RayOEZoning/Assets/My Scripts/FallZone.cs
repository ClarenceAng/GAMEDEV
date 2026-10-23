using UnityEngine;

public class FallZone : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player fell!");

            PlayerMovement player =
                other.GetComponent<PlayerMovement>();

            if (player != null)
            {
                player.Respawn();
            }
        }
    }
}