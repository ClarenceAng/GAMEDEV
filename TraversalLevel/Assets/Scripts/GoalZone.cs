using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GoalZone : MonoBehaviour
{
    [SerializeField]
    GameObject levelCompleteText;

    bool levelComplete;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !levelComplete)
        {
            levelComplete = true;
            levelCompleteText.SetActive(true);

            other.GetComponent<PlayerMovement>().enabled = false;
        }
    }

    void Update()
    {
        // press R to play again
        if (levelComplete && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
