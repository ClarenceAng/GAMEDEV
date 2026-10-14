using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class SnowballGame : MonoBehaviour
{
    public enum GameMode
    {
        Training,
        Showdown
    }

    public static SnowballGame instance;
    public string levelTitle = "Icicle Knockdown";
    public int totalTargets = 8;
    public GameMode gameMode = GameMode.Training;

    private int knockedDown;
    private GUIStyle titleStyle;
    private GUIStyle infoStyle;
    private GUIStyle centerStyle;
    private GUIStyle smallCenterStyle;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        GameObject player = GameObject.Find("player");
        if (player != null)
            playerHealth = player.GetComponent<PlayerHealth>();
    }

    public void TargetHit()
    {
        knockedDown = Mathf.Min(knockedDown + 1, totalTargets);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current.rKey.wasPressedThisFrame)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            SceneManager.LoadScene(0);
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            SceneManager.LoadScene(1);
    }

    private void SetupStyles()
    {
        if (titleStyle != null)
            return;

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.fontSize = 24;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = Color.white;

        infoStyle = new GUIStyle(GUI.skin.label);
        infoStyle.fontSize = 15;
        infoStyle.normal.textColor = Color.white;

        centerStyle = new GUIStyle(GUI.skin.label);
        centerStyle.alignment = TextAnchor.MiddleCenter;
        centerStyle.fontSize = 28;
        centerStyle.fontStyle = FontStyle.Bold;
        centerStyle.wordWrap = true;
        centerStyle.normal.textColor = Color.white;

        smallCenterStyle = new GUIStyle(centerStyle);
        smallCenterStyle.fontSize = 18;
        smallCenterStyle.fontStyle = FontStyle.Normal;
    }

    private void OnGUI()
    {
        SetupStyles();

        bool playerDead = playerHealth != null && playerHealth.IsDead;
        bool levelWon = knockedDown >= totalTargets;

        GUI.color = new Color(0.11f, 0.25f, 0.37f, 0.91f);
        GUI.Box(new Rect(12, 12, 420, gameMode == GameMode.Showdown ? 140 : 116), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(28, 20, 390, 34), "SNOWBALL SHOWDOWN", titleStyle);
        GUI.Label(new Rect(28, 55, 390, 24), levelTitle, infoStyle);

        if (gameMode == GameMode.Training)
        {
            GUI.Label(new Rect(28, 84, 390, 24), "Targets knocked down: " + knockedDown + " / " + totalTargets, infoStyle);
        }
        else
        {
            GUI.Label(new Rect(28, 84, 390, 24), "Enemy snowmen down: " + knockedDown + " / " + totalTargets, infoStyle);
            float hp = playerHealth != null ? playerHealth.CurrentHealth : 0f;
            GUI.Label(new Rect(28, 110, 80, 20), "HP", infoStyle);
            GUI.color = new Color(0.16f, 0.18f, 0.25f, 0.95f);
            GUI.Box(new Rect(68, 111, 220, 18), GUIContent.none);
            GUI.color = new Color(0.56f, 0.89f, 0.98f, 1f);
            GUI.Box(new Rect(68, 111, 220f * Mathf.Clamp01(hp / 100f), 18), GUIContent.none);
            GUI.color = Color.white;
            GUI.Label(new Rect(296, 108, 80, 24), Mathf.CeilToInt(hp).ToString(), infoStyle);
        }

        float bottom = Screen.height - 48;
        GUI.color = new Color(0.11f, 0.25f, 0.37f, 0.91f);
        GUI.Box(new Rect(12, bottom - 8, Mathf.Min(1000, Screen.width - 24), 43), GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(24, bottom, Screen.width - 40, 25),
            "WASD Move   |   Hold RMB Aim   |   LMB Snowball   |   Space Jump   |   1 Training   |   2 Showdown   |   R Restart", infoStyle);

        GUI.Label(new Rect(Screen.width / 2f - 9, Screen.height / 2f - 13, 18, 24), "+", smallCenterStyle);

        if (levelWon)
        {
            DrawCenterPanel("ALL TARGETS DOWN!",
                gameMode == GameMode.Training
                    ? "You cleared the training range. Press 2 for the real showdown or R to play again."
                    : "You won the showdown! Press R to replay or 1 to return to training.");
        }
        else if (playerDead)
        {
            DrawCenterPanel("YOU MELTED!",
                "The other snowmen won this round. Press R to reset the showdown.");
        }

        GUI.color = Color.white;
    }

    private void DrawCenterPanel(string heading, string subtext)
    {
        float width = Mathf.Min(680f, Screen.width - 60f);
        float height = 130f;
        Rect rect = new Rect(Screen.width * 0.5f - width * 0.5f, Screen.height * 0.5f - height * 0.5f, width, height);
        GUI.color = new Color(0.07f, 0.30f, 0.38f, 0.93f);
        GUI.Box(rect, GUIContent.none);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 18f, rect.y + 12f, rect.width - 36f, 42f), heading, centerStyle);
        GUI.Label(new Rect(rect.x + 20f, rect.y + 66f, rect.width - 40f, 36f), subtext, smallCenterStyle);
    }
}
