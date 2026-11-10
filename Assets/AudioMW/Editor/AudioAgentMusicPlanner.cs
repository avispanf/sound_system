using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioAgentMusicDraft
    {
        public string Destination = "Assets";
        public string Intent;
        public AudioAgentTrackDraft[] Tracks;
        public AudioAgentProfileDraft Profile;
    }
    [Serializable] public sealed class AudioAgentTrackDraft
    {
        public string Name;
        public string Purpose;
        public string ReplaceTrackGuid;
        public string MixerGroupId;
        public double Tempo;
        public int BeatsPerBar = 4;
        public string Intro;
        public string Body;
        public bool ForceToMono;
        public AudioAgentStemDraft[] Stems = new AudioAgentStemDraft[0];
    }
    [Serializable] public sealed class AudioAgentStemDraft
    {
        public string Name;
        public string File;
        public string ParameterGuid;
        public float LowWeight = 1;
        public float HighWeight = 1;
        public float FadeSeconds = .25f;
        public bool ForceToMono;
    }
    [Serializable] public sealed class AudioAgentProfileDraft
    {
        public string Name;
        public string ReplaceProfileGuid;
        public AudioAgentStateDraft[] States;
        public AudioAgentTransitionDraft[] Transitions = new AudioAgentTransitionDraft[0];
        public string Quantization = "Bar";
        public float FadeSeconds = .5f;
        public string Curve = "EqualPower";
    }
    [Serializable] public sealed class AudioAgentStateDraft { public string Name; public string Track; }
    [Serializable] public sealed class AudioAgentTransitionDraft
    {
        public string From;
        public string To;
        public string Quantization = "Bar";
        public float FadeSeconds = .5f;
        public string Curve = "EqualPower";
    }
    [Serializable] public sealed class AudioAgentTarget
    {
        public string Path;
        public string Guid;
        public string Json;
        public string Hash;
    }
    [Serializable] public sealed class AudioAgentMusicChange
    {
        public AudioAgentTrackDraft Settings;
        public AudioAgentTarget Target;
        public AudioAgentTarget[] Parameters;
        public string Route;
        public string MixerId;
        public string MixerHash;
        public string Before;
        public AudioAgentFile[] Files;
    }
    [Serializable] public sealed class AudioAgentProfileChange
    {
        public AudioAgentProfileDraft Settings;
        public AudioAgentTarget Target;
        public string Before;
    }
    public static class AudioAgentMusicPlanner
    {
        public static AudioAgentPlan Prepare(AudioAgentMusicDraft draft, string session)
        {
            AudioAgentPlanner.Ready();
            if (draft == null || string.IsNullOrWhiteSpace(draft.Intent) || draft.Intent.Length > 2000) throw new ArgumentException("Describe the music integration in 1–2000 characters.");
            AudioAgentPlanner.AssetPath(draft.Destination);
            if (!AssetDatabase.IsValidFolder(draft.Destination)) throw new ArgumentException("Choose an existing folder under Assets.");
            if (draft.Tracks == null || draft.Tracks.Length < 1 || draft.Tracks.Length > 8) throw new ArgumentException("Propose 1–8 music tracks.");
            var review = new AudioAgentReview { Id = System.Guid.NewGuid().ToString("N"), Project = AudioProjectPlanner.ProjectIdentity(), Session = session, Intent = draft.Intent, Unity = Application.unityVersion, BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
            review.Folder = draft.Destination + "/AudioMW-" + review.Id.Substring(0, 12);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var targets = new HashSet<string>(); var changes = new List<AudioAgentMusicChange>(); long bytes = 0; int count = 0;
            foreach (var track in draft.Tracks)
            {
                if (track == null) throw new ArgumentException("Missing track.");
                Name(track.Name); if (!names.Add(track.Name)) throw new ArgumentException("Track names must be unique.");
                if (string.IsNullOrWhiteSpace(track.Purpose) || track.Purpose.Length > 2000) throw new ArgumentException("Explain every track's gameplay purpose.");
                if (double.IsNaN(track.Tempo) || double.IsInfinity(track.Tempo) || track.Tempo < 10 || track.Tempo > 400 || track.BeatsPerBar < 1 || track.BeatsPerBar > 16) throw new ArgumentException("Specify BPM 10–400 and 1–16 beats per bar.");
                if (track.Stems == null || track.Stems.Length > 16 || (string.IsNullOrEmpty(track.Body) && track.Stems.Length == 0)) throw new ArgumentException("A track needs a looping body or stems, at most 16 stems.");
                var target = Target<MusicTrack>(track.ReplaceTrackGuid, review.Folder + "/" + track.Name + "-Track.asset");
                if (!targets.Add(target.Path)) throw new ArgumentException("A track appears twice in the plan.");
                var change = new AudioAgentMusicChange { Settings = track, Target = target, MixerId = track.MixerGroupId };
                if (!string.IsNullOrEmpty(target.Guid))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<MusicTrack>(target.Path);
                    if (existing.HasMarkers) throw new ArgumentException("Marker-authored tracks require manual music editing; this plan supports beat/bar transitions.");
                    if (!string.IsNullOrEmpty(track.MixerGroupId)) throw new ArgumentException("Track replacement preserves its mixer route; omit mixerGroupId.");
                    change.MixerId = existing.MixerGroup != null ? GlobalObjectId.GetGlobalObjectIdSlow(existing.MixerGroup).ToString() : "";
                    change.Before = "Replace intro/body and all layers: " + (existing.IntroClip != null ? existing.IntroClip.name : "no intro") + " / " + (existing.LoopClip != null ? existing.LoopClip.name : "no body") + " / " + string.Join(", ", existing.Layers.Select(l => l != null ? l.Name : "missing")) + ". BPM " + existing.Tempo + ", meter " + existing.BeatsPerBar + ", loop " + existing.Loop + ". Preserve GUID, references and gain " + existing.VolumeDb.ToString("0.0") + " dB.";
                }
                else { if (string.IsNullOrEmpty(track.MixerGroupId)) throw new ArgumentException("New tracks need an explicit mixer group."); change.Before = "Create a new track, gain 0 dB. New gameplay hook required."; }
                var group = AudioAgentPlanner.ResolveGroup(change.MixerId); change.Route = group != null ? AssetDatabase.GetAssetPath(group) + " / " + group.name : "Existing default output";
                if (group != null) { AudioAgentPlanner.Clean(group.audioMixer); change.MixerHash = AudioAgentPlanner.Fingerprint(AssetDatabase.GetAssetPath(group)); }
                var files = new List<AudioAgentFile>(); var parameters = new List<AudioAgentTarget>(); var stems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrEmpty(track.Intro)) files.Add(File(track.Intro, track.ForceToMono, review.Folder, ref count, ref bytes));
                if (!string.IsNullOrEmpty(track.Body)) files.Add(File(track.Body, track.ForceToMono, review.Folder, ref count, ref bytes));
                foreach (var stem in track.Stems)
                {
                    if (stem == null) throw new ArgumentException("Missing stem."); Name(stem.Name);
                    if (!stems.Add(stem.Name)) throw new ArgumentException("Stem names must be unique within a track.");
                    Unit(stem.LowWeight); Unit(stem.HighWeight); Fade(stem.FadeSeconds);
                    if (string.IsNullOrEmpty(stem.ParameterGuid) && stem.LowWeight != stem.HighWeight) throw new ArgumentException("A changing stem weight requires an existing parameter.");
                    if (!string.IsNullOrEmpty(stem.ParameterGuid)) parameters.Add(Target<SoundParameter>(stem.ParameterGuid, null));
                    files.Add(File(stem.File, stem.ForceToMono, review.Folder, ref count, ref bytes));
                }
                change.Parameters = parameters.ToArray(); change.Files = files.ToArray(); changes.Add(change);
            }
            review.Music = changes.ToArray();
            if (draft.Profile != null && !string.IsNullOrEmpty(draft.Profile.Name))
            {
                var profile = draft.Profile; Name(profile.Name); var target = Target<MusicStateProfile>(profile.ReplaceProfileGuid, review.Folder + "/" + profile.Name + "-States.asset");
                ValidateProfile(profile, draft.Tracks.Select(t => t.Name).ToArray());
                string before = "Create profile; assign it to a MusicStateController or existing gameplay hook.";
                if (!string.IsNullOrEmpty(target.Guid))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<MusicStateProfile>(target.Path);
                    if (existing.States.Any(s => s == null || !profile.States.Any(n => n.Name == s.Name))) throw new ArgumentException("Keep all existing state names to preserve gameplay requests.");
                    before = "Replace all states and transition rules; keep profile GUID and existing state names. Before: " + string.Join(", ", existing.States.Select(s => s.Name + " → " + (s.Track != null ? s.Track.name : "missing"))) + ". Rules: " + string.Join(", ", existing.Transitions.Where(t => t != null).Select(t => t.From + " → " + t.To + " " + t.Quantization + " " + t.FadeSeconds + "s " + t.Curve)) + ". Default: " + existing.DefaultQuantization + " " + existing.DefaultFadeSeconds + "s " + existing.DefaultCurve;
                }
                review.HasProfile = true; review.Profile = new AudioAgentProfileChange { Settings = profile, Target = target, Before = before };
            }
            review.Limitations = new[] { "Music plans replace the complete reviewed track content. Loop is enabled. Existing routing, gain and GUIDs are preserved; new tracks use 0 dB.", "All body/stem clips must share duration and sample rate and span whole bars at the reviewed tempo. Unity validates decoded audio on Apply; a mismatch rolls back the import. Intro length must also span whole bars.", "Source keeps the original channel layout; Force mono uses Unity's downmix import setting. Turning it off restores stereo only when the source is stereo. No stereo synthesis or loudness normalization is performed by AudioMW.", "New tracks/profiles need gameplay bindings. Existing references stay valid. This plan does not create scene/code hooks or certify listening quality." };
            return new AudioAgentPlan(review);
        }
        private static AudioAgentFile File(string path, bool mono, string folder, ref int count, ref long bytes)
        {
            var file = AudioAgentPlanner.InspectFile(path); bytes += file.Bytes;
            if (++count > 64 || bytes > 512L * 1024 * 1024) throw new ArgumentException("Use at most 64 clips and 512 MB per plan.");
            file.ForceToMono = mono; file.Destination = folder + "/" + count.ToString("D3") + "-" + file.Sha256.Substring(0, 12) + Path.GetExtension(file.Source).ToLowerInvariant(); return file;
        }
        private static AudioAgentTarget Target<T>(string guid, string newPath) where T : Object
        {
            if (string.IsNullOrEmpty(guid)) { if (newPath == null) throw new ArgumentException("An existing asset GUID is required."); return new AudioAgentTarget { Path = newPath }; }
            if (!Regex.IsMatch(guid, "^[a-f0-9]{32}$")) throw new ArgumentException("Use an asset GUID from inspect.");
            string path = AssetDatabase.GUIDToAssetPath(guid); AudioAgentPlanner.AssetPath(path); var asset = AssetDatabase.LoadAssetAtPath<T>(path); AudioAgentPlanner.Clean(asset);
            return new AudioAgentTarget { Path = path, Guid = guid, Json = EditorJsonUtility.ToJson(asset), Hash = AudioAgentPlanner.Fingerprint(path) };
        }
        private static void CheckTarget(AudioAgentTarget target)
        {
            if (string.IsNullOrEmpty(target.Guid)) return;
            var asset = AssetDatabase.LoadMainAssetAtPath(target.Path); AudioAgentPlanner.Clean(asset);
            if (AssetDatabase.AssetPathToGUID(target.Path) != target.Guid || AudioAgentPlanner.Fingerprint(target.Path) != target.Hash || EditorJsonUtility.ToJson(asset) != target.Json) throw new InvalidOperationException("A music asset or parameter changed. Propose again: " + target.Path);
        }
        internal static void Check(AudioAgentReview review)
        {
            foreach (var track in review.Music)
            {
                CheckTarget(track.Target); foreach (var parameter in track.Parameters) CheckTarget(parameter);
                var group = AudioAgentPlanner.ResolveGroup(track.MixerId);
                if (group != null) { AudioAgentPlanner.Clean(group.audioMixer); if (AudioAgentPlanner.Fingerprint(AssetDatabase.GetAssetPath(group)) != track.MixerHash) throw new InvalidOperationException("Music mixer changed. Propose again."); }
            }
            if (review.HasProfile) CheckTarget(review.Profile.Target);
        }
        internal static void Apply(AudioAgentReview review, List<AudioAgentTarget> touched)
        {
            foreach (var track in review.Music) ValidateClips(track);
            var tracks = new Dictionary<string, MusicTrack>(StringComparer.Ordinal);
            foreach (var change in review.Music)
            {
                var s = change.Settings; var track = string.IsNullOrEmpty(change.Target.Guid) ? ScriptableObject.CreateInstance<MusicTrack>() : AssetDatabase.LoadAssetAtPath<MusicTrack>(change.Target.Path); touched.Add(change.Target);
                int index = 0; track.IntroClip = string.IsNullOrEmpty(s.Intro) ? null : Clip(change.Files[index++]); track.LoopClip = string.IsNullOrEmpty(s.Body) ? null : Clip(change.Files[index++]);
                track.Tempo = s.Tempo; track.BeatsPerBar = s.BeatsPerBar; track.Loop = true;
                track.Layers = s.Stems.Select(stem => MusicLayer.CreateRuntime(stem.Name, Clip(change.Files[index++]), string.IsNullOrEmpty(stem.ParameterGuid) ? null : AssetDatabase.LoadAssetAtPath<SoundParameter>(AssetDatabase.GUIDToAssetPath(stem.ParameterGuid)), AnimationCurve.Linear(0, stem.LowWeight, 1, stem.HighWeight), stem.FadeSeconds)).ToArray();
                for (int i = 0; i < track.Layers.Length; i++) if (string.IsNullOrEmpty(s.Stems[i].ParameterGuid))
                {
                    var serialized = new SerializedObject(track); serialized.FindProperty("layers").GetArrayElementAtIndex(i).FindPropertyRelative("defaultWeight").floatValue = s.Stems[i].LowWeight; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                if (string.IsNullOrEmpty(change.Target.Guid)) { track.name = s.Name; track.MixerGroup = AudioAgentPlanner.ResolveGroup(change.MixerId); AssetDatabase.CreateAsset(track, change.Target.Path); }
                else EditorUtility.SetDirty(track);
                AssetDatabase.SaveAssetIfDirty(track); tracks.Add(s.Name, track);
            }
            if (!review.HasProfile) return;
            var p = review.Profile; var settings = p.Settings; var profile = string.IsNullOrEmpty(p.Target.Guid) ? ScriptableObject.CreateInstance<MusicStateProfile>() : AssetDatabase.LoadAssetAtPath<MusicStateProfile>(p.Target.Path); touched.Add(p.Target);
            profile.States = settings.States.Select(s => new MusicState { Name = s.Name, Track = tracks[s.Track] }).ToArray();
            profile.Transitions = settings.Transitions.Select(t => new MusicTransitionRule { From = t.From, To = t.To, Quantization = Quantization(t.Quantization), FadeSeconds = t.FadeSeconds, Curve = Curve(t.Curve) }).ToArray();
            profile.DefaultQuantization = Quantization(settings.Quantization); profile.DefaultFadeSeconds = settings.FadeSeconds; profile.DefaultCurve = Curve(settings.Curve);
            string error = profile.Validate(); if (error.Length != 0) throw new InvalidOperationException(error);
            if (string.IsNullOrEmpty(p.Target.Guid)) { profile.name = settings.Name; AssetDatabase.CreateAsset(profile, p.Target.Path); } else EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }
        private static AudioClip Clip(AudioAgentFile file) => AssetDatabase.LoadAssetAtPath<AudioClip>(file.Destination);
        private static void ValidateClips(AudioAgentMusicChange change)
        {
            var clips = change.Files.Select(Clip).ToArray(); var s = change.Settings; int offset = string.IsNullOrEmpty(s.Intro) ? 0 : 1; var body = clips[offset];
            foreach (var clip in clips)
            {
                double bars = MusicTrack.GetExactDuration(clip) * s.Tempo / (60 * s.BeatsPerBar);
                double tolerance = 2.0 / clip.frequency * s.Tempo / (60 * s.BeatsPerBar);
                if (Math.Round(bars) < 1 || Math.Abs(bars - Math.Round(bars)) > tolerance) throw new InvalidOperationException("Music clip must span whole bars at the reviewed BPM: " + clip.name);
            }
            foreach (var clip in clips.Skip(offset)) if (clip.frequency != body.frequency || Math.Abs(clip.samples - body.samples) > 1) throw new InvalidOperationException("Body and stems must have matching duration and sample rate.");
        }
        internal static void Rollback(List<AudioAgentTarget> touched)
        {
            foreach (var target in touched.Where(t => !string.IsNullOrEmpty(t.Guid)).Reverse())
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(target.Path); EditorJsonUtility.FromJsonOverwrite(target.Json, asset); EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
            }
        }
        private static void ValidateProfile(AudioAgentProfileDraft p, string[] tracks)
        {
            Fade(p.FadeSeconds); Quantization(p.Quantization); Curve(p.Curve);
            if (p.States == null || p.States.Length < 1 || p.States.Length > 8 || p.Transitions == null || p.Transitions.Length > 64) throw new ArgumentException("Use 1–8 states and at most 64 transition rules.");
            var names = new HashSet<string>(); var used = new HashSet<string>();
            foreach (var state in p.States) { if (state == null) throw new ArgumentException("Missing state."); Name(state.Name); if (!names.Add(state.Name) || !tracks.Contains(state.Track) || !used.Add(state.Track)) throw new ArgumentException("States need unique names and distinct proposed tracks."); }
            var routes = new HashSet<string>();
            foreach (var t in p.Transitions)
            {
                if (t == null || !names.Contains(t.From) || !names.Contains(t.To) || !routes.Add(t.From + "\n" + t.To)) throw new ArgumentException("Transition endpoints must be unique existing states.");
                Fade(t.FadeSeconds); Quantization(t.Quantization); Curve(t.Curve);
            }
        }
        private static void Name(string value) { if (value == null || !Regex.IsMatch(value, "^[A-Za-z0-9][A-Za-z0-9 _-]{0,63}$")) throw new ArgumentException("Use 1–64 letters, digits, spaces, underscores or hyphens for names."); }
        private static void Unit(float value) { if (float.IsNaN(value) || value < 0 || value > 1) throw new ArgumentException("Weights must be between 0 and 1."); }
        private static void Fade(float value) { if (float.IsNaN(value) || value < 0 || value > 30) throw new ArgumentException("Fade must be between 0 and 30 seconds."); }
        private static MusicQuantization Quantization(string value) { if (value != "Immediate" && value != "Beat" && value != "Bar") throw new ArgumentException("Use Immediate, Beat or Bar quantization."); return (MusicQuantization)Enum.Parse(typeof(MusicQuantization), value); }
        private static MusicFadeCurve Curve(string value) { if (value != "Linear" && value != "EqualPower") throw new ArgumentException("Use Linear or EqualPower fade curve."); return (MusicFadeCurve)Enum.Parse(typeof(MusicFadeCurve), value); }
    }
}
