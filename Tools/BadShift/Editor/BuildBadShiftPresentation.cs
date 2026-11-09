using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class BuildBadShiftPresentation
{
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated project required");
        SerializedObject settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);settings.FindProperty("activeInputHandler").intValue=2;settings.ApplyModifiedPropertiesWithoutUndo();
        PlayerSettings.resizableWindow=true;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        BadShiftTour tour=demo.GetComponent<BadShiftTour>();if(tour==null)tour=demo.gameObject.AddComponent<BadShiftTour>();tour.Game=demo.Game;tour.Demo=demo;demo.Game.Tour=tour;
        demo.Game.View.backgroundColor=new Color(.17f,.22f,.28f);
        foreach(Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional)light.intensity=.95f;
        EditorUtility.SetDirty(demo.Game);EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
