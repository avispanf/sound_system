using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildBadShift
{
    private const string Root = "Assets/BadShift";
    private static Material cream, sage, coral, lilac, navy;
    private static Material Mat(string name, Color color)
    {
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = color; AssetDatabase.CreateAsset(m, Root + "/" + name + ".mat"); return m;
    }
    private static GameObject Box(string name, Vector3 p, Vector3 scale, Material m, bool collider = true)
    {
        GameObject o = GameObject.CreatePrimitive(PrimitiveType.Cube); o.name=name; o.transform.position=p; o.transform.localScale=scale; o.GetComponent<Renderer>().sharedMaterial=m;
        if (!collider) UnityEngine.Object.DestroyImmediate(o.GetComponent<Collider>()); return o;
    }
    private static GameObject Model(string path, Vector3 p, float height)
    {
        GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(prefab==null) throw new Exception("Missing model "+path);
        GameObject o=(GameObject)PrefabUtility.InstantiatePrefab(prefab); o.transform.position=Vector3.zero;
        Renderer[] rs=o.GetComponentsInChildren<Renderer>(); Bounds b=rs[0].bounds; foreach(Renderer r in rs)b.Encapsulate(r.bounds);
        float scale=height/Mathf.Max(.01f,b.size.y);o.transform.localScale*=scale; o.transform.position=p-Vector3.up*b.min.y*scale;
        foreach(Renderer r in rs)
        {
            Material[] ms=r.sharedMaterials;
            for(int i=0;i<ms.Length;i++)
            {
                Material copy=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if(ms[i]!=null) { copy.color=ms[i].color; copy.mainTexture=ms[i].mainTexture; }
                string texture=path.Contains("Skeleton")?"Assets/ThirdParty/Skeletons/skeleton_texture.png":path.Contains("Restaurant")?"Assets/ThirdParty/Restaurant/restaurantbits_texture.png":"Assets/ThirdParty/Food/Textures/colormap.png";
                Texture2D t=AssetDatabase.LoadAssetAtPath<Texture2D>(texture);if(t!=null)copy.mainTexture=t;
                AssetDatabase.CreateAsset(copy,AssetDatabase.GenerateUniqueAssetPath(Root+"/Model.mat"));ms[i]=copy;
            }
            r.sharedMaterials=ms;
        }
        return o;
    }
    private static void Sign(string text, Vector3 p, Material m)
    {
        Box(text+" sign",p,new Vector3(3,.12f,.8f),m,false);
        GameObject g=new GameObject(text);g.transform.position=p+Vector3.up*.08f;g.transform.rotation=Quaternion.Euler(90,0,0);
        TextMesh t=g.AddComponent<TextMesh>();t.text=text;t.fontSize=48;t.characterSize=.12f;t.anchor=TextAnchor.MiddleCenter;t.color=Color.white;
    }
    public static void Run()
    {
        try
        {
            if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT project required");
            if(AssetDatabase.IsValidFolder(Root))throw new Exception("Existing BAD SHIFT authoring preserved");
            AssetDatabase.CreateFolder("Assets","BadShift");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            cream=Mat("Cream",new Color(.95f,.88f,.75f));sage=Mat("Sage",new Color(.55f,.7f,.5f));coral=Mat("Coral",new Color(.94f,.4f,.3f));lilac=Mat("Lilac",new Color(.65f,.5f,.76f));navy=Mat("Navy",new Color(.16f,.22f,.29f));
            AudioMWPlayground game=UnityEngine.Object.FindFirstObjectByType<AudioMWPlayground>();
            foreach(PlaygroundSurface s in UnityEngine.Object.FindObjectsByType<PlaygroundSurface>(FindObjectsSortMode.None))s.GetComponent<Renderer>().sharedMaterial=s.State==0?cream:s.State==1?sage:navy;
            foreach(string n in new[]{"North wall","West wall","East wall"}) { GameObject w=GameObject.Find(n);w.GetComponent<Renderer>().sharedMaterial=cream; }
            GameObject.Find("Danger zone boundary").GetComponent<Renderer>().sharedMaterial=coral;
            Renderer body=game.GetComponent<Renderer>(); if(body!=null) body.enabled=false;
            GameObject skeleton=Model("Assets/ThirdParty/Skeletons/Skeleton_Minion.fbx",game.transform.position-Vector3.up*.9f,1.8f); skeleton.transform.SetParent(game.transform,true);
            string[] food={"banana","tomato","carrot","burger","cheese"};
            for(int i=0;i<game.Pickups.Length;i++)
            {
                PlaygroundPickup pick=game.Pickups[i];pick.GetComponent<Renderer>().enabled=false;
                GameObject f=Model("Assets/ThirdParty/Food/"+food[i]+".fbx",pick.transform.position-Vector3.up*.3f,.65f);f.transform.SetParent(pick.transform,true);
            }
            BadShiftDemo demo=game.gameObject.AddComponent<BadShiftDemo>();demo.Game=game;
            Model("Assets/ThirdParty/Restaurant/kitchencounter_straight_A.fbx",new Vector3(3,0,0),1.2f);
            demo.Microwave=Box("Opinionated microwave",new Vector3(3,1.65f,0),new Vector3(1.5f,.8f,.9f),coral).transform;
            Box("Microwave glass",new Vector3(2.8f,1.65f,-.47f),new Vector3(.9f,.5f,.04f),navy,false);
            Box("Microwave button",new Vector3(3.5f,1.65f,-.48f),new Vector3(.15f,.2f,.05f),cream,false);
            demo.Fridge=Model("Assets/ThirdParty/Restaurant/fridge_A.fbx",new Vector3(9,0,8),2.8f).transform;
            for(int z=-6;z<=6;z+=3) { Box("Distance marker",new Vector3(9,.02f,z),new Vector3(1,.02f,.12f),cream,false); }
            demo.Boss=Model("Assets/ThirdParty/Food/broccoli.fbx",new Vector3(-8,0,6),3.5f).transform;
            for(int i=0;i<2;i++) { GameObject eye=GameObject.CreatePrimitive(PrimitiveType.Sphere);eye.transform.position=new Vector3(-8.35f+i*.7f,2.2f,5.25f);eye.transform.localScale=new Vector3(.18f,.25f,.12f);eye.GetComponent<Renderer>().sharedMaterial=navy;UnityEngine.Object.DestroyImmediate(eye.GetComponent<Collider>()); }
            Box("Manager lectern",new Vector3(-8,.7f,4.5f),new Vector3(2,1.4f,1),coral);
            GameObject horn=GameObject.CreatePrimitive(PrimitiveType.Cylinder);horn.name="Manager megaphone";horn.transform.position=new Vector3(-6.7f,1.6f,4.5f);horn.transform.rotation=Quaternion.Euler(90,0,0);horn.transform.localScale=new Vector3(.5f,.4f,.5f);horn.GetComponent<Renderer>().sharedMaterial=cream;
            demo.Dancer=Model("Assets/ThirdParty/Skeletons/Skeleton_Minion.fbx",new Vector3(0,0,7),1.8f).transform;
            Box("Rave stage",new Vector3(0,.08f,7),new Vector3(3,.16f,2),lilac);
            GameObject lamp=new GameObject("Kitchen rave light");lamp.transform.position=new Vector3(0,4,6);demo.RaveLight=lamp.AddComponent<Light>();demo.RaveLight.type=LightType.Point;demo.RaveLight.range=9;
            demo.Room=new GameObject("Stockroom station").transform;demo.Room.position=new Vector3(-3,0,7);
            Box("Stockroom back",new Vector3(-3,1.5f,9),new Vector3(4,3,.2f),lilac);
            Box("Stockroom side",new Vector3(-5,1.5f,7.5f),new Vector3(.2f,3,3),lilac);
            for(int x=-4;x<=-2;x++)for(int z=6;z<=8;z++)Box("Stockroom tile",new Vector3(x,.02f,z),new Vector3(.96f,.04f,.96f),(x+z)%2==0?lilac:cream,false);
            demo.RoomSource=demo.Room.gameObject.AddComponent<AudioSource>();demo.RoomSource.playOnAwake=false;demo.RoomSource.clip=game.Impact.Clips[0];demo.RoomSource.volume=.35f;demo.RoomSource.outputAudioMixerGroup=game.Impact.MixerGroup;
            demo.Echo=demo.Room.gameObject.AddComponent<AudioEchoFilter>();demo.Echo.delay=280;demo.Echo.decayRatio=.45f;demo.Echo.wetMix=.7f;demo.Echo.dryMix=1;
            demo.Reverb=demo.Room.gameObject.AddComponent<AudioReverbFilter>();demo.Reverb.reverbPreset=AudioReverbPreset.Bathroom;demo.Echo.enabled=false;demo.Reverb.enabled=false;
            AudioClip[] clips=AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Playground/Audio"}).Select(g=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(g))).Where(c=>c.name.StartsWith("SFX_Blip")).ToArray();
            demo.MicrowaveRandom=SoundEvent.CreateRuntime(clips);demo.MicrowaveRandom.SelectionMode=ClipSelectionMode.Random;demo.MicrowaveRandom.Volume=.3f;demo.MicrowaveRandom.MixerGroup=game.Impact.MixerGroup;AssetDatabase.CreateAsset(demo.MicrowaveRandom,Root+"/MicrowaveRandom.asset");
            demo.MicrowaveBag=UnityEngine.Object.Instantiate(demo.MicrowaveRandom);demo.MicrowaveBag.SelectionMode=ClipSelectionMode.ShuffleBag;AssetDatabase.CreateAsset(demo.MicrowaveBag,Root+"/MicrowaveBag.asset");
            demo.FridgeSound=SoundEvent.CreateRuntime(game.Ambience.Clips);demo.FridgeSound.Loop=true;demo.FridgeSound.Volume=.15f;demo.FridgeSound.SpatialBlend=1;demo.FridgeSound.MinDistance=1;demo.FridgeSound.MaxDistance=15;demo.FridgeSound.MixerGroup=game.Impact.MixerGroup;AssetDatabase.CreateAsset(demo.FridgeSound,Root+"/Fridge.asset");
            Sign("MICROWAVE / X",new Vector3(3,.05f,-1.5f),coral);Sign("MANAGER / X",new Vector3(-8,.05f,3),coral);Sign("DRY / ECHO / REVERB",new Vector3(-3,.08f,5.5f),lilac);Sign("FRIDGE / DISTANCE",new Vector3(9,.05f,-7),navy);
            for(int z=-5;z<=0;z+=5) { Model("Assets/ThirdParty/Restaurant/table_round_A.fbx",new Vector3(-9,0,z),1);Model("Assets/ThirdParty/Restaurant/chair_A.fbx",new Vector3(-10.5f,0,z),1); }
            RenderSettings.ambientLight=new Color(.8f,.75f,.7f);RenderSettings.ambientIntensity=1;
            foreach(Light l in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.type==LightType.Directional){l.color=new Color(1,.9f,.8f);l.intensity=1.4f;l.shadows=LightShadows.Soft;}
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(game.gameObject.scene,"Assets/Playground/RelayField.unity");
            Debug.Log("BAD SHIFT scene authored");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
