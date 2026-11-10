using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FinalizeBadShift
{
    public static void Run()
    {
        if(!File.Exists(Path.Combine(Application.dataPath,"..",".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        var demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        if(GameObject.Find("Final cafe layout")==null)
        {
            var root=new GameObject("Final cafe layout").transform;
            var all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            var room=all.Where(t=>t.name.StartsWith("Stockroom")||t.name.StartsWith("Toilet")||t.name.StartsWith("Sink")||t.name.StartsWith("Faucet")||t.name.StartsWith("Bathroom mirror")||t.name=="Flush button").ToArray();
            var group=new GameObject("Central bathroom").transform;group.SetParent(root);group.position=demo.Room.position;
            foreach(var part in room.Where(t=>!room.Any(p=>p!=t&&t.IsChildOf(p))))part.SetParent(group,true);
            group.position+=new Vector3(3,0,-5);
            GameObject.Find("Pushable picnic assembly").transform.position=new Vector3(0,0,-5.8f);
            foreach(string name in new[]{"Pendant cable","Coral pendant shade","Exit floor label"}){var obj=GameObject.Find(name);if(obj!=null)obj.SetActive(false);}
            foreach(var renderer in demo.Game.Exit.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            var text=GameObject.Find("Cafe lettering").GetComponent<TextMesh>();var band=GameObject.Find("Cafe sign cream band").GetComponent<Renderer>().bounds;
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.transform.rotation=Quaternion.identity;
            text.characterSize*=Mathf.Min(1,(band.size.x-.44f)/text.GetComponent<Renderer>().bounds.size.x);
            var label=text.GetComponent<Renderer>().bounds;text.transform.position+=new Vector3(band.center.x-label.center.x,band.center.y-label.center.y,0);
            foreach(var side in new[]{new Vector3(-12,5,0),new Vector3(12,5,0),new Vector3(0,5,-10),new Vector3(0,5,10)})
            {
                var edge=new GameObject("Invisible level boundary");edge.transform.SetParent(root);edge.transform.position=side;
                edge.AddComponent<BoxCollider>().size=side.x!=0?new Vector3(.4f,12,20.4f):new Vector3(24.4f,12,.4f);
            }
        }
        var crate=GameObject.Find("Tip-over tomato crate").transform;
        Vector3 crateDelta=new Vector3(-2.8f,0,-4.8f)-crate.position;crate.position+=crateDelta;
        foreach(var tomato in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Loose crate tomato"))tomato.position+=crateDelta;
        if(demo.GetComponent<BadShiftAudioGuide>()==null)demo.gameObject.AddComponent<BadShiftAudioGuide>();
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();
        Debug.Log("BAD SHIFT final layout saved: central bathroom, invisible boundaries, inset sign, no pendant or exit marker.");EditorApplication.Exit(0);
    }
}
