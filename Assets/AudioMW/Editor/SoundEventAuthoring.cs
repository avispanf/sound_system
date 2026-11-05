using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public enum SoundEventPreset { OneShot, Footsteps, UI, Ambience }

    public static class SoundEventAuthoring
    {
        public static SoundEvent Create(AudioClip[] clips, SoundEventPreset preset)
        {
            AudioClip[] usable = (clips ?? new AudioClip[0]).Where(clip => clip != null).Distinct()
                .OrderBy(clip => clip.name, StringComparer.Ordinal).ThenBy(clip => AssetDatabase.GetAssetPath(clip), StringComparer.Ordinal).ToArray();
            if (usable.Length == 0) throw new ArgumentException("Select audio clips in the Project window to continue.", nameof(clips));
            SoundEvent item = SoundEvent.CreateRuntime(usable);
            item.SelectionMode = ClipSelectionMode.ShuffleBag;
            item.SpatialBlend = preset == SoundEventPreset.UI ? 0f : 1f;
            if (preset == SoundEventPreset.Footsteps)
            {
                item.VolumeDb = -3f; item.VolumeRandomDbMin = -2f; item.VolumeRandomDbMax = 2f;
                item.MaxInstancesPerEmitter = 2; item.LimitPolicy = VoiceLimitPolicy.StealOldest;
            }
            if (preset == SoundEventPreset.Ambience) { item.Loop = true; item.AllowVirtualization = true; }
            return item;
        }

        public static SoundEvent CreateAsset(AudioClip[] clips, SoundEventPreset preset, string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) || !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Save the Sound Event as an asset under Assets.", nameof(path));
            if (!AssetDatabase.IsValidFolder(System.IO.Path.GetDirectoryName(path).Replace('\\', '/')))
                throw new ArgumentException("Choose an existing asset folder.", nameof(path));
            SoundEvent item = Create(clips, preset);
            AssetDatabase.CreateAsset(item, AssetDatabase.GenerateUniqueAssetPath(path));
            Undo.RegisterCreatedObjectUndo(item, "Create sound event");
            Selection.activeObject = item;
            EditorGUIUtility.PingObject(item);
            return item;
        }

        public static SoundEmitter AssignEmitter(SoundEvent item, GameObject target)
        {
            if (item == null || target == null || EditorUtility.IsPersistent(target) || !target.scene.IsValid())
                throw new ArgumentException("Choose a scene GameObject and one Sound Event.");
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Assign sound event");
            SoundEmitter emitter = target.GetComponent<SoundEmitter>();
            if (emitter == null) emitter = Undo.AddComponent<SoundEmitter>(target);
            SerializedObject serialized = new SerializedObject(emitter);
            serialized.FindProperty("soundEvent").objectReferenceValue = item;
            serialized.ApplyModifiedProperties();
            Undo.CollapseUndoOperations(group);
            return emitter;
        }

    }
}
