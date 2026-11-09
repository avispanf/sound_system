using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ArrangeBadShift
{
    private static void Open()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
    }
    public static void Inspect()
    {
        Open();BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        var lines=new List<string>();
        foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if(!new[]{"Microwave","Kitchen","Stockroom","Rave","Pushable","Picnic","Coral cafe chair","Manager","Finished","Reference","Cafe lettering"}.Any(n=>t.name.Contains(n)))continue;
            Renderer r=t.GetComponent<Renderer>();lines.Add(t.name+" position "+t.position+" scale "+t.lossyScale+(r!=null?" bounds "+r.bounds:""));
        }
        foreach(Transform t in demo.Fridge.GetComponentsInChildren<Transform>())
        {
            MeshFilter f=t.GetComponent<MeshFilter>();lines.Add("FRIDGE "+t.name+" position "+t.position+" rotation "+t.eulerAngles+" scale "+t.lossyScale+(f!=null?" mesh "+f.sharedMesh.name+" vertices "+f.sharedMesh.vertexCount+" bounds "+f.GetComponent<Renderer>().bounds:""));
        }
        File.WriteAllLines("layout-audit.txt",lines);EditorApplication.Exit(0);
    }

    private static Transform layout;
    private static Mesh rounded;
    private static Material porcelain, slate, water;
    private static GameObject Part(string name, Vector3 position, Vector3 size, Material material, bool solid=false)
    {
        GameObject item=new GameObject(name);item.transform.SetParent(layout,false);item.transform.position=position;item.transform.localScale=size;
        item.AddComponent<MeshFilter>().sharedMesh=rounded;item.AddComponent<MeshRenderer>().sharedMaterial=material;
        if(solid)item.AddComponent<BoxCollider>();return item;
    }
    private static Material Material(string name,Color color)
    {
        Material material=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};material.SetFloat("_Smoothness",.25f);
        AssetDatabase.CreateAsset(material,"Assets/BadShift/Layout/"+name+".mat");return material;
    }
    private static GameObject Basin(string name,Vector3 position,Vector3 scale,Vector2[] profile)
    {
        const int segments=32;var vertices=new List<Vector3>();var triangles=new List<int>();
        foreach(Vector2 ring in profile)for(int i=0;i<segments;i++){float a=i*Mathf.PI*2/segments;vertices.Add(new Vector3(Mathf.Cos(a)*ring.x,ring.y,Mathf.Sin(a)*ring.x));}
        for(int r=0;r<profile.Length-1;r++)for(int i=0;i<segments;i++)
        {
            int a=r*segments+i,b=r*segments+(i+1)%segments,c=b+segments,d=a+segments;triangles.AddRange(new[]{a,d,c,a,c,b});
        }
        Mesh mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,"Assets/BadShift/Layout/"+name+".asset");
        GameObject item=new GameObject(name);item.transform.SetParent(layout,false);item.transform.position=position;item.transform.localScale=scale;
        item.AddComponent<MeshFilter>().sharedMesh=mesh;item.AddComponent<MeshRenderer>().sharedMaterial=porcelain;return item;
    }
    private static Rigidbody[] TableRow(float z)
    {
        return UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>Mathf.Abs(b.position.z-z)<.8f&&(b.name=="Pushable dining table"||b.name=="Coral cafe chair"||b.name=="Milk carton"||b.name=="Breakable ceramic cup")).ToArray();
    }
    private static void FridgeDoor(BadShiftDemo demo,Transform visual,bool upper)
    {
        Bounds bounds=visual.GetComponent<Renderer>().bounds;
        GameObject hinge=new GameObject(upper?"Upper refrigerator hinge":"Lower refrigerator hinge");hinge.transform.SetParent(layout,false);hinge.transform.position=visual.position;
        visual.SetParent(hinge.transform,true);
        GameObject shape=new GameObject("Door collision");shape.transform.SetParent(hinge.transform,false);
        BoxCollider collider=shape.AddComponent<BoxCollider>();collider.center=bounds.center-hinge.transform.position;collider.size=bounds.size;
        Rigidbody body=hinge.AddComponent<Rigidbody>();body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
        BadShiftDoor door=hinge.AddComponent<BadShiftDoor>();door.Leaf=collider;door.Sound=demo.Game.Door.Sound;door.OpenAngle=-90;door.DegreesPerSecond=240;
        if(upper)demo.Game.FridgeDoors.Upper=door;else demo.Game.FridgeDoors.Lower=door;
    }
    private static void SpaceChairs()
    {
        foreach(Rigidbody chair in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None).Where(b=>b.name=="Coral cafe chair"))
        {
            Vector3 position=chair.transform.position;position.x=-8+(position.x<-8?-1.4f:1.4f);chair.transform.position=position;
        }
    }
    public static void CorrectChairClearance()
    {
        Open();SpaceChairs();var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    public static void FinishKitchenAlignment()
    {
        Open();BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        float delta=6-GameObject.Find("Kitchen counter top").transform.position.x;
        foreach(Transform item in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Kitchen cabinet")||t.name.StartsWith("Kitchen counter")||t.name=="Cabinet door"||t.name=="Cabinet handle"||t.name=="Kitchen shelf"||t.name=="jar_A_medium"))item.position+=Vector3.right*delta;
        demo.Microwave.position+=Vector3.right*delta;
        demo.Game.FridgeDoors.Lower.OpenAngle=-90;demo.Game.FridgeDoors.Upper.OpenAngle=-90;
        EditorUtility.SetDirty(demo.Game.FridgeDoors.Lower);EditorUtility.SetDirty(demo.Game.FridgeDoors.Upper);
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void Run()
    {
        Open();if(GameObject.Find("Balanced cafe layout")!=null)throw new Exception("Preserve authored layout");
        AssetDatabase.CreateFolder("Assets/BadShift","Layout");layout=new GameObject("Balanced cafe layout").transform;
        rounded=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        porcelain=Material("Bathroom porcelain",new Color(.94f,.9f,.8f));slate=Material("Bathroom fittings",new Color(.22f,.28f,.33f));water=Material("Basin water",new Color(.42f,.66f,.73f));
        BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        Transform[] all=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        Rigidbody[] first=TableRow(-2),last=TableRow(2);
        foreach(Rigidbody item in first)item.transform.position+=new Vector3(1,0,-2.8f);
        foreach(Rigidbody item in last)item.transform.position+=new Vector3(1,0,-.4f);
        foreach(Rigidbody item in first){GameObject copy=UnityEngine.Object.Instantiate(item.gameObject);copy.name=item.name;copy.transform.position+=Vector3.forward*3.2f;}
        SpaceChairs();
        foreach(PlaygroundPickup banana in demo.Game.Pickups.Where(p=>p.AutoCollect).OrderBy(p=>p.transform.position.x).Select((p,index)=>{p.transform.position=new Vector3(-10+index*2,.9f,-7.5f);return p;}))EditorUtility.SetDirty(banana);
        GameObject.Find("Pushable picnic assembly").transform.position=new Vector3(0,0,-1.6f);
        Vector3 crateDelta=new Vector3(3,0,-1.8f);GameObject.Find("Tip-over tomato crate").transform.position+=crateDelta;
        foreach(Transform tomato in all.Where(t=>t.name=="Loose crate tomato"))tomato.position+=crateDelta;
        Transform ball=GameObject.Find("Kickable tomato pickup").transform;ball.position=new Vector3(0,ball.position.y,-3.5f);
        demo.Boss.position+=Vector3.forward*1.2f;GameObject.Find("Manager collision").transform.position+=Vector3.forward*1.2f;
        foreach(Transform item in all.Where(t=>new[]{"Manager lectern","Manager desk top","Manager paperwork","Megaphone bell","Megaphone stand","Megaphone neck"}.Contains(t.name)))item.position+=Vector3.forward*1.2f;
        Vector3 kitchenDelta=new Vector3(2.3f,0,1.95f);
        foreach(Transform item in all.Where(t=>t.name.StartsWith("Kitchen cabinet")||t.name.StartsWith("Kitchen counter")||t.name=="Cabinet door"||t.name=="Cabinet handle"))item.position+=kitchenDelta;
        demo.Microwave.position+=kitchenDelta;Physics.SyncTransforms();
        float counterTop=GameObject.Find("Kitchen counter top").GetComponent<Renderer>().bounds.max.y;
        demo.Microwave.position+=Vector3.up*(counterTop+.04f-demo.MicrowaveVisual.GetComponent<Renderer>().bounds.min.y);
        foreach(float x in new[]{-.5f,.5f})
        {
            GameObject foot=Part("Microwave foot",new Vector3(6+x,counterTop+.02f,8.05f),new Vector3(.13f,.04f,.5f),slate);foot.transform.SetParent(demo.MicrowaveVisual,true);
        }
        Transform shelf=GameObject.Find("Kitchen shelf").transform;shelf.position=new Vector3(6,3.5f,8.6f);shelf.localScale=new Vector3(3.2f,.2f,.6f);
        int jarIndex=0;foreach(Transform jar in all.Where(t=>t.name=="jar_A_medium").OrderBy(t=>t.position.x))jar.position=new Vector3(5.2f+jarIndex++*.8f,3.6f,8.6f);
        if(PrefabUtility.IsPartOfPrefabInstance(demo.Fridge))PrefabUtility.UnpackPrefabInstance(demo.Fridge.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(string name in new[]{"Fridge memo","Fridge magnet"}){GameObject old=GameObject.Find(name);if(old!=null)old.SetActive(false);}
        demo.Fridge.localScale*=.8f;Physics.SyncTransforms();Bounds fridge=demo.Fridge.GetComponent<Renderer>().bounds;
        demo.Fridge.position+=new Vector3(9.4f-fridge.center.x,0,8.9f-fridge.max.z);Physics.SyncTransforms();
        foreach(Collider old in demo.Fridge.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(old);
        MeshCollider housing=demo.Fridge.gameObject.AddComponent<MeshCollider>();housing.sharedMesh=demo.Fridge.GetComponent<MeshFilter>().sharedMesh;
        demo.Game.FridgeDoors=demo.gameObject.AddComponent<BadShiftFridge>();
        Transform lower=demo.Fridge.Find("fridge_A_door_bottom"),upper=demo.Fridge.Find("fridge_A_door_top");
        FridgeDoor(demo,lower,false);FridgeDoor(demo,upper,true);
        demo.Game.Door.OpenAngle=95;demo.Game.Door.DegreesPerSecond=250;
        foreach(string name in new[]{"Mop handle","pot_A"}){GameObject old=GameObject.Find(name);if(old!=null)old.SetActive(false);}
        Part("Toilet pedestal",new Vector3(-2.25f,.21f,8),new Vector3(.42f,.42f,.54f),porcelain,true);
        Basin("Toilet bowl",new Vector3(-2.25f,.3f,7.95f),new Vector3(1,1,1.2f),new[]{new Vector2(.15f,0),new Vector2(.27f,.08f),new Vector2(.42f,.38f),new Vector2(.43f,.44f),new Vector2(.31f,.44f),new Vector2(.28f,.32f),new Vector2(.13f,.17f),new Vector2(.02f,.17f)}).AddComponent<BoxCollider>();
        Basin("Toilet seat",new Vector3(-2.25f,.74f,7.95f),new Vector3(1,1,1.2f),new[]{new Vector2(.43f,0),new Vector2(.43f,.045f),new Vector2(.31f,.045f),new Vector2(.31f,0),new Vector2(.43f,0)});
        Part("Toilet cistern",new Vector3(-2.25f,.98f,8.47f),new Vector3(.75f,.7f,.36f),porcelain,true);
        Part("Flush button",new Vector3(-2.25f,1.34f,8.47f),new Vector3(.15f,.025f,.1f),slate);
        Part("Toilet water",new Vector3(-2.25f,.48f,7.95f),new Vector3(.24f,.02f,.28f),water);
        Part("Sink pedestal",new Vector3(-4,.44f,8.28f),new Vector3(.25f,.88f,.3f),porcelain,true);
        Basin("Sink basin",new Vector3(-4,.85f,8.28f),new Vector3(1.4f,1,.85f),new[]{new Vector2(.15f,0),new Vector2(.4f,.18f),new Vector2(.42f,.25f),new Vector2(.34f,.25f),new Vector2(.24f,.08f),new Vector2(.02f,.08f)}).AddComponent<BoxCollider>();
        Part("Faucet stem",new Vector3(-4,1.19f,8.55f),new Vector3(.08f,.3f,.08f),slate);
        Part("Faucet spout",new Vector3(-4,1.32f,8.44f),new Vector3(.08f,.08f,.3f),slate);
        Part("Bathroom mirror frame",new Vector3(-4,1.96f,8.86f),new Vector3(1.16f,1.05f,.06f),slate);
        Part("Bathroom mirror",new Vector3(-4,1.96f,8.82f),new Vector3(1.02f,.91f,.02f),water);
        Transform exitFrame=GameObject.Find("Shift exit frame").transform;exitFrame.gameObject.SetActive(false);demo.Game.Exit.position=new Vector3(9.4f,.08f,-8.2f);
        GameObject exitLabel=new GameObject("Exit floor label");exitLabel.transform.SetParent(layout,false);exitLabel.transform.position=demo.Game.Exit.position+Vector3.up*.09f;exitLabel.transform.rotation=Quaternion.Euler(90,0,0);
        TextMesh text=exitLabel.AddComponent<TextMesh>();text.text="EXIT";text.fontSize=64;text.characterSize=.13f;text.anchor=TextAnchor.MiddleCenter;text.color=Color.white;
        EditorUtility.SetDirty(demo.Game);EditorUtility.SetDirty(demo);EditorUtility.SetDirty(demo.Game.Door);
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
