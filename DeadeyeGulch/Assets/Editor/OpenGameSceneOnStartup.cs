// When Unity opens this project it normally restores whatever scene was open last, which for a fresh project is the
// empty template "SampleScene", so pressing Play does nothing. This opens the Fence Line scene instead, once per Editor
// session, but only when that is safe:
//   - the only open scene is the untitled one or the template SampleScene,
//   - it has no unsaved changes,
//   - and we are not playing / compiling / importing.
// A scene you opened on purpose is never replaced. Batch-mode runs (tests, CI) are left alone.

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class OpenGameSceneOnStartup
{
    const string GameScene = DeadeyeSceneBuilder.FenceLinePath;
    const string TemplateScene = "Assets/Scenes/SampleScene.unity";
    const string DoneKey = "DeadeyeGulch.StartupSceneHandled";
    const int SettleFrames = 30; // let Unity finish restoring its own last-open scene first

    static int frames;

    static OpenGameSceneOnStartup()
    {
        if (Application.isBatchMode || SessionState.GetBool(DoneKey, false)) return;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        bool busy = EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;
        if (busy) { frames = 0; return; }
        if (++frames < SettleFrames) return;

        EditorApplication.update -= Tick;
        SessionState.SetBool(DoneKey, true);
        OpenIfNeeded();
    }

    /// <summary>Opens the game scene if the editor is sitting on an empty/template scene. Returns true if it did.</summary>
    public static bool OpenIfNeeded()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) == null) return false;
        if (SceneManager.sceneCount != 1) return false;

        Scene open = SceneManager.GetActiveScene();
        bool onDefault = string.IsNullOrEmpty(open.path) || open.path == TemplateScene;
        if (!onDefault || open.isDirty) return false;

        EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
        return true;
    }
}
