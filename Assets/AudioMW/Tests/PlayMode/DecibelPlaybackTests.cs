using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class DecibelPlaybackTests
    {
        private readonly List<Object> assets = new List<Object>();
        [SetUp] public void SetUp() { AudioSystem.Shutdown(); AudioListener.pause = false; }
        [TearDown] public void TearDown()
        {
            AudioSystem.Shutdown();
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
        }
        private SoundEvent Event()
        {
            AudioClip clip = AudioTestUtil.MakeSine(1); assets.Add(clip);
            SoundEvent item = SoundEvent.CreateRuntime(clip); assets.Add(item); item.SpatialBlend = 0; return item;
        }
        [UnityTest] public IEnumerator ParentChildAndLeafDecibelsCombineInTheActualVoice()
        {
            SoundEvent leaf = Event(); leaf.VolumeDb = -6; leaf.VolumeRandomDbMin = leaf.VolumeRandomDbMax = -2;
            SoundEvent root = Event(); root.ContainerMode = ContainerMode.Layered; root.VolumeDb = -6;
            SoundEventChild child = SoundEventChild.Create(leaf); child.GainDb = -3; root.Children = new[] { child };
            SoundHandle handle = AudioSystem.PlayTracked(root);
            Assert.AreEqual(-17, AudioGain.ToDecibels(handle.Voice.Source.volume), 0.0001f);
            yield return null;
        }
        [UnityTest] public IEnumerator MusicRuntimeDecibelsLeaveTheTrackAssetUntouched()
        {
            SoundEvent item = Event(); MusicTrack track = MusicTrack.CreateRuntime(null, item.GetClip(0), 120, 4); assets.Add(track);
            track.VolumeDb = -6; AudioSystem.PlayMusic(track); AudioSystem.Music.SetVolumeDb(-12);
            Assert.AreEqual(-12, AudioSystem.Music.VolumeDb, 0.0001f); Assert.AreEqual(-6, track.VolumeDb, 0.0001f);
            yield return null;
        }
        [UnityTest] public IEnumerator DebuggerCapturesOcclusionAndUnityVolumeLimiting()
        {
            SoundEvent item = Event(); item.VolumeDb = -6.0206f;
            SoundParameter gain = SoundParameter.CreateRuntime(0, 1, 1); assets.Add(gain);
            item.ParameterBindings = new[] { ParameterBinding.CreateRuntime(gain, ParameterTarget.Volume, AnimationCurve.Linear(0, 4, 1, 4)) };
            Voice voice = AudioSystem.Play(item); voice.ApplyOcclusion(0.25f, 22000);
            AudioSystem.Debugger.Record(item, PlaybackOutcome.Played, voice, Vector3.zero, false);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(0.25f, record.OcclusionMultiplier); Assert.AreEqual(0.5f, record.FinalVolume, 0.00001f);
            Assert.AreEqual(-6.0206f, record.FinalVolumeDb, 0.0001f); Assert.IsFalse(record.WasVolumeLimited);
            StringAssert.Contains("occlusion", record.DescribeVolumeChain()); StringAssert.Contains("dB", record.DescribeVolumeDecibels());
            voice.ClearOcclusion(); AudioSystem.Debugger.Record(item, PlaybackOutcome.Played, voice, Vector3.zero, false);
            AudioSystem.Debugger.TryGetLast(out record); Assert.IsTrue(record.WasVolumeLimited); Assert.AreEqual(1, record.FinalVolume);
            StringAssert.Contains("limited", record.DescribeVolumeDecibels());
            yield return null;
        }
        [UnityTest] public IEnumerator SilentDecibelEventProducesAnExplicitMuteDiagnostic()
        {
            SoundEvent item = Event(); item.VolumeDb = float.NegativeInfinity;
            SoundHandle handle = AudioSystem.PlayTracked(item); Assert.AreEqual(0, handle.Voice.Source.volume);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(float.NegativeInfinity, record.FinalVolumeDb); StringAssert.Contains("−∞", record.DescribeVolumeDecibels());
            yield return null;
        }
    }
}
