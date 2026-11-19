using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using AudioMW;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildCatalogPlayer
{
    public static void Build()
    {
        try
        {
            if (!Application.isBatchMode || !File.Exists(".audiomw-test-project") || !File.Exists("addressables-packed-validation.json"))
                throw new InvalidOperationException("Build the verified packed catalog in an isolated fixture first.");
            BuildClient();
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    public static void BuildRemote()
    {
        try
        {
            if (!Application.isBatchMode || !File.Exists(".audiomw-test-project") || !File.Exists("addressables-packed-validation.json"))
                throw new InvalidOperationException("Build the local catalog in a separate generated project first.");
            string origin = Environment.GetEnvironmentVariable("AUDIOMW_CATALOG_ORIGIN");
            if (!Uri.TryCreate(origin,UriKind.Absolute,out var uri) || !uri.IsLoopback || uri.Scheme != "http")
                throw new InvalidOperationException("A loopback HTTP origin is required.");
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
            BuildClient();
            const string clipPath = "Assets/AudioMWRemoteCatalogProbe.wav";
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AudioSystem).Assembly);
            File.Copy(Path.Combine(package.resolvedPath,"Samples~/Demo/Audio/MUS_Bass.wav"),clipPath,true);
            AssetDatabase.ImportAsset(clipPath,ImportAssetOptions.ForceSynchronousImport);
            var group = settings.CreateGroup("Remote validation",false,false,false,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(settings,AddressableAssetSettings.kRemoteBuildPath);
            schema.LoadPath.SetVariableByName(settings,AddressableAssetSettings.kRemoteLoadPath);
            schema.UseAssetBundleCache = false;
            schema.RetryCount = 0;
            settings.profileSettings.SetValue(settings.activeProfileId,AddressableAssetSettings.kRemoteBuildPath,"ServerData");
            settings.profileSettings.SetValue(settings.activeProfileId,AddressableAssetSettings.kRemoteLoadPath,origin);
            settings.RemoteCatalogBuildPath.SetVariableByName(settings,AddressableAssetSettings.kRemoteBuildPath);
            settings.RemoteCatalogLoadPath.SetVariableByName(settings,AddressableAssetSettings.kRemoteLoadPath);
            settings.BuildRemoteCatalog = true;
            settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(clipPath),group).address = AudioMWCatalogPlayer.RemoteKey;
            AssetDatabase.SaveAssets();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult build);
            if (!string.IsNullOrEmpty(build.Error)) throw new InvalidOperationException(build.Error);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    private static void BuildClient()
    {
        EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("Packed audio validation").AddComponent<AudioMWCatalogPlayer>();
        const string scene = "Assets/CatalogValidation.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene);
        PlayerSettings.productName = "AudioMW Packed Catalog";
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[]{ scene }, target = BuildTarget.StandaloneOSX, locationPathName = "CatalogPlayer/AudioMW Packed Catalog.app" });
        Debug.Log("PACKED_PLAYER_BUILD " + result.summary.result + " errors=" + result.summary.totalErrors);
        if (result.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Native catalog Player build failed.");
    }
}
