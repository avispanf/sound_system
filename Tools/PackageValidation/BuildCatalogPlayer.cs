using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
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
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            new GameObject("Packed audio validation").AddComponent<AudioMWCatalogPlayer>();
            const string scene = "Assets/CatalogValidation.unity";
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scene);
            PlayerSettings.productName = "AudioMW Packed Catalog";
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[]{ scene }, target = BuildTarget.StandaloneOSX, locationPathName = "CatalogPlayer/AudioMW Packed Catalog.app" });
            Debug.Log("PACKED_PLAYER_BUILD " + result.summary.result + " errors=" + result.summary.totalErrors);
            EditorApplication.Exit(result.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception ex) { Debug.LogException(ex); EditorApplication.Exit(1); }
    }
}
