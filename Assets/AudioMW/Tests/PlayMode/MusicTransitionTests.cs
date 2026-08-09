using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class MusicTransitionTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopMusic();
        }

        [UnityTest]
        public IEnumerator TransitionFromIdleStartsPlayback()
        {
            MusicTrack track = MakeTrack(120.0);

            AudioSystem.TransitionMusic(track, MusicQuantization.Immediate);

            Assert.IsTrue(AudioSystem.Music.IsPlaying);
            Assert.AreSame(track, AudioSystem.Music.CurrentTrack);
            Assert.IsFalse(AudioSystem.Music.IsTransitionPending);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TransitionSchedulesPendingTrack()
        {
            MusicTrack first = MakeTrack(120.0);
            MusicTrack second = MakeTrack(90.0);

            AudioSystem.PlayMusic(first);

            yield return null;

            AudioSystem.TransitionMusic(second, MusicQuantization.Bar);

            Assert.IsTrue(AudioSystem.Music.IsTransitionPending);
            Assert.AreSame(second, AudioSystem.Music.PendingTrack);
            Assert.AreSame(first, AudioSystem.Music.CurrentTrack);
        }

        [UnityTest]
        public IEnumerator PendingStartLandsOnBarBoundary()
        {
            MusicTrack first = MakeTrack(120.0);
            MusicTrack second = MakeTrack(120.0);

            AudioSystem.PlayMusic(first);

            yield return null;

            AudioSystem.TransitionMusic(second, MusicQuantization.Bar);

            double start = AudioSystem.Music.Clock.StartDspTime;
            double pending = AudioSystem.Music.PendingStartDspTime;
            double bars = (pending - start) / AudioSystem.Music.Clock.SecondsPerBar;

            Assert.AreEqual(System.Math.Round(bars), bars, 1e-6);
            Assert.Greater(pending, AudioSettings.dspTime);
        }

        [UnityTest]
        public IEnumerator TransitionCompletesAndAdoptsNewTempo()
        {
            MusicTrack first = MakeTrack(120.0);
            MusicTrack second = MakeTrack(90.0);

            AudioSystem.PlayMusic(first);

            yield return null;

            AudioSystem.TransitionMusic(second, MusicQuantization.Immediate);

            yield return AudioTestUtil.WaitUntil(() => !AudioSystem.Music.IsTransitionPending, "transition to complete");

            Assert.IsFalse(AudioSystem.Music.IsTransitionPending);
            Assert.AreSame(second, AudioSystem.Music.CurrentTrack);
            Assert.AreEqual(90.0, AudioSystem.Music.Clock.Tempo, 1e-9);
        }

        [UnityTest]
        public IEnumerator SecondTransitionReplacesFirstPending()
        {
            MusicTrack first = MakeTrack(120.0);
            MusicTrack second = MakeTrack(100.0);
            MusicTrack third = MakeTrack(80.0);

            AudioSystem.PlayMusic(first);

            yield return null;

            AudioSystem.TransitionMusic(second, MusicQuantization.Bar);
            AudioSystem.TransitionMusic(third, MusicQuantization.Bar);

            Assert.AreSame(third, AudioSystem.Music.PendingTrack);
        }

        [UnityTest]
        public IEnumerator StopClearsPendingTransition()
        {
            MusicTrack first = MakeTrack(120.0);
            MusicTrack second = MakeTrack(90.0);

            AudioSystem.PlayMusic(first);

            yield return null;

            AudioSystem.TransitionMusic(second, MusicQuantization.Bar);
            AudioSystem.StopMusic();

            Assert.IsFalse(AudioSystem.Music.IsTransitionPending);
            Assert.IsNull(AudioSystem.Music.PendingTrack);
        }

        [UnityTest]
        public IEnumerator StingerPlaysWithoutDisturbingTrack()
        {
            MusicTrack track = MakeTrack(120.0);

            AudioSystem.PlayMusic(track);

            yield return null;

            AudioSystem.PlayStinger(MakeSine(0.2f), MusicQuantization.Beat, 0.05f);

            yield return new WaitForSeconds(0.4f);

            Assert.IsTrue(AudioSystem.Music.IsPlaying);
            Assert.AreSame(track, AudioSystem.Music.CurrentTrack);
        }

        [UnityTest]
        public IEnumerator NullStingerIsIgnored()
        {
            MusicTrack track = MakeTrack(120.0);
            AudioSystem.PlayMusic(track);

            Assert.DoesNotThrow(() => AudioSystem.PlayStinger(null, MusicQuantization.Beat));

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyTransitionTargetIsIgnored()
        {
            MusicTrack track = MakeTrack(120.0);
            AudioSystem.PlayMusic(track);

            yield return null;

            AudioSystem.TransitionMusic(MusicTrack.CreateRuntime(null, null, 120.0, 4), MusicQuantization.Bar);

            Assert.IsFalse(AudioSystem.Music.IsTransitionPending);
            Assert.AreSame(track, AudioSystem.Music.CurrentTrack);
        }

        private static MusicTrack MakeTrack(double bpm)
        {
            MusicTrack track = MusicTrack.CreateRuntime(null, MakeSine(1f), bpm, 4);
            track.Volume = 0.05f;
            return track;
        }

        private static AudioClip MakeSine(float seconds)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * seconds));
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Sin(2f * Mathf.PI * 440f * i / sampleRate) * 0.2f;
            }

            AudioClip clip = AudioClip.Create("AudioMW_TransitionSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
