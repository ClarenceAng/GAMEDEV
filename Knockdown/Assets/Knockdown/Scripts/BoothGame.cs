using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Keeps score. Knock all the cans down to win. Press R to play again.
public class BoothGame : MonoBehaviour
{
    public int balls = 15;
    public KnockdownTarget[] targets;
    public TMP_Text statusText;
    public TMP_Text messageText;
    public bool gameOver = false;

    float outOfBallsTimer = 0f;

    void Update()
    {
        // press R to restart
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        // count the cans that are down
        int cansDown = 0;
        for (int i = 0; i < targets.Length; i++)
        {
            if (targets[i].IsDown())
            {
                cansDown++;
            }
        }

        statusText.text = "Balls: " + balls + "    Cans: " + cansDown + "/" + targets.Length;

        if (gameOver)
        {
            return;
        }

        if (cansDown == targets.Length)
        {
            EndGame("YOU WIN!");
        }
        else if (balls == 0)
        {
            // wait 3 seconds so the last ball has time to hit something
            outOfBallsTimer = outOfBallsTimer + Time.deltaTime;
            if (outOfBallsTimer > 3f)
            {
                EndGame("OUT OF BALLS");
            }
        }
    }

    void EndGame(string title)
    {
        gameOver = true;
        messageText.text = title + "\n<size=40%>Press R to play again</size>";
    }
}
