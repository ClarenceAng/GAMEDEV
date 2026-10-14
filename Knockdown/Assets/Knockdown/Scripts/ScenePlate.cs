using UnityEngine;
using UnityEngine.SceneManagement;

// A plate on the ground. Step on it to switch to its version of the game.
public class ScenePlate : MonoBehaviour
{
    public string scene;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && SceneManager.GetActiveScene().name != scene)
        {
            SceneManager.LoadScene(scene);
        }
    }
}
