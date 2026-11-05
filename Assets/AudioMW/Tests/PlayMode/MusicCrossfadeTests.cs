using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class MusicCrossfadeTests
    {
        private readonly List<Object> assets = new List<Object>();
        private MusicPlayer Player { get { return AudioSystem.Music; } }

        [SetUp]
        public void SetUp() { AudioSystem.StopMusic(); Player.SetVolume(1); }
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopMusic(); Player.SetVolume(1);
            foreach (Object item in assets) if (item != null) Object.Destroy(item);
            assets.Clear();
        }
        private MusicTrack Track(float seconds = 0.5f)
        {
            AudioClip clip = AudioTestUtil.MakeSine(seconds);
            MusicTrack track = MusicTrack.CreateRuntime(null, clip, 240, 4);
            track.Volume = 0.05f;
            assets.Add(clip); assets.Add(track);
            return track;
        }

        [UnityTest]
        public IEnumerator CancelledTransitionLeavesCurrentTrackPlayingBeyondOldBoundary()
        {
            MusicTrack original = Track();
            Player.Play(original);
            Player.TransitionTo(Track(), MusicQuantization.Beat);
            double boundary = Player.PendingStartDspTime;
            Assert.AreEqual(MusicQuantization.Beat, Player.PendingQuantization);
            Assert.AreEqual(MusicFadeCurve.Linear, Player.PendingCurve);
            Assert.AreEqual(0, Player.PendingFadeSeconds);
            Assert.IsTrue(Player.CancelTransition());
            yield return AudioTestUtil.WaitUntil(() => AudioSettings.dspTime > boundary + 0.1, "cancelled boundary to pass");
            Assert.AreSame(original, Player.CurrentTrack);
            Assert.IsFalse(Player.IsTransitionPending);
            Assert.AreEqual(1, Player.TransitionGain, 0.001);
            Assert.IsTrue(Sources().Exists(source => source.isPlaying && source.clip == original.LoopClip));
        }

        [UnityTest]
        public IEnumerator CrossfadeRetainsOutgoingTrackUntilTheEnvelopeEnds()
        {
            MusicTrack original = Track(1);
            MusicTrack next = Track(1);
            Player.Play(original);
            Player.TransitionTo(next, MusicQuantization.Immediate, 0.6f, MusicFadeCurve.EqualPower);
            yield return AudioTestUtil.WaitUntil(() => Player.CurrentTrack == next, "crossfade boundary");
            Assert.IsTrue(Player.IsCrossfading);
            Assert.IsTrue(Sources().Exists(source => source.clip == original.LoopClip));
            Assert.That(Player.TransitionGain, Is.InRange(0f, 0.8f));
            yield return AudioTestUtil.WaitUntil(() => !Player.IsCrossfading, "crossfade cleanup");
            Assert.AreEqual(1, Player.TransitionGain, 0.001);
            Assert.IsFalse(Sources().Exists(source => source.clip == original.LoopClip));
        }

        [UnityTest]
        public IEnumerator ReturningToCurrentStateCancelsPendingDestination()
        {
            MusicTrack current = Track();
            Player.Play(current);
            double start = Player.Clock.StartDspTime;
            Player.TransitionTo(Track(), MusicQuantization.Bar, 0.4f);
            Player.TransitionTo(current, MusicQuantization.Bar, 0.4f);
            Assert.IsFalse(Player.IsTransitionPending);
            Assert.AreEqual(start, Player.Clock.StartDspTime);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReplacementAdoptsOnlyLastPendingDestination()
        {
            Player.Play(Track());
            MusicTrack rejected = Track();
            MusicTrack last = Track();
            Player.TransitionTo(rejected, MusicQuantization.Bar, 0.5f);
            Player.TransitionTo(last, MusicQuantization.Immediate, 0.2f);
            yield return AudioTestUtil.WaitUntil(() => Player.CurrentTrack == last && !Player.IsCrossfading, "replacement and fade to finish");
            Assert.IsFalse(Sources().Exists(source => source.clip == rejected.LoopClip));
        }

        [UnityTest]
        public IEnumerator RapidTransitionsDuringFadeKeepEarlierTailAlive()
        {
            Player.Play(Track(1));
            MusicTrack middle = Track(1), last = Track(1);
            Player.TransitionTo(middle, MusicQuantization.Immediate, 0.6f);
            yield return AudioTestUtil.WaitUntil(() => Player.CurrentTrack == middle, "first boundary");
            Player.TransitionTo(last, MusicQuantization.Immediate, 0.3f);
            yield return AudioTestUtil.WaitUntil(() => Player.CurrentTrack == last, "second boundary");
            Assert.GreaterOrEqual(Player.RetiringTrackCount, 2);
            yield return AudioTestUtil.WaitUntil(() => !Player.IsCrossfading, "all fade tails to end");
            Assert.AreSame(last, Player.CurrentTrack);
        }

        [UnityTest]
        public IEnumerator StopCancelsScheduledStingersAndEveryFadeTail()
        {
            Player.Play(Track());
            Player.PlayStinger(Track().LoopClip, MusicQuantization.Bar);
            Player.PlayStinger(Track().LoopClip, MusicQuantization.Bar);
            Assert.AreEqual(2, Sources().FindAll(source => source.name.StartsWith("Music Stinger") && source.clip != null).Count);
            Player.TransitionTo(Track(), MusicQuantization.Immediate, 1);
            yield return AudioTestUtil.WaitUntil(() => Player.IsCrossfading, "fade start");
            Player.Stop();
            yield return null;
            Assert.IsFalse(Player.IsPlaying);
            Assert.IsFalse(Player.IsCrossfading);
            Assert.IsFalse(Sources().Exists(source => source.clip != null));
        }

        [UnityTest]
        public IEnumerator RuntimeVolumeDoesNotModifyTrackAsset()
        {
            MusicTrack track = Track();
            Player.Play(track);
            Player.SetVolume(0.25f);
            Assert.AreEqual(0.05f, track.Volume);
            Assert.AreEqual(0.25f, Player.Volume);
            yield return null;
            Assert.That(Sources().Find(source => source.clip == track.LoopClip).volume, Is.EqualTo(0.0125f).Within(0.001f));
        }

        [UnityTest]
        public IEnumerator IntroOnlyTrackEndsAndCanBeUsedAsTransitionTarget()
        {
            MusicTrack intro = Track(0.2f);
            intro.IntroClip = intro.LoopClip;
            intro.LoopClip = null;
            Player.Play(Track());
            Player.TransitionTo(intro, MusicQuantization.Immediate, 0.05f);
            yield return AudioTestUtil.WaitUntil(() => Player.CurrentTrack == intro, "intro-only destination");
            Assert.IsTrue(Sources().Exists(source => source.clip == intro.IntroClip));
            yield return AudioTestUtil.WaitUntil(() => !Player.IsPlaying, "intro-only completion");
        }

        [UnityTest]
        public IEnumerator NativeAudioCallbackProcessesTheScheduledMusic()
        {
            bool hasListener = false;
            foreach (AudioListener item in Object.FindObjectsOfType<AudioListener>()) if (item.isActiveAndEnabled) hasListener = true;
            if (!hasListener)
            {
                GameObject listener = new GameObject("Crossfade DSP Listener");
                listener.AddComponent<AudioListener>();
                assets.Add(listener);
            }
            MusicTrack track = Track(1);
            Player.Play(track);
            MusicDspGain gain = Sources().Find(source => source.clip == track.LoopClip).GetComponent<MusicDspGain>();
            yield return AudioTestUtil.WaitUntil(() => gain.ProcessedFrames > 0, "native music DSP callback");
            Assert.Greater(gain.ProcessedFrames, 0);
        }

        [UnityTest]
        public IEnumerator AudioThreadCompletesEnvelopeWhileMainThreadIsStalled()
        {
            bool hasListener = false;
            foreach (AudioListener item in Object.FindObjectsOfType<AudioListener>()) if (item.isActiveAndEnabled) hasListener = true;
            if (!hasListener)
            {
                GameObject listener = new GameObject("Stall DSP Listener");
                listener.AddComponent<AudioListener>(); assets.Add(listener);
            }
            MusicTrack first = Track(2), next = Track(2);
            Player.Play(first);
            MusicDspGain outgoing = Sources().Find(source => source.clip == first.LoopClip).GetComponent<MusicDspGain>();
            yield return AudioTestUtil.WaitUntil(() => outgoing.ProcessedFrames > 0, "audio engine to begin");
            Player.TransitionTo(next, MusicQuantization.Immediate, 0.15f);
            MusicDspGain incoming = Sources().Find(source => source.clip == next.LoopClip).GetComponent<MusicDspGain>();
            System.Threading.Thread.Sleep(450);
            Assert.Greater(incoming.ProcessedFrames, 0);
            Assert.AreEqual(1, incoming.Envelope.Evaluate(AudioSettings.dspTime), 0.001);
            Assert.AreEqual(0, outgoing.Envelope.Evaluate(AudioSettings.dspTime), 0.001);
            yield return AudioTestUtil.WaitUntil(() => Player.CurrentTrack == next && !Player.IsCrossfading, "main-thread cleanup after stall");
        }

        private static List<AudioSource> Sources()
        {
            return new List<AudioSource>(AudioRuntime.Instance.GetComponentsInChildren<AudioSource>());
        }
    }
}
