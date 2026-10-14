using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor only. When Unity starts on an empty scene, this opens the static booth
// so pressing Play shows the game.
[InitializeOnLoad]
public class OpenBoothScene
{
    static OpenBoothScene()
    {
        // this runs every time the scripts compile, so only do it once when Unity starts
        if (Application.isBatchMode || SessionState.GetBool("BoothSceneOpened", false))
        {
            return;
        }
        SessionState.SetBool("BoothSceneOpened", true);
        EditorApplication.delayCall += OpenBooth;
    }

    static void OpenBooth()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path == "" && scene.isDirty == false)
        {
            EditorSceneManager.OpenScene("Assets/Knockdown/Scenes/StaticTargets.unity");
        }
    }
}
