using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("AudioMW.Tests.EditMode")]

namespace AudioMW.Editor
{
    internal sealed class StudioMixerChannel
    {
        internal AudioMixerGroup Group, Parent;
        internal bool Master;
        internal string ExposedGain;
        internal string[] Effects;
        internal Hash128 VolumeId;
        internal bool HasVolumeId;
        internal bool? Mute, Solo;
        internal Texture Icon;

        internal static List<StudioMixerChannel> Read(AudioMixer mixer)
        {
            var channels = mixer.FindMatchingGroups("").Distinct().ToDictionary(group => group, group => new StudioMixerChannel { Group = group });
            using (var source = new SerializedObject(mixer))
            {
                var master = source.FindProperty("m_MasterGroup")?.objectReferenceValue;
                var exposures = source.FindProperty("m_ExposedParameters");
                foreach (var channel in channels.Values)
                using (var group = new SerializedObject(channel.Group))
                {
                    channel.Master = channel.Group == master;
                    channel.Icon = EditorGUIUtility.ObjectContent(channel.Group, typeof(AudioMixerGroup)).image;
                    channel.Mute = ReadSwitch(group, "m_Mute");
                    channel.Solo = ReadSwitch(group, "m_Solo");
                    channel.HasVolumeId = TryId(group.FindProperty("m_Volume"), out channel.VolumeId);
                    var children = group.FindProperty("m_Children");
                    if (children != null && children.isArray)
                        for (int i = 0; i < children.arraySize; i++)
                            if (children.GetArrayElementAtIndex(i).objectReferenceValue is AudioMixerGroup child && channels.TryGetValue(child, out var node)) node.Parent = channel.Group;
                    var effects = group.FindProperty("m_Effects");
                    if (effects != null && effects.isArray)
                    {
                        channel.Effects = new string[effects.arraySize];
                        for (int i = 0; i < effects.arraySize; i++)
                        {
                            var effect = effects.GetArrayElementAtIndex(i).objectReferenceValue;
                            if (effect != null)
                                using (var data = new SerializedObject(effect))
                                    channel.Effects[i] = data.FindProperty("m_EffectName")?.stringValue;
                        }
                    }
                    if (channel.HasVolumeId && exposures != null && exposures.isArray)
                        for (int i = 0; i < exposures.arraySize; i++)
                        {
                            var entry = exposures.GetArrayElementAtIndex(i);
                            if (TryId(entry.FindPropertyRelative("guid"), out var id) && id == channel.VolumeId)
                                channel.ExposedGain = entry.FindPropertyRelative("name")?.stringValue;
                        }
                }
            }
            return channels.Values.OrderBy(c => PathOf(c, channels), StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static bool? ReadSwitch(SerializedObject data, string name)
        {
            var field = data.FindProperty(name);
            return field != null && field.propertyType == SerializedPropertyType.Boolean ? (bool?)field.boolValue : null;
        }

        internal bool SetSwitch(bool solo, bool value)
        {
            if (!Editable(Group)) return false;
            using (var data = new SerializedObject(Group))
            {
                var field = data.FindProperty(solo ? "m_Solo" : "m_Mute");
                if (field == null || field.propertyType != SerializedPropertyType.Boolean) return false;
                field.boolValue = value;
                data.ApplyModifiedProperties();
                if (solo) Solo = value;
                else Mute = value;
                return true;
            }
        }

        private static string PathOf(StudioMixerChannel channel, Dictionary<AudioMixerGroup, StudioMixerChannel> channels)
        {
            string path = channel.Group.name;
            var seen = new HashSet<AudioMixerGroup> { channel.Group };
            while (channel.Parent != null && seen.Add(channel.Parent) && channels.TryGetValue(channel.Parent, out channel))
                path = channel.Group.name + "/" + path;
            return path;
        }

        private static bool TryId(SerializedProperty key, out Hash128 id)
        {
            id = default;
            if (key == null || key.type != "GUID") return false;
            var parts = new uint[4];
            for (int i = 0; i < parts.Length; i++)
            {
                var part = key.FindPropertyRelative("data[" + i + "]");
                if (part == null || part.propertyType != SerializedPropertyType.Integer) return false;
                parts[i] = (uint)part.longValue;
            }
            id = new Hash128(parts[0], parts[1], parts[2], parts[3]);
            return id.isValid;
        }

        internal static bool Editable(Object asset)
        {
            return asset != null && !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling
                && AssetDatabase.GetAssetPath(asset).StartsWith("Assets/", StringComparison.Ordinal)
                && AssetDatabase.IsOpenForEdit(asset);
        }

        internal bool Level(AudioMixerSnapshot snapshot, out float value, float? write = null)
        {
            value = 0;
            if (!HasVolumeId || snapshot == null || snapshot.audioMixer != Group.audioMixer) return false;
            if (write.HasValue && (!Editable(snapshot) || float.IsNaN(write.Value) || write.Value < -80 || write.Value > 20)) return false;
            using (var data = new SerializedObject(snapshot))
            {
                var map = data.FindProperty("m_FloatValues");
                if (map == null || map.type != "map" || !map.isArray) return false;
                SerializedProperty gain = null;
                for (int i = 0; i < map.arraySize; i++)
                {
                    var entry = map.GetArrayElementAtIndex(i);
                    if (!TryId(entry.FindPropertyRelative("first"), out var id)) return false;
                    if (id != VolumeId) continue;
                    if (gain != null) return false;
                    gain = entry.FindPropertyRelative("second");
                    if (gain == null || gain.propertyType != SerializedPropertyType.Float) return false;
                    value = gain.floatValue;
                }
                if (!write.HasValue || value == write.Value) return true;
                if (gain == null)
                {
                    int index = map.arraySize++;
                    var entry = map.GetArrayElementAtIndex(index);
                    var key = entry.FindPropertyRelative("first");
                    gain = entry.FindPropertyRelative("second");
                    if (key == null || key.type != "GUID" || gain == null || gain.propertyType != SerializedPropertyType.Float) return false;
                    using (var group = new SerializedObject(Group))
                    {
                        var volume = group.FindProperty("m_Volume");
                        if (!TryId(volume, out var current) || current != VolumeId) return false;
                        for (int i = 0; i < 4; i++)
                        {
                            string part = "data[" + i + "]";
                            var dest = key.FindPropertyRelative(part);
                            if (dest == null || dest.propertyType != SerializedPropertyType.Integer) return false;
                            dest.longValue = volume.FindPropertyRelative(part).longValue;
                        }
                    }
                }
                gain.floatValue = write.Value;
                value = write.Value;
                return data.ApplyModifiedProperties();
            }
        }
    }

    internal static class StudioMixerRoutes
    {
        internal static bool Supports(Object asset)
        {
            return asset is SoundEvent sound ? !AudioInspectionData.IsParent(sound)
                : asset is VoiceLine || asset is MusicTrack || asset is MusicArrangement score && score.Version == 1;
        }

        internal static AudioMixerGroup Output(Object asset, string laneId = null)
        {
            if (asset is MusicArrangement score)
                return string.IsNullOrEmpty(laneId) ? score.Output : score.Lanes?.FirstOrDefault(lane => lane != null && lane.Id == laneId)?.Output;
            return AudioMixPlanner.Output(asset);
        }

        internal static bool CanConnect(AudioMixer mixer, AudioMixerGroup output)
        {
            if (mixer == null) return false;
            var seen = new HashSet<AudioMixer> { mixer };
            while (output != null)
            {
                var destination = output.audioMixer;
                if (destination == null || !seen.Add(destination)) return false;
                output = destination.outputAudioMixerGroup;
            }
            return true;
        }

        internal static bool Connect(AudioMixer mixer, AudioMixerGroup output)
        {
            if (!StudioMixerChannel.Editable(mixer) || !CanConnect(mixer, output)) return false;
            if (mixer.outputAudioMixerGroup == output) return true;
            Undo.RecordObject(mixer, "Route mixer output");
            mixer.outputAudioMixerGroup = output;
            EditorUtility.SetDirty(mixer);
            return mixer.outputAudioMixerGroup == output;
        }

        internal static bool Assign(Object asset, string laneId, AudioMixerGroup group)
        {
            if (!Supports(asset) || !StudioMixerChannel.Editable(asset)) return false;
            using (var data = new SerializedObject(asset))
            {
                var field = data.FindProperty(asset is MusicArrangement ? "Output" : "mixerGroup");
                if (!string.IsNullOrEmpty(laneId))
                {
                    if (!(asset is MusicArrangement score) || score.Lanes == null) return false;
                    int index = score.Lanes.FindIndex(lane => lane != null && lane.Id == laneId);
                    if (index < 0 || score.Lanes.Count(lane => lane != null && lane.Id == laneId) != 1) return false;
                    field = data.FindProperty("Lanes").GetArrayElementAtIndex(index).FindPropertyRelative("Output");
                }
                if (field == null || field.propertyType != SerializedPropertyType.ObjectReference) return false;
                if (field.objectReferenceValue == group) return true;
                field.objectReferenceValue = group;
                return data.ApplyModifiedProperties();
            }
        }
    }
}
