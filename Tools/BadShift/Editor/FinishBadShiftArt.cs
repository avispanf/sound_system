using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class FinishBadShiftArt
{
    private const string Root = "Assets/BadShift/Reference";
    private static Transform furniture;
    private static Mesh rounded;
    private static Material wood, coral, cream, slate, lavender;

    private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + "/" + name + ".mat");

    private static GameObject Part(string name, Vector3 position, Vector3 scale, Material material, bool solid = false)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(furniture);
        part.transform.position = position;
        part.transform.localScale = scale;
        part.AddComponent<MeshFilter>().sharedMesh = rounded;
        part.AddComponent<MeshRenderer>().sharedMaterial = material;
        if (solid) part.AddComponent<BoxCollider>();
        return part;
    }

    private static void Chair(Vector3 position, float yaw)
    {
        Transform group = new GameObject("Coral cafe chair").transform;
        group.SetParent(furniture);
        group.position = position;
        GameObject seat = Part("Chair seat", position + Vector3.up * .5f, new Vector3(.8f,.16f,.8f), coral, true);
        seat.transform.SetParent(group, true);
        GameObject back = Part("Chair back", position + new Vector3(0,.95f,.34f), new Vector3(.8f,.95f,.14f), coral, true);
        back.transform.SetParent(group, true);
        foreach (float x in new[] { -.28f, .28f }) foreach (float z in new[] { -.28f, .28f })
        {
            GameObject leg = Part("Chair leg", position + new Vector3(x,.25f,z), new Vector3(.09f,.5f,.09f), wood);
            leg.transform.SetParent(group, true);
        }
        group.rotation = Quaternion.Euler(0,yaw,0);
    }

    private static void Lighting(BadShiftDemo demo)
    {
        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (pipeline == null) throw new Exception("Active URP pipeline required");
        pipeline.supportsHDR = true;
        EditorUtility.SetDirty(pipeline);
        UniversalAdditionalCameraData camera = demo.Game.View.GetUniversalAdditionalCameraData();
        camera.renderPostProcessing = true;
        camera.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camera.antialiasingQuality = AntialiasingQuality.High;
        camera.volumeLayerMask = 1;
        demo.Game.View.allowHDR = true;
        EditorUtility.SetDirty(camera);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, Root + "/ClayGrade.asset");
        Tonemapping tone = profile.Add<Tonemapping>();
        tone.mode.Override(TonemappingMode.Neutral);
        ColorAdjustments color = profile.Add<ColorAdjustments>();
        color.saturation.Override(-8);
        color.contrast.Override(-4);
        AssetDatabase.AddObjectToAsset(tone, profile);
        AssetDatabase.AddObjectToAsset(color, profile);
        Volume volume = new GameObject("Clay lighting grade").AddComponent<Volume>();
        volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = profile;
        UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        foreach (ScreenSpaceAmbientOcclusion ao in renderer.rendererFeatures.OfType<ScreenSpaceAmbientOcclusion>())
        {
            SerializedObject settings = new SerializedObject(ao);
            settings.FindProperty("m_Settings.Intensity").floatValue = .85f;
            settings.FindProperty("m_Settings.Radius").floatValue = .45f;
            settings.FindProperty("m_Settings.DirectLightingStrength").floatValue = .2f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            ao.SetActive(true);
        }
        EditorUtility.SetDirty(renderer);
        foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional) { light.shadowStrength = .65f; light.shadows = LightShadows.Soft; }
        Debug.Log("Clay grade: HDR, Neutral tone, SMAA High, global volume layer 0, SSAO enabled");
    }

    public static void FinalGrade()
    {
        if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated BAD SHIFT required");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        foreach (Volume volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
        {
            Debug.Log("Scene volume: " + volume.name + " / " + volume.sharedProfile);
            if (volume.name != "Clay lighting grade") volume.enabled = false;
        }
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root + "/ClayGrade.asset");
        UniversalRenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        pipeline.volumeProfile = profile;
        EditorUtility.SetDirty(pipeline);
        profile.TryGet(out Tonemapping tone); tone.mode.Override(TonemappingMode.None);
        profile.TryGet(out ColorAdjustments color); color.contrast.Override(0); color.saturation.Override(-3);
        EditorUtility.SetDirty(tone); EditorUtility.SetDirty(color);
        BadShiftDemo demo = UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        Transform glass = GameObject.Find("Microwave glass").transform;
        glass.localPosition = new Vector3(-.13f,0,-.54f);
        glass.localRotation = Quaternion.identity;
        glass.localScale = new Vector3(.6f,.62f,.04f);
        Debug.Log("Appliance shell: " + demo.MicrowaveVisual.GetComponent<Renderer>().bounds + " / glass: " + glass.GetComponent<Renderer>().bounds);
        EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);EditorSceneManager.SaveScene(demo.gameObject.scene);AssetDatabase.SaveAssets();BuildAudioPlayground.BuildPlayer();
    }
    public static void Run()
    {
        try
        {
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), ".bad-shift-demo"))) throw new Exception("Isolated BAD SHIFT required");
            EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
            if (GameObject.Find("Finished cafe furniture") != null) throw new Exception("Preserve existing furniture authoring");
            furniture = new GameObject("Finished cafe furniture").transform;
            rounded = AssetDatabase.LoadAssetAtPath<Mesh>(Root + "/SoftCube.asset");
            wood = Load("Honey oak"); coral = Load("Terracotta"); cream = Load("Warm porcelain"); slate = Load("Slate"); lavender = Load("Lavender");
            BadShiftDemo demo = UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
            foreach (GameObject root in demo.gameObject.scene.GetRootGameObjects())
            {
                string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(root) ?? "";
                if (path.EndsWith("/table_round_A.fbx") || path.EndsWith("/chair_A.fbx")) root.SetActive(false);
            }
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t.name == "Square cafe tabletop" || t.name == "mustard") t.gameObject.SetActive(false);
            foreach (float z in new[] { -2f, 2f })
            {
                Vector3 center = new Vector3(-9,0,z);
                Part("Square dining table", center + Vector3.up * .9f, new Vector3(1.7f,.16f,1.6f), wood, true);
                foreach (float x in new[] { -.6f, .6f }) foreach (float offset in new[] { -.55f, .55f })
                    Part("Dining table leg", center + new Vector3(x,.43f,offset), new Vector3(.13f,.86f,.13f), wood);
                Chair(center + Vector3.left * 1.1f, 90);
                Chair(center + Vector3.right * 1.1f, -90);
                Part("Milk carton", center + new Vector3(.25f,1.19f,0), new Vector3(.22f,.42f,.22f), cream);
                Part("Milk blue label", center + new Vector3(.25f,1.12f,-.12f), new Vector3(.2f,.16f,.02f), lavender);
            }
            GameObject shell = new GameObject("Microwave clay shell");
            shell.transform.SetParent(demo.Microwave, false);
            shell.AddComponent<MeshFilter>().sharedMesh = demo.Microwave.GetComponent<MeshFilter>().sharedMesh;
            shell.AddComponent<MeshRenderer>().sharedMaterials = demo.Microwave.GetComponent<MeshRenderer>().sharedMaterials;
            demo.Microwave.GetComponent<MeshRenderer>().enabled = false;
            demo.MicrowaveVisual = shell.transform;
            foreach (string name in new[] { "Microwave glass", "Microwave button" }) GameObject.Find(name).transform.SetParent(shell.transform, true);
            Transform oldCounter = demo.gameObject.scene.GetRootGameObjects().Select(g => g.transform).FirstOrDefault(t => t.name == "kitchencounter_straight_A");
            if (oldCounter != null) oldCounter.gameObject.SetActive(false);
            Part("Kitchen cabinet body", new Vector3(3,.65f,6.1f), new Vector3(3,1.3f,1.5f), slate, true);
            Part("Kitchen counter top", new Vector3(3,1.3f,6.1f), new Vector3(3.2f,.16f,1.7f), slate);
            for (int i = 0; i < 2; i++)
            {
                Part("Cabinet door", new Vector3(2.25f+i*1.5f,.67f,5.32f), new Vector3(1.4f,1.1f,.08f), slate);
                Part("Cabinet handle", new Vector3(2.75f+i*.5f,.78f,5.24f), new Vector3(.08f,.35f,.08f), cream);
            }
            Part("Stockroom right return", new Vector3(-1.05f,1.6f,7.5f), new Vector3(.18f,3.2f,3), lavender, true);
            Material grout = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            grout.color = new Color(.42f,.28f,.48f); AssetDatabase.CreateAsset(grout, Root + "/LavenderGrout.mat");
            for (int i = 0; i < 5; i++) Part("Stockroom tile joint", new Vector3(-3,.3f+i*.6f,9-.12f), new Vector3(3.8f,.02f,.015f), grout);
            for (int i = 0; i < 6; i++) Part("Stockroom tile joint", new Vector3(-4.6f+i*.6f,1.5f,8.88f), new Vector3(.02f,3,.015f), grout);
            Transform platform = GameObject.Find("Rave stage").transform;
            platform.GetComponent<MeshFilter>().sharedMesh = rounded;
            foreach (Transform t in GameObject.Find("Floor seams").GetComponentsInChildren<Transform>())
            {
                if (t.name != "Floor joint") continue;
                Vector3 scale = t.localScale;
                if (scale.x < .1f) scale.x = .055f;
                if (scale.z < .1f) scale.z = .055f;
                t.localScale = scale;
            }
            Transform horn = GameObject.Find("Megaphone bell").transform;
            GameObject.Find("Megaphone dark mouth").transform.SetParent(horn, true);
            Part("Megaphone neck", new Vector3(-6.6f,2,4.9f), new Vector3(.35f,.35f,.6f), Load("Mustard"));
            Lighting(demo);
            EditorUtility.SetDirty(demo);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(demo.gameObject.scene);
            EditorSceneManager.SaveScene(demo.gameObject.scene);
            Debug.Log("Finished cafe art, lighting and appliance motion authored");
            EditorApplication.Exit(0);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
