using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class ScattererTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();

            if (host != null)
            {
                Object.Destroy(host);
                host = null;
            }
        }

        [UnityTest]
        public IEnumerator SpawnPlacesVoiceWithinRadiusBand()
        {
            SoundScatterer scatterer = MakeScatterer();
            scatterer.Configure(10f, 20f, 5f, 10f, 4);

            Vector3 origin = scatterer.transform.position;

            for (int i = 0; i < 32; i++)
            {
                Vector3 position = scatterer.NextPosition();
                float distance = Vector3.Distance(origin, position);

                Assert.GreaterOrEqual(distance, 4.99f);
                Assert.LessOrEqual(distance, 10.01f);
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnRespectsConcurrencyLimit()
        {
            SoundScatterer scatterer = MakeScatterer();
            scatterer.Configure(10f, 20f, 1f, 2f, 2);

            scatterer.Spawn();
            scatterer.Spawn();
            Voice third = scatterer.Spawn();

            Assert.IsNull(third);
            Assert.AreEqual(2, scatterer.LiveCount);
            Assert.AreEqual(2, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LiveCountDropsWhenVoiceReleases()
        {
            SoundScatterer scatterer = MakeScatterer(0.15f);
            scatterer.Configure(10f, 20f, 1f, 2f, 2);

            Voice voice = scatterer.Spawn();

            Assert.IsNotNull(voice);
            Assert.AreEqual(1, scatterer.LiveCount);

            yield return new WaitForSeconds(0.6f);

            Assert.AreEqual(0, scatterer.LiveCount);
        }

        [UnityTest]
        public IEnumerator StopScatteringResetsLiveCount()
        {
            SoundScatterer scatterer = MakeScatterer();
            scatterer.Configure(10f, 20f, 1f, 2f, 4);

            scatterer.Spawn();
            scatterer.StopScattering();

            Assert.IsFalse(scatterer.IsRunning);
            Assert.AreEqual(0, scatterer.LiveCount);

            yield return null;
        }

        private SoundScatterer MakeScatterer(float clipSeconds = 3f)
        {
            host = new GameObject("Scatterer");
            host.transform.position = new Vector3(11f, 3f, -7f);
            host.SetActive(false);

            SoundScatterer scatterer = host.AddComponent<SoundScatterer>();
            scatterer.AroundListener = false;

            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(clipSeconds));
            soundEvent.SpatialBlend = 0f;
            soundEvent.Volume = 0.05f;
            scatterer.Event = soundEvent;

            host.SetActive(true);
            scatterer.StopScattering();

            return scatterer;
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
