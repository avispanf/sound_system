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

        [UnityTest]
        public IEnumerator DistanceFilterCombinesWithOcclusionAndRestoresPooledDefaults()
        {
            var listeners = Object.FindObjectsOfType<AudioListener>();
            var listenerStates = new bool[listeners.Length];
            for (int i = 0; i < listeners.Length; i++) { listenerStates[i] = listeners[i].enabled; listeners[i].enabled = false; }
            var ears = new GameObject("Distance listener"); ears.AddComponent<AudioListener>();
            var item = MakeEvent(); var plain = MakeEvent();
            var preset = AttenuationPreset.CreateRuntime(1,1,11,AudioRolloffMode.Linear);
            preset.DistanceFiltering = true; preset.DistanceSpread = true; preset.StereoPan = .4f;
            preset.CutoffByDistance = AnimationCurve.Linear(0,22000,1,2000);
            preset.SpreadByDistance = AnimationCurve.Linear(0,180,1,0); item.AttenuationPreset = preset;
            try
            {
                var voice = AudioSystem.PlayAtPosition(item,new Vector3(6,0,0));
                yield return null;
                var filter = voice.Source.GetComponent<AudioLowPassFilter>();
                Assert.NotNull(filter); Assert.IsTrue(filter.enabled);
                Assert.AreEqual(12000,filter.cutoffFrequency,1); Assert.AreEqual(90,voice.Source.spread,1);
                Assert.AreEqual(.4f,voice.Source.panStereo,.001f);
                voice.ApplyOcclusion(1,800); yield return null;
                Assert.AreEqual(800,filter.cutoffFrequency,1);
                voice.ClearOcclusion(); Assert.AreEqual(12000,filter.cutoffFrequency,1);
                ears.transform.position = new Vector3(6,0,0); yield return null; yield return null;
                Assert.IsFalse(filter.enabled); Assert.AreEqual(180,voice.Source.spread,1);
                voice.Stop();
                var next = AudioSystem.PlayAtPosition(plain,Vector3.zero);
                Assert.AreEqual(0,next.Source.panStereo); Assert.AreEqual(0,next.Source.spread); Assert.AreEqual(1,next.Source.dopplerLevel);
            }
            finally
            {
                AudioSystem.StopAll(); Object.Destroy(ears); Object.Destroy(item); Object.Destroy(plain); Object.Destroy(preset);
                for (int i = 0; i < listeners.Length; i++) if (listeners[i] != null) listeners[i].enabled = listenerStates[i];
            }
        }

        [UnityTest]
        public IEnumerator MobileTierKeepsDistanceAndOcclusionProcessingNeutral()
        {
            var previous = AudioRuntimeSettings.ActiveTier;
            var tier = AudioTierConfig.Mobile2D();
            var item = MakeEvent();
            var preset = AttenuationPreset.CreateRuntime(1,1,11,AudioRolloffMode.Linear);
            preset.DistanceFiltering = true;
            preset.CutoffByDistance = AnimationCurve.Constant(0,1,500);
            item.AttenuationPreset = preset;
            try
            {
                AudioRuntimeSettings.ActiveTier = tier;
                var voice = AudioSystem.PlayAtPosition(item,new Vector3(20,0,0));
                voice.ApplyOcclusion(.1f,300);
                yield return null;
                Assert.AreEqual(0,voice.Source.spatialBlend);
                Assert.AreEqual(item.Volume,voice.Source.volume,.001f);
                var filter = voice.Source.GetComponent<AudioLowPassFilter>();
                Assert.IsTrue(filter == null || !filter.enabled);
            }
            finally
            {
                AudioSystem.StopAll(); AudioRuntimeSettings.ActiveTier = previous;
                Object.Destroy(item); Object.Destroy(preset); Object.Destroy(tier);
            }
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
