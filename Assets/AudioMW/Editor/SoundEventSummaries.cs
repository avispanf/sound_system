using System;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public static class SoundEventSummaries
    {
        public static string Get(SerializedObject serialized, string section)
        {
            string[] fields;
            switch (section)
            {
                case "Variation":
                    bool db = serialized.FindProperty("volumeVariationMode").enumValueIndex == (int)VolumeVariationMode.Decibels;
                    fields = new[] { "volumeVariationMode", db ? "volumeRandomDbMin" : "volumeRandomMin", db ? "volumeRandomDbMax" : "volumeRandomMax", "pitchRandomMin", "pitchRandomMax" }; break;
                case "Spatial audio": fields = new[] { "attenuationPreset", "spatialBlend" }; break;
                case "Playback limits": fields = new[] { "maxInstances", "maxInstancesPerEmitter", "cooldownSeconds", "concurrencyGroup" }; break;
                case "Parameter bindings": fields = new[] { "parameterBindings" }; break;
                default: fields = new[] { "clipReferences" }; break;
            }
            foreach (string field in fields) if (serialized.FindProperty(field).hasMultipleDifferentValues) return "Mixed";
            Func<string, float> number = name => serialized.FindProperty(name).floatValue;
            if (section == "Variation")
            {
                bool db = serialized.FindProperty("volumeVariationMode").enumValueIndex == (int)VolumeVariationMode.Decibels;
                bool gain = number(db ? "volumeRandomDbMin" : "volumeRandomMin") != 0 || number(db ? "volumeRandomDbMax" : "volumeRandomMax") != 0;
                bool pitch = number("pitchRandomMin") != 0 || number("pitchRandomMax") != 0;
                return gain && pitch ? "Gain + pitch" : gain ? "Gain varied" : pitch ? "Pitch varied" : "Off";
            }
            if (section == "Spatial audio")
            {
                AttenuationPreset preset = serialized.FindProperty("attenuationPreset").objectReferenceValue as AttenuationPreset;
                float blend = preset != null ? preset.SpatialBlend : number("spatialBlend");
                return blend <= 0 ? "2D" : blend >= 1 ? "3D" : "2D / 3D";
            }
            if (section == "Playback limits")
            {
                int emitter = serialized.FindProperty("maxInstancesPerEmitter").intValue;
                int total = serialized.FindProperty("maxInstances").intValue;
                if (emitter > 0) return emitter + " / emitter";
                if (total > 0) return total + " total";
                ConcurrencyGroup group = serialized.FindProperty("concurrencyGroup").objectReferenceValue as ConcurrencyGroup;
                if (group != null && group.MaxInstances > 0) return "Group: " + group.MaxInstances;
                return number("cooldownSeconds") > 0 ? number("cooldownSeconds").ToString("0.##") + " s cooldown" : "Unlimited";
            }
            int count = serialized.FindProperty(section == "Parameter bindings" ? "parameterBindings" : "clipReferences").arraySize;
            return count == 0 ? "Off" : count + (section == "Parameter bindings" ? " bindings" : " references");
        }
    }
}
