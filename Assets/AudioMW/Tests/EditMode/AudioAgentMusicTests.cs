using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW.Tests
{
    public sealed class AudioAgentMusicTests
    {
        private string folder;
        private string source;
        private string second;
        private MusicTrack existing;
        private SoundParameter parameter;
        private AudioAgentMusicDraft draft;
        [SetUp] public void SetUp()
        {
            folder = "Assets/AgentMusicTest-" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            source = Path.GetFullPath("Library/music-" + Guid.NewGuid().ToString("N") + ".wav"); second = source + ".wav"; Wave(source, 16000); Wave(second, 16000);
            parameter = ScriptableObject.CreateInstance<SoundParameter>(); AssetDatabase.CreateAsset(parameter, folder + "/Intensity.asset");
            existing = ScriptableObject.CreateInstance<MusicTrack>(); existing.Volume = .3f;
            existing.MixerGroup = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixPlanner.TemplatePath).FindMatchingGroups("").First();
            AssetDatabase.CreateAsset(existing, folder + "/Explore.asset"); AssetDatabase.SaveAssetIfDirty(existing);
            draft = new AudioAgentMusicDraft { Destination = folder, Intent = "Adaptive cafe music", Tracks = new[] { new AudioAgentTrackDraft { Name = "Explore", Purpose = "Cafe exploration", ReplaceTrackGuid = AssetDatabase.AssetPathToGUID(folder + "/Explore.asset"), Tempo = 120, BeatsPerBar = 4, Body = source, Stems = new[] { new AudioAgentStemDraft { Name = "Pulse", File = second, ParameterGuid = AssetDatabase.AssetPathToGUID(folder + "/Intensity.asset"), LowWeight = 0, HighWeight = 1 } } } } };
        }
        [TearDown] public void TearDown() { AudioAgentBridge.Disable(); AssetDatabase.DeleteAsset(folder); File.Delete(source); File.Delete(second); }
        private static void Wave(string path, int frames)
        {
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + frames * 4); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(8000); w.Write(32000); w.Write((short)4); w.Write((short)16); w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(frames * 4);
                for (int i = 0; i < frames; i++) { w.Write((short)(Mathf.Sin(i * .1f) * 2000)); w.Write((short)(Mathf.Sin(i * .15f) * 1500)); }
            }
        }
        private static void Apply(AudioAgentPlan plan)
        {
            try { typeof(AudioAgentPlanner).GetMethod("Apply", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { plan, "music-test" }); }
            catch (TargetInvocationException e) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
        }
        [Test] public void ProposalHasNoAssetWritesAndKeepsImmutableMusicSettings()
        {
            var before = File.ReadAllBytes(folder + "/Explore.asset"); var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); draft.Tracks[0].Tempo = 999;
            Assert.AreEqual(120, plan.Read().Music[0].Settings.Tempo); Assert.IsFalse(Directory.Exists(plan.Read().Folder)); CollectionAssert.AreEqual(before, File.ReadAllBytes(folder + "/Explore.asset"));
        }
        [Test] public void ApprovedStemsPreserveReferencesRouteGainAndEvaluateIntensity()
        {
            string guid = draft.Tracks[0].ReplaceTrackGuid; var route = existing.MixerGroup; var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); Apply(plan); Apply(plan);
            Assert.AreEqual("applied", plan.State); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(folder + "/Explore.asset")); Assert.AreSame(route, existing.MixerGroup); Assert.AreEqual(.3f, existing.Volume);
            Assert.IsTrue(existing.Loop); Assert.AreEqual(120, existing.Tempo); Assert.AreEqual(2, existing.LoopClip.channels); Assert.AreEqual(16000, existing.Layers[0].Clip.samples); Assert.AreSame(parameter, existing.Layers[0].Parameter);
            Assert.AreEqual(0, existing.Layers[0].EvaluateWeight(parameter.MinValue)); Assert.AreEqual(1, existing.Layers[0].EvaluateWeight(parameter.MaxValue));
        }
        [Test] public void NewMusicAndStateProfileUseExplicitRouteAndQuantizedTransitions()
        {
            var track = draft.Tracks[0]; track.ReplaceTrackGuid = ""; track.MixerGroupId = GlobalObjectId.GetGlobalObjectIdSlow(existing.MixerGroup).ToString();
            draft.Profile = new AudioAgentProfileDraft { Name = "Cafe", States = new[] { new AudioAgentStateDraft { Name = "Explore", Track = "Explore" } }, Transitions = new[] { new AudioAgentTransitionDraft { From = "Explore", To = "Explore", Quantization = "Beat", FadeSeconds = .4f } } };
            var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); Apply(plan); var profile = AssetDatabase.LoadAssetAtPath<MusicStateProfile>(plan.Read().Profile.Target.Path);
            Assert.AreEqual("", profile.Validate()); Assert.AreEqual(MusicQuantization.Bar, profile.DefaultQuantization); Assert.AreEqual(MusicQuantization.Beat, profile.Transitions[0].Quantization); Assert.AreEqual(MusicFadeCurve.EqualPower, profile.Transitions[0].Curve); Assert.AreSame(existing.MixerGroup, profile.States[0].Track.MixerGroup);
        }
        [Test] public void StereoMonoStereoReimportPreservesGuidSamplesSourceAndReferences()
        {
            draft.Tracks[0].ForceToMono = true; draft.Tracks[0].Stems[0].ForceToMono = true; var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); Apply(plan);
            string path = plan.Read().Music[0].Files[0].Destination, guid = AssetDatabase.AssetPathToGUID(path); var bytes = File.ReadAllBytes(path); Assert.AreEqual(1, existing.LoopClip.channels);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path); importer.forceToMono = false; importer.SaveAndReimport(); Assert.AreEqual(2, existing.LoopClip.channels);
            float[] data = new float[existing.LoopClip.samples * 2]; Assert.IsTrue(existing.LoopClip.GetData(data, 0)); Assert.IsTrue(Enumerable.Range(0, 100).Any(i => Mathf.Abs(data[i * 2] - data[i * 2 + 1]) > .001f));
            importer.forceToMono = true; importer.SaveAndReimport(); Assert.AreEqual(1, existing.LoopClip.channels); importer.forceToMono = false; importer.SaveAndReimport(); Assert.AreEqual(2, existing.LoopClip.channels);
            Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(path)); Assert.AreEqual(16000, existing.LoopClip.samples); CollectionAssert.AreEqual(bytes, File.ReadAllBytes(path));
        }
        [Test] public void UnsynchronizedStemsRollbackBeforeChangingExistingTrack()
        {
            Wave(second, 32000); var before = File.ReadAllBytes(folder + "/Explore.asset"); var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test");
            Assert.Throws<InvalidOperationException>(() => Apply(plan)); Assert.AreEqual("failed", plan.State); Assert.IsFalse(Directory.Exists(plan.Read().Folder)); CollectionAssert.AreEqual(before, File.ReadAllBytes(folder + "/Explore.asset"));
        }
        [Test] public void IncorrectTempoRollsBackImport()
        {
            draft.Tracks[0].Tempo = 123; var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); Assert.Throws<InvalidOperationException>(() => Apply(plan)); Assert.IsNull(existing.LoopClip); Assert.IsFalse(Directory.Exists(plan.Read().Folder));
        }
        [Test] public void ChangedParametersAndTrackInvalidatePendingReview()
        {
            var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); parameter.name = "Changed"; EditorUtility.SetDirty(parameter); Assert.That(AudioAgentPlanner.Check(plan, "music-test"), Does.Contain("unsaved")); AssetDatabase.SaveAssetIfDirty(parameter); Assert.That(AudioAgentPlanner.Check(plan, "music-test"), Does.Contain("changed"));
        }
        [Test] public void ExistingProfileCannotLoseGameplayStateNames()
        {
            var profile = ScriptableObject.CreateInstance<MusicStateProfile>(); profile.States = new[] { new MusicState { Name = "Combat", Track = existing } }; AssetDatabase.CreateAsset(profile, folder + "/Profile.asset");
            draft.Profile = new AudioAgentProfileDraft { Name = "Cafe", ReplaceProfileGuid = AssetDatabase.AssetPathToGUID(folder + "/Profile.asset"), States = new[] { new AudioAgentStateDraft { Name = "Explore", Track = "Explore" } } };
            Assert.Throws<ArgumentException>(() => AudioAgentMusicPlanner.Prepare(draft, "music-test"));
            draft.Profile.States[0].Name = "Combat"; string guid = draft.Profile.ReplaceProfileGuid; var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); Apply(plan); Assert.AreEqual(guid, AssetDatabase.AssetPathToGUID(folder + "/Profile.asset")); Assert.AreSame(existing, profile.States[0].Track);
        }
        [Test] public void InvalidWeightsMarkersAndTransitionEndpointsAreRejected()
        {
            draft.Tracks[0].Stems[0].HighWeight = float.NaN; Assert.Throws<ArgumentException>(() => AudioAgentMusicPlanner.Prepare(draft, "music-test")); draft.Tracks[0].Stems[0].HighWeight = 1;
            existing.Markers = new[] { MusicMarker.CreateRuntime("Keep", 0, 0) }; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing); Assert.Throws<ArgumentException>(() => AudioAgentMusicPlanner.Prepare(draft, "music-test")); existing.Markers = new MusicMarker[0]; EditorUtility.SetDirty(existing); AssetDatabase.SaveAssetIfDirty(existing);
            draft.Profile = new AudioAgentProfileDraft { Name = "Cafe", States = new[] { new AudioAgentStateDraft { Name = "Explore", Track = "Explore" } }, Transitions = new[] { new AudioAgentTransitionDraft { From = "Unknown", To = "Explore" } } }; Assert.Throws<ArgumentException>(() => AudioAgentMusicPlanner.Prepare(draft, "music-test"));
        }
        [Test] public void StemOnlyTrackSupportsIntroAndConstantWeight()
        {
            draft.Tracks[0].Intro = source; draft.Tracks[0].Body = ""; draft.Tracks[0].Stems[0].ParameterGuid = ""; draft.Tracks[0].Stems[0].LowWeight = .4f; draft.Tracks[0].Stems[0].HighWeight = .4f;
            var plan = AudioAgentMusicPlanner.Prepare(draft, "music-test"); Apply(plan); Assert.IsNotNull(existing.IntroClip); Assert.IsNull(existing.LoopClip); Assert.AreEqual(2, existing.BodyDuration); Assert.AreEqual(.4f, existing.Layers[0].EvaluateWeight(0)); Assert.AreEqual(.4f, existing.Layers[0].EvaluateWeight(1));
        }
        [Test] public void MusicMcpUsesPendingPlanAndCannotApprove()
        {
            AudioAgentBridge.Enable(); var request = new AudioAgentRequest { SchemaVersion = 1, Project = AudioProjectPlanner.ProjectIdentity(), Session = AudioAgentBridge.Session, Operation = "propose_music", MusicDraft = draft };
            var reply = AudioAgentBridge.Execute(request); Assert.AreEqual("pending", reply.State); Assert.AreEqual(1, reply.Plan.Music.Length); Assert.IsFalse(Directory.Exists(reply.Plan.Folder)); request.Operation = "approve"; Assert.Throws<ArgumentException>(() => AudioAgentBridge.Execute(request));
        }
    }
}
