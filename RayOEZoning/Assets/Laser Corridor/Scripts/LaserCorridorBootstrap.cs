using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// The playable environment is saved in the Unity scene, not generated at Play time.
// This component only connects the existing player, bots, lights, and pickups.
public class LaserCorridorBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void StartStealthGame()
    {
        if (SceneManager.GetActiveScene().name != "LaserCorridor") return;

        GameObject level = GameObject.Find("Stealth Corridor");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        Camera camera = Camera.main;
        if (level == null || player == null || camera == null)
        {
            Debug.LogError("Stealth Corridor: missing level, player, or camera. Open the LaserCorridor scene and use Tools > Stealth Corridor > Build Scene Objects (V6).");
            return;
        }

        camera.backgroundColor = new Color(.005f, .008f, .012f);
        camera.farClipPlane = 420f;
        camera.allowHDR = true;
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(.015f, .025f, .035f);
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 80f;
        RenderSettings.fogEndDistance = 330f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.035f, .055f, .075f);

        // Keep the original snowman renderers hidden for the FPS view.
        // The camera is still controlled by the original player game object.
        GameObject logic = new GameObject("Game Manager");
        StealthCorridorDirector director = logic.AddComponent<StealthCorridorDirector>();
        director.InitializeScene(player, camera, level.transform);
    }
}
