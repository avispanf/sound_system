using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public sealed class BadShiftDemoLauncher : EditorWindow
{
    private const string Pending = "BadShift.WatchPending";
    private const string ScenePath = "Assets/Playground/RelayField.unity";
    static BadShiftDemoLauncher() { EditorApplication.update += StartPendingTour; }
    [MenuItem("Tools/AudioMW/BAD SHIFT Demo")]
    public static void Open() { GetWindow<BadShiftDemoLauncher>("BAD SHIFT Demo").minSize = new Vector2(340, 260); }
    public static void LaunchWatch() { Open(); Launch(true); }
    private static void Launch(bool watch)
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) return;
        if (EditorApplication.isPlaying)
        {
            BadShiftTour tour = Object.FindFirstObjectByType<BadShiftTour>();
            if (tour != null) { if (watch) tour.WatchFromStart(); else tour.Stop(); }
            return;
        }
        if (EditorSceneManager.GetActiveScene().path != ScenePath)
        {
            if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogWarning("Save the current scene before opening BAD SHIFT."); return; }
            EditorSceneManager.OpenScene(ScenePath);
        }
        SessionState.SetBool(Pending, watch);
        EditorApplication.isPlaying = true;
    }
    private static void StartPendingTour()
    {
        if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || EditorApplication.isPaused || Time.timeSinceLevelLoad < .5f) return;
        BadShiftTour tour = Object.FindFirstObjectByType<BadShiftTour>();
        if (tour == null) return;
        SessionState.SetBool(Pending, false); tour.Begin();
    }
    private void OnGUI()
    {
        GUILayout.Space(16);
        using (new EditorGUILayout.HorizontalScope())
        {
            GUILayout.Space(16);
            using (new EditorGUILayout.VerticalScope())
            {
                GUILayout.Label("BAD SHIFT / Audio playground", EditorStyles.boldLabel);
                GUILayout.Space(10);
                EditorGUILayout.HelpBox("Watch the guided demo in Game View, or control the player yourself. Both modes run real AudioMW events. The on-screen cards explain each audio mechanism; audio is placeholder content.", MessageType.Info);
                GUILayout.Space(12);
                using (new EditorGUI.DisabledScope(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))))
                {
                    if (GUILayout.Button("Watch demo in Game View", GUILayout.Height(36))) Launch(true);
                    GUILayout.Space(8);
                    if (GUILayout.Button("Play yourself", GUILayout.Height(36))) Launch(false);
                }
                GUILayout.Space(12);
                GUILayout.Label("Use Game View's Free Aspect to resize the preview.\nStop with Unity's Play button; switch modes in the game UI.", EditorStyles.wordWrappedLabel);
            }
            GUILayout.Space(16);
        }
    }
}
