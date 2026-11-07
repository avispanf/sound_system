using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ImproveBadShift
{
    private static void Controller(Animator animator, string path, AnimationClip[] clips, string[] states, string[] names)
    {
        AnimatorController c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if(c==null)c=AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine sm=c.layers[0].stateMachine;
        for(int i=0;i<states.Length;i++)
        {
            AnimatorState state=sm.states.Select(s=>s.state).FirstOrDefault(s=>s.name==states[i]) ?? sm.AddState(states[i]);
            state.motion=clips.First(a=>a.name==names[i]); if(i==0)sm.defaultState=state;
        }
        animator.runtimeAnimatorController=c;animator.applyRootMotion=false;
        EditorUtility.SetDirty(c);PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
    }
    private static void Line(Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        GameObject line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.name="Floor joint";line.transform.SetParent(parent);line.transform.position=position;line.transform.localScale=scale;line.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(line.GetComponent<Collider>());
    }
    public static void Run()
    {
        try
        {
            if(!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath),".bad-shift-demo")))throw new Exception("Isolated project required");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            BadShiftDemo d=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
            foreach(GameObject root in d.gameObject.scene.GetRootGameObjects())
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root);
                if(path==null || !path.Contains("ThirdParty/Restaurant"))continue;
                foreach(MeshFilter mesh in root.GetComponentsInChildren<MeshFilter>())
                {
                    MeshCollider collider=mesh.GetComponent<MeshCollider>();if(collider==null)collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;collider.convex=false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                }
            }
            int tableIndex=0, chairIndex=0;
            foreach(GameObject root in d.gameObject.scene.GetRootGameObjects().OrderBy(g=>g.transform.position.z))
            {
                string path=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) ?? "";
                bool table=path.EndsWith("/table_round_A.fbx"),chair=path.EndsWith("/chair_A.fbx");
                if(!table && !chair)continue;
                Renderer[] renderers=root.GetComponentsInChildren<Renderer>();Bounds bounds=renderers[0].bounds;foreach(Renderer renderer in renderers)bounds.Encapsulate(renderer.bounds);
                root.transform.localScale*=.7f/bounds.size.y;
                root.transform.position=new Vector3(table?-10:-11,0,(table?tableIndex++:chairIndex++)==0?-2:2);
                PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
            }
            d.Fridge.position=new Vector3(9,0,5.5f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(d.Fridge);
            CharacterController cc=d.Game.GetComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.3f;cc.stepOffset=.25f;cc.slopeLimit=45;cc.skinWidth=.035f;
            string model="Assets/ThirdParty/Skeletons/Skeleton_Minion.fbx";ModelImporter importer=(ModelImporter)AssetImporter.GetAtPath(model);
            ModelImporterClipAnimation[] settings=importer.defaultClipAnimations;
            foreach(var clip in settings)clip.loopTime=new[]{"Idle","Walking_A","Running_A","Jump_Idle","Cheer"}.Contains(clip.name);
            importer.clipAnimations=settings;importer.SaveAndReimport();
            AnimationClip[] clips=AssetDatabase.LoadAllAssetsAtPath(model).OfType<AnimationClip>().ToArray();
            Controller(d.PlayerAnimator,"Assets/BadShift/PlayerV2.controller",clips,new[]{"Idle","Walk","Run","Jump","Interact"},new[]{"Idle","Walking_A","Running_A","Jump_Idle","Interact"});
            Controller(d.DanceAnimator,"Assets/BadShift/EmployeeV2.controller",clips,new[]{"Idle","Cheer"},new[]{"Idle","Cheer"});
            foreach(Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(t.name=="Sphere" && Vector3.Distance(t.position,d.Boss.position)<5)t.SetParent(d.Boss,true);
            GameObject exit=GameObject.Find("Shift exit frame");
            if(exit==null)
            {
                exit=new GameObject("Shift exit frame");exit.transform.position=d.Game.Exit.position;
                Material material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BadShift/Sage.mat");
                foreach(Vector3 offset in new[]{new Vector3(-1.15f,1.3f,0),new Vector3(1.15f,1.3f,0),new Vector3(0,2.6f,0)})
                {GameObject beam=GameObject.CreatePrimitive(PrimitiveType.Cube);beam.transform.SetParent(exit.transform,false);beam.transform.localPosition=offset;beam.transform.localScale=offset.y>2?new Vector3(2.6f,.2f,.25f):new Vector3(.2f,2.6f,.25f);beam.GetComponent<Renderer>().sharedMaterial=material;}
                GameObject label=new GameObject("Shift complete sign");label.transform.SetParent(exit.transform,false);label.transform.localPosition=new Vector3(0,2.95f,-.2f);TextMesh text=label.AddComponent<TextMesh>();text.text="SHIFT COMPLETE";text.anchor=TextAnchor.MiddleCenter;text.characterSize=.09f;text.fontSize=48;
            }
            if(GameObject.Find("Employee collision")==null)
            {
                GameObject body=new GameObject("Employee collision");body.transform.position=d.Dancer.position;
                CapsuleCollider capsule=body.AddComponent<CapsuleCollider>();capsule.height=1.8f;capsule.center=Vector3.up*.9f;capsule.radius=.3f;
            }
            if(GameObject.Find("Manager collision")==null)
            {
                GameObject body=new GameObject("Manager collision");body.transform.position=d.Boss.position;
                CapsuleCollider capsule=body.AddComponent<CapsuleCollider>();capsule.height=3.5f;capsule.center=Vector3.up*1.75f;capsule.radius=.8f;
            }
            if(GameObject.Find("Floor seams")==null)
            {
                GameObject lines=new GameObject("Floor seams");Material seam=new Material(Shader.Find("Universal Render Pipeline/Lit"));seam.color=new Color(.44f,.48f,.46f);AssetDatabase.CreateAsset(seam,"Assets/BadShift/FloorSeam.mat");
                for(int zone=0;zone<2;zone++)
                {
                    float min=zone==0?-12:4;float step=zone==0?1:2;
                    for(float x=min+step;x<min+8;x+=step)Line(lines.transform,new Vector3(x,.012f,0),new Vector3(.015f,.01f,20),seam);
                    for(float z=-10+step;z<10;z+=step)Line(lines.transform,new Vector3(min+4,.012f,z),new Vector3(8,.01f,.015f),seam);
                }
            }
            EditorUtility.SetDirty(d);EditorUtility.SetDirty(cc);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(d.gameObject.scene);EditorSceneManager.SaveScene(d.gameObject.scene);
            Debug.Log("BAD SHIFT physics and animation upgrade saved");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
