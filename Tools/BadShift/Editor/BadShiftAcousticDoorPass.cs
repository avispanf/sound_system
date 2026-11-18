using System;
using System.IO;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BadShiftAcousticDoorPass
{
    public static void Run()
    {
        if (!Application.isBatchMode || !File.Exists(Path.Combine(Application.dataPath,"../.bad-shift-demo")))
            throw new InvalidOperationException("Use the isolated BAD SHIFT project.");
        var scene = EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        var demo = UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        var sc = demo.Game.Soundscape;
        var compressor = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/BadShift/Soundscape/FridgeCompressor.wav");
        if (compressor == null) throw new FileNotFoundException("Generate FridgeCompressor.wav first.");
        var fridge = demo.FridgeSound;
        fridge.Clips = new[]{compressor};
        fridge.Volume = .64f;
        fridge.RolloffMode = AudioRolloffMode.Linear;
        fridge.MinDistance = 2;
        fridge.MaxDistance = 9;
        EditorUtility.SetDirty(fridge);

        int layer = AcousticLayer();
        foreach (var collider in sc.Room.GetComponentsInChildren<Collider>(true))
            if (collider.name.StartsWith("Stockroom",StringComparison.Ordinal) && !collider.isTrigger)
                collider.gameObject.layer = layer;
        demo.Game.Door.Leaf.gameObject.layer = layer;
        if (sc.TapOccluder == null) sc.TapOccluder = sc.Tap.gameObject.AddComponent<SoundOccluder>();
        var serialized = new SerializedObject(sc.TapOccluder);
        serialized.FindProperty("playOnEnable").boolValue = false;
        serialized.FindProperty("soundEvent").objectReferenceValue = sc.TapLoop;
        var settings = serialized.FindProperty("settings");
        settings.FindPropertyRelative("blockingLayers").intValue = 1 << layer;
        settings.FindPropertyRelative("sampleCount").intValue = 5;
        settings.FindPropertyRelative("sampleSpread").floatValue = .20f;
        settings.FindPropertyRelative("maxVolumeAttenuation").floatValue = .77f;
        settings.FindPropertyRelative("occludedCutoff").floatValue = 950;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new IOException(scene.path);
        AssetDatabase.SaveAssets();
    }

    private static int AcousticLayer()
    {
        const string name = "Cafe acoustic shell";
        var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = manager.FindProperty("layers");
        int free = -1;
        for (int i=8; i<layers.arraySize; i++)
        {
            string value = layers.GetArrayElementAtIndex(i).stringValue;
            if (value == name) return i;
            if (free < 0 && string.IsNullOrEmpty(value)) free = i;
        }
        if (free < 0) throw new InvalidOperationException("An available acoustic layer is required.");
        layers.GetArrayElementAtIndex(free).stringValue = name;
        manager.ApplyModifiedPropertiesWithoutUndo();
        return free;
    }
}
