using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public static class SoundEventCreationMenu
    {
        private const string MenuPath = "Assets/AudioMW/Create Sound Event From Selection";

        [MenuItem(MenuPath, true)]
        private static bool Validate()
        {
            return Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets).Length > 0;
        }

        [MenuItem(MenuPath, false, 20)]
        private static void Execute()
        {
            AudioClip[] clips = Selection.GetFiltered<AudioClip>(SelectionMode.DeepAssets);
            if (clips.Length == 0)
            {
                return;
            }

            string directory = System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(clips[0]));
            if (string.IsNullOrEmpty(directory))
            {
                directory = "Assets";
            }

            string assetName = clips.Length == 1 ? clips[0].name : System.IO.Path.GetFileName(directory);
            string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/SFX_" + assetName + ".asset");

            SoundEvent soundEvent = ScriptableObject.CreateInstance<SoundEvent>();
            soundEvent.Clips = SortByName(clips);

            AssetDatabase.CreateAsset(soundEvent, path);
            AssetDatabase.SaveAssets();
            Selection.activeObject = soundEvent;
            EditorGUIUtility.PingObject(soundEvent);
        }

        private static AudioClip[] SortByName(AudioClip[] clips)
        {
            List<AudioClip> sorted = new List<AudioClip>(clips);
            sorted.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return sorted.ToArray();
        }
    }
}
