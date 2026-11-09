using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class FitBadShiftAprons
{
    public static void Run()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        BadShiftDemo demo = UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/BadShift/Reference/SoftCube.asset");
        Material sage = AssetDatabase.LoadAssetAtPath<Material>("Assets/BadShift/Reference/Apron mint.mat");
        Material cream = AssetDatabase.LoadAssetAtPath<Material>("Assets/BadShift/Reference/Warm porcelain.mat");
        foreach (Animator animator in new[] { demo.PlayerAnimator, demo.DanceAnimator })
        {
            Transform actor = animator.transform;
            foreach (Transform old in actor.GetComponentsInChildren<Transform>().Where(t => t.name.IndexOf("apron", StringComparison.OrdinalIgnoreCase) >= 0).ToArray())
                if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Part(actor, mesh, sage, "Apron bib", new Vector3(0,1.35f,.44f), new Vector3(.68f,.65f,.1f));
            Part(actor, mesh, sage, "Apron skirt", new Vector3(0,.99f,.44f), new Vector3(.78f,.46f,.1f));
            foreach (float side in new[] { -.23f, .23f })
            {
                Part(actor, mesh, sage, "Apron strap", new Vector3(side,1.66f,.24f), new Vector3(.09f,.3f,.1f));
                Part(actor, mesh, cream, "Apron button", new Vector3(side,1.49f,.51f), new Vector3(.07f,.07f,.03f));
            }
        }
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();BuildAudioPlayground.BuildPlayer();
    }
    private static void Part(Transform actor, Mesh mesh, Material material, string name, Vector3 offset, Vector3 size)
    {
        GameObject part = new GameObject(name);
        part.transform.SetPositionAndRotation(actor.position + actor.right * offset.x + Vector3.up * offset.y + actor.forward * offset.z, actor.rotation);
        part.transform.localScale = size;
        part.AddComponent<MeshFilter>().sharedMesh = mesh; part.AddComponent<MeshRenderer>().sharedMaterial = material;
        part.transform.SetParent(actor, true);
    }
}
