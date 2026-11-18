using System;
using System.IO;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BadShiftCoffeeCartPass
{
    private const string Folder = "Assets/BadShift/Soundscape/";
    private static Mesh softCube;
    private static Material iron, teal, brass, ceramic;

    public static void Run()
    {
        if (!Application.isBatchMode || !File.Exists(Path.Combine(Application.dataPath, "../.bad-shift-demo")))
            throw new InvalidOperationException("An isolated BAD SHIFT project is required.");
        var scene = EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        var soundscape = Object.FindFirstObjectByType<BadShiftSoundscape>();
        if (soundscape == null || soundscape.Gauge == null) throw new InvalidOperationException("Install the cafe soundscape first.");
        if (Object.FindFirstObjectByType<BadShiftCoffeeMill>() != null) return;
        foreach (var part in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            if (part.name == "Slim cafe ceiling fixture" || part.name == "Neutral white diffuser")
                Object.DestroyImmediate(part.gameObject);
        soundscape.Sky.OfficeDiffusers = Array.Empty<Renderer>();
        soundscape.Stream.transform.SetParent(soundscape.Tap, true);
        soundscape.Room.position = new Vector3(.45f, 0, 7.58f);
        softCube = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        iron = Material("Pump graphite");
        teal = Material("Pump turquoise");
        brass = Material("Valve brass");
        ceramic = Material("Pressure gauge ivory");

        var pipe = soundscape.Pump.parent.Find("Service pipe");
        var wall = soundscape.Room.position;
        var start = soundscape.Pump.parent.TransformPoint(new Vector3(.75f, .34f, .03f));
        var end = new Vector3(wall.x - 1.97f, start.y, start.z);
        pipe.position = (start + end) * .5f;
        pipe.localScale = new Vector3(end.x - start.x + .16f, .18f, .18f);

        var cart = new GameObject("Courtyard coffee cart").transform;
        cart.position = new Vector3(-.95f, 0, 1.4f);
        Box(cart, "Bean cabinet", new Vector3(0,.75f,0), new Vector3(1.58f,1.13f,.93f), teal);
        Box(cart, "Countertop", new Vector3(0,1.36f,0), new Vector3(1.83f,.14f,1.10f), ceramic);
        Box(cart, "Brass cabinet pull", new Vector3(0,1.06f,-.5f), new Vector3(.45f,.05f,.07f), brass);
        foreach (float x in new[]{-.6f,.6f})
            foreach (float z in new[]{-.36f,.36f})
                Shape(cart, "Cart wheel", PrimitiveType.Cylinder, new Vector3(x,.23f,z), new Vector3(.38f,.06f,.38f), iron).localRotation = Quaternion.Euler(90,0,0);
        var collision = cart.gameObject.AddComponent<BoxCollider>();
        collision.center = new Vector3(0,.72f,0); collision.size = new Vector3(1.8f,1.42f,1.08f);
        var target = cart.gameObject.AddComponent<BadShiftUseTarget>();
        target.Kind = BadShiftUseKind.CoffeeMill; target.Label = "Crank coffee mill";
        var mill = cart.gameObject.AddComponent<BadShiftCoffeeMill>();
        var basePos = new Vector3(-.26f,1.68f,0);
        Box(cart, "Coffee mill drawer", basePos, new Vector3(.76f,.51f,.67f), iron);
        Box(cart, "Grounds drawer pull", basePos + new Vector3(0,0,-.36f), new Vector3(.22f,.08f,.07f), brass);
        Shape(cart,"Bean hopper",PrimitiveType.Sphere,basePos + Vector3.up*.32f,new Vector3(.75f,.34f,.68f),brass);
        mill.Crank = new GameObject("Mill crank").transform;
        mill.Crank.SetParent(cart,false); mill.Crank.localPosition = basePos + Vector3.up*.55f;
        Shape(mill.Crank,"Spindle",PrimitiveType.Cylinder,Vector3.down*.04f,new Vector3(.12f,.12f,.12f),iron);
        Box(mill.Crank,"Crank arm",new Vector3(.22f,.06f,0),new Vector3(.53f,.08f,.10f),brass);
        Shape(mill.Crank,"Crank grip",PrimitiveType.Capsule,new Vector3(.45f,.16f,0),new Vector3(.14f,.13f,.14f),teal);
        Shape(cart,"Espresso cup",PrimitiveType.Cylinder,new Vector3(.49f,1.57f,-.11f),new Vector3(.31f,.14f,.31f),ceramic);
        Shape(cart,"Coffee surface",PrimitiveType.Cylinder,new Vector3(.49f,1.712f,-.11f),new Vector3(.25f,.008f,.25f),iron);

        mill.Speed = SoundParameter.CreateRuntime(0,1,0);
        AssetDatabase.CreateAsset(mill.Speed,Folder+"Grinder_Speed.asset");
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"CoffeeBurr.wav");
        if (clip == null) throw new FileNotFoundException("Generate CoffeeBurr.wav first.");
        var sound = SoundEvent.CreateRuntime(clip);
        sound.Loop = true; sound.Volume = .65f;
        sound.MixerGroup = soundscape.PumpLoop.MixerGroup;
        sound.AttenuationPreset = soundscape.PumpLoop.AttenuationPreset;
        sound.ParameterBindings = new[]{
            ParameterBinding.CreateRuntime(mill.Speed,ParameterTarget.Pitch,AnimationCurve.Linear(0,.55f,1,1.65f)),
            ParameterBinding.CreateRuntime(mill.Speed,ParameterTarget.Volume,AnimationCurve.Linear(0,0,1,1))
        };
        AssetDatabase.CreateAsset(sound,Folder+"Coffee_Grinding.asset");
        mill.GrindLoop = sound;
        soundscape.Mill = mill;
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException(scene.path);
        AssetDatabase.SaveAssets();
    }

    private static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>(Folder + name + ".mat");

    private static Transform Box(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var item = new GameObject(name);
        item.AddComponent<MeshFilter>().sharedMesh = softCube;
        item.AddComponent<MeshRenderer>().sharedMaterial = material;
        return Place(item.transform,parent,position,size);
    }

    private static Transform Shape(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 size,Material material)
    {
        var item = GameObject.CreatePrimitive(type);
        item.name = name;
        Object.DestroyImmediate(item.GetComponent<Collider>());
        item.GetComponent<Renderer>().sharedMaterial = material;
        return Place(item.transform,parent,position,size);
    }

    private static Transform Place(Transform item,Transform parent,Vector3 position,Vector3 size)
    {
        item.SetParent(parent,false);
        item.localPosition = position;
        item.localScale = size;
        return item;
    }
}
