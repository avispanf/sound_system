using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class SoundHandleTests
    {
        private GameObject listener;

        [SetUp]
        public void SetUp()
        {
            AudioSystem.StopAll();
            AudioRuntime.Instance.ClearVirtualVoices();
            AudioRuntime.Instance.ClearHandles();

            listener = new GameObject("HandleListener");
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
            AudioRuntime.Instance.ClearHandles();

            if (listener != null)
            {
                Object.Destroy(listener);
                listener = null;
            }
        }

        [UnityTest]
        public IEnumerator UnassignedHandleIsNeverPlaying()
        {
            Assert.IsFalse(SoundHandle.None.IsAssigned);
            Assert.IsFalse(SoundHandle.None.IsPlaying);
            Assert.IsNull(SoundHandle.None.Voice);

            yield return null;
        }

        [UnityTest]
        public IEnumerator RejectedPlayReturnsNoHandle()
        {
            SoundHandle handle = AudioSystem.PlayTracked(null);

            Assert.IsFalse(handle.IsAssigned);

            yield return null;
        }

        [UnityTest]
        public IEnumerator TrackedPlayReturnsALivingHandle()
        {
            SoundHandle handle = AudioSystem.PlayTracked(MakeEvent(false));

            Assert.IsTrue(handle.IsAssigned);
            Assert.IsTrue(handle.IsPlaying);
            Assert.IsNotNull(handle.Voice);

            yield return null;
        }

        [UnityTest]
        public IEnumerator StoppingThroughTheHandleStopsTheVoice()
        {
            SoundHandle handle = AudioSystem.PlayTracked(MakeEvent(false));

            handle.Stop();

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator HandleSetsParametersOnItsVoice()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 1f);
            SoundEvent soundEvent = MakeEvent(false);
            soundEvent.Volume = 1f;
            soundEvent.ParameterBindings = new[]
            {
                ParameterBinding.CreateRuntime(parameter, ParameterTarget.Volume, AnimationCurve.Linear(0f, 0f, 1f, 1f))
            };

            AudioSystem.SetParameter(parameter, 1f);
            SoundHandle handle = AudioSystem.PlayTracked(soundEvent);

            handle.SetParameter(parameter, 0.25f);

            yield return null;

            Assert.AreEqual(0.25f, handle.Voice.Source.volume, 0.02f);
        }

        [UnityTest]
        public IEnumerator HandleSurvivesVirtualization()
        {
            SoundEvent soundEvent = MakeEvent(true);

            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "handle to become virtual");

            Assert.IsTrue(handle.IsPlaying);
            Assert.IsNull(handle.Voice);

            listener.transform.position = new Vector3(0f, 0f, 498f);

            yield return AudioTestUtil.WaitUntil(() => !handle.IsVirtual, "handle to become real again");

            Assert.IsTrue(handle.IsPlaying);
            Assert.IsNotNull(handle.Voice);
        }

        [UnityTest]
        public IEnumerator StoppingAVirtualHandleRemovesIt()
        {
            SoundEvent soundEvent = MakeEvent(true);

            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(soundEvent, new Vector3(0f, 0f, 500f));

            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "handle to become virtual");

            handle.Stop();

            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
        }

        [UnityTest]
        public IEnumerator HandlesAreDistinctPerInstance()
        {
            SoundEvent soundEvent = MakeEvent(false);

            SoundHandle first = AudioSystem.PlayTracked(soundEvent);
            SoundHandle second = AudioSystem.PlayTracked(soundEvent);

            Assert.AreNotEqual(first.Id, second.Id);

            first.Stop();

            Assert.IsFalse(first.IsPlaying);
            Assert.IsTrue(second.IsPlaying);

            yield return null;
        }

        private static SoundEvent MakeEvent(bool spatial)
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(AudioTestUtil.MakeSine(5f));
            soundEvent.Volume = 0.05f;
            soundEvent.Loop = true;
            soundEvent.SpatialBlend = spatial ? 1f : 0f;
            soundEvent.MinDistance = 1f;
            soundEvent.MaxDistance = 25f;
            soundEvent.AllowVirtualization = spatial;
            return soundEvent;
        }
    }
}
