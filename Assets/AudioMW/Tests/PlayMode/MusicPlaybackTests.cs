using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class MusicPlaybackTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopMusic();
        }

        [UnityTest]
        public IEnumerator PlayStartsClockAndSchedulesAudio()
        {
            MusicTrack track = MakeTrack(0.5f, 1f);

            AudioSystem.PlayMusic(track);

            Assert.IsTrue(AudioSystem.Music.IsPlaying);
            Assert.AreSame(track, AudioSystem.Music.CurrentTrack);
            Assert.IsTrue(AudioSystem.Music.Clock.IsRunning);

            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(AudioSystem.Music.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ClockAdoptsTrackTempo()
        {
            MusicTrack track = MakeTrack(0f, 1f);
            track.Tempo = 90.0;
            track.BeatsPerBar = 3;

            AudioSystem.PlayMusic(track);

            Assert.AreEqual(90.0, AudioSystem.Music.Clock.Tempo, 1e-9);
            Assert.AreEqual(3, AudioSystem.Music.Clock.BeatsPerBar);

            yield return null;
        }

        [UnityTest]
        public IEnumerator IntroIsFollowedByLoopWithoutGap()
        {
            MusicTrack track = MakeTrack(0.4f, 0.8f);

            double before = AudioSettings.dspTime;
            AudioSystem.PlayMusic(track);

            double expected = before + 0.05 + track.IntroDuration + track.LoopDuration;

            Assert.AreEqual(expected, AudioSystem.Music.NextScheduleDspTime, 0.02);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoopKeepsRescheduling()
        {
            MusicTrack track = MakeTrack(0f, 0.4f);

            AudioSystem.PlayMusic(track);
            double firstSchedule = AudioSystem.Music.NextScheduleDspTime;

            yield return new WaitForSeconds(0.7f);

            Assert.IsTrue(AudioSystem.Music.IsPlaying);
            Assert.Greater(AudioSystem.Music.NextScheduleDspTime, firstSchedule);
        }

        [UnityTest]
        public IEnumerator BeatCallbacksFire()
        {
            MusicTrack track = MakeTrack(0f, 2f);
            track.Tempo = 600.0;

            int beats = 0;
            AudioSystem.PlayMusic(track);
            AudioSystem.Music.BeatTick += index => beats++;

            yield return new WaitForSeconds(0.6f);

            Assert.Greater(beats, 2);
        }

        [UnityTest]
        public IEnumerator StopHaltsPlaybackAndClock()
        {
            MusicTrack track = MakeTrack(0f, 1f);

            AudioSystem.PlayMusic(track);
            AudioSystem.StopMusic();

            Assert.IsFalse(AudioSystem.Music.IsPlaying);
            Assert.IsFalse(AudioSystem.Music.Clock.IsRunning);
            Assert.IsNull(AudioSystem.Music.CurrentTrack);

            yield return null;
        }

        [UnityTest]
        public IEnumerator EmptyTrackIsIgnored()
        {
            MusicTrack track = MusicTrack.CreateRuntime(null, null, 120.0, 4);

            AudioSystem.PlayMusic(track);

            Assert.IsFalse(AudioSystem.Music.IsPlaying);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ExactDurationUsesSampleCount()
        {
            AudioClip clip = MakeSine(1f);

            Assert.AreEqual(1.0, MusicTrack.GetExactDuration(clip), 1e-6);
            Assert.AreEqual(0.0, MusicTrack.GetExactDuration(null), 1e-9);

            yield return null;
        }

        private static MusicTrack MakeTrack(float introSeconds, float loopSeconds)
        {
            AudioClip intro = introSeconds > 0f ? MakeSine(introSeconds) : null;
            MusicTrack track = MusicTrack.CreateRuntime(intro, MakeSine(loopSeconds), 120.0, 4);
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
                data[i] = Mathf.Sin(2f * Mathf.PI * 220f * i / sampleRate) * 0.2f;
            }

            AudioClip clip = AudioClip.Create("AudioMW_MusicSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
