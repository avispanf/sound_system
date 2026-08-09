using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class PlaybackSmokeTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
        }

        [UnityTest]
        public IEnumerator PlayReturnsActiveVoice()
        {
            SoundEvent soundEvent = MakeEvent(0.25f);
            Voice voice = AudioSystem.Play(soundEvent);

            Assert.IsNotNull(voice);
            Assert.IsTrue(voice.IsActive);
            Assert.IsTrue(voice.Source.isPlaying);

            yield return null;
        }

        [UnityTest]
        public IEnumerator VoiceReleasesAfterClipEnds()
        {
            SoundEvent soundEvent = MakeEvent(0.2f);
            Voice voice = AudioSystem.Play(soundEvent);

            Assert.IsNotNull(voice);

            yield return AudioTestUtil.WaitUntil(() => !voice.IsActive, "voice to release after the clip ends");

            Assert.IsFalse(voice.IsActive);
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator PoolReusesReleasedVoice()
        {
            SoundEvent soundEvent = MakeEvent(0.1f);
            Voice first = AudioSystem.Play(soundEvent);
            int countAfterFirst = AudioRuntime.Instance.Pool.TotalCount;

            yield return AudioTestUtil.WaitUntil(() => !first.IsActive, "first voice to release");

            Voice second = AudioSystem.Play(soundEvent);

            Assert.AreSame(first, second);
            Assert.AreEqual(countAfterFirst, AudioRuntime.Instance.Pool.TotalCount);
        }

        [UnityTest]
        public IEnumerator NullEventDoesNotAllocateVoice()
        {
            Voice voice = AudioSystem.Play(null);

            Assert.IsNull(voice);
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LoopingVoiceStaysActive()
        {
            SoundEvent soundEvent = MakeEvent(0.1f);
            soundEvent.Loop = true;
            Voice voice = AudioSystem.Play(soundEvent);

            yield return AudioTestUtil.WaitFrames(4);

            Assert.IsTrue(voice.IsActive);
            Assert.IsTrue(voice.Source.isPlaying);

            voice.Stop();
            Assert.IsFalse(voice.IsActive);
        }

        [UnityTest]
        public IEnumerator PoolNeverExceedsMaxVoices()
        {
            SoundEvent soundEvent = MakeEvent(2f);
            soundEvent.Loop = true;

            for (int i = 0; i < AudioRuntime.DefaultMaxVoices + 8; i++)
            {
                AudioSystem.Play(soundEvent);
            }

            yield return null;

            Assert.LessOrEqual(AudioRuntime.Instance.Pool.TotalCount, AudioRuntime.DefaultMaxVoices);
        }

        private static SoundEvent MakeEvent(float seconds)
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(seconds));
            soundEvent.SpatialBlend = 0f;
            soundEvent.Volume = 0.05f;
            return soundEvent;
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

            AudioClip clip = AudioClip.Create("AudioMW_TestSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
