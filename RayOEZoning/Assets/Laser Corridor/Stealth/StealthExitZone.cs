using UnityEngine;

// The finish line. The player needs 3 intel files and the access key to win.
// Keeping this in its own file lets Unity serialize the scene component correctly.
public class StealthExitZone : MonoBehaviour
{
    private StealthCorridorDirector director;

    public void Initialize(StealthCorridorDirector owner)
    {
        director = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (director != null && other.CompareTag("Player"))
            director.ReachedExit();
    }
}
