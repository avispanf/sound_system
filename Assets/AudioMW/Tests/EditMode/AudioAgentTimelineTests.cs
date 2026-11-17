using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class AudioAgentTimelineTests
    {
        private string folder, path;
        private MusicArrangement score;
        private SoundParameter parameter;
        private AudioClip source;
        [SetUp] public void SetUp()
        {
            folder = "Assets/TimelineAgentTest-" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            using (var w = new BinaryWriter(File.Create(folder + "/source.wav")))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + 64000); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(8000); w.Write(16000); w.Write((short)2); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(64000); w.Write(new byte[64000]);
            }
            AssetDatabase.ImportAsset(folder + "/source.wav", ImportAssetOptions.ForceSynchronousImport); source = AssetDatabase.LoadAssetAtPath<AudioClip>(folder + "/source.wav");
            parameter = ScriptableObject.CreateInstance<SoundParameter>(); AssetDatabase.CreateAsset(parameter, folder + "/parameter.asset");
            score = ScriptableObject.CreateInstance<MusicArrangement>(); score.Lanes.Add(new ArrangementLane { Name = "Kitchen", Parameter = parameter }); score.Lanes[0].Clips.Add(new ArrangementClip { Source = source, Duration = 4 });
            path = folder + "/score.asset"; AssetDatabase.CreateAsset(score, path); AssetDatabase.SaveAssetIfDirty(score);
        }
        [TearDown] public void TearDown() { AudioAgentBridge.Disable(); Undo.ClearAll(); AssetDatabase.DeleteAsset(folder); }
        private AudioAgentTimelineDraft Draft(params AudioAgentTimelineEdit[] edits)
        {
            string hash = (string)typeof(AudioAgentPlanner).GetMethod("Fingerprint", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { path });
            return new AudioAgentTimelineDraft { Intent = "Disposable timeline test", ArrangementGuid = AssetDatabase.AssetPathToGUID(path), ExpectedHash = hash, Edits = edits };
        }
        private AudioAgentTimelineEdit Edit() => new AudioAgentTimelineEdit { Operation = "edit_clip", ClipId = score.Lanes[0].Clips[0].Id, Start = 1, Trim = .5, Duration = 2, FadeIn = .2, FadeOut = .3, Gain = .7f };
        private static void Apply(AudioAgentPlan plan) => typeof(AudioAgentPlanner).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { plan, "timeline-test" });
        [Test] public void ProposalIsImmutableAndDoesNotWriteAssets()
        {
            var bytes = File.ReadAllBytes(path); var edit = Edit(); var plan = AudioAgentTimelinePlanner.Prepare(Draft(edit), "timeline-test"); edit.Start = 19;
            Assert.AreEqual("pending", plan.State); Assert.AreEqual(0, score.Lanes[0].Clips[0].Start); CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path)); Assert.That(plan.Read().Timeline.After, Does.Contain("start 1s")); Assert.AreEqual("", AudioAgentPlanner.Check(plan, "timeline-test"));
        }
        [Test] public void ApplyRetainsIdentityReferencesCurvesAndSupportsUndoRedo()
        {
            string guid = AssetDatabase.AssetPathToGUID(path), id = score.Lanes[0].Clips[0].Id;
            var plan = AudioAgentTimelinePlanner.Prepare(Draft(Edit()), "timeline-test"); Apply(plan);
            Assert.AreEqual("applied", plan.State); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path)); Assert.AreEqual(id, score.Lanes[0].Clips[0].Id); Assert.AreSame(source, score.Lanes[0].Clips[0].Source); Assert.AreSame(parameter, score.Lanes[0].Parameter); Assert.AreEqual(.5f, score.Lanes[0].Weight.Evaluate(.5f), .001f);
            Undo.PerformUndo(); Assert.AreEqual(0, score.Lanes[0].Clips[0].Start); Undo.PerformRedo(); Assert.AreEqual(1, score.Lanes[0].Clips[0].Start); AssetDatabase.SaveAssetIfDirty(score);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); Assert.AreEqual(.5, AssetDatabase.LoadAssetAtPath<MusicArrangement>(path).Lanes[0].Clips[0].Trim);
        }
        [Test] public void StaleInspectUnsavedTargetAndDirtyDependenciesAreRejected()
        {
            var draft = Draft(Edit()); score.Tempo = 130; EditorUtility.SetDirty(score); Assert.Throws<InvalidOperationException>(() => AudioAgentTimelinePlanner.Prepare(draft, "timeline-test")); AssetDatabase.SaveAssetIfDirty(score); Assert.Throws<InvalidOperationException>(() => AudioAgentTimelinePlanner.Prepare(draft, "timeline-test"));
            var plan = AudioAgentTimelinePlanner.Prepare(Draft(Edit()), "timeline-test"); EditorUtility.SetDirty(parameter); Assert.That(AudioAgentPlanner.Check(plan, "timeline-test"), Does.Contain("unsaved"));
        }
        [Test] public void ChangedSourceAndWrongSessionInvalidatePlan()
        {
            var plan = AudioAgentTimelinePlanner.Prepare(Draft(Edit()), "timeline-test"); Assert.That(AudioAgentPlanner.Check(plan, "other"), Does.Contain("session"));
            var importer = (AudioImporter)AssetImporter.GetAtPath(folder + "/source.wav"); importer.forceToMono = !importer.forceToMono; importer.SaveAndReimport(); Assert.That(AudioAgentPlanner.Check(plan, "timeline-test"), Does.Contain("changed"));
        }
        [Test] public void InvalidTimingAndUnknownIdentitiesAreRejectedWithoutMutation()
        {
            var e = Edit(); e.Duration = 20; Assert.Throws<ArgumentException>(() => AudioAgentTimelinePlanner.Prepare(Draft(e), "timeline-test")); e.Duration = 2; e.FadeIn = 3; Assert.Throws<ArgumentException>(() => AudioAgentTimelinePlanner.Prepare(Draft(e), "timeline-test")); e.FadeIn = .2; e.ClipId = "missing"; Assert.Throws<ArgumentException>(() => AudioAgentTimelinePlanner.Prepare(Draft(e), "timeline-test")); Assert.AreEqual(0, score.Lanes[0].Clips[0].Start);
        }
        [Test] public void DuplicateAddCueBoundsAndRemovalsMatchReviewedState()
        {
            var lane = score.Lanes[0]; var clip = lane.Clips[0];
            var plan = AudioAgentTimelinePlanner.Prepare(Draft(new AudioAgentTimelineEdit { Operation = "duplicate_clip", ClipId = clip.Id, LaneId = lane.Id, Start = 4 }, new AudioAgentTimelineEdit { Operation = "add_cue", Name = "Combat", Start = 4 }, new AudioAgentTimelineEdit { Operation = "set_bounds", Exit = 8, Loop = true, LoopEnd = 8 }), "timeline-test"); Apply(plan);
            Assert.AreEqual(2, score.Lanes[0].Clips.Count); Assert.AreNotEqual(clip.Id, score.Lanes[0].Clips[1].Id); Assert.AreEqual(4, score.Markers[0].Seconds); Assert.IsTrue(score.Loop);
            var remove = AudioAgentTimelinePlanner.Prepare(Draft(new AudioAgentTimelineEdit { Operation = "remove_clip", ClipId = score.Lanes[0].Clips[1].Id }, new AudioAgentTimelineEdit { Operation = "remove_cue", ClipId = score.Markers[0].Id }), "timeline-test"); Apply(remove); Assert.AreEqual(1, score.Lanes[0].Clips.Count); Assert.IsEmpty(score.Markers);
        }
        [Test] public void AddedSourceAndTrackEditsAreExplicitAndValidated()
        {
            var e = Edit(); e.Operation = "add_clip"; e.ClipId = null; e.LaneId = score.Lanes[0].Id; e.SourceGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
            var plan = AudioAgentTimelinePlanner.Prepare(Draft(e, new AudioAgentTimelineEdit { Operation = "edit_lane", LaneId = e.LaneId, Name = "Cafe", Gain = .5f }), "timeline-test"); Apply(plan); Assert.AreEqual("Cafe", score.Lanes[0].Name); Assert.AreEqual(2, score.Lanes[0].Clips.Count); Assert.AreSame(parameter, score.Lanes[0].Parameter);
        }
        [Test] public void BridgeExposesInventoryAndPendingReviewButNoApproval()
        {
            AudioAgentBridge.Enable(); var request = new AudioAgentRequest { SchemaVersion = 1, Session = AudioAgentBridge.Session, Project = AudioProjectPlanner.ProjectIdentity(), Operation = "inspect" };
            var inventory = AudioAgentBridge.Execute(request).Inventory; Assert.IsTrue(inventory.Arrangements.Any(a => a.Path == path));
            request.ArrangementGuid = AssetDatabase.AssetPathToGUID(path); Assert.That(AudioAgentBridge.Execute(request).Timeline.Detail, Does.Contain(score.Lanes[0].Clips[0].Id));
            request.Operation = "propose_timeline"; request.TimelineDraft = Draft(Edit()); var reply = AudioAgentBridge.Execute(request); Assert.AreEqual("pending", reply.State); Assert.IsNotNull(reply.Plan.Timeline); request.Operation = "approve"; Assert.Throws<ArgumentException>(() => AudioAgentBridge.Execute(request)); Assert.AreEqual(0, score.Lanes[0].Clips[0].Start);
        }
    }
}
