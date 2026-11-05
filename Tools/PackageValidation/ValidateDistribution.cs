using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ValidateDistribution
{
    public static void Run()
    {
        try
        {
            UnityEditor.PackageManager.PackageInfo package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AudioSystem).Assembly);
            if (package == null || package.name != "com.avispanf.audiomw") throw new Exception("AudioMW is not installed as a UPM package.");
            Sample[] samples = Sample.FindByPackage(package.name, package.version).ToArray();
            if (samples.Length != 1) throw new Exception("Expected one declared demo sample.");
            if (!samples[0].Import(Sample.ImportOptions.OverridePreviousImports)) throw new Exception("Package Manager sample import failed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string scenePath = Path.Combine(samples[0].importPath, "AudioMW Demo.unity").Replace('\\', '/');
            string projectRoot = Path.GetDirectoryName(Application.dataPath).Replace('\\', '/');
            if (Path.IsPathRooted(scenePath)) scenePath = scenePath.Substring(projectRoot.Length + 1);
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            int components = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0) throw new Exception("Missing script: " + child.name);
                    foreach (Component component in child.GetComponents<Component>())
                    {
                        if (component == null) throw new Exception("Missing component: " + child.name);
                        CheckReferences(component);
                        components++;
                    }
                }
            }
            if (!scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<Camera>() != null)) throw new Exception("Demo has no camera.");
            if (!scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<AudioListener>() != null)) throw new Exception("Demo has no listener.");
            string[] dependencies = AssetDatabase.GetDependencies(scenePath, true);
            foreach (string dependency in dependencies)
            {
                if (dependency.EndsWith(".asset") || dependency.EndsWith(".mat"))
                {
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(dependency);
                    if (asset == null) throw new Exception("Cannot load " + dependency);
                    CheckReferences(asset);
                }
                if (dependency.EndsWith(".shader"))
                {
                    Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(dependency);
                    if (shader == null || ShaderUtil.ShaderHasError(shader)) throw new Exception("Shader import failed: " + dependency);
                }
            }
            string stylesheet = package.resolvedPath + "/Editor/UI/AudioMWStyles.uss";
            if (!File.Exists(stylesheet)) throw new Exception("Browser stylesheet was not shipped.");
            string report = JsonUtility.ToJson(new Report { version = package.version, source = package.source.ToString(), scene = scenePath, components = components, dependencies = dependencies.Length, success = true }, true);
            File.WriteAllText(Path.Combine(projectRoot, "distribution-validation.json"), report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void CheckReferences(UnityEngine.Object target)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                throw new Exception("Missing reference: " + target.name + "." + property.propertyPath);
    }

    [Serializable]
    private sealed class Report
    {
        public bool success;
        public string version;
        public string source;
        public string scene;
        public int components;
        public int dependencies;
    }
}
