using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ClayPerformancePass
{
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        foreach(var actor in new[]{demo.PlayerAnimator,demo.DanceAnimator})
        {
            if(actor.GetComponent<BadShiftClayArms>()!=null)throw new Exception("Preserve authored arm performance");
            var arms=actor.gameObject.AddComponent<BadShiftClayArms>();arms.Demo=demo;arms.Employee=actor==demo.DanceAnimator;
        }
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);EditorApplication.Exit(0);
    }
}
