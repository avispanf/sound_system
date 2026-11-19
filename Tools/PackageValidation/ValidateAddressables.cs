using System;
using System.IO;
using System.Linq;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine.ResourceManagement.ResourceProviders;
using AudioMW;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

public static class ValidateAddressables
{
    [Serializable]
    private sealed class Report
    {
        public bool success;
        public string unity;
        public string addressables;
        public bool actualAssetLoadTested;
    }

    public static void Run()
    {
        AddressableAudioAssetLoader loader = new AddressableAudioAssetLoader();
        if (loader.CanLoad("") || !loader.CanLoad("probe") || loader.GetType().Assembly.GetName().Name != "AudioMW.Addressables")
            throw new InvalidOperationException("Optional provider setup failed.");
        bool complete = false;
        loader.LoadAsync("", (key, clip) => complete = clip == null);
        if (!complete) throw new InvalidOperationException("Invalid-address completion failed.");
        loader.ReleaseAll();
        Report report = new Report
        {
            success = true,
            unity = Application.unityVersion,
            addressables = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Addressables).Assembly).version,
            actualAssetLoadTested = false
        };
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "addressables-compile-validation.json"), JsonUtility.ToJson(report, true));
        EditorApplication.Exit(0);
    }
    private const string Address = "audiomw-validation/kitchen-bass";
    private const string SessionKey = "AudioMW.Validation.PackedCatalog";
    private const string ProbePath = "Assets/AudioMWCatalogProbe.wav";
    private static string ReportPath => Path.Combine(Path.GetDirectoryName(Application.dataPath), "addressables-packed-validation.json");

    [Serializable]
    private sealed class PackedReport
    {
        public string unity;
        public string addressables;
        public string provider;
        public int samples;
        public int channels;
        public int sampleRate;
        public bool sharedClip;
        public bool released;
        public bool reloaded;
    }

    public static void RunPacked()
    {
        try
        {
            if (!Application.isBatchMode || !File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".audiomw-test-project")))
                throw new InvalidOperationException("Use a generated batch-mode test project for the packed catalog.");
            File.Delete(ReportPath);
            EditorUtility.audioMasterMute = true;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AudioSystem).Assembly);
            File.Copy(Path.Combine(package.resolvedPath, "Samples~/Demo/Audio/MUS_Bass.wav"), ProbePath, true);
            AssetDatabase.ImportAsset(ProbePath, ImportAssetOptions.ForceSynchronousImport);
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(ProbePath), settings.DefaultGroup);
            entry.address = Address;
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult build);
            if (!string.IsNullOrEmpty(build.Error)) throw new InvalidOperationException(build.Error);
            int packed = settings.DataBuilders.FindIndex(builder => builder is BuildScriptPackedPlayMode);
            if (packed < 0) throw new InvalidOperationException("Use Existing Build is unavailable.");
            settings.ActivePlayModeDataBuilderIndex = packed;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            SessionState.SetBool(SessionKey, true);
            RegisterPackedRun();
            EditorApplication.isPlaying = true;
        }
        catch (Exception error) { Fail(error); }
    }

    [InitializeOnLoadMethod]
    private static void RegisterPackedRun()
    {
        if (!SessionState.GetBool(SessionKey, false)) return;
        EditorApplication.update -= StartWhenPlaying;
        EditorApplication.update += StartWhenPlaying;
    }

    private static void StartWhenPlaying()
    {
        if (!Application.isPlaying) return;
        EditorApplication.update -= StartWhenPlaying;
        ReadPackedClip();
    }

    private static void ReadPackedClip()
    {
        try
        {
            AudioListener.volume = 0;
            if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null)
                new GameObject("Muted catalog validation listener").AddComponent<AudioListener>();
            var locations = Addressables.LoadResourceLocationsAsync(Address, typeof(AudioClip));
            string provider;
            try
            {
                var location = locations.WaitForCompletion().Single();
                provider = location.ProviderId;
                if (provider != typeof(BundledAssetProvider).FullName || !location.HasDependencies)
                    throw new InvalidOperationException("The clip did not resolve from a bundle: " + provider);
            }
            finally { Addressables.Release(locations); }
            var loader = new AddressableAudioAssetLoader();
            int calls = 0;
            AudioClip first = null, second = null, reloaded = null;
            loader.LoadAsync(Address, (key, value) => { first = value; calls++; });
            loader.LoadAsync(Address, (key, value) => { second = value; calls++; });
            double deadline = EditorApplication.timeSinceStartup + 30;
            var report = new PackedReport { unity = Application.unityVersion, provider = provider,
                addressables = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Addressables).Assembly).version };
            void Poll()
            {
                try
                {
                    if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Packed clip load timed out.");
                    if (calls < 2) return;
                    if (!report.released)
                    {
                        if (first == null || first != second || first.samples <= 0 || loader.RetainedAddressCount != 1)
                            throw new InvalidOperationException("Two requests must return the same loaded clip.");
                        report.sharedClip = true;
                        report.samples = first.samples;
                        report.channels = first.channels;
                        report.sampleRate = first.frequency;
                        loader.Release(Address);
                        if (loader.RetainedAddressCount != 1) throw new InvalidOperationException("First release removed an active entry.");
                        loader.Release(Address);
                        if (loader.RetainedAddressCount != 0) throw new InvalidOperationException("Last release retained the address.");
                        report.released = true;
                        loader.LoadAsync(Address, (key, value) => { reloaded = value; calls++; });
                        return;
                    }
                    if (calls < 3) return;
                    if (reloaded == null || reloaded.samples != report.samples || reloaded.frequency != report.sampleRate || reloaded.channels != report.channels)
                        throw new InvalidOperationException("Reloaded clip metadata differs.");
                    loader.ReleaseAll();
                    report.reloaded = true;
                    File.WriteAllText(ReportPath, JsonUtility.ToJson(report, true));
                    EditorApplication.update -= Poll;
                    SessionState.EraseBool(SessionKey);
                    EditorApplication.Exit(0);
                }
                catch (Exception error)
                {
                    EditorApplication.update -= Poll;
                    loader.ReleaseAll();
                    Fail(error);
                }
            }
            EditorApplication.update += Poll;
        }
        catch (Exception error) { Fail(error); }
    }

    private static void Fail(Exception error)
    {
        SessionState.EraseBool(SessionKey);
        Debug.LogException(error);
        EditorApplication.Exit(1);
    }

}
