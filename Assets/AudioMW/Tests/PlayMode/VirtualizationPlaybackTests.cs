using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class VirtualizationPlaybackTests
    {
        private GameObject listener;

        [SetUp]
        public void SetUp()
        {
            AudioSystem.StopAll();
            AudioRuntime.Instance.ClearVirtualVoices();

            listener = new GameObject("TestListener");
            listener.transform.position = Vector3.zero;

            AudioListener[] existing = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);

            for (int i = 0; i < existing.Length; i++)
            {
                existing[i].enabled = false;
            }

            listener.AddComponent<AudioListener>();
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
            AudioRuntime.Instance.ClearVirtualVoices();
            AudioRuntime.Instance.VirtualizationEnabled = true;

            if (listener != null)
            {
                Object.Destroy(listener);
                listener = null;
            }
        }

        [UnityTest]
        public IEnumerator DistantVoiceIsDemotedWhenVirtualizationAllowed()
        {
            SoundEvent soundEvent = MakeSpatialEvent(true);

            AudioSystem.PlayAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitUntil(
                () => AudioRuntime.Instance.VirtualVoiceCount > 0,
                "distant voice to be virtualised");

            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator DistantVoiceStaysRealWhenVirtualizationDisallowed()
        {
            SoundEvent soundEvent = MakeSpatialEvent(false);

            AudioSystem.PlayAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitFrames(4);

            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator NearVoiceIsNotDemoted()
        {
            SoundEvent soundEvent = MakeSpatialEvent(true);

            AudioSystem.PlayAtPosition(soundEvent, new Vector3(0f, 0f, 1f));

            yield return AudioTestUtil.WaitFrames(4);

            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator TwoDimensionalVoiceIsNeverDemoted()
        {
            SoundEvent soundEvent = MakeSpatialEvent(true);
            soundEvent.SpatialBlend = 0f;

            AudioSystem.PlayAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitFrames(4);

            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator VirtualVoiceIsPromotedWhenListenerApproaches()
        {
            SoundEvent soundEvent = MakeSpatialEvent(true);

            AudioSystem.PlayAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitUntil(
                () => AudioRuntime.Instance.VirtualVoiceCount > 0,
                "voice to be virtualised");

            listener.transform.position = new Vector3(0f, 0f, 498f);

            yield return AudioTestUtil.WaitUntil(
                () => AudioSystem.ActiveVoiceCount > 0,
                "voice to be promoted back");

            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
        }

        [UnityTest]
        public IEnumerator DisablingVirtualizationStopsDemotion()
        {
            AudioRuntime.Instance.VirtualizationEnabled = false;

            SoundEvent soundEvent = MakeSpatialEvent(true);
            AudioSystem.PlayAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitFrames(4);

            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);
        }

        private static SoundEvent MakeSpatialEvent(bool allowVirtualization)
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(AudioTestUtil.MakeSine(5f));
            soundEvent.Volume = 0.05f;
            soundEvent.Loop = true;
            soundEvent.SpatialBlend = 1f;
            soundEvent.MinDistance = 1f;
            soundEvent.MaxDistance = 25f;
            soundEvent.AllowVirtualization = allowVirtualization;
            return soundEvent;
        }
    }
}
