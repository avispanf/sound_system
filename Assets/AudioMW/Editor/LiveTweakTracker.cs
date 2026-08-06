using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    [InitializeOnLoad]
    public static class LiveTweakTracker
    {
        private const string SessionKey = "AudioMW.LiveTweak.Snapshot";
        private const string ChangedKey = "AudioMW.LiveTweak.Changed";

        private static readonly string[] TrackedTypes =
        {
            "SoundEvent",
            "SoundParameter",
            "SoundBank",
            "MusicTrack",
            "VoiceLine",
            "AttenuationPreset",
            "MixerRoutingProfile"
        };

        static LiveTweakTracker()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static List<string> ChangedGuids
        {
            get
            {
                LiveTweakSnapshot stored = Load(ChangedKey);
                List<string> guids = new List<string>();

                for (int i = 0; i < stored.Entries.Count; i++)
                {
                    guids.Add(stored.Entries[i].Guid);
                }

                return guids;
            }
        }

        public static bool HasChanges
        {
            get { return ChangedGuids.Count > 0; }
        }

        public static void ClearChanges()
        {
            SessionState.EraseString(ChangedKey);
        }

        public static bool TryGetOriginalJson(string guid, out string json)
        {
            return Load(ChangedKey).TryGet(guid, out json);
        }

        public static void Revert(string guid)
        {
            string originalJson;

            if (!TryGetOriginalJson(guid, out originalJson))
            {
                return;
            }

            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

            if (asset == null)
            {
                return;
            }

            Undo.RecordObject(asset, "Revert live tweak");
            EditorJsonUtility.FromJsonOverwrite(originalJson, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssetIfDirty(asset);

            Forget(guid);
        }

        public static void RevertAll()
        {
            List<string> guids = ChangedGuids;

            for (int i = 0; i < guids.Count; i++)
            {
                Revert(guids[i]);
            }

            ClearChanges();
        }

        public static void Forget(string guid)
        {
            LiveTweakSnapshot changed = Load(ChangedKey);

            for (int i = changed.Entries.Count - 1; i >= 0; i--)
            {
                if (changed.Entries[i].Guid == guid)
                {
                    changed.Entries.RemoveAt(i);
                }
            }

            Save(ChangedKey, changed);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                Save(SessionKey, Capture());
                ClearChanges();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                LiveTweakSnapshot before = Load(SessionKey);
                LiveTweakSnapshot after = Capture();
                List<string> changed = LiveTweakSnapshot.FindChanged(before, after);

                LiveTweakSnapshot originals = new LiveTweakSnapshot();

                for (int i = 0; i < changed.Count; i++)
                {
                    string original;

                    if (before.TryGet(changed[i], out original))
                    {
                        originals.Add(changed[i], original);
                    }
                }

                Save(ChangedKey, originals);
            }
        }

        public static LiveTweakSnapshot Capture()
        {
            LiveTweakSnapshot snapshot = new LiveTweakSnapshot();

            for (int t = 0; t < TrackedTypes.Length; t++)
            {
                string[] guids = AssetDatabase.FindAssets("t:" + TrackedTypes[t]);

                for (int i = 0; i < guids.Length; i++)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

                    if (asset != null)
                    {
                        snapshot.Add(guids[i], EditorJsonUtility.ToJson(asset));
                    }
                }
            }

            return snapshot;
        }

        private static LiveTweakSnapshot Load(string key)
        {
            string raw = SessionState.GetString(key, null);

            if (string.IsNullOrEmpty(raw))
            {
                return new LiveTweakSnapshot();
            }

            LiveTweakSnapshot snapshot = JsonUtility.FromJson<LiveTweakSnapshot>(raw);
            return snapshot ?? new LiveTweakSnapshot();
        }

        private static void Save(string key, LiveTweakSnapshot snapshot)
        {
            SessionState.SetString(key, JsonUtility.ToJson(snapshot));
        }
    }
}
