using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildPlayerSmoke
{
    public static void Run()
    {
        string project = Path.GetDirectoryName(Application.dataPath);
        try
        {
            if (!File.Exists(Path.Combine(project, ".audiomw-test-project"))) throw new Exception("Player smoke builds require a generated isolated fixture.");
            ValidateDistribution.Validate();
            string[] args = Environment.GetCommandLineArgs();
            BuildTarget target = BuildTarget.StandaloneOSX;
            string backend = "Mono";
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--audiomw-backend") backend = args[i + 1];
            if (backend != "Mono" && backend != "IL2CPP") throw new Exception("Backend must be Mono or IL2CPP.");
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--audiomw-target") target = (BuildTarget)Enum.Parse(typeof(BuildTarget), args[i + 1]);
            if (target != BuildTarget.StandaloneOSX && target != BuildTarget.StandaloneLinux64 && target != BuildTarget.StandaloneWindows64) throw new Exception("Only desktop smoke targets are supported.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, target)) throw new Exception("Install the requested desktop build support module: " + target);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            const string folder = "Assets/AudioMWPlayerValidation/Generated";
            Directory.CreateDirectory(folder); AssetDatabase.Refresh();
            string clipPath = AssetDatabase.GUIDToAssetPath(AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Samples" }).First());
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            SoundEvent simple = SoundEvent.CreateRuntime(clip); simple.Loop = true; simple.SpatialBlend = 0; simple.Volume = 0;
            SoundEvent layered = SoundEvent.CreateRuntime(); layered.ContainerMode = ContainerMode.Layered;
            layered.Children = new[] { SoundEventChild.Create(simple), SoundEventChild.Create(simple) };
            SoundParameter state = SoundParameter.CreateRuntime(0, 5, 1);
            SoundEvent switched = SoundEvent.CreateRuntime(); switched.ContainerMode = ContainerMode.Switch; switched.SwitchParameter = state;
            switched.Children = new[] { SoundEventChild.Create(simple, 1) };
            MusicTrack musicA = MusicTrack.CreateRuntime(null, clip, 120, 4); musicA.Volume = 0;
            MusicTrack musicB = MusicTrack.CreateRuntime(null, clip, 120, 4); musicB.Volume = 0;
            UnityEngine.Object[] assets = { simple, layered, state, switched, musicA, musicB };
            string[] names = { "Simple", "Layered", "State", "Switch", "MusicA", "MusicB" };
            for (int i = 0; i < assets.Length; i++)
            {
                string assetPath = folder + "/" + names[i] + ".asset";
                if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null) AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.CreateAsset(assets[i], assetPath);
            }
            AudioMWPlayerSmoke smoke = new GameObject("AudioMW Player Smoke").AddComponent<AudioMWPlayerSmoke>();
            smoke.Simple = simple; smoke.Layered = layered; smoke.Switch = switched; smoke.State = state; smoke.MusicA = musicA; smoke.MusicB = musicB;
            const string scene = folder + "/PlayerSmoke.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene); AssetDatabase.SaveAssets();
            PlayerSettings.productName = "AudioMWSmoke";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, backend == "IL2CPP" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
            PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, backend == "IL2CPP" ? ManagedStrippingLevel.High : ManagedStrippingLevel.Low);
            string extension = target == BuildTarget.StandaloneOSX ? ".app" : target == BuildTarget.StandaloneWindows64 ? ".exe" : "";
            string output = Path.Combine(project, "PlayerSmoke-" + backend, "AudioMWSmoke" + extension);
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene }, target = target, locationPathName = output, options = BuildOptions.None });
            bool success = report.summary.result == BuildResult.Succeeded;
            File.WriteAllText(Path.Combine(project, "player-build-" + backend + ".json"), JsonUtility.ToJson(new Result { success = success, unity = Application.unityVersion, target = target.ToString(), backend = backend, stripping = backend == "IL2CPP" ? "High" : "Low", player = output, errors = report.summary.totalErrors }, true));
            EditorApplication.Exit(success ? 0 : 1);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [Serializable] private sealed class Result
    {
        public bool success;
        public string unity;
        public string target;
        public string backend;
        public string stripping;
        public string player;
        public int errors;
    }
}
