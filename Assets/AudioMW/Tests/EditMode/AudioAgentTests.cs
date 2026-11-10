using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using AudioMW.Editor;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class AudioAgentTests
    {
        private string folder;
        private string source;
        private SoundEvent sound;
        private AudioAgentDraft draft;
        [SetUp] public void SetUp()
        {
            folder = "Assets/AgentTest-" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            source = Path.GetFullPath("Library/agent-test-" + Guid.NewGuid().ToString("N") + ".wav"); WriteWave(source);
            sound = ScriptableObject.CreateInstance<SoundEvent>(); sound.Volume = .27f; sound.Loop = true;
            AssetDatabase.CreateAsset(sound, folder + "/Existing.asset"); AssetDatabase.SaveAssetIfDirty(sound);
            draft = new AudioAgentDraft { Destination = folder, Intent = "Use supplied microwave variation", Events = new[] { new AudioAgentEventDraft { Name = "Microwave", Purpose = "E interaction", ReplaceEventGuid = AssetDatabase.AssetPathToGUID(folder + "/Existing.asset"), Files = new[] { source } } } };
        }
        [TearDown] public void TearDown() { AudioAgentBridge.Disable(); AssetDatabase.DeleteAsset(folder); if (File.Exists(source)) File.Delete(source); }
        private static void WriteWave(string path)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + 1600); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(8000); w.Write(16000); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(1600); for (int i = 0; i < 800; i++) w.Write((short)(Mathf.Sin(i * .2f) * 1000));
            }
        }
        private static void Internal(Type type, string method, params object[] args)
        {
            try { type.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
            catch (TargetInvocationException error) { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        }
        private static void Apply(AudioAgentPlan plan, string session = "test") => Internal(typeof(AudioAgentPlanner), "Apply", plan, session);
        [Test] public void DraftAndRejectDoNotImportOrModifyAssets()
        {
            string before = File.ReadAllText(folder + "/Existing.asset"); var plan = AudioAgentPlanner.Prepare(draft, "test");
            Assert.IsFalse(Directory.Exists(plan.Read().Folder)); Assert.AreEqual(before, File.ReadAllText(folder + "/Existing.asset"));
            Internal(typeof(AudioAgentBridge), "Reject", plan); Assert.AreEqual("rejected", plan.State); Assert.Throws<InvalidOperationException>(() => Apply(plan)); Assert.AreEqual(before, File.ReadAllText(folder + "/Existing.asset"));
        }
        [Test] public void ApplyPreservesGuidGainLoopAndImportsRealClipExactlyOnce()
        {
            var plan = AudioAgentPlanner.Prepare(draft, "test"); string guid = draft.Events[0].ReplaceEventGuid; Apply(plan); Apply(plan);
            Assert.AreEqual("applied", plan.State); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(folder + "/Existing.asset")); Assert.AreEqual(.27f, sound.Volume); Assert.IsTrue(sound.Loop); Assert.AreEqual(1, sound.Clips.Length); Assert.AreEqual(800, sound.Clips[0].samples); Assert.AreEqual(1, Directory.GetFiles(plan.Read().Folder, "*.wav").Length);
        }
        [Test] public void ChangedAttachmentRequiresFreshReview()
        {
            var plan = AudioAgentPlanner.Prepare(draft, "test"); using (var file = File.OpenWrite(source)) { file.Position = 50; file.WriteByte(17); }
            Assert.That(AudioAgentPlanner.Check(plan, "test"), Does.Contain("attachment changed")); Assert.Throws<InvalidOperationException>(() => Apply(plan)); Assert.IsFalse(Directory.Exists(plan.Read().Folder));
        }
        [Test] public void ChangedEventAndUnsavedEditsInvalidateReview()
        {
            var plan = AudioAgentPlanner.Prepare(draft, "test"); sound.Volume = .8f; EditorUtility.SetDirty(sound);
            Assert.That(AudioAgentPlanner.Check(plan, "test"), Does.Contain("unsaved")); AssetDatabase.SaveAssetIfDirty(sound); Assert.That(AudioAgentPlanner.Check(plan, "test"), Does.Contain("event changed"));
        }
        [Test] public void WrongSessionOrReusedDestinationBlocksApply()
        {
            var plan = AudioAgentPlanner.Prepare(draft, "test"); Assert.Throws<InvalidOperationException>(() => Apply(plan, "other"));
            Directory.CreateDirectory(plan.Read().Folder); File.WriteAllText(plan.Read().Folder + "/keep.txt", "keep"); Assert.Throws<InvalidOperationException>(() => Apply(plan)); Assert.AreEqual("keep", File.ReadAllText(plan.Read().Folder + "/keep.txt"));
        }
        [Test] public void MissingFileTraversalAndNonAudioFailBeforeImport()
        {
            draft.Destination = "Assets/../Outside"; Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test")); draft.Destination = folder;
            draft.Events[0].Files[0] = source + "missing"; Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test")); draft.Events[0].Files[0] = source;
            File.WriteAllText(source, "this is not an audio header"); Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test"));
        }
        [Test] public void NewEventRequiresExplicitRouteAndGetsNoGameplayHook()
        {
            draft.Events[0].ReplaceEventGuid = null; Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test"));
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixPlanner.TemplatePath); var group = mixer.FindMatchingGroups("").First();
            draft.Events[0].MixerGroupId = GlobalObjectId.GetGlobalObjectIdSlow(group).ToString(); var plan = AudioAgentPlanner.Prepare(draft, "test"); Apply(plan);
            var created = AssetDatabase.LoadAssetAtPath<SoundEvent>(plan.Read().Changes[0].EventPath); Assert.AreSame(group, created.MixerGroup); Assert.IsFalse(created.Loop); Assert.AreEqual(1, created.Clips.Length);
        }
        [Test] public void LayeredAndWeightedReplacementsAreRejected()
        {
            sound.ContainerMode = ContainerMode.Layered; EditorUtility.SetDirty(sound); AssetDatabase.SaveAssetIfDirty(sound); Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test"));
            sound.ContainerMode = ContainerMode.Simple; sound.SelectionMode = ClipSelectionMode.WeightedRandom; EditorUtility.SetDirty(sound); AssetDatabase.SaveAssetIfDirty(sound); Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test"));
        }
        [Test] public void ReviewJsonIsImmutableAndDuplicateTargetsRejected()
        {
            var plan = AudioAgentPlanner.Prepare(draft, "test"); var copy = plan.Read(); copy.Changes[0].Files[0].Source = "evil"; draft.Events[0].Purpose = "changed";
            Assert.AreEqual(source, plan.Read().Changes[0].Files[0].Source); Assert.AreEqual("E interaction", plan.Read().Changes[0].Purpose);
            draft.Events = new[] { draft.Events[0], draft.Events[0] }; Assert.Throws<ArgumentException>(() => AudioAgentPlanner.Prepare(draft, "test"));
        }
        [Test] public void McpHasNoApprovalOperationAndReloadInvalidatesPlans()
        {
            AudioAgentBridge.Enable(); string session = AudioAgentBridge.Session;
            var request = new AudioAgentRequest { SchemaVersion = 1, Project = AudioProjectPlanner.ProjectIdentity(), Session = session, Operation = "propose", Draft = draft };
            var reply = AudioAgentBridge.Execute(request); Assert.IsTrue(reply.Ok); Assert.AreEqual("pending", reply.State);
            request.Operation = "approve"; Assert.Throws<ArgumentException>(() => AudioAgentBridge.Execute(request)); Assert.IsFalse(Directory.Exists(reply.Plan.Folder));
            AudioAgentBridge.Disable(); AudioAgentBridge.Enable(); request.Operation = "status"; request.PlanId = reply.Plan.Id; Assert.Throws<InvalidOperationException>(() => AudioAgentBridge.Execute(request));
            request.Session = AudioAgentBridge.Session; Assert.Throws<ArgumentException>(() => AudioAgentBridge.Execute(request));
        }
        [Test] public void ForeignProjectCannotInspectOrPropose()
        {
            AudioAgentBridge.Enable(); Assert.Throws<InvalidOperationException>(() => AudioAgentBridge.Execute(new AudioAgentRequest { SchemaVersion = 1, Project = "foreign", Session = AudioAgentBridge.Session, Operation = "inspect" }));
        }
        [UnityTest] public IEnumerator MailboxRetriesAreIdempotentAndConflictingIdsDoNotLoseTheOriginalPlan()
        {
            AudioAgentBridge.Enable(); string id = Guid.NewGuid().ToString("N");
            var request = new AudioAgentRequest { Id = id, SchemaVersion = 1, Project = AudioProjectPlanner.ProjectIdentity(), Session = AudioAgentBridge.Session, Operation = "propose", Draft = draft };
            string input = Path.Combine(AudioAgentBridge.Root, request.Session, "requests", id + ".json"), output = Path.Combine(AudioAgentBridge.Root, request.Session, "responses", id + ".json");
            string original = JsonUtility.ToJson(request); string planId = null;
            for (int i = 0; i < 4; i++)
            {
                request.Draft.Intent = "conflicting request";
                File.WriteAllText(input, i == 2 ? JsonUtility.ToJson(request) : original);
                float until = Time.realtimeSinceStartup + 3;
                while (File.Exists(input) && Time.realtimeSinceStartup < until) yield return null;
                Assert.IsFalse(File.Exists(input)); var reply = JsonUtility.FromJson<AudioAgentReply>(File.ReadAllText(output));
                if (i == 2) { Assert.IsFalse(reply.Ok); Assert.That(reply.Error, Does.Contain("reused")); }
                else { Assert.IsTrue(reply.Ok); if (planId == null) planId = reply.Plan.Id; Assert.AreEqual(planId, reply.Plan.Id); }
                Assert.AreEqual(1, AudioAgentBridge.Plans.Count()); Assert.AreEqual("pending", AudioAgentBridge.Plans.First().State);
            }
        }
    }
}
