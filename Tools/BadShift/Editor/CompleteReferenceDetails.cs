using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CompleteReferenceDetails
{
    private const string Folder="Assets/BadShift/Details";
    private static Transform root;
    private static Mesh soft;
    private static Material cream,gold,metal;
    private static Material Mat(string name,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.15f);AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat");return m;
    }
    private static GameObject Box(string name,Vector3 p,Vector3 size,Material m,bool solid=false)
    {
        var g=new GameObject(name);g.transform.SetParent(root);g.transform.position=p;g.transform.localScale=size;g.AddComponent<MeshFilter>().sharedMesh=soft;g.AddComponent<MeshRenderer>().sharedMaterial=m;if(solid)g.AddComponent<BoxCollider>();return g;
    }
    private static Mesh Revolve(string name,Vector2[] rings,int sides,bool flat=false)
    {
        var v=new List<Vector3>();var t=new List<int>();
        foreach(var r in rings)for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*r.x,r.y,Mathf.Sin(a)*r.x));}
        for(int j=0;j<rings.Length-1;j++)for(int i=0;i<sides;i++){int a=j*sides+i,b=j*sides+(i+1)%sides;t.AddRange(new[]{a,a+sides,b+sides,a,b+sides,b});}
        if(flat){var f=t.Select(i=>v[i]).ToList();v=f;t=Enumerable.Range(0,f.Count).ToList();}
        var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/"+name+".asset");return mesh;
    }
    private static GameObject Shape(string name,Mesh mesh,Vector3 p,Material m)
    {
        var g=Box(name,p,Vector3.one,m);g.GetComponent<MeshFilter>().sharedMesh=mesh;return g;
    }
    private static void Open()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
    }
    public static void Run()
    {
        Open();if(GameObject.Find("Reference finishing details")!=null)throw new Exception("Details already authored");
        AssetDatabase.CreateFolder("Assets/BadShift","Details");root=new GameObject("Reference finishing details").transform;
        soft=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        cream=Mat("Warm porcelain",new Color(.96f,.86f,.69f));gold=Mat("Golden megaphone",new Color(1,.64f,.035f));metal=Mat("Warm steel",new Color(.43f,.43f,.49f));
        var rings=new List<Vector2>();for(int i=0;i<=6;i++){float a=i*Mathf.PI/6;rings.Add(new Vector2(Mathf.Sin(a)*.5f,-Mathf.Cos(a)*.5f));}
        Mesh crown=Revolve("Faceted broccoli",rings.ToArray(),9,true);
        foreach(var t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name=="Broccoli crown"))t.GetComponent<MeshFilter>().sharedMesh=crown;
        var bell=GameObject.Find("Megaphone bell");Vector3 bp=bell.transform.position;bell.SetActive(false);
        var mouth=GameObject.Find("Megaphone dark mouth");if(mouth!=null)mouth.SetActive(false);
        Mesh horn=Revolve("Hollow flared horn",new[]{new Vector2(.12f,0),new Vector2(.17f,.25f),new Vector2(.48f,.7f),new Vector2(.49f,.78f),new Vector2(.40f,.78f),new Vector2(.1f,.15f)},24);
        var h=Shape("Golden hollow megaphone",horn,bp+new Vector3(0,0,.3f),gold);h.transform.rotation=Quaternion.Euler(-90,0,-30);
        var sign=GameObject.Find("Cafeteria coral sign");var sb=sign.GetComponent<Renderer>().bounds;
        sign.transform.localScale=new Vector3(2.4f,2.4f,.25f);
        var label=GameObject.Find("Cafe lettering").GetComponent<TextMesh>();label.transform.position=new Vector3(sb.center.x,sb.center.y-.72f,sb.min.z-.03f);label.characterSize=.065f;
        Box("Cafe sign cream band",new Vector3(sb.center.x,sb.center.y-.72f,sb.min.z-.01f),new Vector3(2.23f,.65f,.025f),cream);
        label.color=new Color(.17f,.19f,.2f);
        Box("Coffee cup emblem",new Vector3(sb.center.x-.12f,sb.center.y+.12f,sb.min.z-.04f),new Vector3(.8f,.6f,.08f),cream);
        for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;Box("Cup handle",new Vector3(sb.center.x+.34f+Mathf.Cos(a)*.22f,sb.center.y+.19f+Mathf.Sin(a)*.2f,sb.min.z-.04f),new Vector3(.1f,.1f,.07f),cream);}
        for(int i=0;i<3;i++)for(int j=0;j<3;j++)Box("Coffee steam",new Vector3(sb.center.x-.4f+i*.28f+Mathf.Sin(j*2)*.04f,sb.center.y+.57f+j*.11f,sb.min.z-.04f),new Vector3(.055f,.14f,.04f),cream);
        var table=new GameObject("Kitchen preparation table");table.transform.SetParent(root);table.transform.position=new Vector3(5.8f,0,-1);
        foreach(float x in new[]{-1.15f,1.15f})foreach(float z in new[]{-.65f,.65f})Box("Prep table leg",table.transform.position+new Vector3(x,.66f,z),new Vector3(.13f,1.32f,.13f),metal,true).transform.SetParent(table.transform,true);
        Box("Prep worktop",table.transform.position+Vector3.up*1.38f,new Vector3(2.7f,.16f,1.7f),metal,true).transform.SetParent(table.transform,true);
        Box("Prep lower shelf",table.transform.position+Vector3.up*.3f,new Vector3(2.55f,.12f,1.55f),metal,true).transform.SetParent(table.transform,true);
        var body=table.AddComponent<Rigidbody>();body.mass=22;body.interpolation=RigidbodyInterpolation.Interpolate;
        var impact=table.AddComponent<BadShiftImpactProp>();impact.ImpactSound=UnityEngine.Object.FindFirstObjectByType<AudioMWPlayground>().Impact;
        foreach(string name in new[]{"bread","bowl"}) SpawnFood(name);
        EditorSceneManager.MarkSceneDirty(table.scene);EditorSceneManager.SaveScene(table.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    private static void SpawnFood(string name)
    {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Food/"+(name=="bread"?"loaf":name)+".fbx");var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);PrefabUtility.UnpackPrefabInstance(g,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);g.name="Prep "+name;g.transform.SetParent(root);
            var renderers=g.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            g.transform.localScale*=.85f/Mathf.Max(bounds.size.x,bounds.size.z);bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            g.transform.position+=new Vector3(name=="bread"?6.4f:5.2f,1.49f,-1)-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            foreach(var f in g.GetComponentsInChildren<MeshFilter>()){var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=true;}
            var rb=g.AddComponent<Rigidbody>();rb.mass=.3f;rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;rb.interpolation=RigidbodyInterpolation.Interpolate;
            g.AddComponent<BadShiftImpactProp>().ImpactSound=UnityEngine.Object.FindFirstObjectByType<AudioMWPlayground>().Impact;
    }
    public static void FinishLoaf()
    {
        Open();root=GameObject.Find("Reference finishing details").transform;
        UnityEngine.Object.DestroyImmediate(GameObject.Find("Prep bread"));SpawnFood("bread");
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);EditorSceneManager.SaveScene(root.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void Capture()
    {
        Open();Camera camera=Camera.main;camera.transform.SetPositionAndRotation(new Vector3(0,1,0)+Quaternion.Euler(31,15.5f,0)*Vector3.back*44,Quaternion.Euler(31,15.5f,0));camera.orthographicSize=12.8f;
        var target=new RenderTexture(1600,1000,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("reference-review.png",image.EncodeToPNG());camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.DestroyImmediate(target);EditorApplication.Exit(0);
    }
}
