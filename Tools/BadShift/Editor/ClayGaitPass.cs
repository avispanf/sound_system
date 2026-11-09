using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ClayGaitPass
{
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        foreach(var actor in new[]{d.PlayerAnimator,d.DanceAnimator})
        {
            if(actor.GetComponent<BadShiftClayGait>()!=null)throw new Exception("Gait already authored");
            var gait=actor.gameObject.AddComponent<BadShiftClayGait>();gait.Demo=d;gait.Employee=actor==d.DanceAnimator;if(!gait.Employee)d.Game.ClayGait=gait;
            foreach(var t in actor.GetComponentsInChildren<Transform>())
            {
                if(t.name=="Clay face pivot")t.localScale=Vector3.one*.9f;
                if(t.name=="Apron skirt"){t.localScale=new Vector3(.48f,.27f,.065f);t.localPosition=new Vector3(0,.15f,.18f);}
                if(t.name=="Apron bib")t.localScale=new Vector3(.44f,.43f,.065f);
                if(t.name=="Clay mitten")t.localScale=new Vector3(.15f,.18f,.12f);
            }
        }
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void LinkFootsteps()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();d.Game.ClayGait=d.PlayerAnimator.GetComponent<BadShiftClayGait>();EditorUtility.SetDirty(d.Game);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void RefineSilhouette()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        foreach(var face in UnityEngine.Object.FindObjectsByType<BadShiftClayFace>(FindObjectsSortMode.None))face.Head.localScale=Vector3.one*.9f;
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);EditorApplication.Exit(0);
    }
}
