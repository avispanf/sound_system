using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildBadShiftProps
{
    private const string Root = "Assets/BadShift/Interactions";
    private static Material wood, lavender;
    private static PhysicsMaterial contact, bounce;
    private static SoundEvent impact;
    private static Mesh rounded;
    private static GameObject Part(string name, Transform parent, Vector3 position, Vector3 size, Material material)
    {
        GameObject item = new GameObject(name); item.transform.SetParent(parent, false);
        item.transform.localPosition = position; item.transform.localScale = size;
        item.AddComponent<MeshFilter>().sharedMesh = rounded;
        item.AddComponent<MeshRenderer>().sharedMaterial = material;
        item.AddComponent<BoxCollider>().sharedMaterial = contact;
        return item;
    }
    private static Rigidbody Dynamic(GameObject item, float mass)
    {
        Rigidbody body = item.AddComponent<Rigidbody>(); body.mass = mass;
        body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.linearDamping = .08f; body.angularDamping = .18f; body.solverIterations = 12; body.solverVelocityIterations = 4;
        BadShiftImpactProp sound = item.AddComponent<BadShiftImpactProp>(); sound.ImpactSound = impact;
        return body;
    }
    private static void Ball(GameObject item)
    {
        foreach (Collider collider in item.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
        Renderer[] renderers = item.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
        Bounds bounds = renderers[0].bounds; foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        SphereCollider sphere = item.AddComponent<SphereCollider>(); sphere.center = item.transform.InverseTransformPoint(bounds.center);
        sphere.radius = Mathf.Min(bounds.size.x, bounds.size.z) * .48f / Mathf.Abs(item.transform.lossyScale.x); sphere.sharedMaterial = bounce;
        Dynamic(item, .25f); item.AddComponent<BadShiftKickable>();
    }
    public static void LooseProps()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated project required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root + "/Contact.physicMaterial");
        impact = AssetDatabase.LoadAssetAtPath<SoundEvent>(Root + "/PropImpact.asset");
        BadShiftDemo demo = UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        var items = demo.Game.Pickups.Where(p => !p.AutoCollect).Select(p => p.gameObject).Concat(UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t => t.name == "crate_carrots" || t.name == "jar_A_medium").Select(t => t.gameObject));
        foreach (GameObject item in items)
        {
            if (item.GetComponent<Rigidbody>() != null) continue;
            foreach (Collider collider in item.GetComponentsInChildren<Collider>()) UnityEngine.Object.DestroyImmediate(collider);
            foreach (MeshFilter filter in item.GetComponentsInChildren<MeshFilter>())
            {
                Renderer renderer = filter.GetComponent<Renderer>(); if (renderer == null || !renderer.enabled) continue;
                MeshCollider shape = filter.gameObject.AddComponent<MeshCollider>(); shape.sharedMesh = filter.sharedMesh; shape.convex = true; shape.sharedMaterial = contact;
            }
            Dynamic(item, item.name == "crate_carrots" ? 3 : .4f);
        }
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene); EditorSceneManager.SaveScene(demo.gameObject.scene); AssetDatabase.SaveAssets(); EditorApplication.Exit(0);
    }
    public static void SettleAndInspect()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated project required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        foreach(PlaygroundPickup pickup in demo.Game.Pickups.Where(p=>!p.AutoCollect))
        {
            Collider[] shapes=pickup.GetComponentsInChildren<Collider>();Bounds bounds=shapes[0].bounds;foreach(Collider shape in shapes)bounds.Encapsulate(shape.bounds);
            pickup.transform.position+=Vector3.up*(.025f-bounds.min.y);
        }
        foreach(Rigidbody body in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
        {
            Collider[] shapes=body.GetComponentsInChildren<Collider>();if(shapes.Length==0)continue;
            Bounds bounds=shapes[0].bounds;foreach(Collider shape in shapes)bounds.Encapsulate(shape.bounds);
            Debug.Log("LAYOUT "+body.name+" "+body.position+" "+bounds);
        }
        Debug.Log("LAYOUT EXIT "+demo.Game.Exit.position+" FRIDGE "+demo.Fridge.GetComponentInChildren<Renderer>().bounds);
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void ClearExit()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated project required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        BadShiftDemo demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        Vector3 exit=new Vector3(6,.08f,8.5f);GameObject.Find("Shift exit frame").transform.position+=exit-demo.Game.Exit.position;demo.Game.Exit.position=exit;
        PlaygroundPickup cheese=demo.Game.Pickups.First(p=>p.name=="Gold relay 5");
        Transform visual=cheese.GetComponentsInChildren<Transform>().First(t=>t.name=="cheese");visual.localScale*=.6f;cheese.transform.position=new Vector3(6.3f,.5f,1.8f);
        Physics.SyncTransforms();Collider[] shapes=cheese.GetComponentsInChildren<Collider>();Bounds bounds=shapes[0].bounds;foreach(Collider shape in shapes)bounds.Encapsulate(shape.bounds);cheese.transform.position+=Vector3.up*(.025f-bounds.min.y);
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void SupportShelf()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated project required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        GameObject shelf=GameObject.Find("Kitchen shelf");
        if(shelf.GetComponent<Collider>()==null)shelf.AddComponent<BoxCollider>().sharedMaterial=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root+"/Contact.physicMaterial");
        EditorSceneManager.MarkSceneDirty(shelf.scene);EditorSceneManager.SaveScene(shelf.scene);EditorApplication.Exit(0);
    }
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated project required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        if (GameObject.Find("Pushable picnic assembly") != null) throw new Exception("Props already authored");
        BadShiftDemo demo = UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        rounded = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        wood = AssetDatabase.LoadAssetAtPath<Material>("Assets/BadShift/Reference/Honey oak.mat");
        lavender = AssetDatabase.LoadAssetAtPath<Material>("Assets/BadShift/Reference/Lavender.mat");
        contact = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(Root + "/Contact.physicMaterial");
        impact = AssetDatabase.LoadAssetAtPath<SoundEvent>(Root + "/PropImpact.asset");
        bounce = new PhysicsMaterial("Rubber tomato"){dynamicFriction=.35f,staticFriction=.45f,bounciness=.65f,bounceCombine=PhysicsMaterialCombine.Maximum};
        AssetDatabase.CreateAsset(bounce, Root + "/Tomato.physicMaterial");
        Transform[] all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
        GameObject picnic = new GameObject("Pushable picnic assembly");
        foreach (Transform part in all.Where(t => t.name.StartsWith("Picnic") || t.name.StartsWith("Parasol")))
        {
            part.SetParent(picnic.transform, true);
            if (part.GetComponent<Collider>() == null) part.gameObject.AddComponent<BoxCollider>();
            part.GetComponent<Collider>().sharedMaterial = contact;
        }
        Dynamic(picnic, 22);
        PlaygroundPickup tomato = demo.Game.Pickups.First(p => p.GetComponentsInChildren<Transform>().Any(t => t.name == "tomato"));
        tomato.name = "Kickable tomato pickup"; Ball(tomato.gameObject);
        foreach (Transform old in all.Where(t => t.name == "Jump block" || t.name == "Crate slat" || t.name == "crate_tomatoes")) old.gameObject.SetActive(false);
        GameObject crate = new GameObject("Tip-over tomato crate"); crate.transform.position = new Vector3(-3,0,-3.2f);
        Part("Crate base", crate.transform, new Vector3(0,.12f,0), new Vector3(1.8f,.22f,1.35f), wood);
        foreach (float x in new[]{-.84f,.84f}) Part("Crate side",crate.transform,new Vector3(x,.48f,0),new Vector3(.12f,.65f,1.35f),wood);
        foreach (float z in new[]{-.62f,.62f}) Part("Crate end",crate.transform,new Vector3(0,.48f,z),new Vector3(1.8f,.65f,.12f),wood);
        Dynamic(crate, 3.5f);
        Transform visual = tomato.GetComponentsInChildren<Transform>().First(t => t.name == "tomato");
        for(int i=0;i<4;i++)
        {
            GameObject food=UnityEngine.Object.Instantiate(visual.gameObject);food.name="Loose crate tomato";
            food.transform.localScale=visual.lossyScale*.65f;
            Renderer[] renderers=food.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(Renderer renderer in renderers)bounds.Encapsulate(renderer.bounds);
            food.transform.position+=new Vector3(-3.4f+(i%2)*.8f,.47f,-3.55f+(i/2)*.65f)-bounds.center;
            Ball(food);
        }
        Transform board=GameObject.Find("Cafeteria coral sign").transform;
        TextMesh lettering=GameObject.Find("Cafe lettering").GetComponent<TextMesh>();lettering.anchor=TextAnchor.MiddleCenter;lettering.alignment=TextAlignment.Center;lettering.characterSize=.065f;
        lettering.transform.position=board.position+Vector3.back*.145f;lettering.transform.rotation=Quaternion.identity;
        Bounds letters=lettering.GetComponent<Renderer>().bounds;lettering.transform.position+=board.position+Vector3.back*.145f-letters.center;
        demo.Dancer.position=new Vector3(.25f,.16f,7.4f);PrefabUtility.RecordPrefabInstancePropertyModifications(demo.Dancer);
        GameObject.Find("Employee collision").transform.position=demo.Dancer.position;
        Transform stage=GameObject.Find("Rave stage").transform;stage.position=new Vector3(.25f,.08f,7.4f);stage.localScale=new Vector3(2.2f,.16f,1.8f);
        demo.Microwave.position+=Vector3.right*.7f;
        foreach(Transform part in all.Where(t=>t.name.StartsWith("Kitchen cabinet")||t.name.StartsWith("Kitchen counter")||t.name=="Cabinet door"||t.name=="Cabinet handle"))part.position+=Vector3.right*.7f;
        GameObject hinge=new GameObject("Stockroom hinged door");hinge.transform.position=new Vector3(-4.35f,0,5.65f);
        GameObject leaf=Part("Stockroom door leaf",hinge.transform,new Vector3(1.35f,1.35f,0),new Vector3(2.65f,2.65f,.12f),wood);
        Part("Stockroom door handle",hinge.transform,new Vector3(2.42f,1.25f,-.14f),new Vector3(.12f,.12f,.2f),lavender);
        Rigidbody doorBody=hinge.AddComponent<Rigidbody>();doorBody.isKinematic=true;doorBody.interpolation=RigidbodyInterpolation.Interpolate;
        BadShiftDoor door=hinge.AddComponent<BadShiftDoor>();door.Leaf=leaf.GetComponent<BoxCollider>();door.Sound=impact;demo.Game.Door=door;
        EditorUtility.SetDirty(demo.Game);EditorUtility.SetDirty(demo);
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();
        BuildBadShiftPresentation.Run();
    }
}
