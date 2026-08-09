using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class TierPlaybackTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
            AudioRuntimeSettings.Reset();

            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.RebuildPool(AudioRuntimeSettings.FallbackMaxVoices, 0);
            }
        }

        [UnityTest]
        public IEnumerator RebuildPoolAppliesNewVoiceCeiling()
        {
            AudioRuntime.Instance.RebuildPool(4, 0);

            SoundEvent soundEvent = MakeEvent();

            for (int i = 0; i < 12; i++)
            {
                AudioSystem.Play(soundEvent);
            }

            yield return null;

            Assert.LessOrEqual(AudioRuntime.Instance.Pool.TotalCount, 4);
        }

        [UnityTest]
        public IEnumerator PrewarmCreatesInactiveVoices()
        {
            AudioRuntime.Instance.RebuildPool(8, 5);

            yield return null;

            Assert.AreEqual(5, AudioRuntime.Instance.Pool.TotalCount);
            Assert.AreEqual(0, AudioRuntime.Instance.Pool.ActiveCount);
        }

        [UnityTest]
        public IEnumerator RebuildStopsExistingVoices()
        {
            SoundEvent soundEvent = MakeEvent();
            soundEvent.Loop = true;

            AudioSystem.Play(soundEvent);

            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);

            AudioRuntime.Instance.RebuildPool(8, 0);

            yield return null;

            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator ApplyTierSetsActiveTier()
        {
            AudioTierConfig config = AudioTierConfig.CreateRuntime("runtime tier", 6, DspBufferPreset.GoodLatency);

            AudioSystem.ApplyTier(config);

            yield return null;

            Assert.AreSame(config, AudioSystem.ActiveTier);
            Assert.LessOrEqual(AudioRuntime.Instance.Pool.MaxVoices, 6);
        }

        private static SoundEvent MakeEvent()
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(2f));
            soundEvent.SpatialBlend = 0f;
            soundEvent.Volume = 0.05f;
            soundEvent.Loop = true;
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

            AudioClip clip = AudioClip.Create("AudioMW_TierSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
