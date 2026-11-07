using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioProjectInventoryItem
    {
        public string Path;
        public string Kind;
        public string Detail;
        public string Guid;
    }

    [Serializable] public sealed class AudioProjectRecommendation
    {
        public string Title;
        public string Reason;
    }

    [Serializable] public sealed class AudioProjectReviewData
    {
        public string Project;
        public string Unity;
        public string BuildTarget;
        public string BriefJson;
        public string SourceFingerprint;
        public string TargetFingerprint;
        public List<AudioProjectInventoryItem> Inventory = new List<AudioProjectInventoryItem>();
        public List<AudioProjectRecommendation> Recommendations = new List<AudioProjectRecommendation>();
        public List<AudioLibraryIssue> Issues = new List<AudioLibraryIssue>();
        public int AuditErrors;
        public int AuditWarnings;
    }

    public sealed class AudioProjectPlan
    {
        private readonly string json;
        public string Json => json;
        public string Digest { get; }
        public AudioProjectReviewData Read() => JsonUtility.FromJson<AudioProjectReviewData>(json);
        internal AudioProjectPlan(AudioProjectReviewData data)
        {
            json = JsonUtility.ToJson(data, true); Digest = AudioProjectPlanner.Hash(json);
        }
    }

    public static class AudioProjectPlanner
    {
        public static AudioProjectPlan Analyse(AudioProjectBrief brief)
        {
            ValidateBrief(brief);
            AudioProjectReviewData data = Capture(brief);
            AudioLibraryReport audit = AudioLibraryAudit.Scan(brief.SourceFolder);
            data.AuditErrors = audit.Errors; data.AuditWarnings = audit.Warnings; data.Issues = audit.Issues;
            Recommend(data, "Review file roles and gameplay hooks", "File metadata does not establish intended roles, BPM, loop points or where gameplay should trigger audio. These remain unconfirmed.");
            int mixers = data.Inventory.Count(item => item.Kind == "AudioMixerController" || item.Kind == "AudioMixer");
            Recommend(data, mixers == 0 ? "Propose category groups" : "Review existing routing", mixers == 0 ? "No mixer was found under Assets. Music, SFX, Ambience, VO and UI groups are candidates for the next implementation stage; no groups or effects are created now." : mixers + " existing mixer assets were found. Preserve their routing and confirm role mappings before adding groups.");
            if (audit.Errors + audit.Warnings > 0) Recommend(data, "Review existing library diagnostics", audit.Errors + " errors and " + audit.Warnings + " warnings were captured. Resolve or explicitly accept relevant issues before implementing audio changes.");
            Recommend(data, "Audition a VO ducking proposal", "Dialogue priority and attack/release timing need a listening decision before any gain change.");
            Recommend(data, "Validate platform budgets on target devices", brief.VoiceBudget + " voices and " + brief.MemoryBudgetMb + " MB are requested budgets, not measured usage or applied settings.");
            if (string.IsNullOrWhiteSpace(brief.DeliveryTarget)) Recommend(data, "Choose a mix target", "No delivery target was supplied. Platform alone cannot determine master gain. No gain correction is proposed as an executable change.");
            Recommend(data, "Capture and listen to the game mix", "Clip metadata and library diagnostics do not measure rendered master loudness, frequency balance or audible quality.");
            return new AudioProjectPlan(data);
        }

        public static string Check(AudioProjectPlan plan, AudioProjectBrief current)
        {
            if (plan == null) return "Analyse the project to create a review first.";
            try
            {
                ValidateBrief(current);
                AudioProjectReviewData saved = plan.Read();
                if (saved.Project != ProjectIdentity()) return "This plan belongs to another Unity project.";
                if (saved.BriefJson != JsonUtility.ToJson(current)) return "The brief changed. Analyse again before approving.";
                AudioProjectReviewData fresh = Capture(current);
                if (saved.SourceFingerprint != fresh.SourceFingerprint || saved.BuildTarget != fresh.BuildTarget || saved.Unity != fresh.Unity) return "Project inputs changed. Analyse again before approving.";
                if (saved.TargetFingerprint != fresh.TargetFingerprint) return "The destination changed. Analyse again before approving.";
                return "";
            }
            catch (Exception error) { return error.Message; }
        }

        public static AudioProjectSetup SaveReviewedPlan(AudioProjectPlan plan, AudioProjectBrief current)
        {
            string problem = Check(plan, current);
            if (problem.Length > 0) throw new InvalidOperationException(problem);
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Save the plan in Edit Mode after compilation and import finish.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard unsaved scene edits before approving the plan.");
            AudioProjectReviewData data = plan.Read();
            AudioProjectSetup asset = AssetDatabase.LoadAssetAtPath<AudioProjectSetup>(current.ProfilePath);
            if (asset != null && asset.BriefJson == data.BriefJson && asset.SourceFingerprint == data.SourceFingerprint && SameReview(asset.ReviewedPlanJson, plan.Json)) return asset;
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AudioProjectSetup>();
                asset.Store(data.BriefJson, plan.Json, data.SourceFingerprint);
                AssetDatabase.CreateAsset(asset, current.ProfilePath);
                Undo.RegisterCreatedObjectUndo(asset, "Save AudioMW project setup");
            }
            else
            {
                Undo.RecordObject(asset, "Update AudioMW project setup");
                asset.Store(data.BriefJson, plan.Json, data.SourceFingerprint); EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssetIfDirty(asset);
            return asset;
        }

        private static void ValidateBrief(AudioProjectBrief brief)
        {
            if (brief == null) throw new ArgumentException("Fill in the project brief first.");
            if (new[] { brief.Game, brief.Genre, brief.Audience, brief.Platforms }.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Add game, genre, audience and target platforms before analysis.");
            if (brief.VoiceBudget < 1 || brief.VoiceBudget > 512 || brief.MemoryBudgetMb < 1) throw new ArgumentException("Use a voice budget of 1–512 and a positive memory budget.");
            if (!IsAssetsPath(brief.SourceFolder) || !AssetDatabase.IsValidFolder(brief.SourceFolder)) throw new ArgumentException("Choose an existing content folder under Assets.");
            if (!IsAssetsPath(brief.ProfilePath) || !brief.ProfilePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) || !AssetDatabase.IsValidFolder(Path.GetDirectoryName(brief.ProfilePath).Replace('\\', '/'))) throw new ArgumentException("Choose an .asset destination in an existing Assets folder.");
            Object target = AssetDatabase.LoadMainAssetAtPath(brief.ProfilePath);
            if (target != null && !(target is AudioProjectSetup)) throw new ArgumentException("The destination belongs to another asset. Choose a different path.");
            if (target == null && (File.Exists(brief.ProfilePath) || File.Exists(brief.ProfilePath + ".meta"))) throw new ArgumentException("The destination contains an unrecognized file or metadata. Choose a different path.");
            if (target != null && EditorUtility.IsDirty(target)) throw new ArgumentException("Save or discard unsaved setup-profile edits before analysis.");
        }

        private static bool SameReview(string saved, string current)
        {
            AudioProjectReviewData previous = JsonUtility.FromJson<AudioProjectReviewData>(saved);
            AudioProjectReviewData proposed = JsonUtility.FromJson<AudioProjectReviewData>(current);
            if (previous == null || proposed == null) return false;
            previous.TargetFingerprint = ""; proposed.TargetFingerprint = "";
            return JsonUtility.ToJson(previous) == JsonUtility.ToJson(proposed);
        }

        private static bool IsAssetsPath(string path)
        {
            return path != null && (path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)) && !path.Contains("\\") && !path.Split('/').Any(part => part == ".." || part == "." || part.Length == 0);
        }

        private static AudioProjectReviewData Capture(AudioProjectBrief brief)
        {
            AudioProjectReviewData data = new AudioProjectReviewData { Project = ProjectIdentity(), Unity = Application.unityVersion, BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString(), BriefJson = JsonUtility.ToJson(brief) };
            SortedSet<string> paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string filter in new[] { "t:AudioClip", "t:SoundEvent", "t:SoundBank", "t:MusicTrack", "t:MusicStateProfile", "t:SoundParameter", "t:VoiceLine", "t:AudioTierConfig", "t:MixerRoutingProfile" })
                foreach (string guid in AssetDatabase.FindAssets(filter, new[] { brief.SourceFolder })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            foreach (string filter in new[] { "t:AudioMixer", "t:SoundBank" })
                foreach (string guid in AssetDatabase.FindAssets(filter, new[] { "Assets" })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            StringBuilder fingerprint = new StringBuilder();
            foreach (string path in paths)
            {
                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (!(asset is AudioClip) && !(asset is SoundEvent) && !(asset is SoundBank) && !(asset is MusicTrack) && !(asset is MusicStateProfile) && !(asset is SoundParameter) && !(asset is VoiceLine) && !(asset is AudioTierConfig) && !(asset is MixerRoutingProfile) && !(asset is AudioMixer)) continue;
                    if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save or discard unsaved audio edits: " + path);
                    string detail = "Existing asset; unchanged.";
                    if (asset is AudioClip clip) detail = clip.channels + " channels · " + clip.frequency + " Hz · " + clip.length.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " s · " + clip.loadType + "; role unconfirmed";
                    if (asset is SoundEvent sound) detail = sound.ContainerMode + " · " + sound.Clips.Length + " direct clips · " + sound.Children.Length + " children; existing routing unchanged";
                    if (asset is AudioTierConfig tier) detail = tier.TierName + " · " + tier.MaxVoices + " voices · " + tier.OutputSampleRate + " Hz; existing settings unchanged";
                    if (asset is AudioMixer mixer) detail = "Groups: " + string.Join(", ", mixer.FindMatchingGroups("").Select(group => group.name));
                    data.Inventory.Add(new AudioProjectInventoryItem { Path = path, Kind = asset.GetType().Name, Detail = detail, Guid = AssetDatabase.AssetPathToGUID(path) });
                }
                fingerprint.Append(path).Append('|').Append(AssetDatabase.AssetPathToGUID(path)).Append('|').Append(AssetDatabase.GetAssetDependencyHash(path)).Append('\n');
            }
            foreach (string settings in new[] { "ProjectSettings/AudioManager.asset", "ProjectSettings/ProjectSettings.asset" })
                if (File.Exists(settings)) fingerprint.Append(settings).Append('|').Append(Hash(File.ReadAllText(settings))).Append('\n');
            data.SourceFingerprint = Hash(fingerprint.ToString());
            data.TargetFingerprint = File.Exists(brief.ProfilePath) ? Hash(File.ReadAllText(brief.ProfilePath) + "|" + AssetDatabase.AssetPathToGUID(brief.ProfilePath)) : "missing";
            return data;
        }

        public static string ProjectIdentity() => Hash(Path.GetFullPath(Application.dataPath));
        internal static string Hash(string value)
        {
            using (SHA256 algorithm = SHA256.Create()) return BitConverter.ToString(algorithm.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }
        private static void Recommend(AudioProjectReviewData data, string title, string reason) => data.Recommendations.Add(new AudioProjectRecommendation { Title = title, Reason = reason });
    }
}
