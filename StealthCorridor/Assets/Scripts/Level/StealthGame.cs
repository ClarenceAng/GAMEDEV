using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// Counts the buttons, opens the door at the end and shows the text on the screen.
// If an arrow hits you the level starts again.
public class StealthGame : MonoBehaviour
{
    public int buttonsNeeded = 5;
    public int buttonsPressed = 0;
    public Transform player;
    public Transform door;
    public Transform goal;
    public float doorSpeed = 1.5f;
    public float doorOpenHeight = 4f;

    bool doorOpen = false;
    bool gameOver = false;
    float doorStartY;

    string message = "";
    float messageEndTime;
    GUIStyle textStyle;
    GUIStyle messageStyle;

    void Start()
    {
        doorStartY = door.position.y;
    }

    void Update()
    {
        // slide the door up into the ceiling
        if (doorOpen && door.position.y < doorStartY + doorOpenHeight)
        {
            door.position = door.position + Vector3.up * doorSpeed * Time.deltaTime;
        }

        // made it past the door to the end
        if (gameOver == false && Vector3.Distance(player.position, goal.position) < 2f)
        {
            gameOver = true;
            ShowMessage("You escaped!", 5f);
            Invoke("RestartLevel", 5f);
        }
    }

    public void ButtonPressed()
    {
        buttonsPressed = buttonsPressed + 1;

        if (buttonsPressed >= buttonsNeeded)
        {
            doorOpen = true;
            ShowMessage("All the buttons are pressed - the door is open!", 3f);
        }
        else
        {
            ShowMessage("Button pressed!", 1.5f);
        }
    }

    public void PlayerCaught(string what)
    {
        if (gameOver)
        {
            return;
        }

        gameOver = true;
        ShowMessage("You got hit by " + what + "!", 3f);

        // stop the player from moving and start again
        player.GetComponent<FirstPersonController>().enabled = false;
        Invoke("RestartLevel", 2.5f);
    }

    void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void ShowMessage(string text, float seconds)
    {
        message = text;
        messageEndTime = Time.time + seconds;
    }

    void OnGUI()
    {
        // scale everything so it looks the same on any screen size
        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;

        if (textStyle == null)
        {
            textStyle = new GUIStyle(GUI.skin.label);
            textStyle.fontSize = 24;
            textStyle.fontStyle = FontStyle.Bold;
            textStyle.normal.textColor = Color.white;

            messageStyle = new GUIStyle(textStyle);
            messageStyle.fontSize = 36;
            messageStyle.alignment = TextAnchor.MiddleCenter;
        }

        GUI.Label(new Rect(20, 16, 400, 36), "Buttons: " + buttonsPressed + " / " + buttonsNeeded, textStyle);
        GUI.Label(new Rect(20, 50, 600, 36), "F = flashlight     E = press button", textStyle);

        // a little + in the middle so you can aim at the buttons
        GUI.Label(new Rect(width / 2f - 20f, 360f - 20f, 40f, 40f), "+", messageStyle);

        if (Time.time < messageEndTime)
        {
            GUI.Label(new Rect(0, 160, width, 60), message, messageStyle);
        }
    }
}
