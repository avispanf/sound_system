using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class BadShiftQuietBatch
{
    static BadShiftQuietBatch()
    {
        if(!Application.isBatchMode||!File.Exists(Path.Combine(Application.dataPath,"..",".bad-shift-demo")))return;
        EditorUtility.audioMasterMute=true;
        Debug.Log("BAD SHIFT automated editor: audio output muted; playback simulation remains active.");
    }
}
