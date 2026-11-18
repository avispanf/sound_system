using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public static class BadShiftNightPass
{
    private const string Folder = "Assets/BadShift/Night";
    private const string ScenePath = "Assets/Playground/RelayField.unity";
    private static Transform street;
    private static Mesh cube;

    public static void Run()
    {
        if (!Application.isBatchMode || !File.Exists(Path.Combine(Application.dataPath, "../.bad-shift-demo")))
            throw new InvalidOperationException("Use the isolated BAD SHIFT authoring project in batch mode.");
        var scene = EditorSceneManager.OpenScene(ScenePath);
        if (Object.FindFirstObjectByType<BadShiftNightTraffic>() != null) return;
        cube = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        if (cube == null) throw new InvalidOperationException("Reference geometry missing.");
        street = new GameObject("Night street").transform;
        street.position = new Vector3(-16.75f, 0, 0);
        var traffic = street.gameObject.AddComponent<BadShiftNightTraffic>();
        var asphalt = GameObject.Find("Road shoulder").transform;
        var roadMaterial = asphalt.GetComponent<Renderer>().sharedMaterial;
        var delta = new Vector3(street.position.x - asphalt.position.x, 0, 0);
        asphalt.position += delta;
        asphalt.localScale = new Vector3(3.7f,.3f,24);
        foreach (var dash in Named("Road dashed line")) dash.position += delta;
        var grass = Material("Moonlit grass", new Color(.22f,.38f,.22f));
        var stone = Material("Warm curb", new Color(.47f,.46f,.43f));
        Box("Cafeteria grass verge", new Vector3(-13.52f,-.09f,0),new Vector3(2.85f,.3f,20),grass);
        Box("Street curb",new Vector3(-14.98f,.01f,0),new Vector3(.24f,.36f,20),stone);
        foreach (var z in new[]{-10.25f,10.25f})
        {
            Box("Underpass roof", new Vector3(street.position.x,2.55f,z),new Vector3(3.75f,.35f,3.6f),roadMaterial);
            foreach (var side in new[]{-1.77f,1.77f})
                Box("Underpass pier",new Vector3(street.position.x+side,1.16f,z),new Vector3(.2f,2.4f,3.6f),stone);
        }
        MakeCar(traffic);
        MakeWindows();
        var coral = Material("Coral neon enamel",new Color(.71f,.12f,.08f),new Color(.73f,.068f,.026f));
        var tube = Material("Porcelain neon tube",new Color(1,.82f,.58f),new Color(1,.72f,.43f)*4);
        GameObject.Find("Cafeteria coral sign").GetComponent<Renderer>().sharedMaterial = coral;
        foreach (var name in new[]{"Coffee cup emblem","Cup handle","Coffee steam"})
            foreach (var piece in Named(name)) piece.GetComponent<Renderer>().sharedMaterial = tube;
        var sign = GameObject.Find("Cafeteria coral sign").transform;
        var band = GameObject.Find("Cafe sign cream band").GetComponent<Renderer>();
        band.sharedMaterial = Material("Sign ink panel",new Color(.14f,.08f,.09f));
        GameObject.Find("Cafe lettering").GetComponent<TextMesh>().color = new Color(1,.90f,.71f);
        var plaque=band.bounds;
        foreach (float y in new[]{plaque.min.y+.04f,plaque.max.y-.04f})
            Box("Neon sign trim",new Vector3(plaque.center.x,y,plaque.min.z-.016f),new Vector3(plaque.size.x-.14f,.035f,.04f),tube);
        traffic.Neon = LightAt("Sign transformer and window spill",sign.position + new Vector3(.64f,-.8f,0),
            LightType.Point,new Color(1,.33f,.17f),9,9).transform;
        traffic.Neon.GetComponent<Light>().shadows = LightShadows.Soft;
        LightAt("Kitchen task light",new Vector3(6,4,1),LightType.Point,new Color(1,.72f,.46f),5,9);
        LightAt("Dining table light",new Vector3(-6,4,-1),LightType.Point,new Color(1,.75f,.55f),3,7);
        var spill=LightAt("Neon through dining window",sign.position+new Vector3(.48f,-.68f,.38f),LightType.Spot,new Color(1,.27f,.13f),13,10);
        spill.transform.rotation=Quaternion.LookRotation(new Vector3(1,-.32f,.12f));
        spill.spotAngle=77; spill.innerSpotAngle=38; spill.shadows=LightShadows.Soft;
        ConfigureNight();
        ConfigureDaylight();
        var bus = AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/BadShift/Fridge.asset").MixerGroup;
        traffic.EngineEvent = Loop("Traffic_Motor", "PassingMotor", 2, 24, 2.1f, .68f, bus);
        traffic.NeonEvent = Loop("Window_Neon_Hum", "NeonTransformer", 1.4f, 8, 0, .45f, bus);
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException(ScenePath);
        AssetDatabase.SaveAssets();
    }

    private static void MakeCar(BadShiftNightTraffic traffic)
    {
        var body = GameObject.Find("Lilac car body").transform;
        var origin = new Vector3(body.position.x,0,body.position.z);
        var car = new GameObject("Passing traffic car").transform;
        car.SetParent(street,false);
        car.position = origin;
        var sprung = new GameObject("Clay suspension").transform;
        sprung.SetParent(car,false);
        traffic.Car = car;
        traffic.Suspension = sprung;
        var cabin = GameObject.Find("Lilac car cabin").transform;
        traffic.Paint = new[]{body.GetComponent<Renderer>(), cabin.GetComponent<Renderer>()};
        traffic.Finishes = new[]{traffic.Paint[0].sharedMaterial, Material("Apricot car",new Color(.96f,.36f,.12f)),
            Material("Lagoon car",new Color(.15f,.68f,.62f)), Material("Butter car",new Color(.92f,.72f,.20f))};
        foreach (var name in new[]{"Lilac car body","Lilac car cabin","Car windshield","Car headlight","Car tyre"})
            foreach (var part in Named(name)) part.SetParent(name == "Car tyre" ? car : sprung,true);
        var lamp = Material("Headlamp glass",Color.white,new Color(1,.86f,.59f)*4);
        var tail = Material("Rear lamps",Color.red,new Color(1,.055f,.02f)*2.8f);
        var glass = Material("Car side windows",new Color(.13f,.24f,.34f));
        foreach (var head in Named("Car headlight"))
        {
            head.GetComponent<Renderer>().sharedMaterial = lamp;
            var beam = LightAt("Low beam",head.position+Vector3.back*.09f,LightType.Spot,new Color(1,.84f,.61f),9,9);
            beam.spotAngle = 56; beam.innerSpotAngle = 26;
            beam.transform.rotation = Quaternion.LookRotation(new Vector3(0,-.17f,-1));
            beam.transform.SetParent(sprung,true);
        }
        foreach (var side in new[]{-1f,1f})
        {
            Box("Rear lamp",origin+new Vector3(side*.57f,.64f,1.51f),new Vector3(.28f,.14f,.055f),tail).SetParent(sprung,true);
            Box("Side glazing",origin+new Vector3(side*.756f,1.25f,.2f),new Vector3(.035f,.43f,1.07f),glass).SetParent(sprung,true);
        }
        traffic.RoofRack=Box("Courier roof box",origin+new Vector3(0,1.69f,.18f),new Vector3(.99f,.26f,1.35f),glass).gameObject;
        traffic.TaxiLamp=Box("Taxi roof lamp",origin+new Vector3(0,1.68f,.12f),new Vector3(.60f,.25f,.33f),lamp).gameObject;
        traffic.RoofRack.transform.SetParent(sprung,true);
        traffic.TaxiLamp.transform.SetParent(sprung,true);
        traffic.RoofRack.SetActive(false);
        traffic.TaxiLamp.SetActive(false);
        car.localPosition = new Vector3(.22f,0,4.8f);
    }

    private static void MakeWindows()
    {
        var wall = GameObject.Find("Left cafe wall").GetComponent<Renderer>();
        var plaster = wall.sharedMaterial;
        wall.enabled = false;
        var dado = GameObject.Find("Left coral dado").transform;
        dado.localScale = new Vector3(.1f,1.44f,14);
        dado.position = new Vector3(dado.position.x,.72f,dado.position.z);
        var frame = GameObject.Find("Outer cafe window frame").GetComponent<Renderer>().sharedMaterial;
        var pane = Material("Transparent night glass",new Color(.39f,.71f,.83f,.085f));
        pane.SetFloat("_Surface",1); pane.SetFloat("_ZWrite",0);
        pane.SetFloat("_SrcBlend",(int)BlendMode.SrcAlpha); pane.SetFloat("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
        pane.SetOverrideTag("RenderType","Transparent"); pane.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        pane.renderQueue = (int)RenderQueue.Transparent;
        foreach (var name in new[]{"Cafe window","Window cream inset","Outer cafe window frame","Outer cafe glass"})
            foreach (var item in Named(name)) item.GetComponent<Renderer>().enabled=false;
        Box("Cafe sill wall",new Vector3(-12,.72f,3),new Vector3(.3f,1.44f,14),plaster);
        Box("Cafe lintel",new Vector3(-12,3.01f,3),new Vector3(.3f,.38f,14),plaster);
        float previous = -4;
        foreach (float z in new[]{-1f,1.6f,4.2f})
        {
            float edge = z - 1.02f;
            Box("Window pier",new Vector3(-12,2.13f,(previous+edge)*.5f),new Vector3(.3f,1.38f,edge-previous),plaster);
            previous=z+1.02f;
            foreach(float y in new[]{1.48f,2.78f}) Box("Window rail",new Vector3(-12,y,z),new Vector3(.37f,.08f,2.11f),frame);
            foreach(float offset in new[]{-1f,1f}) Box("Window jamb",new Vector3(-12,2.13f,z+offset),new Vector3(.37f,1.3f,.08f),frame);
            var glass = Box("Clear cafe pane",new Vector3(-12,2.13f,z),new Vector3(.025f,1.23f,1.94f),pane).GetComponent<Renderer>();
            glass.shadowCastingMode = ShadowCastingMode.Off;
        }
        Box("End wall pier",new Vector3(-12,2.13f,(previous+10)*.5f),new Vector3(.3f,1.38f,10-previous),plaster);
    }

    private static void ConfigureNight()
    {
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type != LightType.Directional) continue;
            light.intensity=light.name=="Directional Light"?.38f:.13f;
            light.color=new Color(.63f,.73f,1);
        }
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.19f,.24f,.38f);
        RenderSettings.ambientEquatorColor=new Color(.12f,.15f,.23f);
        RenderSettings.ambientGroundColor=new Color(.09f,.08f,.13f);
        var camera=Camera.main;
        camera.allowHDR=true; camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.075f,.09f,.155f);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
        var volume=new GameObject("Night neon bloom").AddComponent<Volume>();
        volume.isGlobal=true; volume.priority=2;
        var profile=ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile,Folder+"/NeonBloom.asset");
        var bloom=profile.Add<Bloom>(); bloom.threshold.Override(1.05f); bloom.intensity.Override(.34f); bloom.scatter.Override(.66f);
        AssetDatabase.AddObjectToAsset(bloom,profile); EditorUtility.SetDirty(profile);
        volume.sharedProfile=profile;
    }

    private static void ConfigureDaylight()
    {
        var demo=Object.FindFirstObjectByType<BadShiftDemo>();
        var sky=street.gameObject.AddComponent<BadShiftDaylight>();
        demo.Daylight=sky;
        sky.View=Camera.main;
        sky.Battle=Object.FindFirstObjectByType<BadShiftBattlePresentation>();
        sky.Sun=GameObject.Find("Directional Light").GetComponent<Light>();
        sky.Fill=GameObject.Find("Clay studio fill").GetComponent<Light>();
        sky.OfficeLights=new Light[2]; sky.OfficeDiffusers=new Renderer[2];
        var housing=Material("Office lamp housing",new Color(.19f,.22f,.26f));
        var diffuser=Material("Office lamp diffuser",new Color(.48f,.51f,.52f),Color.white*2.8f);
        for(int i=0;i<2;i++)
        {
            var pos=i==0?new Vector3(-5.9f,4.65f,3.7f):new Vector3(5.8f,4.65f,3.7f);
            Box("Slim cafe ceiling fixture",pos,new Vector3(2.85f,.12f,.32f),housing);
            sky.OfficeDiffusers[i]=Box("Neutral white diffuser",pos+Vector3.down*.075f,new Vector3(2.62f,.055f,.25f),diffuser).GetComponent<Renderer>();
            sky.OfficeLights[i]=LightAt("Office downlight",pos+Vector3.down*.15f,LightType.Spot,new Color(1,.985f,.965f),13,13);
            sky.OfficeLights[i].transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);
            sky.OfficeLights[i].spotAngle=119; sky.OfficeLights[i].innerSpotAngle=74;
        }
        var mesh=new Mesh { name="Sunlight through glass" };
        var verts=new Vector3[12];var uv=new Vector2[12];var indices=new int[18];
        for(int i=0;i<3;i++)
        {
            int n=i*4;float baseY=1.61f+i*.48f;
            verts[n]=new Vector3(0,baseY,-.94f);verts[n+1]=new Vector3(0,baseY,.94f);
            verts[n+2]=new Vector3(5.8f+i*.8f,.045f+i*.03f,-.57f);verts[n+3]=new Vector3(5.8f+i*.8f,.045f+i*.03f,-2.86f);
            uv[n]=Vector2.zero;uv[n+1]=Vector2.right;uv[n+2]=Vector2.one;uv[n+3]=Vector2.up;
            int t=i*6;indices[t]=n;indices[t+1]=n+1;indices[t+2]=n+2;indices[t+3]=n;indices[t+4]=n+2;indices[t+5]=n+3;
        }
        mesh.vertices=verts;mesh.uv=uv;mesh.triangles=indices;mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,Folder+"/WindowShaft.asset");
        var shaftMaterial=new Material(Shader.Find("BadShift/Window sunlight"));
        AssetDatabase.CreateAsset(shaftMaterial,Folder+"/SunRays.mat");
        sky.Shafts=new Renderer[3];
        for(int i=0;i<3;i++)
        {
            var t=new GameObject("Cafe window sunbeams").transform;t.SetParent(street,false);t.position=new Vector3(-11.8f,0,-1+i*2.6f);
            t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=t.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=shaftMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;sky.Shafts[i]=renderer;
        }
    }

    private static SoundEvent Loop(string name,string clipName,float near,float far,float doppler,float gain,UnityEngine.Audio.AudioMixerGroup bus)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/"+clipName+".wav");
        if (clip==null) throw new InvalidOperationException(clipName);
        var preset=AttenuationPreset.CreateRuntime(1,near,far,AudioRolloffMode.Linear);
        preset.DopplerLevel=doppler;
        AssetDatabase.CreateAsset(preset,Folder+"/"+name+"_Spatial.asset");
        var sound=SoundEvent.CreateRuntime(clip);
        sound.Loop=true; sound.AttenuationPreset=preset; sound.MixerGroup=bus; sound.Volume=gain;
        AssetDatabase.CreateAsset(sound,Folder+"/"+name+".asset");
        return sound;
    }

    private static Transform[] Named(string name) =>
        Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name==name).ToArray();

    private static Material Material(string name,Color color,Color emission=default)
    {
        var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor",color);
        material.SetFloat("_Smoothness",.27f);
        if(emission.maxColorComponent>0)
        {
            material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            material.SetColor("_EmissionColor",emission);
            material.EnableKeyword("_EMISSION");
        }
        AssetDatabase.CreateAsset(material,Folder+"/"+name+".mat");
        return material;
    }

    private static Transform Box(string name,Vector3 position,Vector3 scale,Material material)
    {
        var t=new GameObject(name).transform;
        t.SetParent(street,false); t.position=position; t.localScale=scale;
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=cube;
        t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
        return t;
    }

    private static Light LightAt(string name,Vector3 position,LightType type,Color color,float intensity,float range)
    {
        var light=new GameObject(name).AddComponent<Light>();
        light.transform.SetParent(street,false); light.transform.position=position;
        light.type=type; light.color=color; light.intensity=intensity; light.range=range;
        return light;
    }
}
