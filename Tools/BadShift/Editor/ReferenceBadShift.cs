using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class ReferenceBadShift
{
    private const string Root = "Assets/BadShift/Reference";
    private static Transform decor;
    private static Mesh rounded;
    private static Material cream, coral, sage, wood, dark, yellow, purple, green;
    private static Material Material(string name, Color color)
    {
        string path = Root + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; m.SetFloat("_Smoothness", .12f); EditorUtility.SetDirty(m); return m;
    }
    private static Mesh Rounded()
    {
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/SoftCube.asset");
        if (mesh != null) return mesh;
        var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var triangles = new List<int>();
        Vector3[] axes = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 normal in axes)
        {
            Vector3 u = Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.forward : Vector3.up);
            Vector3 v = Vector3.Cross(normal, u); int start = vertices.Count;
            for (int y = 0; y <= 8; y++) for (int x = 0; x <= 8; x++)
            {
                Vector3 p = normal * .5f + u * (x / 8f - .5f) + v * (y / 8f - .5f);
                Vector3 q = new Vector3(Mathf.Clamp(p.x, -.42f, .42f), Mathf.Clamp(p.y, -.42f, .42f), Mathf.Clamp(p.z, -.42f, .42f));
                Vector3 n = (p - q).normalized; vertices.Add(q + n * .08f); normals.Add(n);
            }
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++)
            { int a = start + y * 9 + x; triangles.AddRange(new[] { a, a + 1, a + 10, a, a + 10, a + 9 }); }
        }
        mesh = new Mesh { name = "Soft clay cube" }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh, Root + "/SoftCube.asset"); return mesh;
    }
    private static GameObject Box(string name, Vector3 p, Vector3 size, Material mat, bool solid = false)
    {
        GameObject g = new GameObject(name); g.transform.SetParent(decor); g.transform.position = p; g.transform.localScale = size;
        g.AddComponent<MeshFilter>().sharedMesh = rounded; g.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (solid) g.AddComponent<BoxCollider>(); return g;
    }
    private static GameObject Sphere(string name, Vector3 p, Vector3 size, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Sphere);g.name = name; g.transform.SetParent(decor);g.transform.position = p;g.transform.localScale = size;g.GetComponent<Renderer>().sharedMaterial = mat;UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());return g;
    }
    private static void Text(string name, string text, Vector3 p, float size, Material mat)
    {
        GameObject g = new GameObject(name);g.transform.SetParent(decor);g.transform.position = p;g.transform.rotation = Quaternion.Euler(0,180,0);
        TextMesh t=g.AddComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.fontSize=64;t.characterSize=size;t.color=mat.color;
    }
    private static void Prop(string name, Vector3 position, float height)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Restaurant/"+name+".fbx");
        GameObject g=(GameObject)PrefabUtility.InstantiatePrefab(prefab);g.transform.SetParent(decor);
        Renderer[] renderers=g.GetComponentsInChildren<Renderer>();Bounds b=renderers[0].bounds;foreach(Renderer r in renderers)b.Encapsulate(r.bounds);
        float scale=height/b.size.y;g.transform.localScale*=scale;g.transform.position=position-Vector3.up*b.min.y*scale;
        Material atlas=AssetDatabase.LoadAssetAtPath<Material>(Root+"/RestaurantAtlas.mat");
        if(atlas==null){atlas=Material("RestaurantAtlas",Color.white);atlas.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/ThirdParty/Restaurant/restaurantbits_texture.png");}
        foreach(Renderer r in renderers)r.sharedMaterials=r.sharedMaterials.Select(m=>atlas).ToArray();
    }
    private static void Apron(Animator actor)
    {
        Transform chest=actor.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.ToLowerInvariant().Contains("spine")) ?? actor.transform;
        Vector3 p=actor.transform.position;
        GameObject bib=Box("Sage apron bib",p+new Vector3(0,1.05f,.24f),new Vector3(.48f,.65f,.09f),sage);bib.transform.SetParent(chest,true);
        GameObject skirt=Box("Sage apron skirt",p+new Vector3(0,.72f,.23f),new Vector3(.6f,.36f,.09f),sage);skirt.transform.SetParent(chest,true);
        foreach(float x in new[]{-.18f,.18f})
        {
            GameObject strap=Box("Apron shoulder strap",p+new Vector3(x,1.43f,.16f),new Vector3(.08f,.45f,.1f),sage);strap.transform.SetParent(chest,true);
            GameObject button=Sphere("Apron button",p+new Vector3(x,1.23f,.3f),Vector3.one*.07f,cream);button.transform.SetParent(chest,true);
        }
    }
    public static void RepairFridgeOrientation()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        BadShiftDemo d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        d.Fridge.rotation=Quaternion.Euler(0,180,0)*AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Restaurant/fridge_A.fbx").transform.rotation;
        PrefabUtility.RecordPrefabInstancePropertyModifications(d.Fridge);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void FinalDetails()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        if(GameObject.Find("Apron fit complete")!=null)throw new Exception("Apron fitting already applied");
        new GameObject("Apron fit complete");
        foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            float drop=t.name=="Apron shoulder strap"?.5f:t.name=="Apron button"?.22f:t.name=="Sage apron bib"?.15f:0;
            t.position-=Vector3.up*drop;
        }
        GameObject.Find("Manager lectern").transform.localScale=new Vector3(3,1.5f,1.3f);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);EditorApplication.Exit(0);
    }
    public static void Facing()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        BadShiftDemo d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        d.Game.transform.rotation=Quaternion.Euler(0,180,0);d.Dancer.rotation=Quaternion.Euler(0,180,0);d.Fridge.rotation=Quaternion.Euler(0,180,0)*AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ThirdParty/Restaurant/fridge_A.fbx").transform.rotation;
        PrefabUtility.RecordPrefabInstancePropertyModifications(d.Dancer);PrefabUtility.RecordPrefabInstancePropertyModifications(d.Fridge);
        TextMesh sign=GameObject.Find("Cafe lettering").GetComponent<TextMesh>();sign.transform.rotation=Quaternion.identity;sign.characterSize=.065f;
        GameObject jump=GameObject.Find("Jump block");jump.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Honey oak.mat");jump.GetComponent<MeshFilter>().sharedMesh=Rounded();
        decor=GameObject.Find("Clay art refinement").transform;rounded=Rounded();wood=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Honey oak.mat");
        for(int i=0;i<3;i++)Box("Crate slat",new Vector3(-3,.2f+i*.25f,-4.03f),new Vector3(2.1f,.12f,.06f),wood);
        Prop("crate_tomatoes",new Vector3(-3,.8f,-3),.6f);
        foreach(Transform t in GameObject.Find("Floor seams").GetComponentsInChildren<Transform>())if(t.name=="Floor joint"){Vector3 scale=t.localScale;if(scale.x<.1f)scale.x=.03f;if(scale.z<.1f)scale.z=.03f;t.localScale=scale;}
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void FinalArt()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        if(GameObject.Find("Clay art refinement")!=null)throw new Exception("Art refinement already applied");
        decor=new GameObject("Clay art refinement").transform;rounded=Rounded();
        cream=Material("Warm porcelain",new Color(.97f,.9f,.81f));coral=Material("Terracotta",new Color(.9f,.36f,.25f));sage=Material("Apron mint",new Color(.46f,.7f,.56f));wood=Material("Honey oak",new Color(.48f,.25f,.12f));dark=Material("Slate",new Color(.19f,.23f,.29f));yellow=Material("Mustard",new Color(1,.63f,.08f));purple=Material("Lavender",new Color(.62f,.43f,.7f));green=Material("Fresh grass",new Color(.5f,.63f,.31f));
        BadShiftDemo d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        d.Game.View.orthographicSize=12.8f;d.Game.View.backgroundColor=new Color(.98f,.93f,.87f);
        foreach(Animator actor in new[]{d.PlayerAnimator,d.DanceAnimator}){actor.transform.localScale*=1.45f;PrefabUtility.RecordPrefabInstancePropertyModifications(actor.transform);}
        CharacterController controller=d.Game.GetComponent<CharacterController>();controller.height=2.6f;controller.center=Vector3.up*1.3f;
        GameObject employee=GameObject.Find("Employee collision");employee.GetComponent<CapsuleCollider>().height=2.6f;employee.GetComponent<CapsuleCollider>().center=Vector3.up*1.3f;
        foreach(Renderer r in d.Boss.GetComponentsInChildren<Renderer>())r.enabled=false;
        GameObject stem=Box("Broccoli friendly stem",new Vector3(-8,2.5f,6),new Vector3(1.3f,2.4f,1.2f),Material("Broccoli stem",new Color(.55f,.7f,.18f)));stem.transform.SetParent(d.Boss,true);
        for(int i=0;i<7;i++)
        {
            float angle=i*Mathf.PI*2/7;GameObject leaf=Sphere("Broccoli crown",new Vector3(-8+Mathf.Cos(angle)*1.05f,3.9f+(i%2)*.45f,6+Mathf.Sin(angle)*.5f),new Vector3(1.9f,1.75f,1.6f),Material("Broccoli leaf "+i,new Color(.22f+i*.012f,.43f+i*.013f,.12f)));leaf.transform.SetParent(d.Boss,true);
        }
        for(int i=0;i<2;i++){GameObject eye=Sphere("Sphere",new Vector3(-8.27f+i*.54f,2.85f,5.37f),new Vector3(.13f,.25f,.1f),dark);eye.transform.SetParent(d.Boss,true);}
        GameObject arm=Box("Broccoli pointing arm",new Vector3(-6.95f,2.2f,5.8f),new Vector3(1,.25f,.3f),Material("Broccoli stem",new Color(.55f,.7f,.18f)));arm.transform.SetParent(d.Boss,true);
        d.Fridge.localScale*=1.35f;PrefabUtility.RecordPrefabInstancePropertyModifications(d.Fridge);
        foreach(Transform t in GameObject.Find("Floor seams").GetComponentsInChildren<Transform>())if(t.name=="Floor joint")t.position=new Vector3(t.position.x,.055f,t.position.z);
        for(int i=0;i<3;i++){Box("Outer cafe window frame",new Vector3(-12.19f,2.15f,-1+i*2.6f),new Vector3(.08f,1.5f,2.3f),dark);Box("Outer cafe glass",new Vector3(-12.25f,2.15f,-1+i*2.6f),new Vector3(.06f,1.22f,2.02f),cream);}
        Box("Road shoulder",new Vector3(-14.1f,-.12f,0),new Vector3(3.7f,.3f,20),Material("Road",new Color(.42f,.39f,.39f)));
        for(int i=0;i<6;i++)Box("Road dashed line",new Vector3(-15.2f,.05f,-8+i*3),new Vector3(.15f,.03f,1.4f),cream);
        Box("Lilac car body",new Vector3(-13.9f,.55f,-3),new Vector3(1.65f,.7f,3),purple);Box("Lilac car cabin",new Vector3(-13.9f,1.15f,-2.8f),new Vector3(1.5f,.9f,1.6f),purple);
        Box("Car windshield",new Vector3(-13.9f,1.23f,-3.65f),new Vector3(1.2f,.55f,.06f),dark);
        for(int i=0;i<2;i++){Sphere("Car headlight",new Vector3(-14.45f+i*1.1f,.65f,-4.52f),new Vector3(.28f,.28f,.06f),cream);for(int j=0;j<2;j++)Sphere("Car tyre",new Vector3(-14.7f+i*1.6f,.35f,-4+j*2),new Vector3(.25f,.7f,.7f),dark);}
        Box("Cafeteria sign pole",new Vector3(-13.6f,1.7f,3.5f),new Vector3(.18f,3.4f,.18f),wood);Box("Cafeteria coral sign",new Vector3(-13.6f,3.25f,3.5f),new Vector3(2.4f,1.6f,.25f),coral);Text("Cafe lettering","CAFETERIA",new Vector3(-13.6f,3.1f,3.34f),.12f,cream);
        foreach(Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){l.color=new Color(1,.95f,.88f);l.intensity=1.15f;l.shadowStrength=.65f;}
        EditorUtility.SetDirty(d.Game);EditorUtility.SetDirty(controller);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);AssetDatabase.SaveAssets();BuildAudioPlayground.BuildPlayer();
    }
    public static void Finish()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        foreach(Transform t in GameObject.Find("Reference diorama").GetComponentsInChildren<Transform>())
            if(t.name.StartsWith("Picnic") || t.name.StartsWith("Parasol")) t.position += Vector3.forward * 2;
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        BuildAudioPlayground.BuildPlayer();
    }
    public static void Run()
    {
        try
        {
            if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            if(GameObject.Find("Reference diorama")!=null)throw new Exception("Reference authoring already applied; preserve edits");
            if(!AssetDatabase.IsValidFolder(Root))AssetDatabase.CreateFolder("Assets/BadShift","Reference");
            decor=new GameObject("Reference diorama").transform;rounded=Rounded();
            cream=Material("Warm porcelain",new Color(.97f,.86f,.7f));coral=Material("Terracotta",new Color(.92f,.36f,.23f));sage=Material("Apron mint",new Color(.42f,.68f,.51f));wood=Material("Honey oak",new Color(.48f,.25f,.12f));dark=Material("Slate",new Color(.19f,.23f,.29f));yellow=Material("Mustard",new Color(1,.63f,.08f));purple=Material("Lavender",new Color(.62f,.43f,.7f));green=Material("Fresh grass",new Color(.48f,.63f,.24f));
            BadShiftDemo d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();d.Game.DioramaCamera=true;d.Game.View.orthographic=true;d.Game.View.orthographicSize=16.8f;d.Game.View.clearFlags=CameraClearFlags.SolidColor;d.Game.View.backgroundColor=new Color(.96f,.88f,.77f);d.Game.View.transform.position=new Vector3(-18,23,-32);d.Game.View.transform.LookAt(new Vector3(0,1,0));
            BadShiftClayMotion motion=d.gameObject.AddComponent<BadShiftClayMotion>();motion.Demo=d;
            foreach(Material m in AssetDatabase.FindAssets("t:Material",new[]{"Assets/BadShift"}).Select(g=>AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g)))){if(m.HasProperty("_Smoothness"))m.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(m);}
            foreach(TextMesh t in UnityEngine.Object.FindObjectsByType<TextMesh>(FindObjectsSortMode.None))if(t.transform.eulerAngles.x>80&&t.transform.eulerAngles.x<100)t.gameObject.SetActive(false);
            foreach(string n in new[]{"MICROWAVE / X sign","MANAGER / X sign","DRY / ECHO / REVERB sign","FRIDGE / DISTANCE sign","South wall"}){GameObject g=GameObject.Find(n);if(g!=null){Renderer r=g.GetComponent<Renderer>();if(r!=null)r.enabled=false;}}
            GameObject boundary=GameObject.Find("Danger zone boundary");if(boundary!=null)boundary.GetComponent<Renderer>().enabled=false;
            foreach(PlaygroundSurface floor in UnityEngine.Object.FindObjectsByType<PlaygroundSurface>(FindObjectsSortMode.None))floor.GetComponent<Renderer>().sharedMaterial=floor.State==0?Material("Stone taupe",new Color(.57f,.49f,.44f)):floor.State==1?green:dark;
            Box("Diorama plinth",new Vector3(0,-.4f,0),new Vector3(24.8f,.7f,20.8f),cream);
            GameObject north=GameObject.Find("North wall");north.transform.position=new Vector3(0,2.4f,10);north.transform.localScale=new Vector3(24,4.8f,.4f);north.GetComponent<Renderer>().sharedMaterial=cream;
            Box("Coral back dado",new Vector3(0,.8f,9.74f),new Vector3(24,1.6f,.1f),coral);
            Box("Left cafe wall",new Vector3(-12,1.6f,3),new Vector3(.3f,3.2f,14),cream);
            Box("Left coral dado",new Vector3(-11.8f,.8f,3),new Vector3(.1f,1.6f,14),coral);
            for(int i=0;i<3;i++){Box("Cafe window",new Vector3(-11.72f,2.15f,-1+i*2.6f),new Vector3(.08f,1.45f,2.25f),dark);Box("Window cream inset",new Vector3(-11.66f,2.15f,-1+i*2.6f),new Vector3(.05f,1.2f,2),cream);}
            GameObject lectern=GameObject.Find("Manager lectern");lectern.GetComponent<Renderer>().sharedMaterial=wood;lectern.transform.localScale=new Vector3(3.5f,1.5f,1.3f);lectern.transform.position=new Vector3(-8,.75f,4.5f);
            Box("Manager desk top",new Vector3(-8,1.55f,4.5f),new Vector3(3.8f,.2f,1.6f),wood);
            Box("Manager paperwork",new Vector3(-8,1.67f,4.25f),new Vector3(.6f,.02f,.4f),cream);
            GameObject oldhorn=GameObject.Find("Manager megaphone");if(oldhorn!=null)oldhorn.SetActive(false);
            Sphere("Megaphone bell",new Vector3(-6.6f,2,4.5f),new Vector3(.85f,.85f,.65f),yellow);
            Sphere("Megaphone dark mouth",new Vector3(-6.6f,2,4.15f),new Vector3(.6f,.6f,.05f),wood);
            Box("Megaphone stand",new Vector3(-6.6f,1.8f,4.6f),new Vector3(.12f,.7f,.12f),dark);
            Box("Notice board",new Vector3(-7,3.2f,9.65f),new Vector3(2.5f,1.7f,.15f),wood);
            for(int i=0;i<2;i++)Box("Notice paper",new Vector3(-7.6f+i,3.2f,9.53f),new Vector3(.55f,.9f,.03f),cream);
            Box("Kitchen shelf",new Vector3(3,3.5f,9.3f),new Vector3(4,.2f,1),dark);
            for(int i=0;i<3;i++)Prop("jar_A_medium",new Vector3(2+i*.75f,3.6f,9.3f),.7f);
            d.Microwave.position=new Vector3(3,1.65f,6.1f);d.Microwave.GetComponent<MeshFilter>().sharedMesh=rounded;
            GameObject.Find("Microwave glass").transform.position=new Vector3(2.8f,1.65f,5.62f);GameObject.Find("Microwave button").transform.position=new Vector3(3.5f,1.65f,5.6f);
            foreach(GameObject root in d.gameObject.scene.GetRootGameObjects())if(root.name=="kitchencounter_straight_A"){root.transform.position=new Vector3(3,0,6.1f);PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);}
            Prop("crate_carrots",new Vector3(10.5f,0,3.3f),.7f);
            Box("Fridge memo",new Vector3(9.35f,1.5f,4.77f),new Vector3(.4f,.55f,.03f),cream);Sphere("Fridge magnet",new Vector3(9.35f,1.75f,4.73f),Vector3.one*.1f,yellow);
            Box("Stockroom lintel",new Vector3(-3,2.9f,5.9f),new Vector3(3.8f,.45f,.3f),purple);
            Box("Stockroom doorway left",new Vector3(-4.65f,1.45f,5.9f),new Vector3(.5f,2.9f,.3f),purple,true);
            Box("Stockroom doorway right",new Vector3(-1.35f,1.45f,5.9f),new Vector3(.5f,2.9f,.3f),purple,true);
            Box("Mop handle",new Vector3(-4,1,8.2f),new Vector3(.08f,1.8f,.08f),wood);Prop("pot_A",new Vector3(-4,0,8),.45f);
            Box("Pendant cable",new Vector3(0,4.1f,7),new Vector3(.05f,1.4f,.05f),dark);Sphere("Coral pendant shade",new Vector3(0,3.5f,7),new Vector3(1.1f,.45f,1.1f),coral);
            d.RaveLight.transform.position=new Vector3(0,3.3f,6.9f);
            foreach(GameObject root in d.gameObject.scene.GetRootGameObjects())if(root.name.StartsWith("table_round_A")){Vector3 p=root.transform.position;Box("Square cafe tabletop",p+Vector3.up*.72f,new Vector3(1.4f,.15f,1.4f),wood);Prop("mustard",p+Vector3.up*.8f,.3f);}
            Box("Picnic tabletop",new Vector3(0,.85f,-2),new Vector3(2.5f,.2f,1.2f),wood,true);
            foreach(float x in new[]{-.85f,.85f})Box("Picnic leg",new Vector3(x,.4f,-2),new Vector3(.2f,.8f,1),wood);
            foreach(float z in new[]{-3f,-1f})Box("Picnic bench",new Vector3(0,.43f,z),new Vector3(2.6f,.18f,.4f),wood,true);
            Box("Parasol pole",new Vector3(0,1.8f,-2),new Vector3(.09f,3.6f,.09f),wood);
            for(int i=0;i<8;i++){GameObject panel=Sphere("Parasol canopy",new Vector3(Mathf.Cos(i*Mathf.PI/4)*.65f,3.2f,-2+Mathf.Sin(i*Mathf.PI/4)*.65f),new Vector3(1.3f,.35f,1.3f),i%2==0?coral:cream);}
            for(int i=0;i<16;i++){float x=-3.5f+(i%4)*2;float z=-8+(i/4)*3;for(int j=0;j<3;j++){GameObject tuft=Box("Grass tuft",new Vector3(x+j*.12f,.15f,z),new Vector3(.12f,.35f,.08f),green);tuft.transform.rotation=Quaternion.Euler(0,0,(j-1)*25);}}
            Apron(d.PlayerAnimator);Apron(d.DanceAnimator);
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.86f,.82f,.76f);RenderSettings.ambientEquatorColor=new Color(.65f,.58f,.5f);RenderSettings.ambientGroundColor=new Color(.42f,.35f,.3f);
            foreach(Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){l.transform.rotation=Quaternion.Euler(48,-35,0);l.color=new Color(1,.88f,.73f);l.intensity=1.5f;l.shadows=LightShadows.Soft;l.shadowBias=.025f;}
            EditorUtility.SetDirty(d);EditorUtility.SetDirty(d.Game);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);AssetDatabase.SaveAssets();Debug.Log("Reference diorama authored");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
