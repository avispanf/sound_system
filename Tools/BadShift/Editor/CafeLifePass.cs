using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CafeLifePass
{
    private static BadShiftUseTarget Target(GameObject item,BadShiftUseKind kind,string label,BadShiftDoor door=null)
    {
        var target=item.AddComponent<BadShiftUseTarget>();target.Kind=kind;target.Label=label;target.Door=door;return target;
    }
    private static GameObject Stick(Animator actor,Material material)
    {
        var hand=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="hand.r");var stick=new GameObject("Sparring breadstick");stick.transform.SetParent(hand,false);stick.transform.localPosition=new Vector3(0,.12f,0);stick.transform.localScale=new Vector3(.10f,1.1f,.10f);
        stick.AddComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/ClayActors/Sculpted.asset");stick.AddComponent<MeshRenderer>().sharedMaterial=material;stick.SetActive(false);return stick;
    }
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        if(demo.GetComponent<BadShiftCafeLife>()!=null)throw new Exception("Cafe life already authored");
        var life=demo.gameObject.AddComponent<BadShiftCafeLife>();life.Demo=demo;demo.Game.Life=life;
        var reactions=demo.gameObject.AddComponent<BadShiftCafeReactions>();reactions.Life=life;
        Material wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/BadShift/Reference/Honey oak.mat");
        life.PlayerStick=Stick(demo.PlayerAnimator,wood);life.EnemyStick=Stick(demo.DanceAnimator,wood);
        Target(demo.Microwave.gameObject,BadShiftUseKind.Microwave,"Beep microwave");
        Target(GameObject.Find("Manager lectern"),BadShiftUseKind.Boss,"Talk to broccoli boss");
        Target(demo.Game.Door.gameObject,BadShiftUseKind.Bathroom,"Open / close bathroom",demo.Game.Door);
        Target(demo.Fridge.gameObject,BadShiftUseKind.Fridge,"Open / close lower fridge",demo.Game.FridgeDoors.Lower);
        Target(demo.Dancer.gameObject,BadShiftUseKind.Spar,"Spar with breadsticks");
        Target(GameObject.Find("Sink basin"),BadShiftUseKind.Sink,"Tap sink / room sound");
        Target(GameObject.Find("Toilet cistern"),BadShiftUseKind.Toilet,"Tap cistern / room sound");
        foreach(var prop in UnityEngine.Object.FindObjectsByType<BadShiftImpactProp>(FindObjectsSortMode.None))Target(prop.gameObject,BadShiftUseKind.Prop,prop.Breakable?"Toss cup":prop.GetComponent<Rigidbody>().mass<1?"Toss object":"Push furniture");
        EditorUtility.SetDirty(demo.Game);EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
