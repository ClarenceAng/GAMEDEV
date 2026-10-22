using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using StarterAssets;

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

        textStyle = new GUIStyle();
        textStyle.fontSize = 24;
        textStyle.fontStyle = FontStyle.Bold;
        textStyle.normal.textColor = Color.white;

        messageStyle = new GUIStyle(textStyle);
        messageStyle.fontSize = 36;
        messageStyle.alignment = TextAnchor.MiddleCenter;

        controlsStyle = new GUIStyle(textStyle);
        controlsStyle.alignment = TextAnchor.UpperRight;
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            RestartLevel();
        }

        if (doorOpen && door.position.y < doorStartY + doorOpenHeight)
        {
            door.position = door.position + Vector3.up * doorSpeed * Time.deltaTime;
        }

        if (gameOver == false && player.position.z > goal.position.z)
        {
            gameOver = true;
            ShowMessage("You escaped!", 5f);

            // start again in 5 seconds
            Invoke("RestartLevel", 5f);
        }
    }

    public void ButtonPressed()
    {
        buttonsPressed = buttonsPressed + 1;

        if (buttonsPressed == buttonsNeeded)
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
        // no more hits after the game is over, or right after you got hit
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

        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;

        GUI.Label(new Rect(20, 16, 400, 36), "HP: " + health, textStyle);
        GUI.Label(new Rect(20, 50, 400, 36), "Buttons: " + buttonsPressed + " / " + buttonsNeeded, textStyle);

        // controls in the top right
        GUI.Label(new Rect(width - 420, 16, 400, 36), "F = flashlight", controlsStyle);
        GUI.Label(new Rect(width - 420, 50, 400, 36), "E = press button", controlsStyle);
        GUI.Label(new Rect(width - 420, 84, 400, 36), "R = restart", controlsStyle);


        if (Time.time < messageEndTime)
        {
            GUI.Label(new Rect(0, 160, width, 60), message, messageStyle);
        }
    }
}
