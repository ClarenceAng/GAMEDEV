using UnityEngine;


[RequireComponent(typeof(CharacterController))]
public class FallRespawn : MonoBehaviour
{
    [SerializeField]
    float fallThreshold = -10f;

    CharacterController controller;
    Vector3 respawnPoint;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        respawnPoint = transform.position; // starting position by default
    }

    void Update()
    {
        if (transform.position.y < fallThreshold)
        {
            Respawn();
        }
    }

    public void SetRespawnPoint(Vector3 point)
    {
        respawnPoint = point;
    }

    public void Respawn()
    {
        
        
        controller.enabled = false;
        transform.position = respawnPoint;
        controller.enabled = true;
    }
}
