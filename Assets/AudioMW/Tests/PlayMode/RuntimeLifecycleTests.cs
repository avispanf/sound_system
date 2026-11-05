using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class RuntimeLifecycleTests
    {
        private readonly List<Object> assets = new List<Object>();
        [SetUp] public void SetUp() { AudioSystem.StopAll(); }
        [TearDown] public void TearDown()
        {
            AudioSystem.StopAll();
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
        }
        private SoundEvent Event(bool spatial = false)
        {
            AudioClip clip = AudioTestUtil.MakeSine(1);
            SoundEvent item = SoundEvent.CreateRuntime(clip);
            item.Loop = true; item.SpatialBlend = spatial ? 1 : 0;
            assets.Add(item); assets.Add(clip);
            return item;
        }

        [UnityTest]
        public IEnumerator UnloadingAnEmitterSceneStopsAttachedTwoDimensionalAudio()
        {
            Scene scene = SceneManager.CreateScene("AudioMW Lifecycle");
            GameObject emitter = new GameObject("Attached 2D target");
            SceneManager.MoveGameObjectToScene(emitter, scene);
            SoundHandle handle = AudioSystem.PlayTrackedAttached(Event(), emitter.transform);
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "attached 2D sound to release after scene unload");
            Assert.AreEqual(0, AudioRuntime.Instance.TrackedHandleCount);
        }

        [UnityTest]
        public IEnumerator RuntimeRecreationCannotReassignAnOldHandle()
        {
            Object.Destroy(AudioRuntime.Instance.gameObject);
            yield return null;
            SoundEvent item = Event();
            SoundHandle old = AudioSystem.PlayTracked(item);
            Object.Destroy(AudioRuntime.Instance.gameObject);
            yield return null;
            SoundHandle current = AudioSystem.PlayTracked(item);
            Assert.AreNotEqual(old.Id, current.Id);
            Assert.IsFalse(old.IsPlaying);
            old.Stop();
            Assert.IsTrue(current.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ReleaseCallbackCanStartAnotherSound()
        {
            SoundEvent item = Event();
            SoundHandle first = AudioSystem.PlayTracked(item);
            SoundHandle next = SoundHandle.None;
            first.Voice.Released += _ => next = AudioSystem.PlayTracked(item);
            first.Stop();
            Assert.IsTrue(next.IsPlaying);
            Assert.IsNotNull(next.Voice.Source.clip);
            yield return null;
            Assert.IsTrue(next.IsPlaying);
        }

        [UnityTest]
        public IEnumerator ReleaseCallbackCanStopAllWithoutReenteringItsOwnStop()
        {
            SoundHandle first = AudioSystem.PlayTracked(Event());
            AudioSystem.PlayTracked(Event());
            first.Voice.Released += _ => AudioSystem.StopAll();
            Assert.DoesNotThrow(() => first.Stop());
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ReleaseClearsTheClipSnapshot()
        {
            SoundHandle handle = AudioSystem.PlayTracked(Event());
            Voice voice = handle.Voice;
            handle.Stop();
            Assert.IsNull(voice.CurrentParameters.Clip);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedRootStopsItsEntireCompositeInstance()
        {
            SoundEvent root = Event(); root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(Event()), SoundEventChild.Create(Event()) };
            SoundHandle handle = AudioSystem.PlayTracked(root);
            Object.Destroy(root);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying && AudioRuntime.Instance.TrackedHandleCount == 0, "destroyed root cleanup");
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
        }

        [UnityTest]
        public IEnumerator CooldownHistoryDropsExpiredAndDestroyedEvents()
        {
            SoundEvent item = Event(); item.CooldownSeconds = 0.05f;
            AudioSystem.PlayTracked(item).Stop();
            SoundEvent destroyed = Event(); destroyed.CooldownSeconds = 30;
            AudioSystem.PlayTracked(destroyed).Stop();
            Object.Destroy(destroyed);
            yield return AudioTestUtil.WaitUntil(() => AudioRuntime.Instance.CooldownEntryCount == 0, "cooldown history sweep", 2f);
        }

        [UnityTest]
        public IEnumerator ShutdownRejectsPlaybackFromReleaseCallbacks()
        {
            SoundEvent item = Event();
            SoundHandle handle = AudioSystem.PlayTracked(item);
            SoundHandle rejected = SoundHandle.None;
            handle.Voice.Released += _ => rejected = AudioSystem.PlayTracked(item);
            AudioSystem.Shutdown();
            Assert.IsFalse(rejected.IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackOutcome.RejectedRuntimeStopping, record.Outcome);
            Assert.IsTrue(AudioSystem.PlayTracked(item).IsPlaying);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StealCallbackCanStartAnotherSoundWithoutCorruptingAdmission()
        {
            AudioRuntime.Instance.RebuildPool(2, 0);
            SoundEvent item = Event(); item.MaxInstances = 1;
            SoundHandle first = AudioSystem.PlayTracked(item);
            SoundHandle callback = SoundHandle.None;
            first.Voice.Released += _ => callback = AudioSystem.PlayTracked(Event());
            SoundHandle second = AudioSystem.PlayTracked(item);
            Assert.IsFalse(first.IsPlaying);
            Assert.IsTrue(second.IsPlaying);
            Assert.IsTrue(callback.IsPlaying);
            yield return null;
            Assert.IsNotNull(second.Voice.Source.clip);
            AudioRuntime.Instance.RebuildPool(AudioRuntime.DefaultMaxVoices, 0);
        }

        [UnityTest]
        public IEnumerator ShutdownClearsDuckingAndRejectsVoiceOverReentry()
        {
            SoundParameter duck = SoundParameter.CreateRuntime(0, 1, 0);
            assets.Add(duck);
            VoiceLine line = VoiceLine.CreateRuntime(Event().Clips[0], "Test", "Shutdown");
            assets.Add(line);
            VoiceOverDirector director = AudioSystem.VoiceOver;
            SoundParameter previous = director.DuckParameter;
            director.DuckParameter = duck;
            director.Play(line);
            yield return AudioTestUtil.WaitUntil(() => director.DuckValue > 0, "duck attack");
            bool accepted = true;
            System.Action<VoiceLine> callback = _ => accepted = director.Play(line);
            director.LineFinished += callback;
            try
            {
                AudioSystem.Shutdown();
                Assert.IsFalse(accepted);
                Assert.IsFalse(director.IsSpeaking);
                Assert.AreEqual(0, director.DuckValue);
                yield return null;
                Assert.AreEqual(0, AudioSystem.GetParameter(duck));
            }
            finally { director.LineFinished -= callback; director.DuckParameter = previous; }
        }
    }
}
