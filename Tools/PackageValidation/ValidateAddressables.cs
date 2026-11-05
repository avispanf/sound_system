using System;
using System.IO;
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
}
