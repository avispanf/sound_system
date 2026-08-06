using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class SpatialPlaybackTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
        }

        [UnityTest]
        public IEnumerator PresetOverridesEventSpatialSettings()
        {
            SoundEvent soundEvent = MakeEvent();
            soundEvent.MinDistance = 1f;
            soundEvent.MaxDistance = 5f;
            soundEvent.SpatialBlend = 0f;
            soundEvent.AttenuationPreset = AttenuationPreset.CreateRuntime(1f, 4f, 40f, AudioRolloffMode.Linear);

            Voice voice = AudioSystem.PlayAtPosition(soundEvent, Vector3.zero);

            Assert.AreEqual(4f, voice.Source.minDistance, 0.001f);
            Assert.AreEqual(40f, voice.Source.maxDistance, 0.001f);
            Assert.AreEqual(1f, voice.Source.spatialBlend, 0.001f);
            Assert.AreEqual(AudioRolloffMode.Linear, voice.Source.rolloffMode);

            yield return null;
        }

        [UnityTest]
        public IEnumerator EventSettingsApplyWithoutPreset()
        {
            SoundEvent soundEvent = MakeEvent();
            soundEvent.AttenuationPreset = null;
            soundEvent.MinDistance = 3f;
            soundEvent.MaxDistance = 12f;

            Voice voice = AudioSystem.PlayAtPosition(soundEvent, Vector3.zero);

            Assert.AreEqual(3f, voice.Source.minDistance, 0.001f);
            Assert.AreEqual(12f, voice.Source.maxDistance, 0.001f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CustomCurvePresetSetsCustomRolloff()
        {
            SoundEvent soundEvent = MakeEvent();
            soundEvent.AttenuationPreset = AttenuationPreset.CreateRuntime(
                1f, 1f, 20f, AudioRolloffMode.Custom, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            Voice voice = AudioSystem.PlayAtPosition(soundEvent, Vector3.zero);

            Assert.AreEqual(AudioRolloffMode.Custom, voice.Source.rolloffMode);
            Assert.Greater(voice.Source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).length, 0);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotZoneTracksOccupancy()
        {
            GameObject host = new GameObject("Zone");
            host.AddComponent<BoxCollider>().isTrigger = true;
            MixerSnapshotZone zone = host.AddComponent<MixerSnapshotZone>();

            Assert.IsFalse(zone.IsOccupied);

            zone.EnterManually();
            Assert.IsTrue(zone.IsOccupied);

            zone.ExitManually();
            Assert.IsFalse(zone.IsOccupied);

            Object.Destroy(host);

            yield return null;
        }

        [UnityTest]
        public IEnumerator SnapshotZoneWithoutSnapshotsIsSafe()
        {
            GameObject host = new GameObject("Zone");
            host.AddComponent<BoxCollider>().isTrigger = true;
            MixerSnapshotZone zone = host.AddComponent<MixerSnapshotZone>();

            Assert.DoesNotThrow(zone.EnterManually);
            Assert.DoesNotThrow(zone.ExitManually);

            Object.Destroy(host);

            yield return null;
        }

        private static SoundEvent MakeEvent()
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(1f));
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

            AudioClip clip = AudioClip.Create("AudioMW_SpatialSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
