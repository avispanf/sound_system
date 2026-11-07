using System;
using System.IO;
using System.Linq;
using AudioMW.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidateProjectSetup
{
    private static string Project => Path.GetDirectoryName(Application.dataPath);
    private static AudioProjectBrief Brief() => new AudioProjectBrief
    {
        Game = "Relay Field",
        Genre = "3D collection game: movement, pickups and danger-zone music",
        Audience = "Indie developers and playtesters",
        Platforms = "macOS desktop; additional platforms unverified",
        Perspective = "Third-person overview",
        ListeningDevices = "Headphones or desktop speakers; user listening pass pending",
        MixIntent = "Clear dialogue cues over adaptive music and ambience",
        ContentNotes = "Synthetic demo placeholders; replace before sound-design sign-off",
        SourceFolder = "Assets/Playground",
        ProfilePath = "Assets/RelayFieldSetup.asset"
    };

    private static void Guard()
    {
        if (!File.Exists(Path.Combine(Project, ".audiomw-playground"))) throw new InvalidOperationException("Use the generated Relay Field project.");
    }
    public static void Run()
    {
        try
        {
            Guard(); AudioProjectBrief brief = Brief();
            if (File.Exists(brief.ProfilePath)) throw new InvalidOperationException("Existing setup profile is preserved. Use a fresh validation destination.");
            string[] paths = Directory.GetFiles("Assets/Playground", "*", SearchOption.AllDirectories).OrderBy(value => value).ToArray();
            byte[][] before = paths.Select(File.ReadAllBytes).ToArray();
            AudioProjectPlan plan = AudioProjectPlanner.Analyse(brief);
            if (File.Exists(brief.ProfilePath)) throw new Exception("Analysis wrote the destination.");
            AudioProjectSetupWindow window = ScriptableObject.CreateInstance<AudioProjectSetupWindow>();
            window.LoadDraft(brief); window.Analyse(); window.Reject(); UnityEngine.Object.DestroyImmediate(window);
            if (File.Exists(brief.ProfilePath)) throw new Exception("Reject wrote the destination.");
            brief.Audience = "Changed";
            if (AudioProjectPlanner.Check(plan, brief).Length == 0) throw new Exception("Changed brief was accepted.");
            brief = Brief();
            AudioProjectSetup saved = AudioProjectPlanner.SaveReviewedPlan(plan, brief);
            for (int i = 0; i < paths.Length; i++) if (!before[i].SequenceEqual(File.ReadAllBytes(paths[i]))) throw new Exception("Existing game content changed: " + paths[i]);
            if (!paths.SequenceEqual(Directory.GetFiles("Assets/Playground", "*", SearchOption.AllDirectories).OrderBy(value => value))) throw new Exception("Existing game content set changed.");
            if (saved.Revision != 1 || saved.Brief.Game != "Relay Field") throw new Exception("Saved profile did not round-trip.");
            string reports = Path.Combine(Project, "TestReports"); Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "project-setup-review.json"), plan.Json);
            File.WriteAllText(Path.Combine(reports, "project-setup-validation.json"), "{\"analysisReadOnly\":true,\"rejectionReadOnly\":true,\"changedBriefRejected\":true,\"profileSaved\":true,\"gameContentUnchanged\":true}");
            Debug.Log("Relay Field project setup checks passed. Profile saved; game content unchanged.");
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    public static void OpenReview()
    {
        Guard(); EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        AudioProjectSetupWindow.Open();
        AudioProjectSetupWindow window = EditorWindow.GetWindow<AudioProjectSetupWindow>();
        AudioProjectSetup profile = AssetDatabase.LoadAssetAtPath<AudioProjectSetup>(Brief().ProfilePath);
        window.LoadDraft(profile != null ? profile.Brief : Brief());
        window.position = new Rect(90, 90, 620, 850);
    }
}
