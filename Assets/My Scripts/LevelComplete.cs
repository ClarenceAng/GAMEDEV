using UnityEngine;

public class LevelComplete : MonoBehaviour
{
    public GameObject completeText;

    private bool levelCompleted = false;

    private void OnTriggerEnter(Collider other)
    {
        if (levelCompleted)
            return;

        if (other.CompareTag("Player"))
        {
            levelCompleted = true;

            Debug.Log("LEVEL COMPLETE!");

            if (completeText != null)
            {
                completeText.SetActive(true);
            }
        }
    }
}