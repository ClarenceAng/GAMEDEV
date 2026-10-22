using UnityEngine;
using UnityEngine.SceneManagement;
using StarterAssets;

// Counts the buttons, opens the door at the end and shows the text on the screen.
// Every hit takes away 20 HP. At 0 HP the level starts again.
public class StealthGame : MonoBehaviour
{
    public int maxHealth = 100;
    public int health = 100;
    public int damagePerHit = 20;
    public float invulnerableSeconds = 1f; // so one hit doesnt take away HP every frame
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
    float invulnerableUntil;

    string message = "";
    float messageEndTime;
    GUIStyle textStyle;
    GUIStyle messageStyle;
    GUIStyle controlsStyle;

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

        // the goal sits right behind the door, so stepping onto the green part means you made it
        if (gameOver == false && player.position.z > goal.position.z)
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

    public void PlayerHit(string what)
    {
        if (gameOver || Time.time < invulnerableUntil)
        {
            return;
        }

        health = health - damagePerHit;
        invulnerableUntil = Time.time + invulnerableSeconds;

        if (health <= 0)
        {
            health = 0;
            gameOver = true;
            ShowMessage("No HP left - back to the start!", 3f);

            // stop the player from moving and start again
            player.GetComponent<FirstPersonController>().enabled = false;
            Invoke("RestartLevel", 2.5f);
        }
        else
        {
            ShowMessage("You got hit by " + what + "!", 1.5f);
        }
    }

    // the +20 HP power up. gives back false if health is already full
    public bool Heal(int amount)
    {
        if (health >= maxHealth)
        {
            return false;
        }

        health = health + amount;
        if (health > maxHealth)
        {
            health = maxHealth;
        }
        return true;
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

            controlsStyle = new GUIStyle(textStyle);
            controlsStyle.alignment = TextAnchor.UpperRight;
        }

        GUI.Label(new Rect(20, 16, 400, 36), "HP: " + health, textStyle);
        GUI.Label(new Rect(20, 50, 400, 36), "Buttons: " + buttonsPressed + " / " + buttonsNeeded, textStyle);

        // controls in the top right
        GUI.Label(new Rect(width - 420, 16, 400, 36), "F = flashlight", controlsStyle);
        GUI.Label(new Rect(width - 420, 50, 400, 36), "E = press button", controlsStyle);

        // a little + in the middle so you can aim at the buttons
        GUI.Label(new Rect(width / 2f - 20f, 360f - 20f, 40f, 40f), "+", messageStyle);

        if (Time.time < messageEndTime)
        {
            GUI.Label(new Rect(0, 160, width, 60), message, messageStyle);
        }
    }
}
