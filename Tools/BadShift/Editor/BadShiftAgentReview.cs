using System;
using System.IO;
using AudioMW.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BadShiftAgentReview
{
    public static void Open()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "..", ".bad-shift-demo"))) throw new InvalidOperationException("Use only the isolated BAD SHIFT project.");
        EditorUtility.audioMasterMute = true;
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Preserve unsaved scenes before opening the fixture.");
        EditorSceneManager.OpenScene("Assets/Playground/RelayField.unity");
        AudioAgentReviewWindow.Open();
        EditorApplication.delayCall += AudioAgentBridge.Enable;
    }
}
