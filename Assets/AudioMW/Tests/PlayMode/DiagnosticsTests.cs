using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class DiagnosticsTests
    {
        [SetUp]
        public void SetUp()
        {
            AudioSystem.Shutdown();

            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.ResetCounters();
            }
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
        }

        [UnityTest]
        public IEnumerator PlayRequestsAreCounted()
        {
            SoundEvent soundEvent = MakeEvent();

            AudioSystem.Play(soundEvent);
            AudioSystem.Play(soundEvent);

            Assert.AreEqual(2, AudioRuntime.Instance.PlayRequests);
            Assert.AreEqual(0, AudioRuntime.Instance.RejectedRequests);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NullEventCountsAsRejected()
        {
            AudioSystem.Play(null);

            Assert.AreEqual(0, AudioRuntime.Instance.PlayRequests);
            Assert.AreEqual(1, AudioRuntime.Instance.RejectedRequests);

            yield return null;
        }

        [UnityTest]
        public IEnumerator EventWithoutClipsCountsAsRejected()
        {
            SoundEvent empty = SoundEvent.CreateRuntime();

            AudioSystem.Play(empty);

            Assert.AreEqual(1, AudioRuntime.Instance.PlayRequests);
            Assert.AreEqual(1, AudioRuntime.Instance.RejectedRequests);

            yield return null;
        }

        [UnityTest]
        public IEnumerator StealsAreCountedWhenPoolSaturates()
        {
            SoundEvent soundEvent = MakeEvent();
            soundEvent.Loop = true;

            for (int i = 0; i < AudioRuntime.DefaultMaxVoices + 4; i++)
            {
                AudioSystem.Play(soundEvent);
            }

            Assert.GreaterOrEqual(AudioRuntime.Instance.Pool.StealCount, 4);

            yield return null;
        }

        [UnityTest]
        public IEnumerator HudReportsRuntimeState()
        {
            GameObject host = new GameObject("Hud");
            AudioDebugHud hud = host.AddComponent<AudioDebugHud>();

            SoundEvent soundEvent = MakeEvent();
            soundEvent.Loop = true;
            AudioSystem.Play(soundEvent);

            yield return null;

            string report = hud.BuildReport();

            StringAssert.Contains("AudioMW", report);
            StringAssert.Contains("voices 1", report);

            Object.Destroy(host);
        }

        private static SoundEvent MakeEvent()
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(2f));
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
