using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class CompositePlaybackTests
    {
        private readonly List<Object> assets = new List<Object>();
        private GameObject listener;

        [SetUp]
        public void SetUp()
        {
            AudioSystem.StopAll();
            AudioRuntime.Instance.RebuildPool(8, 0);
            AudioRuntime.Instance.VirtualizationEnabled = true;
            listener = new GameObject("Composite listener");
            listener.AddComponent<AudioListener>();
            assets.Add(listener);
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
            AudioRuntime.Instance.RebuildPool(AudioRuntime.DefaultMaxVoices, 0);
            foreach (Object asset in assets) if (asset != null) Object.Destroy(asset);
            assets.Clear();
        }

        private SoundEvent Leaf(float seconds = 2f, bool loop = true)
        {
            AudioClip clip = AudioTestUtil.MakeSine(seconds);
            SoundEvent item = SoundEvent.CreateRuntime(clip);
            item.SpatialBlend = 0f;
            item.Loop = loop;
            assets.Add(clip);
            assets.Add(item);
            return item;
        }

        private SoundEvent Group(params SoundEvent[] children)
        {
            SoundEvent item = SoundEvent.CreateRuntime();
            item.ContainerMode = ContainerMode.Layered;
            List<SoundEventChild> entries = new List<SoundEventChild>();
            foreach (SoundEvent child in children) entries.Add(SoundEventChild.Create(child));
            item.Children = entries.ToArray();
            assets.Add(item);
            return item;
        }

        private SoundParameter Parameter()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);
            assets.Add(parameter);
            return parameter;
        }

        [UnityTest]
        public IEnumerator EmitterStopCannotStopAnotherOwnersReusedVoice()
        {
            AudioRuntime.Instance.RebuildPool(1, 0);
            GameObject host = new GameObject("Emitter");
            assets.Add(host);
            SoundEmitter emitter = host.AddComponent<SoundEmitter>();
            emitter.Event = Leaf();
            emitter.Play();
            SoundHandle other = AudioSystem.PlayTracked(Leaf());
            emitter.Stop();
            Assert.IsTrue(other.IsPlaying);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EmitterStopsEveryOverlappingPlay()
        {
            GameObject host = new GameObject("Emitter");
            assets.Add(host);
            SoundEmitter emitter = host.AddComponent<SoundEmitter>();
            emitter.Event = Leaf();
            emitter.Play();
            emitter.Play();
            Assert.AreEqual(2, AudioSystem.ActiveVoiceCount);
            emitter.Stop();
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisablingVirtualizationResumesExistingVirtualVoices()
        {
            SoundEvent item = Leaf();
            item.SpatialBlend = 1;
            item.AllowVirtualization = true;
            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(item, new Vector3(0, 0, 500));
            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "voice to virtualize");
            AudioRuntime.Instance.VirtualizationEnabled = false;
            yield return AudioTestUtil.WaitUntil(() => !handle.IsVirtual, "voice to resume with virtualization disabled");
            Assert.IsTrue(handle.IsPlaying);
        }

        [UnityTest]
        public IEnumerator NestedLayersInheritGainPitchAndBindings()
        {
            SoundParameter parameter = Parameter();
            SoundEvent leaf = Leaf();
            leaf.Volume = 0.8f;
            SoundEvent nested = Group(leaf);
            nested.Volume = 0.5f;
            SoundEvent root = Group(nested);
            root.Pitch = 1.2f;
            root.ParameterBindings = new[] { ParameterBinding.CreateRuntime(parameter, ParameterTarget.Volume, AnimationCurve.Linear(0f, 0f, 1f, 1f)) };
            ParameterStore initial = new ParameterStore();
            initial.Set(parameter, 0.5f);
            SoundHandle handle = AudioSystem.PlayTracked(root, initial);
            Assert.AreEqual(0.2f, handle.Voice.Source.volume, 0.001f);
            Assert.AreEqual(1.2f, handle.Voice.Source.pitch, 0.001f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SwitchUsesInitialLocalStateWithoutChangingGlobalState()
        {
            SoundParameter parameter = Parameter();
            SoundEvent a = Leaf();
            SoundEvent b = Leaf();
            SoundEvent root = Group(a, b);
            root.ContainerMode = ContainerMode.Switch;
            root.SwitchParameter = parameter;
            root.Children = new[] { SoundEventChild.Create(a, 0), SoundEventChild.Create(b, 1) };
            ParameterStore initial = new ParameterStore();
            initial.Set(parameter, 1f);
            SoundHandle first = AudioSystem.PlayTracked(root, initial);
            SoundHandle second = AudioSystem.PlayTracked(root);
            Assert.AreSame(b, first.Voice.CurrentEvent);
            Assert.AreSame(a, second.Voice.CurrentEvent);
            Assert.AreEqual(0f, AudioSystem.GetParameter(parameter));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CycleRejectsWholeEventWithoutAllocatingVoices()
        {
            SoundEvent root = Group(Leaf());
            root.Children = new[] { SoundEventChild.Create(Leaf()), SoundEventChild.Create(root) };
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OversizedContainerCannotStealItsOwnLayers()
        {
            AudioRuntime.Instance.RebuildPool(1, 0);
            SoundHandle existing = AudioSystem.PlayTracked(Leaf());
            Assert.IsFalse(AudioSystem.PlayTracked(Group(Leaf(), Leaf())).IsAssigned);
            Assert.IsTrue(existing.IsPlaying);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator StealingStopsTheEntirePreviousGroup()
        {
            AudioRuntime.Instance.RebuildPool(2, 0);
            SoundHandle first = AudioSystem.PlayTracked(Group(Leaf(), Leaf()));
            SoundHandle second = AudioSystem.PlayTracked(Leaf());
            Assert.IsFalse(first.IsPlaying);
            Assert.IsTrue(second.IsPlaying);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CompletedPrimaryCannotBeReusedWhileFollowerIsPlaying()
        {
            SoundHandle first = AudioSystem.PlayTracked(Group(Leaf(0.1f, false), Leaf()));
            Voice primary = first.Voice;
            yield return AudioTestUtil.WaitUntil(() => !primary.IsActive, "first layer to finish");
            SoundHandle other = AudioSystem.PlayTracked(Leaf());
            Assert.AreNotSame(primary, other.Voice);
            Assert.IsTrue(first.IsPlaying);
            primary.Stop();
            Assert.IsFalse(first.IsPlaying);
            Assert.IsTrue(other.IsPlaying);
        }

        [UnityTest]
        public IEnumerator VirtualGroupKeepsParametersAndStopsEveryLayer()
        {
            SoundParameter parameter = Parameter();
            SoundEvent a = Leaf();
            SoundEvent b = Leaf();
            a.SpatialBlend = b.SpatialBlend = 1f;
            a.AllowVirtualization = b.AllowVirtualization = true;
            SoundEvent root = Group(a, b);
            root.ParameterBindings = new[] { ParameterBinding.CreateRuntime(parameter, ParameterTarget.Volume, AnimationCurve.Linear(0f, 0f, 1f, 1f)) };
            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(root, new Vector3(0, 0, 500));
            yield return AudioTestUtil.WaitUntil(() => AudioRuntime.Instance.VirtualVoiceCount == 2, "both layers to virtualize");
            handle.SetParameter(parameter, 0.3f);
            listener.transform.position = new Vector3(0, 0, 500);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsVirtual, "both layers to resume");
            Assert.AreEqual(0.3f, handle.Voice.Source.volume, 0.01f);
            Assert.AreEqual(0.3f, handle.Voice.Followers[0].Source.volume, 0.01f);
            handle.Stop();
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);
            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
        }

        [UnityTest]
        public IEnumerator ExpiredVirtualHandleIsRemovedWithoutAListener()
        {
            SoundEvent item = Leaf(0.2f, false);
            item.SpatialBlend = 1f;
            item.AllowVirtualization = true;
            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(item, new Vector3(0, 0, 500));
            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "voice to virtualize");
            Object.Destroy(listener);
            yield return AudioTestUtil.WaitUntil(() => !handle.IsPlaying, "virtual clip to expire");
            Assert.AreEqual(0, AudioRuntime.Instance.TrackedHandleCount);
        }

        [UnityTest]
        public IEnumerator StopAllRemovesVirtualPlayback()
        {
            SoundEvent item = Leaf();
            item.SpatialBlend = 1f;
            item.AllowVirtualization = true;
            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(item, new Vector3(0, 0, 500));
            yield return AudioTestUtil.WaitUntil(() => handle.IsVirtual, "voice to virtualize");
            AudioSystem.StopAll();
            Assert.IsFalse(handle.IsPlaying);
            Assert.AreEqual(0, AudioRuntime.Instance.VirtualVoiceCount);
        }

        [UnityTest]
        public IEnumerator InstanceLimitRejectsNewestAndCooldownExplainsRejection()
        {
            SoundEvent item = Leaf();
            item.MaxInstances = 1;
            item.LimitPolicy = VoiceLimitPolicy.RejectNewest;
            SoundHandle first = AudioSystem.PlayTracked(item);
            Assert.IsFalse(AudioSystem.PlayTracked(item).IsAssigned);
            Assert.IsTrue(first.IsPlaying);
            EventDebugRecord record;
            AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackOutcome.RejectedInstanceLimit, record.Outcome);
            first.Stop();
            item.CooldownSeconds = 10f;
            Assert.IsFalse(AudioSystem.PlayTracked(item).IsAssigned);
            AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackOutcome.RejectedCooldown, record.Outcome);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PerEmitterLimitDoesNotStealAnotherEmitter()
        {
            GameObject a = new GameObject("Emitter A");
            GameObject b = new GameObject("Emitter B");
            assets.Add(a); assets.Add(b);
            SoundEvent item = Leaf();
            item.MaxInstancesPerEmitter = 1;
            SoundHandle first = AudioSystem.PlayTrackedAttached(item, a.transform);
            SoundHandle second = AudioSystem.PlayTrackedAttached(item, b.transform);
            SoundHandle replacement = AudioSystem.PlayTrackedAttached(item, a.transform);
            Assert.IsFalse(first.IsPlaying);
            Assert.IsTrue(second.IsPlaying);
            Assert.IsTrue(replacement.IsPlaying);
            yield return null;
        }

        [UnityTest]
        public IEnumerator SharedGroupLimitsDifferentEvents()
        {
            ConcurrencyGroup group = ScriptableObject.CreateInstance<ConcurrencyGroup>();
            assets.Add(group);
            group.MaxInstances = 1;
            group.Policy = VoiceLimitPolicy.RejectNewest;
            SoundEvent a = Leaf();
            SoundEvent b = Leaf();
            a.ConcurrencyGroup = b.ConcurrencyGroup = group;
            SoundHandle first = AudioSystem.PlayTracked(a);
            Assert.IsFalse(AudioSystem.PlayTracked(b).IsAssigned);
            Assert.IsTrue(first.IsPlaying);
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuietestPolicyChoosesQuietInstance()
        {
            AudioRuntime.Instance.RebuildPool(2, 0);
            SoundEvent quiet = Leaf(); quiet.Volume = 0.05f;
            SoundHandle loud = AudioSystem.PlayTracked(Leaf());
            SoundHandle soft = AudioSystem.PlayTracked(quiet);
            SoundEvent incoming = Leaf(); incoming.LimitPolicy = VoiceLimitPolicy.StealQuietest;
            AudioSystem.PlayTracked(incoming);
            Assert.IsTrue(loud.IsPlaying);
            Assert.IsFalse(soft.IsPlaying);
            yield return null;
        }

        [UnityTest]
        public IEnumerator FailedCombinedAdmissionDoesNotStopPreviouslySelectedVictims()
        {
            SoundEvent a = Leaf(); a.MaxInstances = 1;
            SoundHandle first = AudioSystem.PlayTracked(a);
            ConcurrencyGroup group = ScriptableObject.CreateInstance<ConcurrencyGroup>();
            assets.Add(group); group.MaxInstances = 1; group.Policy = VoiceLimitPolicy.RejectNewest;
            SoundEvent b = Leaf(); b.ConcurrencyGroup = group;
            SoundHandle second = AudioSystem.PlayTracked(b);
            a.ConcurrencyGroup = group;
            Assert.IsFalse(AudioSystem.PlayTracked(a).IsAssigned);
            Assert.IsTrue(first.IsPlaying);
            Assert.IsTrue(second.IsPlaying);
            yield return null;
        }
    }
}
