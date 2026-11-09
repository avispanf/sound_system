using System;
using System.IO;
using System.Linq;
using AudioMW;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class BattleArenaPass
{
    private const string Root="Assets/BadShift/Battle";
    private static Light Lamp(string name,Vector3 position,Color color,float range,Transform parent=null)
    {
        var go=new GameObject(name);if(parent!=null)go.transform.SetParent(parent,false);go.transform.localPosition=position;
        var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.range=range;light.intensity=0;return light;
    }
    private static SoundEvent Sound(string name,bool spatial,SoundEvent route)
    {
        var item=SoundEvent.CreateRuntime(AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/"+name+".wav"));item.name="Battle_"+name;
        item.MixerGroup=route.MixerGroup;item.SpatialBlend=spatial?1:0;item.Volume=spatial?.45f:.35f;item.MinDistance=2;item.MaxDistance=18;
        AssetDatabase.CreateAsset(item,Root+"/"+item.name+".asset");return item;
    }
    private static ParticleSystem Mist(Vector3 position,Material material,Color color)
    {
        var go=new GameObject("Arena low smoke");go.transform.position=position;go.transform.rotation=Quaternion.Euler(-90,0,0);
        var ps=go.AddComponent<ParticleSystem>();var main=ps.main;main.startLifetime=3;main.startSpeed=.15f;main.startSize=new ParticleSystem.MinMaxCurve(.8f,1.6f);main.startColor=color;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=100;
        var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Circle;shape.radius=1.6f;
        var emission=ps.emission;emission.rateOverTime=9;emission.enabled=false;
        var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.28f,.2f),new GradientAlphaKey(0,1)});fade.color=gradient;
        ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;return ps;
    }
    public static void Refine()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var battle=UnityEngine.Object.FindFirstObjectByType<BadShiftBattle>();
        if(battle==null)throw new Exception("Battle arena missing");
        battle.EnemyBody.height=2.6f;battle.EnemyBody.center=Vector3.up*1.3f;battle.EnemyBody.minMoveDistance=0;
        var contacts=battle.GetComponent<BadShiftWeaponContact>();if(contacts==null)contacts=battle.gameObject.AddComponent<BadShiftWeaponContact>();contacts.Battle=battle;
        var input=battle.GetComponent<BadShiftBattleInput>();if(input==null)input=battle.gameObject.AddComponent<BadShiftBattleInput>();input.Battle=battle;
        if(battle.BlockSound==null){battle.BlockSound=Sound("Block",true,battle.Life.Demo.Game.Impact);battle.Life.Demo.Game.Bank.Events=battle.Life.Demo.Game.Bank.Events.Concat(new[]{battle.BlockSound}).ToArray();EditorUtility.SetDirty(battle.Life.Demo.Game.Bank);}
        var music=battle.Life.Demo.Game.Impact.MixerGroup.audioMixer.FindMatchingGroups("Music").First(g=>g.name=="Music");
        battle.VictorySound.MixerGroup=music;battle.DefeatSound.MixerGroup=music;EditorUtility.SetDirty(battle.VictorySound);EditorUtility.SetDirty(battle.DefeatSound);EditorUtility.SetDirty(battle);AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(battle.gameObject.scene);EditorSceneManager.SaveScene(battle.gameObject.scene);EditorApplication.Exit(0);
    }
    public static void Run()
    {
        try
        {
            if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");var demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
            if(demo.GetComponent<BadShiftBattle>()!=null)throw new Exception("Battle already authored");
            var pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;if(pipeline==null)throw new Exception("URP required");
            var camera=demo.Game.View.GetUniversalAdditionalCameraData();if(!pipeline.supportsHDR||!camera.renderPostProcessing)throw new Exception("HDR and camera post-processing required");camera.volumeLayerMask|=1;
            Vector3 center=new Vector3(7,.12f,-5);
            GameObject oldCollision=GameObject.Find("Employee collision");if(oldCollision!=null)UnityEngine.Object.DestroyImmediate(oldCollision);
            var body=new GameObject("Breadstick Warden controller");body.transform.position=center;
            var cc=body.AddComponent<CharacterController>();cc.minMoveDistance=0;cc.height=2.6f;cc.center=Vector3.up*1.3f;cc.radius=.32f;cc.stepOffset=.2f;cc.skinWidth=.035f;
            demo.Dancer.position=center;demo.Dancer.SetParent(body.transform,true);
            var stage=GameObject.Find("Rave stage");stage.transform.position=new Vector3(7,.035f,-5);stage.transform.localScale=new Vector3(2.1f,.07f,2.1f);
            demo.RaveLight.transform.position=center+Vector3.up*3.3f;
            var enemyController=demo.DanceAnimator.runtimeAnimatorController as AnimatorController;
            var playerController=demo.PlayerAnimator.runtimeAnimatorController as AnimatorController;
            if(!enemyController.layers[0].stateMachine.states.Any(s=>s.state.name=="Walk"))enemyController.layers[0].stateMachine.AddState("Walk").motion=playerController.layers[0].stateMachine.states.First(s=>s.state.name=="Walk").state.motion;
            EditorUtility.SetDirty(enemyController);
            var battle=demo.gameObject.AddComponent<BadShiftBattle>();battle.Life=demo.Game.Life;battle.Life.Battle=battle;battle.EnemyBody=cc;battle.ArenaCenter=center;
            demo.gameObject.AddComponent<BadShiftWeaponContact>().Battle=battle;demo.gameObject.AddComponent<BadShiftBattleInput>().Battle=battle;
            battle.BlockSound=Sound("Block",true,demo.Game.Impact);
            battle.AttackSound=Sound("Attack",true,demo.Game.Impact);battle.HitSound=Sound("Hit",true,demo.Game.Impact);battle.VictorySound=Sound("Victory",false,demo.Game.Impact);battle.DefeatSound=Sound("Defeat",false,demo.Game.Impact);battle.ChallengeSound=Sound("Challenge",false,demo.Game.Impact);
            demo.Game.Bank.Events=demo.Game.Bank.Events.Concat(new[]{battle.AttackSound,battle.HitSound,battle.VictorySound,battle.DefeatSound,battle.ChallengeSound,battle.BlockSound}).ToArray();EditorUtility.SetDirty(demo.Game.Bank);
            var musicGroup=demo.Game.Impact.MixerGroup.audioMixer.FindMatchingGroups("Music").First(g=>g.name=="Music");battle.VictorySound.MixerGroup=musicGroup;battle.DefeatSound.MixerGroup=musicGroup;EditorUtility.SetDirty(battle.VictorySound);EditorUtility.SetDirty(battle.DefeatSound);
            var presentation=demo.gameObject.AddComponent<BadShiftBattlePresentation>();presentation.Battle=battle;
            var profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"/BattleGrade.asset");
            var color=profile.Add<ColorAdjustments>(true);color.postExposure.value=-.3f;color.contrast.value=18;color.saturation.value=-12;
            var vignette=profile.Add<Vignette>(true);vignette.intensity.value=.32f;vignette.smoothness.value=.6f;vignette.color.value=new Color(.02f,.025f,.06f);
            var bloom=profile.Add<Bloom>(true);bloom.intensity.value=.65f;bloom.threshold.value=.9f;
            foreach(var component in profile.components)AssetDatabase.AddObjectToAsset(component,profile);
            var volume=new GameObject("Battle color grade").AddComponent<Volume>();volume.isGlobal=true;volume.priority=20;volume.weight=0;volume.sharedProfile=profile;presentation.Grade=volume;
            presentation.RedLight=Lamp("Arena crimson",center+new Vector3(-2,3,-1),new Color(1,.055f,.12f),10);
            presentation.BlueLight=Lamp("Arena cobalt",center+new Vector3(2,3,1),new Color(.08f,.25f,1),10);
            presentation.PlayerHitLight=Lamp("Player hit light",Vector3.up*1.4f,new Color(1,.3f,.15f),3,demo.Game.transform);
            presentation.EnemyHitLight=Lamp("Enemy hit light",Vector3.up*1.4f,new Color(1,.3f,.15f),3,body.transform);
            var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float radius=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Max(0,1-radius),2)));}texture.Apply();File.WriteAllBytes(Root+"/Smoke.png",texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.Refresh();
            var mistMaterial=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));mistMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Smoke.png"));mistMaterial.SetFloat("_Surface",1);mistMaterial.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mistMaterial.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mistMaterial.SetFloat("_ZWrite",0);mistMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mistMaterial.renderQueue=3000;AssetDatabase.CreateAsset(mistMaterial,Root+"/Smoke.mat");
            presentation.Smoke=new[]{Mist(center+new Vector3(-1,.05f,0),mistMaterial,new Color(.9f,.14f,.2f)),Mist(center+new Vector3(1,.05f,0),mistMaterial,new Color(.1f,.3f,1))};
            var ring=new GameObject("Arena light ring").AddComponent<LineRenderer>();ring.positionCount=80;ring.loop=true;ring.widthMultiplier=.035f;ring.useWorldSpace=true;
            for(int i=0;i<80;i++){float angle=i*Mathf.PI*2/80;ring.SetPosition(i,new Vector3(center.x+Mathf.Cos(angle)*2.7f,.09f,center.z+Mathf.Sin(angle)*2.7f));}ring.sharedMaterial=mistMaterial;ring.startColor=ring.endColor=Color.clear;presentation.Ring=ring;
            EditorUtility.SetDirty(demo);EditorUtility.SetDirty(battle.Life);EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();
            Debug.Log("Battle arena authored; URP HDR and camera post-processing verified; global layer 0, priority 20, overrides enabled");EditorApplication.Exit(0);
        }
        catch(Exception error){Debug.LogException(error);EditorApplication.Exit(1);}
    }
}
