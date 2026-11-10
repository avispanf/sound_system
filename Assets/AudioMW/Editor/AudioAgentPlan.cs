using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioAgentDraft
    {
        public string Destination = "Assets";
        public string Intent;
        public AudioAgentEventDraft[] Events;
    }
    [Serializable] public sealed class AudioAgentEventDraft
    {
        public string Name;
        public string Purpose;
        public string[] Files;
        public string ReplaceEventGuid;
        public string MixerGroupId;
    }
    [Serializable] public sealed class AudioAgentFile
    {
        public string Source;
        public string Sha256;
        public long Bytes;
        public string Destination;
    }
    [Serializable] public sealed class AudioAgentChange
    {
        public string Name;
        public string Purpose;
        public string EventPath;
        public string ReplaceEventGuid;
        public string BeforeJson;
        public string BeforeHash;
        public string Playback;
        public string MixerGroupId;
        public string MixerHash;
        public string Routing;
        public AudioAgentFile[] Files;
    }
    [Serializable] public sealed class AudioAgentReview
    {
        public int SchemaVersion = 1;
        public string Id;
        public string Project;
        public string Session;
        public string Intent;
        public string Folder;
        public string Unity;
        public string BuildTarget;
        public AudioAgentChange[] Changes;
        public string[] Limitations;
    }
    public sealed class AudioAgentPlan
    {
        public string Json { get; }
        public string Digest { get; }
        public string State { get; internal set; } = "pending";
        public string Result { get; internal set; } = "Waiting for review in Unity. No assets changed.";
        public AudioAgentReview Read() => JsonUtility.FromJson<AudioAgentReview>(Json);
        internal AudioAgentPlan(AudioAgentReview review) { Json = JsonUtility.ToJson(review, true); Digest = AudioProjectPlanner.Hash(Json); }
    }
    public static class AudioAgentPlanner
    {
        public static AudioAgentPlan Prepare(AudioAgentDraft draft, string session)
        {
            Ready();
            if (draft == null || string.IsNullOrWhiteSpace(draft.Intent) || draft.Intent.Length > 2000) throw new ArgumentException("Describe the intended integration in 1–2000 characters.");
            AssetPath(draft.Destination);
            if (!AssetDatabase.IsValidFolder(draft.Destination)) throw new ArgumentException("Choose an existing destination folder under Assets.");
            if (draft.Events == null || draft.Events.Length < 1 || draft.Events.Length > 16) throw new ArgumentException("Propose 1–16 events per review.");
            string id = Guid.NewGuid().ToString("N");
            var review = new AudioAgentReview { Id = id, Project = AudioProjectPlanner.ProjectIdentity(), Session = session, Intent = draft.Intent, Folder = draft.Destination + "/AudioMW-" + id.Substring(0, 12), Unity = Application.unityVersion, BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString() };
            var changes = new List<AudioAgentChange>(); var targets = new HashSet<string>(); long total = 0; int count = 0;
            foreach (var item in draft.Events)
            {
                if (item == null || string.IsNullOrWhiteSpace(item.Name) || !Regex.IsMatch(item.Name, "^[A-Za-z0-9][A-Za-z0-9 _-]{0,63}$")) throw new ArgumentException("Event names need 1–64 letters, numbers, spaces, underscores or hyphens.");
                if (string.IsNullOrWhiteSpace(item.Purpose) || item.Purpose.Length > 2000) throw new ArgumentException("Explain the gameplay purpose of each event.");
                if (item.Files == null || item.Files.Length == 0 || item.Files.Length > 32) throw new ArgumentException("Each event needs 1–32 local WAV, OGG or AIFF files.");
                string target = string.IsNullOrEmpty(item.ReplaceEventGuid) ? review.Folder + "/" + item.Name + ".asset" : AssetDatabase.GUIDToAssetPath(item.ReplaceEventGuid);
                AssetPath(target);
                if (!targets.Add(target.ToLowerInvariant())) throw new ArgumentException("An event appears twice in this plan.");
                var change = new AudioAgentChange { Name = item.Name, Purpose = item.Purpose, EventPath = target, ReplaceEventGuid = item.ReplaceEventGuid, MixerGroupId = item.MixerGroupId };
                if (!string.IsNullOrEmpty(item.ReplaceEventGuid))
                {
                    SoundEvent existing = AssetDatabase.LoadAssetAtPath<SoundEvent>(target);
                    if (existing == null || existing.ContainerMode != ContainerMode.Simple || existing.Children.Length != 0 || existing.ClipReferences.Length != 0 || existing.SelectionMode == ClipSelectionMode.WeightedRandom) throw new ArgumentException("Replace only direct, non-weighted SoundEvent clips. Layered, addressed and weighted events require manual authoring.");
                    Clean(existing); change.BeforeJson = EditorJsonUtility.ToJson(existing); change.BeforeHash = Fingerprint(target);
                    change.Playback = "Before: " + string.Join(", ", existing.Clips.Select(clip => clip != null ? clip.name : "missing clip")) + "\nPreserve " + existing.SelectionMode + ", loop " + existing.Loop + ", gain " + (existing.Volume > 0 ? (20 * Mathf.Log10(existing.Volume)).ToString("0.0") : "−∞") + " dB, spatial blend " + existing.SpatialBlend.ToString("0.00") + ".";
                    if (!string.IsNullOrEmpty(item.MixerGroupId)) throw new ArgumentException("Replacement preserves existing routing and playback settings. Do not supply a new mixer group.");
                    change.MixerGroupId = existing.MixerGroup != null ? GlobalObjectId.GetGlobalObjectIdSlow(existing.MixerGroup).ToString() : "";
                }
                else if (string.IsNullOrEmpty(item.MixerGroupId)) throw new ArgumentException("Select a mixer group from inspect for every new event.");
                else change.Playback = "New: RandomNoRepeat, no loop, gain 0 dB, 3D logarithmic attenuation. Gameplay hook still required.";
                var group = ResolveGroup(change.MixerGroupId);
                change.Routing = group != null ? AssetDatabase.GetAssetPath(group) + " / " + group.name : "Existing default output (unchanged)";
                if (group != null) { Clean(group.audioMixer); change.MixerHash = Fingerprint(AssetDatabase.GetAssetPath(group)); }
                var files = new List<AudioAgentFile>();
                foreach (string path in item.Files)
                {
                    if (++count > 64) throw new ArgumentException("Use at most 64 clips per plan.");
                    var file = InspectFile(path); total += file.Bytes;
                    if (total > 512L * 1024 * 1024) throw new ArgumentException("A plan may import at most 512 MB.");
                    file.Destination = review.Folder + "/" + count.ToString("D3") + "-" + file.Sha256.Substring(0, 12) + Path.GetExtension(file.Source).ToLowerInvariant(); files.Add(file);
                }
                change.Files = files.ToArray(); changes.Add(change);
            }
            review.Changes = changes.ToArray();
            review.Limitations = new[] { "Replacements retain event GUIDs, routing, selection, gain and gameplay references. Only the direct clip list changes.", "New events use RandomNoRepeat, 3D logarithmic attenuation, gain 0 dB and no loop. They need a gameplay hook; this plan does not create code or scene bindings.", "No MusicTrack/stem or VoiceLine authoring, automatic normalization, mixer changes or listening certification. Use the existing specialist editors for these tasks." };
            return new AudioAgentPlan(review);
        }
        public static string Check(AudioAgentPlan plan, string session)
        {
            try
            {
                Ready();
                if (plan == null || plan.State != "pending") throw new InvalidOperationException("This plan is not pending.");
                var review = plan.Read();
                if (review.Project != AudioProjectPlanner.ProjectIdentity() || review.Session != session) throw new InvalidOperationException("Project or editor session changed. Propose a new plan.");
                if (review.Unity != Application.unityVersion || review.BuildTarget != EditorUserBuildSettings.activeBuildTarget.ToString()) throw new InvalidOperationException("Unity or build target changed. Propose again.");
                AssetPath(review.Folder);
                if (!AssetDatabase.IsValidFolder(Path.GetDirectoryName(review.Folder).Replace('\\', '/')) || Directory.Exists(review.Folder) || File.Exists(review.Folder) || File.Exists(review.Folder + ".meta")) throw new InvalidOperationException("The destination changed. Propose again.");
                foreach (var change in review.Changes)
                {
                    if (!string.IsNullOrEmpty(change.ReplaceEventGuid))
                    {
                        var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(change.EventPath); Clean(sound);
                        if (AssetDatabase.AssetPathToGUID(change.EventPath) != change.ReplaceEventGuid || Fingerprint(change.EventPath) != change.BeforeHash || EditorJsonUtility.ToJson(sound) != change.BeforeJson) throw new InvalidOperationException("An event changed. Propose again: " + change.EventPath);
                    }
                    var group = ResolveGroup(change.MixerGroupId);
                    if (group != null) { Clean(group.audioMixer); if (Fingerprint(AssetDatabase.GetAssetPath(group)) != change.MixerHash) throw new InvalidOperationException("Mixer changed. Propose again."); }
                    foreach (var file in change.Files) if (InspectFile(file.Source).Sha256 != file.Sha256) throw new InvalidOperationException("An attachment changed. Propose again: " + file.Source);
                }
                return "";
            }
            catch (Exception error) { return error.Message; }
        }
        internal static void Apply(AudioAgentPlan plan, string session)
        {
            if (plan != null && plan.State == "applied") return;
            string problem = Check(plan, session); if (problem.Length != 0) throw new InvalidOperationException(problem);
            var review = plan.Read(); string stage = Path.Combine("Library", "AudioMWAgentStage", review.Id); var touched = new List<AudioAgentChange>(); bool moved = false;
            AudioAgentPlanner.NoLinks(Path.GetFullPath(stage));
            if (Directory.Exists(stage)) throw new InvalidOperationException("Staging exists from an interrupted operation. Inspect it before retrying.");
            try
            {
                Directory.CreateDirectory(stage);
                foreach (var change in review.Changes) foreach (var file in change.Files)
                {
                    string copy = Path.Combine(stage, Path.GetFileName(file.Destination)); File.Copy(file.Source, copy);
                    if (FileHash(copy) != file.Sha256) throw new InvalidOperationException("Attachment changed during copy.");
                }
                problem = Check(plan, session); if (problem.Length != 0) throw new InvalidOperationException(problem);
                Directory.Move(stage, review.Folder); moved = true;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                foreach (var change in review.Changes) foreach (var file in change.Files)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(file.Destination);
                    if (clip == null || clip.samples <= 0 || clip.channels <= 0) throw new InvalidOperationException("Unity could not decode the audio: " + file.Source);
                }
                foreach (var change in review.Changes.Where(item => !string.IsNullOrEmpty(item.ReplaceEventGuid)))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<SoundEvent>(change.EventPath); Clean(existing);
                    if (AssetDatabase.AssetPathToGUID(change.EventPath) != change.ReplaceEventGuid || Fingerprint(change.EventPath) != change.BeforeHash || EditorJsonUtility.ToJson(existing) != change.BeforeJson) throw new InvalidOperationException("An import processor changed the target. Review again.");
                }
                foreach (var change in review.Changes)
                {
                    bool replace = !string.IsNullOrEmpty(change.ReplaceEventGuid);
                    SoundEvent sound = replace ? AssetDatabase.LoadAssetAtPath<SoundEvent>(change.EventPath) : ScriptableObject.CreateInstance<SoundEvent>();
                    touched.Add(change);
                    sound.Clips = change.Files.Select(file => AssetDatabase.LoadAssetAtPath<AudioClip>(file.Destination)).ToArray();
                    if (!replace) { sound.name = change.Name; sound.MixerGroup = ResolveGroup(change.MixerGroupId); AssetDatabase.CreateAsset(sound, change.EventPath); }
                    else EditorUtility.SetDirty(sound);
                    AssetDatabase.SaveAssetIfDirty(sound);
                }
                plan.State = "applied"; plan.Result = "Imported " + review.Changes.Sum(change => change.Files.Length) + " clips and updated " + review.Changes.Length + " events. Verify their intended gameplay triggers; import success is not listening approval.";
            }
            catch (Exception error)
            {
                foreach (var change in touched.Where(item => !string.IsNullOrEmpty(item.ReplaceEventGuid)))
                {
                    var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(change.EventPath); EditorJsonUtility.FromJsonOverwrite(change.BeforeJson, sound); EditorUtility.SetDirty(sound); AssetDatabase.SaveAssetIfDirty(sound);
                }
                if (moved) AssetDatabase.DeleteAsset(review.Folder);
                plan.State = "failed"; plan.Result = "Import rolled back: " + error.Message; throw;
            }
            finally { if (Directory.Exists(stage)) Directory.Delete(stage, true); }
        }
        internal static AudioMixerGroup ResolveGroup(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!GlobalObjectId.TryParse(id, out var global) || !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(global) is AudioMixerGroup group)) throw new ArgumentException("Mixer group no longer exists. Inspect the project again.");
            return group;
        }
        internal static void Ready()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Wait for Edit Mode and completed compilation/import.");
        }
        internal static void Clean(UnityEngine.Object asset)
        {
            if (asset == null || EditorUtility.IsDirty(asset)) throw new InvalidOperationException("An input is missing or has unsaved edits. Save or discard your edits first.");
        }
        internal static string Fingerprint(string path) => AudioProjectPlanner.Hash(FileHash(path) + "|" + FileHash(path + ".meta") + "|" + AssetDatabase.GetAssetDependencyHash(path));
        internal static void AssetPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !(path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)) || path.Contains('\\') || path.Split('/').Any(part => part == ".." || part == "." || part.Length == 0 || part.IndexOfAny(new[] { ':', '*', '?', '"', '<', '>', '|' }) >= 0)) throw new ArgumentException("Use a normal path inside Assets.");
            NoLinks(Path.GetFullPath(path));
        }
        internal static void NoLinks(string path)
        {
            for (string part = path; !string.IsNullOrEmpty(part); part = Path.GetDirectoryName(part))
                if ((File.Exists(part) || Directory.Exists(part)) && (File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("Symbolic links are not supported: " + part);
        }
        internal static AudioAgentFile InspectFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("Attachments need accessible absolute local paths.");
            path = Path.GetFullPath(path); NoLinks(path);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length < 12 || info.Length > 128L * 1024 * 1024) throw new ArgumentException("Audio file missing, empty, or larger than 128 MB: " + path);
            string ext = info.Extension.ToLowerInvariant(); byte[] head = new byte[12];
            using (var stream = File.OpenRead(path)) if (stream.Read(head, 0, 12) != 12) throw new IOException("Cannot read audio header.");
            string a = Encoding.ASCII.GetString(head, 0, 4), b = Encoding.ASCII.GetString(head, 8, 4);
            if (!((ext == ".wav" && a == "RIFF" && b == "WAVE") || (ext == ".ogg" && a == "OggS") || ((ext == ".aif" || ext == ".aiff") && a == "FORM" && (b == "AIFF" || b == "AIFC")))) throw new ArgumentException("Supported attachments are WAV, OGG and AIFF with matching headers.");
            return new AudioAgentFile { Source = path, Sha256 = FileHash(path), Bytes = info.Length };
        }
        internal static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
