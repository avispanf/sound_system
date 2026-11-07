using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BuildAudioPlayground
{
    private const string Root = "Assets/Playground";
    public static void Run()
    {
        try
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            if (!File.Exists(Path.Combine(project, ".audiomw-playground"))) throw new Exception("Use the generated playground project.");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AudioSystem).Assembly);
            Sample sample = Sample.FindByPackage(package.name, package.version).Single();
            if (!sample.Import(Sample.ImportOptions.OverridePreviousImports)) throw new Exception("Demo sample import failed.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(Root + "/Audio"); Directory.CreateDirectory(Root + "/Data"); Directory.CreateDirectory(Root + "/Materials"); AssetDatabase.Refresh();
            foreach (string path in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Samples" }).Select(AssetDatabase.GUIDToAssetPath))
            {
                string destination = Root + "/Audio/" + Path.GetFileName(path);
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(destination) == null) AssetDatabase.CopyAsset(path, destination);
            }
            AssetDatabase.Refresh();
            Func<string, AudioClip> clip = name => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Audio/" + name + ".wav");
            SoundParameter surface = Save(SoundParameter.CreateRuntime(0, 2, 0), "Surface");
            SoundParameter intensity = Save(SoundParameter.CreateRuntime(0, 1, 0), "Intensity");
            SoundParameter duck = Save(SoundParameter.CreateRuntime(0, 1, 0), "VoiceDuck");
            SoundParameter fade = Save(SoundParameter.CreateRuntime(0, 1, 1), "AmbienceFade");
            AudioClip[] steps = { clip("SFX_Blip_A"), clip("SFX_Blip_B"), clip("SFX_Blip_C") };
            if (steps.Any(item => item == null)) throw new Exception("Placeholder audio is missing.");
            SoundEvent[] feet = new SoundEvent[3];
            string[] surfaces = { "Stone", "Grass", "Metal" };
            for (int i = 0; i < 3; i++)
            {
                feet[i] = Save(SoundEvent.CreateRuntime(steps), "Steps_" + surfaces[i]);
                feet[i].SelectionMode = ClipSelectionMode.ShuffleBag; feet[i].Volume = 0.22f; feet[i].Pitch = 0.65f + i * 0.3f;
                feet[i].VolumeVariationMode = VolumeVariationMode.Decibels; feet[i].VolumeRandomDbMin = -2; feet[i].VolumeRandomDbMax = 2;
                SerializedObject variation = new SerializedObject(feet[i]); variation.FindProperty("pitchRandomMin").floatValue = -0.06f; variation.FindProperty("pitchRandomMax").floatValue = 0.06f; variation.ApplyModifiedPropertiesWithoutUndo(); feet[i].MinDistance = 1.5f; feet[i].MaxDistance = 20;
            }
            SoundEvent footsteps = Save(SoundEvent.CreateRuntime(), "Footsteps_Switch"); footsteps.ContainerMode = ContainerMode.Switch; footsteps.SwitchParameter = surface;
            footsteps.Children = feet.Select((item, i) => SoundEventChild.Create(item, i)).ToArray(); footsteps.MaxInstancesPerEmitter = 2; footsteps.LimitPolicy = VoiceLimitPolicy.RejectNewest;
            SoundEvent landing = Save(SoundEvent.CreateRuntime(clip("SFX_Impact")), "Landing"); landing.Volume = 0.18f; landing.MinDistance = 1.5f;
            SoundEvent impact = Save(SoundEvent.CreateRuntime(clip("SFX_Impact")), "Impact"); impact.Volume = 0.35f; impact.MinDistance = 2; impact.MaxDistance = 18;
            SoundEvent pickup = Save(SoundEvent.CreateRuntime(steps[2]), "Pickup"); pickup.Volume = 0.2f; pickup.Pitch = 1.5f; pickup.SpatialBlend = 0;
            SoundEvent stress = Save(SoundEvent.CreateRuntime(steps[0]), "Limit_Test"); stress.Loop = true; stress.Volume = 0.08f; stress.SpatialBlend = 0;
            stress.MaxInstancesPerEmitter = 2; stress.LimitPolicy = VoiceLimitPolicy.RejectNewest;
            SoundEvent ambient = Ambient(clip("MUS_Pad"), "Ambience_Explore", duck, fade);
            SoundEvent danger = Ambient(clip("MUS_Pulse"), "Ambience_Danger", duck, fade);
            MusicTrack explore = Save(MusicTrack.CreateRuntime(clip("MUS_Arp"), clip("MUS_Pad"), 120, 4), "Explore"); explore.Volume = 0.18f;
            explore.Layers = new[] { MusicLayer.CreateRuntime("Pulse", clip("MUS_Pulse"), intensity, AnimationCurve.Linear(0, 0, 1, 0.7f)) };
            MusicTrack combat = Save(MusicTrack.CreateRuntime(null, clip("MUS_Pad"), 120, 4), "Combat"); combat.Volume = 0.18f;
            combat.Layers = new[] { MusicLayer.CreateRuntime("Pulse", clip("MUS_Pulse"), intensity, AnimationCurve.Linear(0, 0.3f, 1, 1)), MusicLayer.CreateRuntime("Bass", clip("MUS_Bass"), intensity, AnimationCurve.Linear(0, 0, 1, 1)) };
            MusicStateProfile music = Save(ScriptableObject.CreateInstance<MusicStateProfile>(), "Music_States");
            music.States = new[] { new MusicState { Name = "Explore", Track = explore }, new MusicState { Name = "Combat", Track = combat } };
            music.DefaultQuantization = MusicQuantization.Bar; music.DefaultFadeSeconds = 1; music.DefaultCurve = MusicFadeCurve.EqualPower;
            if (music.Validate() != "") throw new Exception(music.Validate());
            VoiceLine first = Save(VoiceLine.CreateRuntime(clip("VO_Line_A"), "Guide", "Synthetic VO cue: music and ambience should duck, then recover."), "Guide_A"); first.Volume = 0.35f;
            VoiceLine second = Save(VoiceLine.CreateRuntime(clip("VO_Line_B"), "Guide", "Synthetic VO cue queued. Watch Duck return to zero after the line."), "Guide_B"); second.Volume = 0.35f;
            SoundBank bank = Save(SoundBank.CreateRuntime(footsteps, landing, impact, pickup, stress, ambient, danger), "Playground_Bank");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            UnityEngine.Object.DestroyImmediate(camera.GetComponent<AudioListener>());
            Color[] colors = { new Color(0.38f,0.43f,0.5f), new Color(0.18f,0.44f,0.31f), new Color(0.25f,0.4f,0.58f) };
            for (int i = 0; i < 3; i++)
            {
                GameObject floor = Cube(surfaces[i], new Vector3(-8 + i * 8,-0.25f,0), new Vector3(8,0.5f,20), colors[i]);
                PlaygroundSurface zone = floor.AddComponent<PlaygroundSurface>(); zone.State = i; zone.Label = surfaces[i];
            }
            Cube("North wall",new Vector3(0,0.5f,10),new Vector3(24,1,0.4f),Color.gray);
            Cube("South wall",new Vector3(0,0.5f,-10),new Vector3(24,1,0.4f),Color.gray);
            Cube("West wall",new Vector3(-12,0.5f,0),new Vector3(0.4f,1,20),Color.gray);
            Cube("East wall",new Vector3(12,0.5f,0),new Vector3(0.4f,1,20),Color.gray);
            GameObject border = Cube("Danger zone boundary",new Vector3(0,0.015f,3.5f),new Vector3(24,0.02f,0.25f),new Color(0.9f,0.25f,0.2f)); UnityEngine.Object.DestroyImmediate(border.GetComponent<Collider>());
            Cube("Jump block",new Vector3(-3,0.4f,-3),new Vector3(2,0.8f,2),new Color(0.25f,0.25f,0.3f));
            GameObject player = new GameObject("Player — AudioMW integration"); player.layer = 2; player.transform.position = new Vector3(0,0.2f,-8);
            CharacterController controller = player.AddComponent<CharacterController>(); controller.minMoveDistance = 0; controller.height = 1.8f; controller.radius = 0.35f; controller.center = Vector3.up * 0.9f;
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.name = "Player body"; body.layer = 2; body.transform.SetParent(player.transform,false); body.transform.localPosition = Vector3.up * 0.9f; body.transform.localScale = new Vector3(0.65f,0.9f,0.65f); UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            body.GetComponent<Renderer>().sharedMaterial = Material("Player",new Color(0.9f,0.93f,1));
            GameObject listener = new GameObject("Player ears"); listener.transform.SetParent(player.transform,false); listener.transform.localPosition = Vector3.up * 1.5f; listener.AddComponent<AudioListener>();
            AudioMWPlayground game = player.AddComponent<AudioMWPlayground>();
            game.Footsteps = footsteps; game.Landing = landing; game.Impact = impact; game.Pickup = pickup; game.StressLoop = stress; game.Ambience = ambient; game.DangerAmbience = danger; game.AmbienceFade = fade;
            game.Listener = listener.transform;
            game.Surface = surface; game.Intensity = intensity; game.VoiceDuck = duck; game.Bank = bank; game.Music = music; game.Lines = new[] { first,second }; game.Stinger = clip("MUS_Stinger"); game.View = camera;
            Vector3[] positions = { new Vector3(-9,0.9f,-5),new Vector3(0,0.9f,-2),new Vector3(9,0.9f,0),new Vector3(-7,0.9f,6),new Vector3(7,0.9f,6) };
            game.Pickups = new PlaygroundPickup[positions.Length];
            for (int i = 0; i < positions.Length; i++) game.Pickups[i] = Cube("Gold relay " + (i+1),positions[i],Vector3.one*0.65f,new Color(1,0.7f,0.15f)).AddComponent<PlaygroundPickup>();
            GameObject exit = Cube("Exit — collect all relays",new Vector3(10,0.08f,7),new Vector3(2,0.15f,2),Color.cyan); UnityEngine.Object.DestroyImmediate(exit.GetComponent<Collider>()); game.Exit = exit.transform;
            camera.transform.position = player.transform.position + new Vector3(0,14,-12); camera.transform.LookAt(player.transform.position);
            EditorSceneManager.SaveScene(scene,Root + "/RelayField.unity"); AssetDatabase.SaveAssets();
            PlayerSettings.companyName = "AudioMW"; PlayerSettings.productName = "AudioMW Relay Field";
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Root + "/RelayField.unity",true) };
            var audit = AudioMW.Editor.AudioLibraryAudit.Scan(Root);
            File.WriteAllText(Path.Combine(project,"playground-setup.json"),JsonUtility.ToJson(audit,true));
            if(audit.Errors > 0) throw new Exception("Playground library audit failed.");
            EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    public static void BuildPlayer()
    {
        try
        {
            string project = Path.GetDirectoryName(Application.dataPath);
            if (!File.Exists(Path.Combine(project, ".audiomw-playground"))) throw new Exception("Use the generated playground project.");
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Root + "/RelayField.unity" },
                locationPathName = Path.Combine(project, "Build/RelayField.app"),
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            Debug.Log("Relay Field build: " + report.summary.result + " · " + report.summary.totalErrors + " errors");
            EditorApplication.Exit(report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    private static SoundEvent Ambient(AudioClip clip,string name,SoundParameter duck,SoundParameter fade)
    {
        SoundEvent item = Save(SoundEvent.CreateRuntime(clip),name); item.Loop = true; item.SpatialBlend = 0; item.Volume = 0.055f;
        item.ParameterBindings = new[] { ParameterBinding.CreateRuntime(duck,ParameterTarget.Volume,AnimationCurve.Linear(0,1,1,0.25f)), ParameterBinding.CreateRuntime(fade,ParameterTarget.Volume,AnimationCurve.Linear(0,0,1,1)) }; return item;
    }
    private static T Save<T>(T asset,string name) where T:UnityEngine.Object
    {
        string path = Root + "/Data/" + name + ".asset"; if(AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
        asset.name = name; AssetDatabase.CreateAsset(asset,path); EditorUtility.SetDirty(asset); return asset;
    }
    private static Material Material(string name,Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
        material.color = color; EditorUtility.SetDirty(material); return material;
    }
    private static GameObject Cube(string name,Vector3 position,Vector3 scale,Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = Material(name,color); return go;
    }
}
