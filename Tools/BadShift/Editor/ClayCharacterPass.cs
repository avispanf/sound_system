using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class ClayCharacterPass
{
    private static BadShiftDemo Open()
    {
        if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");return UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
    }
    public static void Inspect()
    {
        var d=Open();var lines=new List<string>();
        foreach(var actor in new[]{d.PlayerAnimator,d.DanceAnimator})
        {
            lines.Add("ACTOR "+actor.name+" "+actor.transform.position+" scale "+actor.transform.lossyScale);
            foreach(var t in actor.GetComponentsInChildren<Transform>())lines.Add(t.name+" p "+t.position+" local "+t.localPosition+" rot "+t.eulerAngles);
            foreach(var r in actor.GetComponentsInChildren<Renderer>())lines.Add("RENDER "+r.name+" "+r.GetType().Name+" "+r.bounds);
        }
        File.WriteAllLines("clay-inspect.txt",lines);EditorApplication.Exit(0);
    }

    private static Mesh soft;
    private static Material bone,ink,mint;
    private static Transform Part(Transform parent,string name,Vector3 position,Vector3 size,Material material)
    {
        var g=new GameObject(name);g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=size;
        g.AddComponent<MeshFilter>().sharedMesh=soft;g.AddComponent<MeshRenderer>().sharedMaterial=material;return g.transform;
    }
    private static Material Material(string name,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=color;m.SetFloat("_Smoothness",.12f);AssetDatabase.CreateAsset(m,"Assets/BadShift/ClayActors/"+name+".mat");return m;
    }
    private static void Actor(BadShiftDemo demo,Animator animator,bool employee)
    {
        if(PrefabUtility.IsPartOfPrefabInstance(animator))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(animator),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        var bones=animator.GetComponentsInChildren<Transform>();
        Transform Find(string name)=>bones.First(t=>t.name==name);
        foreach(var r in animator.GetComponentsInChildren<SkinnedMeshRenderer>())r.enabled=false;
        foreach(var t in bones.Where(t=>t.name.StartsWith("Apron")).ToArray())if(t!=null)UnityEngine.Object.DestroyImmediate(t.gameObject);
        var head=new GameObject("Clay face pivot").transform;head.SetParent(Find("head"),false);
        Part(head,"Clay skull",new Vector3(0,.39f,0),new Vector3(.78f,.67f,.66f),bone);
        Part(head,"Clay jaw",new Vector3(0,.1f,.09f),new Vector3(.46f,.23f,.45f),bone);
        var eyes=new List<Transform>();
        foreach(float x in new[]{-.16f,.16f})eyes.Add(Part(head,"Oval ink eye",new Vector3(x,.40f,.335f),new Vector3(.115f,.21f,.055f),ink));
        Part(Find("chest"),"Clay neck",new Vector3(0,.18f,0),new Vector3(.14f,.28f,.14f),bone);
        for(int i=0;i<3;i++)Part(Find("chest"),"Clay rib",new Vector3(0,-.03f-i*.13f,0),new Vector3(.52f-i*.035f,.075f,.27f),bone);
        Part(Find("spine"),"Clay spine",new Vector3(0,.15f,0),new Vector3(.12f,.4f,.12f),bone);
        Part(Find("hips"),"Clay pelvis",Vector3.zero,new Vector3(.42f,.17f,.24f),bone);
        foreach(string side in new[]{"l","r"})
        {
            foreach(var pair in new[]{new[]{"upperarm.","lowerarm."},new[]{"lowerarm.","wrist."},new[]{"upperleg.","lowerleg."},new[]{"lowerleg.","foot."}})
            {
                Transform a=Find(pair[0]+side),b=Find(pair[1]+side);Vector3 delta=a.InverseTransformPoint(b.position);
                var limb=Part(a,"Clay limb",delta*.5f,new Vector3(.12f,delta.magnitude,.12f),bone);limb.localRotation=Quaternion.FromToRotation(Vector3.up,delta.normalized);
                Part(a,"Clay joint",Vector3.zero,Vector3.one*.16f,bone);
            }
            Part(Find("hand."+side),"Clay mitten",new Vector3(0,.04f,0),new Vector3(.18f,.20f,.14f),bone);
            var foot=Part(Find("foot."+side),"Clay shoe",Vector3.zero,new Vector3(.22f,.12f,.38f),bone);foot.rotation=animator.transform.rotation;foot.position=Find("toes."+side).position+Vector3.up*.015f;
        }
        Part(Find("chest"),"Apron bib",new Vector3(0,-.06f,.19f),new Vector3(.49f,.45f,.075f),mint);
        Part(Find("hips"),"Apron skirt",new Vector3(0,.13f,.20f),new Vector3(.62f,.39f,.075f),mint);
        foreach(float x in new[]{-.18f,.18f})
        {
            Part(Find("chest"),"Apron strap",new Vector3(x,.19f,.15f),new Vector3(.075f,.22f,.08f),mint);
            Part(Find("chest"),"Apron button",new Vector3(x,.08f,.24f),Vector3.one*.05f,bone);
        }
        var face=animator.gameObject.AddComponent<BadShiftClayFace>();face.Demo=demo;face.Head=head;face.Eyes=eyes.ToArray();face.Employee=employee;
        animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    }
    public static void Run()
    {
        var d=Open();if(UnityEngine.Object.FindFirstObjectByType<BadShiftClayFace>()!=null)throw new Exception("Preserve authored clay actors");
        AssetDatabase.CreateFolder("Assets/BadShift","ClayActors");soft=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        bone=Material("Cream clay",new Color(.94f,.85f,.71f));ink=Material("Ink eyes",new Color(.075f,.085f,.10f));mint=Material("Sage apron",new Color(.43f,.67f,.51f));
        Actor(d,d.PlayerAnimator,false);Actor(d,d.DanceAnimator,true);
        if(GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)throw new Exception("URP required");
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.66f,.71f,.77f);RenderSettings.ambientEquatorColor=new Color(.58f,.57f,.54f);RenderSettings.ambientGroundColor=new Color(.37f,.33f,.29f);
        foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))if(light.type==LightType.Directional){light.intensity=1.05f;light.color=new Color(1,.91f,.80f);light.shadows=LightShadows.Soft;light.shadowStrength=.48f;light.shadowBias=.035f;}
        var fill=new GameObject("Clay studio fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.32f;fill.color=new Color(.79f,.86f,1);fill.shadows=LightShadows.None;fill.transform.rotation=Quaternion.Euler(35,-120,0);
        EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
    public static void FinishShapes()
    {
        var d=Open();var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        var rounded=UnityEngine.Object.Instantiate(source);rounded.name="Sculpted clay bevel";var v=rounded.vertices;var n=rounded.normals;
        for(int i=0;i<v.Length;i++)v[i]=(v[i]-n[i]*.08f)*(.32f/.42f)+n[i]*.18f;
        rounded.vertices=v;rounded.RecalculateBounds();AssetDatabase.CreateAsset(rounded,"Assets/BadShift/ClayActors/Sculpted.asset");
        var sphere=GameObject.CreatePrimitive(PrimitiveType.Sphere);Mesh eye=sphere.GetComponent<MeshFilter>().sharedMesh;
        foreach(var actor in new[]{d.PlayerAnimator,d.DanceAnimator})foreach(var f in actor.GetComponentsInChildren<MeshFilter>())f.sharedMesh=f.name=="Oval ink eye"?eye:rounded;
        UnityEngine.Object.DestroyImmediate(sphere);EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);AssetDatabase.SaveAssets();EditorApplication.Exit(0);
    }
}
