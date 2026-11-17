using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioAgentTimelineDraft
    {
        public string Intent;
        public string ArrangementGuid;
        public string ExpectedHash;
        public AudioAgentTimelineEdit[] Edits;
    }
    [Serializable] public sealed class AudioAgentTimelineEdit
    {
        public string Operation;
        public string LaneId;
        public string ClipId;
        public string SourceGuid;
        public string Name;
        public double Start, Trim, Duration, FadeIn, FadeOut;
        public float Gain = 1;
        public bool Repeat, Loop;
        public double Entry, Exit, LoopStart, LoopEnd, Tempo;
        public int BeatsPerBar;
    }
    [Serializable] public sealed class AudioAgentTimelineDependency
    {
        public string Guid, Path, Hash;
    }
    [Serializable] public sealed class AudioAgentTimelineChange
    {
        public string Guid, Path, BeforeHash, BeforeJson, AfterJson, Before, After;
        public string[] Edits;
        public AudioAgentTimelineDependency[] Dependencies;
    }
    public static class AudioAgentTimelinePlanner
    {
        public static AudioAgentAsset Inspect(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            string path = AssetDatabase.GUIDToAssetPath(guid); AudioAgentPlanner.AssetPath(path);
            var score = AssetDatabase.LoadAssetAtPath<MusicArrangement>(path);
            if (score == null) throw new ArgumentException("Arrangement GUID unavailable.");
            return new AudioAgentAsset { Id = guid, Path = path, Name = score.name, Detail = Describe(score) };
        }
        public static string Describe(MusicArrangement score)
        {
            var clips = score.Lanes.SelectMany(lane => lane.Clips).Select(clip => new AudioAgentTimelineEdit { Operation = "edit_clip", ClipId = clip.Id, LaneId = score.Lanes.First(l => l.Clips.Contains(clip)).Id, SourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(clip.Source)), Start = clip.Start, Trim = clip.Trim, Duration = clip.Duration, FadeIn = clip.FadeIn, FadeOut = clip.FadeOut, Gain = clip.Gain, Repeat = clip.Repeat }).ToArray();
            return JsonUtility.ToJson(new TimelineDescription { Hash = AudioAgentPlanner.Fingerprint(AssetDatabase.GetAssetPath(score)), Tempo = score.Tempo, BeatsPerBar = score.BeatsPerBar, Entry = score.Entry, Exit = score.Exit, Loop = score.Loop, LoopStart = score.LoopStart, LoopEnd = score.LoopEnd, Lanes = score.Lanes.Select(l => new TimelineLane { Id = l.Id, Name = l.Name, Gain = l.Gain, Output = l.Output != null ? l.Output.name : "Inherit arrangement output", ParameterGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(l.Parameter)) }).ToArray(), Clips = clips, Markers = score.Markers.ToArray() });
        }
        [Serializable] private sealed class TimelineDescription
        {
            public string Hash;
            public double Tempo, Entry, Exit, LoopStart, LoopEnd;
            public int BeatsPerBar;
            public bool Loop;
            public TimelineLane[] Lanes;
            public AudioAgentTimelineEdit[] Clips;
            public ArrangementMarker[] Markers;
        }
        [Serializable] private sealed class TimelineLane { public string Id, Name, Output, ParameterGuid; public float Gain; }
        public static AudioAgentPlan Prepare(AudioAgentTimelineDraft draft, string session)
        {
            AudioAgentPlanner.Ready();
            if (draft == null || string.IsNullOrWhiteSpace(draft.Intent) || draft.Intent.Length > 2000 || draft.Edits == null || draft.Edits.Length < 1 || draft.Edits.Length > 64) throw new ArgumentException("Describe the timeline change and supply 1–64 edits.");
            string path = AssetDatabase.GUIDToAssetPath(draft.ArrangementGuid); AudioAgentPlanner.AssetPath(path);
            var original = AssetDatabase.LoadAssetAtPath<MusicArrangement>(path); AudioAgentPlanner.Clean(original);
            CleanDependencies(path);
            string hash = AudioAgentPlanner.Fingerprint(path);
            if (hash != draft.ExpectedHash) throw new InvalidOperationException("Arrangement changed since inspect. Inspect again before proposing edits.");
            var score = UnityEngine.Object.Instantiate(original);
            try
            {
                var changes = new List<string>(); var dependencies = AssetDatabase.GetDependencies(path).Where(p => p != path && p.StartsWith("Assets/", StringComparison.Ordinal)).Select(p => new AudioAgentTimelineDependency { Guid = AssetDatabase.AssetPathToGUID(p), Path = p, Hash = AudioAgentPlanner.Fingerprint(p) }).ToList();
                foreach (var edit in draft.Edits)
                {
                    Execute(score, edit, dependencies); changes.Add(JsonUtility.ToJson(edit));
                }
                string issue = score.Validate(); if (issue.Length > 0) throw new ArgumentException(issue);
                if (score.Lanes.Count > 32 || score.Lanes.Sum(l => l.Clips.Count) > 256 || score.Markers.Count > 128 || score.Exit > 86400) throw new ArgumentException("Timeline proposal limit: 32 tracks, 256 clips, 128 cues and 24 hours.");
                var change = new AudioAgentTimelineChange { Guid = draft.ArrangementGuid, Path = path, BeforeHash = hash, BeforeJson = EditorJsonUtility.ToJson(original), Before = Summary(original), After = Summary(score), Edits = changes.ToArray(), Dependencies = dependencies.ToArray() };
                score.name = original.name; score.hideFlags = original.hideFlags; change.AfterJson = EditorJsonUtility.ToJson(score);
                return new AudioAgentPlan(new AudioAgentReview { Id = Guid.NewGuid().ToString("N"), Project = AudioProjectPlanner.ProjectIdentity(), Session = session, Intent = draft.Intent, Unity = Application.unityVersion, BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), HasTimeline = true, Timeline = change, Limitations = new[] { "Edit the existing arrangement only; no import, playback, scene change or automatic legacy conversion.", "Times are seconds. Tempo changes the grid, not source speed. Existing routing and parameter curves are preserved.", "Apply only after human review in Unity. Running audition retains its snapshot; restart to hear authored changes." } });
            }
            finally { UnityEngine.Object.DestroyImmediate(score); }
        }
        private static string Summary(MusicArrangement score)
        {
            return score.Tempo + " BPM / " + score.BeatsPerBar + " · entry " + score.Entry + "s / exit " + score.Exit + "s · loop " + score.Loop + " [" + score.LoopStart + ", " + score.LoopEnd + "]\n" + string.Join("\n", score.Lanes.Select(l => l.Name + " [" + l.Id + "] gain " + l.Gain + " · " + string.Join("; ", l.Clips.Select(c => (c.Source != null ? c.Source.name : "Missing source") + " [" + c.Id + "] start " + c.Start + "s, trim " + c.Trim + "s, duration " + c.Duration + "s, fades " + c.FadeIn + "/" + c.FadeOut + "s, repeat " + c.Repeat + ", gain " + c.Gain)))) + "\nCues: " + string.Join(", ", score.Markers.Select(m => m.Name + " [" + m.Id + "] " + m.Seconds + "s"));
        }
        private static void CleanDependencies(string path)
        {
            foreach (string dependency in AssetDatabase.GetDependencies(path))
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(dependency);
                if (asset != null) AudioAgentPlanner.Clean(asset);
                var importer = AssetImporter.GetAtPath(dependency);
                if (importer != null) AudioAgentPlanner.Clean(importer);
            }
        }
        private static void Name(string name) { if (string.IsNullOrWhiteSpace(name) || name.Length > 128) throw new ArgumentException("Use a name of 1–128 characters."); }
        private static void Execute(MusicArrangement score, AudioAgentTimelineEdit edit, List<AudioAgentTimelineDependency> dependencies)
        {
            if (edit == null) throw new ArgumentException("Missing edit.");
            var lane = score.Lanes.FirstOrDefault(l => l.Id == edit.LaneId);
            var owner = score.Lanes.FirstOrDefault(l => l.Clips.Any(c => c.Id == edit.ClipId));
            var clip = owner?.Clips.First(c => c.Id == edit.ClipId);
            switch (edit.Operation)
            {
                case "add_lane": Name(edit.Name); score.Lanes.Add(new ArrangementLane { Name = edit.Name, Gain = edit.Gain }); break;
                case "edit_lane": if (lane == null) throw new ArgumentException("Track ID unavailable."); Name(edit.Name); lane.Name = edit.Name; lane.Gain = edit.Gain; break;
                case "remove_lane": if (lane == null) throw new ArgumentException("Track ID unavailable."); score.Lanes.Remove(lane); break;
                case "add_clip":
                    if (lane == null) throw new ArgumentException("Track ID unavailable. Add a track in a separate reviewed plan first.");
                    string path = AssetDatabase.GUIDToAssetPath(edit.SourceGuid); AudioAgentPlanner.AssetPath(path);
                    var source = AssetDatabase.LoadAssetAtPath<AudioClip>(path); AudioAgentPlanner.Clean(source); CleanDependencies(path);
                    dependencies.Add(new AudioAgentTimelineDependency { Guid = edit.SourceGuid, Path = path, Hash = AudioAgentPlanner.Fingerprint(path) });
                    clip = new ArrangementClip { Source = source }; lane.Clips.Add(clip); SetClip(clip, edit); break;
                case "edit_clip": if (clip == null) throw new ArgumentException("Clip ID unavailable."); SetClip(clip, edit); break;
                case "duplicate_clip":
                    if (clip == null || lane == null) throw new ArgumentException("Clip or destination track ID unavailable.");
                    var copy = JsonUtility.FromJson<ArrangementClip>(JsonUtility.ToJson(clip)); copy.Id = Guid.NewGuid().ToString("N"); copy.Start = edit.Start; lane.Clips.Add(copy); break;
                case "remove_clip": if (clip == null) throw new ArgumentException("Clip ID unavailable."); owner.Clips.Remove(clip); break;
                case "set_bounds": score.Entry = edit.Entry; score.Exit = edit.Exit; score.Loop = edit.Loop; score.LoopStart = edit.LoopStart; score.LoopEnd = edit.LoopEnd; break;
                case "set_tempo": score.Tempo = edit.Tempo; score.BeatsPerBar = edit.BeatsPerBar; break;
                case "add_cue": Name(edit.Name); score.Markers.Add(new ArrangementMarker { Name = edit.Name, Seconds = edit.Start }); break;
                case "remove_cue": if (score.Markers.RemoveAll(m => m.Id == edit.ClipId) != 1) throw new ArgumentException("Cue ID unavailable."); break;
                default: throw new ArgumentException("Unsupported timeline operation.");
            }
        }
        private static void SetClip(ArrangementClip clip, AudioAgentTimelineEdit edit)
        {
            clip.Start = edit.Start; clip.Trim = edit.Trim; clip.Duration = edit.Duration; clip.FadeIn = edit.FadeIn; clip.FadeOut = edit.FadeOut; clip.Gain = edit.Gain; clip.Repeat = edit.Repeat;
        }
        internal static void Check(AudioAgentTimelineChange change)
        {
            if (change == null) return;
            AudioAgentPlanner.AssetPath(change.Path); CleanDependencies(change.Path);
            var score = AssetDatabase.LoadAssetAtPath<MusicArrangement>(change.Path); AudioAgentPlanner.Clean(score);
            if (AssetDatabase.AssetPathToGUID(change.Path) != change.Guid || AudioAgentPlanner.Fingerprint(change.Path) != change.BeforeHash || EditorJsonUtility.ToJson(score) != change.BeforeJson) throw new InvalidOperationException("Timeline or its dependencies changed. Inspect and propose again.");
            foreach (var dependency in change.Dependencies)
            {
                AudioAgentPlanner.AssetPath(dependency.Path); CleanDependencies(dependency.Path); AudioAgentPlanner.Clean(AssetDatabase.LoadMainAssetAtPath(dependency.Path));
                if (AssetDatabase.AssetPathToGUID(dependency.Path) != dependency.Guid || AudioAgentPlanner.Fingerprint(dependency.Path) != dependency.Hash) throw new InvalidOperationException("Timeline source changed. Propose again.");
            }
        }
        internal static void Apply(AudioAgentPlan plan)
        {
            var change = plan.Read().Timeline; Check(change);
            var score = AssetDatabase.LoadAssetAtPath<MusicArrangement>(change.Path);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply reviewed timeline plan"); Undo.RegisterCompleteObjectUndo(score, "Apply reviewed timeline plan");
            try
            {
                EditorJsonUtility.FromJsonOverwrite(change.AfterJson, score);
                string issue = score.Validate(); if (issue.Length > 0) throw new InvalidOperationException(issue);
                EditorUtility.SetDirty(score); AssetDatabase.SaveAssetIfDirty(score); Undo.CollapseUndoOperations(group);
                plan.State = "applied"; plan.Result = "Applied " + change.Edits.Length + " reviewed timeline edits. Arrangement identity retained. Restart audition to hear changes.";
            }
            catch
            {
                Undo.RevertAllDownToGroup(group); EditorJsonUtility.FromJsonOverwrite(change.BeforeJson, score); EditorUtility.SetDirty(score); AssetDatabase.SaveAssetIfDirty(score);
                plan.State = "failed"; plan.Result = "Timeline application failed; original content restored."; throw;
            }
        }
    }
}
