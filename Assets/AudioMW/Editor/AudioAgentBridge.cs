using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioAgentDescriptor
    {
        public int SchemaVersion = 1;
        public string Project;
        public string ProjectPath;
        public string Session;
        public long Heartbeat;
    }
    [Serializable] public sealed class AudioAgentRequest
    {
        public int SchemaVersion;
        public string Id;
        public string Project;
        public string Session;
        public string Operation;
        public string PlanId;
        public AudioAgentDraft Draft;
        public AudioAgentMusicDraft MusicDraft;
    }
    [Serializable] public sealed class AudioAgentReply
    {
        public int SchemaVersion = 1;
        public bool Ok;
        public string Error;
        public string State;
        public string Result;
        public string Digest;
        public AudioAgentReview Plan;
        public AudioAgentInventory Inventory;
    }
    [Serializable] public sealed class AudioAgentInventory
    {
        public string Project;
        public string ProjectPath;
        public string Session;
        public string Unity;
        public string Platform;
        public AudioAgentAsset[] Events;
        public AudioAgentAsset[] Groups;
        public AudioAgentAsset[] Tracks;
        public AudioAgentAsset[] Profiles;
        public AudioAgentAsset[] Parameters;
        public AudioAgentAsset[] Setups;
        public bool Truncated;
    }
    [Serializable] public sealed class AudioAgentAsset
    {
        public string Name;
        public string Path;
        public string Id;
        public string Detail;
    }
    [InitializeOnLoad] public static class AudioAgentBridge
    {
        private static readonly Dictionary<string, AudioAgentPlan> plans = new Dictionary<string, AudioAgentPlan>();
        private static readonly Dictionary<string, string> requests = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> replies = new Dictionary<string, string>();
        private static double nextPoll;
        public static string Session { get; private set; }
        public static bool Enabled => !string.IsNullOrEmpty(Session);
        public static IEnumerable<AudioAgentPlan> Plans => plans.Values.Reverse();
        public static string Root => Path.GetFullPath("Library/AudioMWAgent");
        private static string SessionRoot => Path.Combine(Root, Session);
        public static event Action Changed;
        static AudioAgentBridge()
        {
            EditorApplication.update += Poll;
            AssemblyReloadEvents.beforeAssemblyReload += Disable;
            EditorApplication.quitting += Disable;
        }
        public static void Enable()
        {
            AudioAgentPlanner.Ready(); if (Enabled) return;
            AudioAgentPlanner.NoLinks(Root); Directory.CreateDirectory(Root);
            Session = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(Path.Combine(SessionRoot, "requests")); Directory.CreateDirectory(Path.Combine(SessionRoot, "responses"));
            Publish(); Changed?.Invoke();
        }
        public static void Disable()
        {
            if (!Enabled) return;
            foreach (var plan in plans.Values.Where(value => value.State == "pending")) { plan.State = "expired"; plan.Result = "Bridge stopped or editor reloaded. Propose again."; }
            string file = Path.Combine(Root, "connection.json");
            if (File.Exists(file)) File.Delete(file);
            Session = null; plans.Clear(); requests.Clear(); replies.Clear(); Changed?.Invoke();
        }
        private static void Publish()
        {
            var descriptor = new AudioAgentDescriptor { Project = AudioProjectPlanner.ProjectIdentity(), ProjectPath = Path.GetDirectoryName(Application.dataPath), Session = Session, Heartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            Write(Path.Combine(Root, "connection.json"), JsonUtility.ToJson(descriptor));
        }
        private static void Poll()
        {
            if (!Enabled || EditorApplication.timeSinceStartup < nextPoll) return;
            nextPoll = EditorApplication.timeSinceStartup + .3;
            try
            {
                Publish();
                foreach (string path in Directory.EnumerateFiles(Path.Combine(SessionRoot, "requests"), "*.json").Take(4))
                {
                    string id = Path.GetFileNameWithoutExtension(path);
                    if (!Guid.TryParseExact(id, "N", out _)) { File.Delete(path); continue; }
                    string output = Path.Combine(SessionRoot, "responses", id + ".json");
                    AudioAgentReply reply;
                    try
                    {
                        AudioAgentPlanner.NoLinks(path);
                        if (new FileInfo(path).Length > 128 * 1024) throw new ArgumentException("Request too large.");
                        string json = File.ReadAllText(path); var request = JsonUtility.FromJson<AudioAgentRequest>(json);
                        if (request == null || request.Id != id) throw new ArgumentException("Request identity mismatch.");
                        string hash = AudioProjectPlanner.Hash(json);
                        if (requests.TryGetValue(id, out string previous))
                        {
                            if (hash != previous) throw new ArgumentException("Request ID reused with different content.");
                            Write(output, replies[id]); File.Delete(path); continue;
                        }
                        if (requests.Count >= 512) throw new InvalidOperationException("Session request limit reached. Restart the bridge in Unity.");
                        reply = Execute(request); requests.Add(id, hash); replies.Add(id, JsonUtility.ToJson(reply, true));
                    }
                    catch (Exception error) { reply = new AudioAgentReply { Ok = false, Error = error.Message }; }
                    Write(output, JsonUtility.ToJson(reply, true)); File.Delete(path);
                }
            }
            catch (Exception error) { Debug.LogWarning("AudioMW agent bridge stopped: " + error.Message); Disable(); }
        }
        public static AudioAgentReply Execute(AudioAgentRequest request)
        {
            if (!Enabled || request == null || request.SchemaVersion != 1 || request.Project != AudioProjectPlanner.ProjectIdentity() || request.Session != Session) throw new InvalidOperationException("Wrong project, protocol or editor session. Reconnect and propose again.");
            AudioAgentPlanner.Ready();
            if (request.Operation == "inspect") return new AudioAgentReply { Ok = true, Inventory = Inspect(), State = "ready" };
            if (request.Operation == "propose" || request.Operation == "propose_music")
            {
                if (plans.Count >= 64) throw new InvalidOperationException("Review limit reached. Restart the bridge in Unity.");
                var plan = request.Operation == "propose_music" ? AudioAgentMusicPlanner.Prepare(request.MusicDraft, Session) : AudioAgentPlanner.Prepare(request.Draft, Session); plans.Add(plan.Read().Id, plan); Changed?.Invoke(); return Reply(plan);
            }
            if (request.Operation != "status" && request.Operation != "review") throw new ArgumentException("Unknown operation. Applying or approving through MCP is not supported.");
            if (string.IsNullOrEmpty(request.PlanId) || !plans.TryGetValue(request.PlanId, out var selected)) throw new ArgumentException("Plan unavailable in this editor session. Propose again.");
            if (request.Operation == "review") AudioAgentReviewWindow.ShowPlan(request.PlanId);
            return Reply(selected);
        }
        internal static AudioAgentPlan Find(string id) => id != null && plans.TryGetValue(id, out var value) ? value : null;
        internal static void Reject(AudioAgentPlan plan)
        {
            if (plan == null || plan.State != "pending") return;
            plan.State = "rejected"; plan.Result = "Rejected in Unity. No assets changed."; Receipt(plan); Changed?.Invoke();
        }
        internal static void Approve(AudioAgentPlan plan)
        {
            if (!Enabled) throw new InvalidOperationException("Enable the bridge again and prepare a new plan.");
            Receipt(plan);
            try { AudioAgentPlanner.Apply(plan, Session); }
            finally { Receipt(plan); Changed?.Invoke(); }
        }
        private static void Receipt(AudioAgentPlan plan)
        {
            string folder = Path.Combine(Root, "receipts"); Directory.CreateDirectory(folder);
            Write(Path.Combine(folder, plan.Read().Id + ".json"), JsonUtility.ToJson(Reply(plan), true));
        }
        private static AudioAgentReply Reply(AudioAgentPlan plan) => new AudioAgentReply { Ok = true, State = plan.State, Result = plan.Result, Plan = plan.Read(), Digest = plan.Digest };
        private static AudioAgentInventory Inspect()
        {
            string[] ids = AssetDatabase.FindAssets("t:SoundEvent", new[] { "Assets" });
            var groups = new List<AudioAgentAsset>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" }).Take(32))
            {
                var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(guid)); if (mixer == null) continue;
                foreach (var group in mixer.FindMatchingGroups("")) groups.Add(new AudioAgentAsset { Name = group.name, Path = AssetDatabase.GetAssetPath(group), Id = GlobalObjectId.GetGlobalObjectIdSlow(group).ToString(), Detail = "Existing mixer group; gains are not changed by import." });
            }
            return new AudioAgentInventory
            {
                Project = AudioProjectPlanner.ProjectIdentity(), ProjectPath = Path.GetDirectoryName(Application.dataPath), Session = Session, Unity = Application.unityVersion, Platform = EditorUserBuildSettings.activeBuildTarget.ToString(), Truncated = ids.Length > 256 || groups.Count > 256,
                Events = ids.Take(256).Select(guid => { string path = AssetDatabase.GUIDToAssetPath(guid); var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>(path); return new AudioAgentAsset { Name = sound.name, Id = guid, Path = path, Detail = sound.ContainerMode + " / " + sound.Clips.Length + " clips / " + (sound.MixerGroup != null ? sound.MixerGroup.name : "default output") }; }).ToArray(),
                Groups = groups.Take(256).ToArray(),
                Tracks = Assets<MusicTrack>("MusicTrack", track => JsonUtility.ToJson(track)),
                Profiles = Assets<MusicStateProfile>("MusicStateProfile", profile => JsonUtility.ToJson(profile)),
                Parameters = Assets<SoundParameter>("SoundParameter", parameter => JsonUtility.ToJson(parameter)),
                Setups = AssetDatabase.FindAssets("t:AudioProjectSetup", new[] { "Assets" }).Take(16).Select(guid => { string path = AssetDatabase.GUIDToAssetPath(guid); var setup = AssetDatabase.LoadAssetAtPath<AudioProjectSetup>(path); return new AudioAgentAsset { Name = setup.name, Id = guid, Path = path, Detail = JsonUtility.ToJson(setup.Brief) }; }).ToArray()
            };
        }
        private static AudioAgentAsset[] Assets<T>(string type, Func<T, string> detail) where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }).Take(256).Select(guid => { string path = AssetDatabase.GUIDToAssetPath(guid); var asset = AssetDatabase.LoadAssetAtPath<T>(path); return new AudioAgentAsset { Name = asset.name, Id = guid, Path = path, Detail = detail(asset) }; }).ToArray();
        private static void Write(string path, string json)
        {
            AudioAgentPlanner.NoLinks(path); string temp = path + ".tmp"; AudioAgentPlanner.NoLinks(temp); File.WriteAllText(temp, json);
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
    }
}
