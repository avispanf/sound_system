using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public enum AudioMixBus { Unchanged, Music, SFX, Ambience, VO, UI }
    [Serializable] public sealed class AudioRouteChoice
    {
        public Object Asset;
        public AudioMixBus Bus;
        public bool RemoveSourceDuck;
    }
    [Serializable] public sealed class AudioMixRequest
    {
        public AudioProjectSetup Setup;
        public string Folder = "Assets";
        public string Name = "AudioMWMix";
        public AudioMixer ExistingMixer;
        public SoundParameter DuckParameter;
        public GameObject SceneTarget;
        public float MusicDuckDb = -6;
        public float AmbienceDuckDb = -6;
        public float AttackSeconds = 0.1f;
        public float ReleaseSeconds = 0.4f;
        public bool AllowReroute;
        public AudioRouteChoice[] Routes = new AudioRouteChoice[0];
    }
    [Serializable] public sealed class AudioMixChange
    {
        public string Path;
        public string Before;
        public string After;
        public string Why;
    }
    public sealed class AudioMixPlan
    {
        internal readonly string Signature;
        internal readonly string Inputs;
        internal readonly string Project;
        private readonly string changes;
        public bool AlreadyApplied { get; }
        public AudioMixChange[] ReadChanges() => JsonUtility.FromJson<Rows>(changes).Items;
        [Serializable] private sealed class Rows { public AudioMixChange[] Items; }
        internal AudioMixPlan(string signature, string inputs, bool applied, List<AudioMixChange> rows)
        {
            Signature = signature; Inputs = inputs; Project = AudioProjectPlanner.ProjectIdentity(); AlreadyApplied = applied;
            changes = JsonUtility.ToJson(new Rows { Items = rows.ToArray() });
        }
    }

    public static class AudioMixPlanner
    {
        public const string MusicDuck = "AudioMW_MusicDuckDb";
        public const string AmbienceDuck = "AudioMW_AmbienceDuckDb";
        public const string TemplateGuid = "8f28d703cd8d407d95180230c1356afe";
        public static string TemplatePath => AssetDatabase.GUIDToAssetPath(TemplateGuid);
        private static string PathFor(AudioMixRequest request, string suffix) => request.Folder + "/" + request.Name + suffix;
        public static string ReceiptPath(AudioMixRequest request) => PathFor(request, ".installation.asset");
        public static string MixPath(AudioMixRequest request) => PathFor(request, ".profile.asset");
        public static AudioMixPlan Prepare(AudioMixRequest request)
        {
            Validate(request);
            string signature = Signature(request);
            AudioMixInstallation receipt = AssetDatabase.LoadAssetAtPath<AudioMixInstallation>(ReceiptPath(request));
            if (receipt != null)
            {
                if (receipt.Signature != signature || receipt.AppliedState != Fingerprint(receipt.Files)) throw new InvalidOperationException("This installation has changed. Existing content is preserved. Review it manually or choose a new output name.");
                return new AudioMixPlan(signature, Inputs(request), true, new List<AudioMixChange> { new AudioMixChange { Path = ReceiptPath(request), Before = "Applied", After = "No changes", Why = "The same installation is present and unchanged. No duplicate mixer, profiles or scope will be created." } });
            }
            foreach (string path in Outputs(request)) if (File.Exists(path) || File.Exists(path + ".meta")) throw new InvalidOperationException("Destination already exists: " + path);
            AudioMixer mixer = request.ExistingMixer != null ? request.ExistingMixer : AssetDatabase.LoadAssetAtPath<AudioMixer>(TemplatePath);
            ValidateMixer(mixer);
            AudioMixScope scope = request.SceneTarget.GetComponent<AudioMixScope>();
            if (scope != null) throw new InvalidOperationException("The scene target already has an AudioMixScope. Existing configuration is preserved.");
            List<AudioMixChange> changes = new List<AudioMixChange>();
            changes.Add(new AudioMixChange { Path = request.ExistingMixer != null ? AssetDatabase.GetAssetPath(mixer) : PathFor(request, ".mixer"), Before = request.ExistingMixer != null ? "Existing mixer" : "Missing", After = request.ExistingMixer != null ? "Reuse compatible mixer; graph unchanged" : "Copy standard mixer: Master, Music/MusicDuck, SFX, Ambience/AmbienceDuck, VO, UI", Why = "Separate duck subgroups preserve category/user faders. No master normalization or extra effects are added." });
            foreach (AudioRouteChoice route in request.Routes.Where(item => item != null && item.Bus != AudioMixBus.Unchanged))
            {
                AudioMixerGroup before = Output(route.Asset);
                if (before != null && !request.AllowReroute) throw new InvalidOperationException("Explicitly allow replacing existing routes before changing " + AssetDatabase.GetAssetPath(route.Asset));
                changes.Add(new AudioMixChange { Path = AssetDatabase.GetAssetPath(route.Asset) + " · " + route.Asset.name, Before = before != null ? AssetDatabase.GetAssetPath(before) + "/" + before.name : "Default output", After = GroupPath(route.Bus) + (route.RemoveSourceDuck ? "; remove volume binding for selected VO duck parameter" : ""), Why = "Explicit category mapping. Authored clip gain, variation and other parameter bindings remain unchanged." });
            }
            changes.Add(new AudioMixChange { Path = MixPath(request), Before = "Missing", After = "Create routing and mix profiles; Music " + request.MusicDuckDb + " dB, Ambience " + request.AmbienceDuckDb + " dB during VO; attack " + request.AttackSeconds + " s, release " + request.ReleaseSeconds + " s", Why = "One global VO envelope controls dedicated duck buses. Values are a reviewed creative choice, not a platform loudness standard." });
            changes.Add(new AudioMixChange { Path = request.SceneTarget.scene.path, Before = "No mix scope on " + request.SceneTarget.name, After = "Add AudioMixScope, assign generated profile and save this scene", Why = "Register routing/ducking before gameplay starts and release ownership when the scene closes." });
            changes.Add(new AudioMixChange { Path = ReceiptPath(request), Before = "Missing", After = "Create installation receipt", Why = "Detect later edits and prevent duplicate or destructive reapplication." });
            return new AudioMixPlan(signature, Inputs(request), false, changes);
        }

        public static string Check(AudioMixPlan plan, AudioMixRequest request)
        {
            if (plan == null) return "Prepare a routing review first.";
            try
            {
                Validate(request);
                if (plan.Project != AudioProjectPlanner.ProjectIdentity()) return "This plan belongs to another project.";
                if (plan.Signature != Signature(request) || plan.Inputs != Inputs(request)) return "The request or project inputs changed. Prepare a new review.";
                return "";
            }
            catch (Exception error) { return error.Message; }
        }

        public static AudioMixInstallation Apply(AudioMixPlan plan, AudioMixRequest request, Action<string> progress = null)
        {
            string problem = Check(plan, request); if (problem.Length > 0) throw new InvalidOperationException(problem);
            if (plan.AlreadyApplied) return AssetDatabase.LoadAssetAtPath<AudioMixInstallation>(ReceiptPath(request));
            Prepare(request);
            string[] originals = request.Routes.Where(item => item != null && item.Bus != AudioMixBus.Unchanged).Select(item => AssetDatabase.GetAssetPath(item.Asset)).Concat(new[] { request.SceneTarget.scene.path }).Distinct().Where(File.Exists).ToArray();
            Dictionary<string, byte[]> backup = originals.ToDictionary(path => path, File.ReadAllBytes);
            List<string> created = new List<string>();
            string journal = System.IO.Path.Combine("Library/AudioMW/Transactions", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(journal);
            for (int i = 0; i < originals.Length; i++) File.WriteAllBytes(System.IO.Path.Combine(journal, i + ".before"), backup[originals[i]]);
            File.WriteAllLines(System.IO.Path.Combine(journal, "paths.txt"), originals);
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Apply AudioMW routing plan");
            try
            {
                AudioMixer mixer = request.ExistingMixer;
                if (mixer == null)
                {
                    string path = PathFor(request, ".mixer"); created.Add(path);
                    if (!AssetDatabase.CopyAsset(TemplatePath, path)) throw new IOException("Mixer template copy failed.");
                    mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path); ValidateMixer(mixer);
                }
                SoundParameter duck = request.DuckParameter;
                if (duck == null) duck = Create(SoundParameter.CreateRuntime(0, 1, 0), PathFor(request, ".duck.asset"), created);
                MixerRoutingProfile routing = Create(MixerRoutingProfile.CreateRuntime(mixer,
                    MixerParameterBinding.CreateRuntime(duck, MusicDuck, AnimationCurve.Linear(0, 0, 1, 1), 0, request.MusicDuckDb),
                    MixerParameterBinding.CreateRuntime(duck, AmbienceDuck, AnimationCurve.Linear(0, 0, 1, 1), 0, request.AmbienceDuckDb)), PathFor(request, ".routing.asset"), created);
                AudioMixProfile profile = ScriptableObject.CreateInstance<AudioMixProfile>(); profile.Routing = routing; profile.VoiceDuck = duck; profile.AttackSeconds = request.AttackSeconds; profile.ReleaseSeconds = request.ReleaseSeconds;
                Create(profile, MixPath(request), created); progress?.Invoke("Assets created");
                foreach (AudioRouteChoice route in request.Routes.Where(item => item != null && item.Bus != AudioMixBus.Unchanged))
                {
                    Undo.RecordObject(route.Asset, "Route AudioMW asset"); Assign(route.Asset, Group(mixer, route.Bus));
                    if (route.RemoveSourceDuck && route.Asset is SoundEvent sound) sound.ParameterBindings = sound.ParameterBindings.Where(binding => binding == null || binding.Parameter != duck || binding.Target != ParameterTarget.Volume).ToArray();
                    EditorUtility.SetDirty(route.Asset);
                }
                AudioMixScope scope = Undo.AddComponent<AudioMixScope>(request.SceneTarget); Undo.RecordObject(scope, "Assign mix profile"); scope.Profile = profile;
                PrefabUtility.RecordPrefabInstancePropertyModifications(scope);
                foreach (string path in created) foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
                foreach (AudioRouteChoice route in request.Routes.Where(item => item != null && item.Bus != AudioMixBus.Unchanged)) AssetDatabase.SaveAssetIfDirty(route.Asset);
                progress?.Invoke("Routes saved");
                AudioMixInstallation receipt = Create(ScriptableObject.CreateInstance<AudioMixInstallation>(), ReceiptPath(request), created);
                Undo.CollapseUndoOperations(group);
                if (!EditorSceneManager.SaveScene(request.SceneTarget.scene)) throw new IOException("The reviewed scene could not be saved.");
                string[] tracked = created.Where(path => path != ReceiptPath(request)).Concat(InputFiles(request).Where(path => path != TemplatePath)).Distinct().OrderBy(path => path, StringComparer.Ordinal).ToArray();
                receipt.Record(plan.Signature, Fingerprint(tracked), tracked, profile); EditorUtility.SetDirty(receipt); AssetDatabase.SaveAssetIfDirty(receipt);
                File.WriteAllText(System.IO.Path.Combine(journal, "result.txt"), "Applied");
                return receipt;
            }
            catch (Exception failure)
            {
                List<Exception> failures = new List<Exception> { failure };
                try { Undo.RevertAllDownToGroup(group); } catch (Exception error) { failures.Add(error); }
                foreach (string path in created.AsEnumerable().Reverse())
                    try { if (File.Exists(path) && !AssetDatabase.DeleteAsset(path)) throw new IOException("Could not remove " + path); } catch (Exception error) { failures.Add(error); }
                foreach (var entry in backup)
                    try { File.WriteAllBytes(entry.Key, entry.Value); AssetDatabase.ImportAsset(entry.Key, ImportAssetOptions.ForceSynchronousImport); } catch (Exception error) { failures.Add(error); }
                File.WriteAllText(System.IO.Path.Combine(journal, "result.txt"), failures.Count == 1 ? "Rolled back; inspect scene dirty state before continuing." : "Rollback incomplete. Original files are retained in this journal; inspect before continuing.");
                if (failures.Count > 1) throw new AggregateException("Apply failed and rollback needs attention. Backups: " + journal, failures);
                throw;
            }
        }

        private static T Create<T>(T asset, string path, List<string> created) where T : Object
        {
            created.Add(path); AssetDatabase.CreateAsset(asset, path); Undo.RegisterCreatedObjectUndo(asset, "Create AudioMW mix asset"); EditorUtility.SetDirty(asset); return asset;
        }
        public static string GroupPath(AudioMixBus bus) => "Master/" + (bus == AudioMixBus.Music ? "Music/MusicDuck" : bus == AudioMixBus.Ambience ? "Ambience/AmbienceDuck" : bus.ToString());
        public static AudioMixerGroup Group(AudioMixer mixer, AudioMixBus bus)
        {
            AudioMixerGroup[] groups = mixer.FindMatchingGroups(GroupPath(bus));
            if (groups.Length != 1) throw new InvalidOperationException("Mixer needs exactly one group at " + GroupPath(bus)); return groups[0];
        }
        public static void ValidateMixer(AudioMixer mixer)
        {
            if (mixer == null) throw new InvalidOperationException("The standard mixer template is missing.");
            foreach (AudioMixBus bus in new[] { AudioMixBus.Music, AudioMixBus.SFX, AudioMixBus.Ambience, AudioMixBus.VO, AudioMixBus.UI }) Group(mixer, bus);
            float value;
            if (!mixer.GetFloat(MusicDuck, out value) || !mixer.GetFloat(AmbienceDuck, out value)) throw new InvalidOperationException("Mixer must expose dedicated AudioMW music and ambience duck parameters.");
        }
        public static AudioMixerGroup Output(Object asset) => asset is SoundEvent sound ? sound.MixerGroup : asset is MusicTrack music ? music.MixerGroup : asset is VoiceLine voice ? voice.MixerGroup : null;
        private static void Assign(Object asset, AudioMixerGroup group)
        {
            if (asset is SoundEvent sound) sound.MixerGroup = group;
            else if (asset is MusicTrack music) music.MixerGroup = group;
            else if (asset is VoiceLine voice) voice.MixerGroup = group;
        }
        private static void Validate(AudioMixRequest request)
        {
            if (request == null || request.Setup == null || string.IsNullOrEmpty(request.Setup.ReviewedPlanJson)) throw new ArgumentException("Choose a saved project setup profile.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) throw new InvalidOperationException("Prepare and apply routing in Edit Mode after import/compilation finishes.");
            if (request.Folder == null || !(request.Folder == "Assets" || request.Folder.StartsWith("Assets/", StringComparison.Ordinal)) || request.Folder.Contains("\\") || request.Folder.Split('/').Any(part => part == ".." || part == "." || part.Length == 0) || !AssetDatabase.IsValidFolder(request.Folder)) throw new ArgumentException("Choose an existing output folder under Assets.");
            if (request.Name == null || !Regex.IsMatch(request.Name, "^[A-Za-z][A-Za-z0-9_-]{0,63}$")) throw new ArgumentException("Use a short output name containing letters, digits, underscores or hyphens.");
            if (new[] { request.MusicDuckDb, request.AmbienceDuckDb, request.AttackSeconds, request.ReleaseSeconds }.Any(value => float.IsNaN(value) || float.IsInfinity(value)) || request.MusicDuckDb > 0 || request.MusicDuckDb < -80 || request.AmbienceDuckDb > 0 || request.AmbienceDuckDb < -80 || request.AttackSeconds < 0 || request.ReleaseSeconds < 0) throw new ArgumentException("Ducking needs finite attenuation from -80 to 0 dB and non-negative attack/release.");
            if (request.SceneTarget == null || EditorUtility.IsPersistent(request.SceneTarget) || !request.SceneTarget.scene.IsValid() || string.IsNullOrEmpty(request.SceneTarget.scene.path)) throw new ArgumentException("Choose a GameObject in a saved scene.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++) if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save or discard unsaved scene changes before reviewing routing.");
            if (request.DuckParameter != null && (!request.DuckParameter.IsGlobal || request.DuckParameter.MinValue != 0 || request.DuckParameter.MaxValue != 1 || request.DuckParameter.DefaultValue != 0)) throw new ArgumentException("The VO duck parameter must span 0–1 with default 0.");
            if (request.Routes == null || !request.Routes.Any(item => item != null && item.Bus != AudioMixBus.Unchanged)) throw new ArgumentException("Choose at least one explicit asset category.");
            HashSet<Object> seen = new HashSet<Object>();
            foreach (AudioRouteChoice route in request.Routes)
            {
                if (route == null || route.Bus == AudioMixBus.Unchanged) continue;
                if (!Enum.IsDefined(typeof(AudioMixBus), route.Bus)) throw new ArgumentException("Choose a supported route category.");
                if (route.RemoveSourceDuck && route.Bus != AudioMixBus.Music && route.Bus != AudioMixBus.Ambience) throw new ArgumentException("Source duck removal requires a ducked Music or Ambience bus.");
                if (!(route.Asset is SoundEvent) && !(route.Asset is MusicTrack) && !(route.Asset is VoiceLine)) throw new ArgumentException("Only persistent Sound Events, Music Tracks and VO Lines can be routed.");
                if (!EditorUtility.IsPersistent(route.Asset) || !AssetDatabase.GetAssetPath(route.Asset).StartsWith("Assets/", StringComparison.Ordinal) || !seen.Add(route.Asset)) throw new ArgumentException("Route each project asset once. Package assets are read-only.");
                if (route.RemoveSourceDuck && (!(route.Asset is SoundEvent) || request.DuckParameter == null)) throw new ArgumentException("Removing a source duck binding needs a Sound Event and an explicitly selected duck parameter.");
            }
            foreach (string path in InputFiles(request).Where(path => !path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))) foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path)) if (asset != null && EditorUtility.IsDirty(asset)) throw new InvalidOperationException("Save or discard unsaved audio edits: " + path);
        }
        private static string Key(Object asset)
        {
            if (asset == null) return "none";
            if (asset is GameObject go && !EditorUtility.IsPersistent(go)) return GlobalObjectId.GetGlobalObjectIdSlow(go).ToString();
            string guid; long id; if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out guid, out id)) throw new ArgumentException("Save referenced assets first."); return guid + ":" + id;
        }
        private static string Signature(AudioMixRequest request)
        {
            StringBuilder text = new StringBuilder();
            text.Append(Key(request.Setup)).Append('|').Append(request.Folder).Append('|').Append(request.Name).Append('|').Append(Key(request.ExistingMixer)).Append('|').Append(Key(request.DuckParameter)).Append('|').Append(Key(request.SceneTarget));
            foreach (float value in new[] { request.MusicDuckDb, request.AmbienceDuckDb, request.AttackSeconds, request.ReleaseSeconds }) text.Append('|').Append(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            text.Append('|').Append(request.AllowReroute);
            foreach (AudioRouteChoice route in request.Routes.Where(item => item != null && item.Bus != AudioMixBus.Unchanged).OrderBy(item => Key(item.Asset), StringComparer.Ordinal)) text.Append('|').Append(Key(route.Asset)).Append(':').Append(route.Bus).Append(':').Append(route.RemoveSourceDuck);
            return AudioProjectPlanner.Hash(text.ToString());
        }
        private static IEnumerable<string> Outputs(AudioMixRequest request)
        {
            if (request.ExistingMixer == null) yield return PathFor(request, ".mixer");
            if (request.DuckParameter == null) yield return PathFor(request, ".duck.asset");
            yield return PathFor(request, ".routing.asset"); yield return MixPath(request); yield return ReceiptPath(request);
        }
        private static IEnumerable<string> InputFiles(AudioMixRequest request)
        {
            return request.Routes.Where(item => item != null && item.Bus != AudioMixBus.Unchanged).Select(item => AssetDatabase.GetAssetPath(item.Asset)).Concat(new[] { AssetDatabase.GetAssetPath(request.Setup), request.SceneTarget.scene.path, AssetDatabase.GetAssetPath(request.DuckParameter), request.ExistingMixer != null ? AssetDatabase.GetAssetPath(request.ExistingMixer) : TemplatePath }).Where(path => !string.IsNullOrEmpty(path)).Distinct().OrderBy(path => path, StringComparer.Ordinal);
        }
        private static string Fingerprint(IEnumerable<string> paths)
        {
            StringBuilder value = new StringBuilder();
            foreach (string path in paths.OrderBy(item => item, StringComparer.Ordinal)) value.Append(path).Append('|').Append(File.Exists(path) ? AudioProjectPlanner.Hash(Convert.ToBase64String(File.ReadAllBytes(path))) : "missing").Append('|').Append(AssetDatabase.AssetPathToGUID(path));
            return AudioProjectPlanner.Hash(value.ToString());
        }
        private static string Inputs(AudioMixRequest request) => Fingerprint(InputFiles(request).Concat(Outputs(request)).Distinct()) + "|" + Application.unityVersion + "|" + EditorUserBuildSettings.activeBuildTarget;
    }
}
