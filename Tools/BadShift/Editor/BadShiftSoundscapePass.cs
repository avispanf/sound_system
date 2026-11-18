using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using U = UnityEngine.Object;

public static class BadShiftSoundscapePass
{
    private const string Folder="Assets/BadShift/Soundscape/";
    private static Mesh cube;
    private static Transform yard;
    private static Material metal, enamel, brass, water;
    private static AudioMixerGroup bus;

    public static void Run()
    {
        if(!Application.isBatchMode || !File.Exists(Path.Combine(Application.dataPath,"../.bad-shift-demo")))throw new InvalidOperationException();
        var scene=EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        var demo=U.FindFirstObjectByType<BadShiftDemo>();
        if(demo.Game.Soundscape!=null)
        {
            if(demo.Game.Soundscape.Gauge!=null)return;
            CompleteYard(demo.Game.Soundscape);
            EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene))throw new IOException(scene.path);
            AssetDatabase.SaveAssets();
            return;
        }
        var street=U.FindFirstObjectByType<BadShiftNightTraffic>();
        var sc=demo.gameObject.AddComponent<BadShiftSoundscape>();
        sc.Street=street;sc.Sky=demo.Daylight;sc.Game=demo.Game;sc.Game.Soundscape=sc;
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        sc.Room=GameObject.Find("Central bathroom").transform;
        sc.Cistern=GameObject.Find("Toilet cistern").transform;
        sc.Tap=GameObject.Find("Faucet spout").transform;
        sc.ListenerReverb=sc.Game.Listener.gameObject.AddComponent<AudioReverbFilter>();
        var rv=sc.ListenerReverb;rv.reverbPreset=AudioReverbPreset.User;rv.dryLevel=0;rv.roomHF=-190;rv.decayTime=1.12f;
        rv.decayHFRatio=.78f;rv.reflectionsLevel=-380;rv.reflectionsDelay=.013f;rv.reverbLevel=-210;rv.reverbDelay=.023f;rv.room=-10000;rv.enabled=false;
        bus=demo.MicrowaveRandom.MixerGroup;
        sc.TapLoop=Event("Sink_Running","TapFlow",true,7,.37f);
        sc.Flush=Event("Toilet_Flush","FlushRefill",false,10,.75f);
        sc.PumpLoop=Event("Service_Pump","PumpRotor",true,10,.50f);
        sc.SwitchOn=Event("Neon_Relay_On","SignIgnition",false,17,.80f);
        sc.SwitchOff=Event("Neon_Relay_Off","SignRelease",false,17,.70f);
        street.SirenEvent=Event("Patrol_Siren","PatrolSiren",true,31,.50f);
        street.SirenEvent.AttenuationPreset.DopplerLevel=1.35f;
        EditorUtility.SetDirty(street.SirenEvent.AttenuationPreset);
        sc.SignLights=new[]{street.Neon.GetComponent<Light>(),GameObject.Find("Neon through dining window").GetComponent<Light>()};
        sc.NeonParts=U.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(x=>new[]{"Cafeteria coral sign","Cup handle","Coffee steam","Coffee cup emblem","Neon sign trim"}.Contains(x.name)).Cast<Renderer>().ToArray();
        MakeYard(sc);
        MakePatrol(street);
        CompleteYard(sc);
        var day=Bed("Opening_Birds","OpeningBirds",sc.Game.Ambience);
        sc.Game.NightAmbience=Bed("Closing_Crickets","ClosingCrickets",sc.Game.Ambience);
        sc.Game.Ambience=day;
        var music=U.Instantiate(sc.Game.Music);
        var explore=music.States.First(x=>x.Name=="Explore").Track;
        var night=MusicTrack.CreateRuntime(null,Clip("ClosingKeys"),explore.Tempo,explore.BeatsPerBar);
        night.Volume=explore.Volume;night.MixerGroup=explore.MixerGroup;
        AssetDatabase.CreateAsset(night,Folder+"Closing_Keys.asset");
        music.States=music.States.Concat(new[]{new MusicState{Name="Night",Track=night}}).ToArray();
        music.Transitions=music.Transitions.Concat(new[]{
            new MusicTransitionRule{From="Explore",To="Night",Quantization=MusicQuantization.Bar,FadeSeconds=.8f},
            new MusicTransitionRule{From="Night",To="Explore",Quantization=MusicQuantization.Bar,FadeSeconds=.8f}
        }).ToArray();
        AssetDatabase.CreateAsset(music,Folder+"Cafe_Shifts.asset");
        sc.Game.Music=music;
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene))throw new IOException(scene.path);
        AssetDatabase.SaveAssets();
    }

    private static void MakeYard(BadShiftSoundscape sc)
    {
        yard=new GameObject("Water service yard").transform;
        yard.position=new Vector3(.35f,0,7);
        metal=Mat("Pump graphite",new Color(.22f,.27f,.28f));
        enamel=Mat("Pump turquoise",new Color(.18f,.52f,.51f));
        brass=Mat("Valve brass",new Color(.86f,.54f,.16f));
        water=Mat("Running water",new Color(.42f,.81f,.94f));
        Box("Service pad",new Vector3(0,.06f,0),new Vector3(3.4f,.12f,2.35f),metal);
        var tank=Shape("Pressure tank",PrimitiveType.Capsule,new Vector3(.75f,1.31f,.36f),new Vector3(1.05f,1.25f,1.05f),enamel);
        foreach(float y in new[]{.65f,1.93f})Shape("Tank seam",PrimitiveType.Cylinder,new Vector3(.75f,y,.36f),new Vector3(1.07f,.036f,1.07f),brass);
        var motor=Box("Circulation pump",new Vector3(-.68f,.62f,-.05f),new Vector3(1.1f,.85f,.78f),enamel);
        sc.Pump=motor; motor.gameObject.AddComponent<BoxCollider>();
        tank.gameObject.AddComponent<CapsuleCollider>().height=2;
        var target=motor.gameObject.AddComponent<BadShiftUseTarget>();target.Kind=BadShiftUseKind.Pump;target.Label="Prime water pump";
        var fan=new GameObject("Pump impeller").transform;fan.SetParent(yard,false);fan.localPosition=new Vector3(-.68f,.63f,-.47f);sc.Impeller=fan;
        for(int i=0;i<3;i++)
        {
            var vane=Box("Cooling vane",fan.localPosition,new Vector3(.64f,.07f,.06f),brass);
            vane.SetParent(fan,true);vane.localRotation=Quaternion.Euler(0,0,60*i);
        }
        Box("Service pipe",new Vector3(.75f,.34f,-1.54f),new Vector3(.18f,.18f,3.1f),brass);
        Box("Pump manifold",new Vector3(0,.34f,.03f),new Vector3(1.6f,.18f,.18f),brass);
        sc.Stream=Shape("Tap water",PrimitiveType.Cylinder,Vector3.zero,new Vector3(.055f,.16f,.055f),water).gameObject;
        sc.Stream.transform.position=sc.Tap.GetComponent<Renderer>().bounds.center+new Vector3(0,-.18f,-.12f);
        sc.Stream.SetActive(false);
    }

    private static void MakePatrol(BadShiftNightTraffic street)
    {
        street.Beams=street.Car.GetComponentsInChildren<Light>(true);
        street.HeadlampFaces=street.Car.GetComponentsInChildren<Renderer>(true).Where(x=>x.name=="Car headlight").ToArray();
        var marker=Mat("Parking lamp",new Color(1,.81f,.36f),new Color(1,.61f,.17f));
        foreach(var face in street.HeadlampFaces)
        {
            var t=Box("Front position lamp",Vector3.zero,new Vector3(.106f,.09f,.05f),marker);
            t.SetParent(street.Suspension,true);t.position=face.bounds.center+street.Car.rotation*new Vector3(.076f,0,-.046f);
        }
        var bar=new GameObject("Patrol lightbar").transform;bar.SetParent(street.Suspension,false);bar.localPosition=new Vector3(0,1.69f,.15f);
        street.EmergencyBar=bar.gameObject;
        foreach(bool blue in new[]{false,true})
        {
            var col=blue?new Color(.055f,.31f,1):new Color(1,.045f,.025f);
            var lens=Box(blue?"Blue beacon":"Red beacon",Vector3.zero,new Vector3(.41f,.22f,.30f),Mat(blue?"Beacon blue":"Beacon red",col,col*3));
            lens.SetParent(bar,true);lens.localPosition=new Vector3(blue?.23f:-.23f,0,0);
            if(blue)street.Blue=lens.GetComponent<Renderer>();else street.Red=lens.GetComponent<Renderer>();
        }
        bar.gameObject.SetActive(false);
    }

    private static void CompleteYard(BadShiftSoundscape sc)
    {
        yard=sc.Pump.parent;
        yard.position=new Vector3(-2.8f,0,7.25f);
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        brass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"Valve brass.mat");
        var dial=Mat("Pressure gauge ivory",new Color(.94f,.88f,.68f));
        var gauge=Shape("Pressure gauge face",PrimitiveType.Cylinder,new Vector3(.75f,1.68f,-.18f),new Vector3(.52f,.065f,.52f),dial);
        gauge.localRotation=Quaternion.Euler(90,0,0);
        sc.Gauge=Box("Pressure gauge needle",new Vector3(.75f,1.68f,-.26f),new Vector3(.045f,.33f,.028f),brass);
        sc.BowlWater=GameObject.Find("Toilet water").transform;
        sc.FlushButton=GameObject.Find("Flush button").transform;
        sc.Street.PatrolPaint=Mat("Patrol enamel",new Color(.80f,.85f,.87f));
    }

    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+name+".wav")??throw new InvalidOperationException(name);
    private static SoundEvent Bed(string name,string source,SoundEvent template)
    {
        var sound=U.Instantiate(template);sound.Clips=new[]{Clip(source)};sound.Volume=.30f;
        AssetDatabase.CreateAsset(sound,Folder+name+".asset");return sound;
    }
    private static SoundEvent Event(string name,string source,bool loop,float far,float volume)
    {
        var preset=AttenuationPreset.CreateRuntime(1,1.3f,far,AudioRolloffMode.Linear);
        preset.DopplerLevel=0;
        AssetDatabase.CreateAsset(preset,Folder+name+"_Attenuation.asset");
        var sound=SoundEvent.CreateRuntime(Clip(source));sound.Loop=loop;sound.AttenuationPreset=preset;sound.MixerGroup=bus;sound.Volume=volume;
        AssetDatabase.CreateAsset(sound,Folder+name+".asset");return sound;
    }
    private static Material Mat(string name,Color color,Color emission=default)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",color);
        if(emission.maxColorComponent>0){m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;m.SetColor("_EmissionColor",emission);m.EnableKeyword("_EMISSION");}
        AssetDatabase.CreateAsset(m,Folder+name+".mat");return m;
    }
    private static Transform Box(string name,Vector3 p,Vector3 size,Material mat)
    {
        var t=new GameObject(name).transform;t.SetParent(yard,false);t.localPosition=p;t.localScale=size;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=cube;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;
    }
    private static Transform Shape(string name,PrimitiveType type,Vector3 p,Vector3 size,Material mat)
    {
        var o=GameObject.CreatePrimitive(type);U.DestroyImmediate(o.GetComponent<Collider>());o.name=name;
        o.transform.SetParent(yard,false);o.transform.localPosition=p;o.transform.localScale=size;o.GetComponent<Renderer>().sharedMaterial=mat;return o.transform;
    }
}
