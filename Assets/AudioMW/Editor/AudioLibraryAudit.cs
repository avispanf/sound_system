using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioLibraryIssue
    {
        public string Severity;
        public string Rule;
        public string AssetPath;
        public string AssetName;
        public string Guid;
        public long LocalId;
        public string Property;
        public string Message;
        [NonSerialized] public Object Asset;
    }

    [Serializable] public sealed class AudioLibraryReport
    {
        public string UnityVersion;
        public string Scope;
        public int AssetsScanned;
        public int Errors;
        public int Warnings;
        public List<AudioLibraryIssue> Issues = new List<AudioLibraryIssue>();
    }

    public static class AudioLibraryAudit
    {
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("RunBatch is a command-line entry point. Use Library Audit in the Editor.");
            try
            {
                string scope = "Assets";
                string output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "audiomw-library-audit.json");
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i + 1 < args.Length; i++)
                {
                    if (args[i] == "--audiomw-audit-scope") scope = args[i + 1];
                    if (args[i] == "--audiomw-audit-report") output = args[i + 1];
                }
                AudioLibraryReport report = Scan(scope);
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log("AudioMW Library Audit: " + report.AssetsScanned + " assets, " + report.Errors + " errors, " + report.Warnings + " warnings.");
                EditorApplication.Exit(report.Errors == 0 ? 0 : 1);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(2); }
        }

        public static AudioLibraryReport Scan(string scope = "Assets")
        {
            if ((scope != "Assets" && !scope.StartsWith("Assets/", StringComparison.Ordinal)) || !AssetDatabase.IsValidFolder(scope)) throw new ArgumentException("Choose an existing folder under Assets.", nameof(scope));
            AudioLibraryReport report = new AudioLibraryReport { Scope = scope, UnityVersion = Application.unityVersion };
            HashSet<SoundEvent> bankEvents = new HashSet<SoundEvent>();
            foreach (string guid in AssetDatabase.FindAssets("t:SoundBank", new[] { "Assets" }))
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is SoundBank bank) foreach (SoundEvent item in bank.Events) Cover(item, bankEvents);
            SortedSet<string> paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { scope })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string path in paths)
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is SoundEvent) && !(asset is SoundParameter) && !(asset is SoundBank) && !(asset is MusicTrack) && !(asset is MusicStateProfile) && !(asset is VoiceLine)) continue;
                    report.AssetsScanned++;
                    Inspect(asset, report, bankEvents);
                }
            report.Issues.Sort((a, b) =>
            {
                int order = string.CompareOrdinal(a.AssetPath, b.AssetPath);
                if (order == 0) order = a.LocalId.CompareTo(b.LocalId);
                if (order == 0) order = string.CompareOrdinal(a.Rule, b.Rule);
                return order == 0 ? string.CompareOrdinal(a.Property, b.Property) : order;
            });
            return report;
        }

        private static void Cover(SoundEvent item, HashSet<SoundEvent> visited)
        {
            Stack<SoundEvent> pending = new Stack<SoundEvent>(); pending.Push(item);
            while (pending.Count > 0)
            {
                SoundEvent current = pending.Pop();
                if (current == null || !visited.Add(current)) continue;
                foreach (SoundEventChild child in current.Children) if (child != null) pending.Push(child.Event);
            }
        }

        private static void Add(AudioLibraryReport report, Object asset, string severity, string rule, string property, string message)
        {
            string guid; long id;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out id);
            report.Issues.Add(new AudioLibraryIssue { Asset = asset, AssetName = asset.name, AssetPath = AssetDatabase.GetAssetPath(asset), Guid = guid, LocalId = id, Severity = severity, Rule = rule, Property = property, Message = message });
            if (severity == "Error") report.Errors++; else report.Warnings++;
        }

        private static void Inspect(Object asset, AudioLibraryReport report, HashSet<SoundEvent> bankEvents)
        {
            using (SerializedObject serialized = new SerializedObject(asset))
            {
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                        Add(report, asset, "Error", "Missing reference", property.propertyPath, "An asset reference is missing. Assign its replacement or clear it in the Inspector.");
            }
            SoundEvent sound = asset as SoundEvent;
            if (sound != null)
            {
                string message = SoundEventValidation.Describe(sound);
                if (!string.IsNullOrEmpty(message)) Add(report, sound, "Warning", "Event content", "", message);
                if (sound.ContainerMode == ContainerMode.Simple)
                {
                    for (int i = 0; i < sound.Clips.Length; i++) if (sound.Clips[i] == null) Add(report, sound, "Warning", "Empty source slot", "clips.Array.data[" + i + "]", "Audio clip slot " + (i + 1) + " has no clip. A selection can produce no playback; review selection mode and weight.");
                    bool addressed = false;
                    foreach (ClipReference reference in sound.ClipReferences) if (reference != null && reference.IsAddressed) addressed = true;
                    if (addressed && !bankEvents.Contains(sound)) Add(report, sound, "Warning", "Bank membership", "clipReferences", "Addressed clips have no containing Sound Bank under Assets. Include the event in a bank, or review your custom loading integration.");
                    if (sound.SelectionMode == ClipSelectionMode.WeightedRandom)
                        for (int i = 0; i < sound.ClipWeights.Length; i++)
                            if (!Finite(sound.ClipWeights[i]) || sound.ClipWeights[i] < 0) Add(report, sound, "Warning", "Invalid weight", "clipWeights.Array.data[" + i + "]", "Source weight " + (i + 1) + " is treated as zero at runtime. Enter a finite non-negative weight.");
                }
                foreach (ParameterBinding binding in sound.ParameterBindings)
                    if (binding == null || !binding.IsValid) { Add(report, sound, "Warning", "Parameter binding", "parameterBindings", "At least one binding has no parameter or usable curve and is ignored at runtime."); break; }
            }
            SoundParameter parameter = asset as SoundParameter;
            if (parameter != null && (!Finite(parameter.MinValue) || !Finite(parameter.MaxValue) || !Finite(parameter.DefaultValue) || parameter.MinValue > parameter.MaxValue))
                Add(report, parameter, "Error", "Parameter range", "", "Use a finite ordered minimum/maximum and a finite default value.");
            MusicStateProfile profile = asset as MusicStateProfile;
            if (profile != null)
            {
                string error = profile.Validate();
                if (!string.IsNullOrEmpty(error)) Add(report, profile, "Error", "Music profile", "", error);
            }
            MusicTrack track = asset as MusicTrack;
            if (track != null)
            {
                if (!track.HasContent) Add(report, track, "Warning", "Music content", "", "Assign an intro, loop or playable layer.");
                if (double.IsNaN(track.Tempo) || double.IsInfinity(track.Tempo) || track.Tempo <= 0 || track.BeatsPerBar < 1) Add(report, track, "Warning", "Music timing", "", "Review tempo and beats per bar before using quantized transitions.");
            }
            SoundBank bank = asset as SoundBank;
            if (bank != null)
                for (int i = 0; i < bank.Events.Length; i++) if (bank.Events[i] == null) Add(report, bank, "Warning", "Empty bank event", "events.Array.data[" + i + "]", "Bank event slot " + (i + 1) + " is empty and contributes no content.");
            VoiceLine line = asset as VoiceLine;
            if (line != null && !line.IsValid) Add(report, line, "Warning", "Voice content", "clip", "Assign a voice clip before requesting this line.");
        }

        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
    }
}
