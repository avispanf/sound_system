using System;
using System.IO;
using System.Linq;
using AudioMW;
using AudioMW.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidateMixSetup
{
    private static void Guard()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".audiomw-playground"))) throw new InvalidOperationException("Use the generated Relay Field project.");
    }
    private static AudioMixRequest Request()
    {
        Guard();
        AudioMWPlayground game = UnityEngine.Object.FindFirstObjectByType<AudioMWPlayground>();
        if (game == null) throw new InvalidOperationException("Open the saved Relay Field scene.");
        var assets = new[] { "t:SoundEvent", "t:MusicTrack", "t:VoiceLine" }.SelectMany(filter => AssetDatabase.FindAssets(filter, new[] { "Assets/Playground/Data" })).Distinct().Select(guid => AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid)));
        return new AudioMixRequest
        {
            Setup = AssetDatabase.LoadAssetAtPath<AudioProjectSetup>("Assets/RelayFieldSetup.asset"), Folder = "Assets/Playground/Data", Name = "RelayMix", SceneTarget = game.gameObject, DuckParameter = game.VoiceDuck,
            Routes = assets.Select(asset => new AudioRouteChoice { Asset = asset, Bus = asset is MusicTrack ? AudioMixBus.Music : asset is VoiceLine ? AudioMixBus.VO : asset.name.StartsWith("Ambience_") ? AudioMixBus.Ambience : AudioMixBus.SFX, RemoveSourceDuck = asset is SoundEvent && asset.name.StartsWith("Ambience_") }).ToArray()
        };
    }
    public static void Run()
    {
        try
        {
            Guard(); EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            AudioMixRequest request = Request(); AudioMixPlan plan = AudioMixPlanner.Prepare(request);
            string[] files = Directory.GetFiles("Assets/Playground", "*", SearchOption.AllDirectories).OrderBy(path => path).ToArray(); byte[][] before = files.Select(File.ReadAllBytes).ToArray();
            AudioMixSetupWindow window = ScriptableObject.CreateInstance<AudioMixSetupWindow>(); window.LoadDraft(request); window.Prepare(); window.Reject(); UnityEngine.Object.DestroyImmediate(window);
            if (!files.SequenceEqual(Directory.GetFiles("Assets/Playground", "*", SearchOption.AllDirectories).OrderBy(path => path))) throw new Exception("Read/reject changed files.");
            for (int i = 0; i < files.Length; i++) if (!before[i].SequenceEqual(File.ReadAllBytes(files[i]))) throw new Exception("Read/reject wrote " + files[i]);
            AudioMixInstallation receipt = AudioMixPlanner.Apply(plan, request);
            if (!AudioMixPlanner.Prepare(request).AlreadyApplied) throw new Exception("Repeat is not idempotent.");
            foreach (AudioRouteChoice route in request.Routes) if (AudioMixPlanner.Output(route.Asset) == null) throw new Exception("Missing route: " + route.Asset.name);
            string reports = Path.Combine(Path.GetDirectoryName(Application.dataPath), "TestReports"); Directory.CreateDirectory(reports);
            File.WriteAllText(Path.Combine(reports, "mix-installation.json"), JsonUtility.ToJson(new Report { readAndRejectUnchanged = true, applied = true, repeatNoOp = true, routes = request.Routes.Length, receipt = AssetDatabase.GetAssetPath(receipt) }, true));
            Debug.Log("Relay Field mix setup verified."); EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
    [Serializable] private sealed class Report { public bool readAndRejectUnchanged; public bool applied; public bool repeatNoOp; public int routes; public string receipt; }
    public static void OpenReview()
    {
        Guard(); EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity"); AudioMixSetupWindow.Open();
        AudioMixSetupWindow window = EditorWindow.GetWindow<AudioMixSetupWindow>(); window.LoadDraft(Request()); window.position = new Rect(90, 90, 640, 850); window.Prepare();
    }
}
