using UnityEngine;

public class GameHUD : MonoBehaviour
{
    public PlayerHealth health;

    string message;
    float messageEndTime;
    GUIStyle healthStyle;
    GUIStyle messageStyle;

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

        if (healthStyle == null)
        {
            healthStyle = new GUIStyle(GUI.skin.label);
            healthStyle.fontSize = 24;
            healthStyle.fontStyle = FontStyle.Bold;
            healthStyle.normal.textColor = Color.black;

            messageStyle = new GUIStyle(GUI.skin.label);
            messageStyle.fontSize = 36;
            messageStyle.fontStyle = FontStyle.Bold;
            messageStyle.alignment = TextAnchor.MiddleCenter;
        }

        // health in the top left
        if (health != null)
        {
            GUI.Label(new Rect(20, 16, 300, 36), "HP: " + health.currentHealth, healthStyle);
        }

        // died / reached the end message
        if (Time.time < messageEndTime)
        {
            GUI.Label(new Rect(0, 120, width, 60), message, messageStyle);
        }
    }
}
