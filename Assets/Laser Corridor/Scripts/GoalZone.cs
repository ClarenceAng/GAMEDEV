using UnityEngine;

public class GoalZone : MonoBehaviour
{
    private LaserCorridorGameManager manager;

    public void Configure(LaserCorridorGameManager gameManager)
    {
        manager = gameManager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && manager != null)
            manager.PlayerReachedGoal();
    }
}
