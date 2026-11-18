using System;
using System.IO;
using AudioMW;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BadShiftEmitterPass
{
    public static void Run()
    {
        if(!Application.isBatchMode || !File.Exists(Path.Combine(Application.dataPath,"../.bad-shift-demo"))) throw new InvalidOperationException("Use the isolated BAD SHIFT project.");
        var scene=EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        var demo=UnityEngine.Object.FindFirstObjectByType<BadShiftDemo>();
        var emitter=demo.Fridge.GetComponent<SoundEmitter>();
        if(emitter==null)
        {
            emitter=demo.Fridge.gameObject.AddComponent<SoundEmitter>();
            emitter.Event=demo.FridgeSound;
        }
        if(demo.CompressorEmitter==emitter && !scene.isDirty) return;
        demo.CompressorEmitter=emitter;
        PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
        EditorSceneManager.MarkSceneDirty(scene);
        if(!EditorSceneManager.SaveScene(scene)) throw new IOException(scene.path);
    }
}
