using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PolishBadShift
{
    public static void FinishAndBuild()
    {
        ModelImporter importer=(ModelImporter)AssetImporter.GetAtPath("Assets/ThirdParty/Skeletons/Skeleton_Minion.fbx");
        ModelImporterClipAnimation[] clips=importer.defaultClipAnimations;
        foreach(ModelImporterClipAnimation clip in clips) if(clip.name=="Idle" || clip.name=="Walking_A" || clip.name=="Cheer")clip.loopTime=true;
        importer.clipAnimations=clips;importer.SaveAndReimport();
        BuildAudioPlayground.BuildPlayer();
    }
    public static void Run()
    {
        try
        {
            if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated fixture required");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            BadShiftDemo d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
            d.Game.transform.Find("Player body").GetComponent<Renderer>().enabled=false;
            d.Game.transform.Find("Skeleton_Minion").localPosition = Vector3.zero;
            GameObject wall=GameObject.Find("North wall");wall.transform.position=new Vector3(0,1.5f,10);wall.transform.localScale=new Vector3(24,3,.4f);
            PlayerSettings.productName="BAD SHIFT";
            string model="Assets/ThirdParty/Skeletons/Skeleton_Minion.fbx";
            ModelImporter importer=(ModelImporter)AssetImporter.GetAtPath(model);importer.animationType=ModelImporterAnimationType.Generic;importer.importAnimation=true;importer.SaveAndReimport();
            AnimationClip[] clips=AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__")).ToArray();
            Debug.Log("Skeleton clips: "+string.Join(",",clips.Select(c=>c.name)));
            AnimationClip idle=clips.First(c=>c.name=="Idle"), walk=clips.First(c=>c.name=="Walking_A"), cheer=clips.First(c=>c.name=="Cheer");
            AnimatorController controller=AnimatorController.CreateAnimatorControllerAtPath("Assets/BadShift/Staff.controller");controller.AddParameter("Speed",AnimatorControllerParameterType.Float);
            AnimatorStateMachine sm=controller.layers[0].stateMachine;AnimatorState rest=sm.AddState("Idle");rest.motion=idle;AnimatorState moving=sm.AddState("Walk");moving.motion=walk;sm.defaultState=rest;
            AnimatorStateTransition go=rest.AddTransition(moving);go.hasExitTime=false;go.duration=.12f;go.AddCondition(AnimatorConditionMode.Greater,.1f,"Speed");AnimatorStateTransition stop=moving.AddTransition(rest);stop.hasExitTime=false;stop.duration=.12f;stop.AddCondition(AnimatorConditionMode.Less,.1f,"Speed");
            Animator player=d.Game.GetComponentsInChildren<Animator>().FirstOrDefault();if(player==null)player=d.Game.transform.Find("Skeleton_Minion").gameObject.AddComponent<Animator>();player.runtimeAnimatorController=controller;player.applyRootMotion=false;
            Animator dancer=d.Dancer.GetComponent<Animator>();if(dancer==null)dancer=d.Dancer.gameObject.AddComponent<Animator>();
            AnimatorController dance=AnimatorController.CreateAnimatorControllerAtPath("Assets/BadShift/Dance.controller");AnimatorState pose=dance.layers[0].stateMachine.AddState("Cheer");pose.motion=cheer;dance.layers[0].stateMachine.defaultState=pose;dancer.runtimeAnimatorController=dance;dancer.applyRootMotion=false;
            d.PlayerAnimator=player;d.DanceAnimator=dancer;
            PrefabUtility.RecordPrefabInstancePropertyModifications(player);PrefabUtility.RecordPrefabInstancePropertyModifications(dancer);PrefabUtility.RecordPrefabInstancePropertyModifications(d.Game.transform.Find("Skeleton_Minion"));
            EditorUtility.SetDirty(d);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);Debug.Log("BAD SHIFT animation polish saved");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
