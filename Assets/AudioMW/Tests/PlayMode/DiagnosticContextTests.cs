using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class DiagnosticContextTests
    {
        private readonly List<Object> extras = new List<Object>();
        private SoundEvent item;
        private AudioClip clip;
        private GameObject emitter;
        [SetUp] public void SetUp()
        {
            AudioSystem.Shutdown(); clip = AudioTestUtil.MakeSine(1); item = SoundEvent.CreateRuntime(clip); item.Loop = true;
            emitter = new GameObject("Diagnostic emitter");
        }
        [TearDown] public void TearDown() { AudioSystem.Shutdown(); foreach (Object extra in extras) Object.Destroy(extra); extras.Clear(); Object.Destroy(item); Object.Destroy(clip); Object.Destroy(emitter); }
        private SoundEvent Container(ContainerMode mode, params SoundEventChild[] children)
        {
            SoundEvent parent = SoundEvent.CreateRuntime(); extras.Add(parent); parent.ContainerMode = mode; parent.Children = children; return parent;
        }

        [UnityTest] public IEnumerator NestedUnloadedSourceCapturesTheLeafThatNeedsItsBank()
        {
            item.Clips = new AudioClip[0]; item.ClipReferences = new[] { ClipReference.Addressed("steps/missing") };
            SoundEvent root = Container(ContainerMode.Layered, SoundEventChild.Create(item));
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreSame(root, record.Event); Assert.AreSame(item, record.Rejection.ProblemEvent);
            Assert.AreEqual(PlaybackOutcome.RejectedClipNotLoaded, record.Outcome);
            yield return null;
        }

        [UnityTest] public IEnumerator SwitchCapturesActualLocalStateAndKeepsItAfterLaterEdits()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 5, 1); extras.Add(parameter);
            SoundEvent branch = Container(ContainerMode.Switch, SoundEventChild.Create(item, 1)); branch.SwitchParameter = parameter; branch.name = "Surface selector";
            SoundEvent root = Container(ContainerMode.Layered, SoundEventChild.Create(branch));
            ParameterStore local = new ParameterStore(); local.Set(parameter, 3.6f);
            Assert.IsFalse(AudioSystem.PlayTracked(root, initialParameters: local).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreSame(branch, record.Rejection.ProblemEvent); Assert.AreEqual(PlaybackContentIssue.NoSwitchMatch, record.Rejection.ContentIssue);
            Assert.AreEqual(3.6f, record.Rejection.SwitchValue); Assert.AreEqual(4, record.Rejection.SwitchState);
            branch.name = "Renamed"; local.Set(parameter, 1);
            StringAssert.Contains("Surface selector", record.DescribeOutcome()); StringAssert.Contains("state 4", record.DescribeOutcome());
            yield return null;
        }

        [UnityTest] public IEnumerator MissingSwitchParameterCapturesTheNestedSelector()
        {
            SoundEvent branch = Container(ContainerMode.Switch, SoundEventChild.Create(item));
            SoundEvent root = Container(ContainerMode.Layered, SoundEventChild.Create(branch));
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreSame(branch, record.Rejection.ProblemEvent); Assert.AreEqual(PlaybackContentIssue.MissingSwitchParameter, record.Rejection.ContentIssue);
            Assert.IsFalse(record.HasGainCapture);
            yield return null;
        }

        [UnityTest] public IEnumerator UnusableWeightedLeafCapturesItsOwnEvent()
        {
            item.SelectionMode = ClipSelectionMode.WeightedRandom; item.ClipWeights = new[] { 0f };
            SoundEvent root = Container(ContainerMode.Layered, SoundEventChild.Create(item));
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreSame(item, record.Rejection.ProblemEvent); Assert.AreEqual(PlaybackContentIssue.NoUsableSource, record.Rejection.ContentIssue);
            yield return null;
        }

        [UnityTest] public IEnumerator ActualCycleCapturesTheRepeatedEvent()
        {
            SoundEvent root = Container(ContainerMode.Layered); root.Children = new[] { SoundEventChild.Create(root) };
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackContentIssue.CircularReference, record.Rejection.ContentIssue); Assert.AreSame(root, record.Rejection.ProblemEvent);
            yield return null;
        }

        [UnityTest] public IEnumerator AcceptedLayeredRequestDoesNotExposeIgnoredBranchFailure()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 5, 3); extras.Add(parameter);
            SoundEvent branch = Container(ContainerMode.Switch, SoundEventChild.Create(item, 1)); branch.SwitchParameter = parameter;
            SoundEvent root = Container(ContainerMode.Layered, SoundEventChild.Create(branch), SoundEventChild.Create(item));
            Assert.IsTrue(AudioSystem.PlayTracked(root).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackOutcome.Played, record.Outcome); Assert.IsNull(record.Rejection.ProblemEvent); Assert.AreEqual(PlaybackContentIssue.None, record.Rejection.ContentIssue);
            yield return null;
        }

        [UnityTest] public IEnumerator AFollowingValidRequestDoesNotRetainSwitchFailureContext()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 5, 3); extras.Add(parameter);
            SoundEvent root = Container(ContainerMode.Switch, SoundEventChild.Create(item, 1)); root.SwitchParameter = parameter;
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            ParameterStore local = new ParameterStore(); local.Set(parameter, 1);
            Assert.IsTrue(AudioSystem.PlayTracked(root, initialParameters: local).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackOutcome.Played, record.Outcome); Assert.IsNull(record.Rejection.ProblemEvent);
            yield return null;
        }

        [UnityTest] public IEnumerator NestingLimitCapturesTheEventBeyondTheSupportedDepth()
        {
            SoundEvent root = Container(ContainerMode.Layered); SoundEvent cursor = root;
            for (int i = 0; i < 32; i++)
            {
                SoundEvent next = Container(ContainerMode.Layered); cursor.Children = new[] { SoundEventChild.Create(next) }; cursor = next;
            }
            cursor.Children = new[] { SoundEventChild.Create(item) };
            Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.AreEqual(PlaybackContentIssue.NestingLimit, record.Rejection.ContentIssue); Assert.AreSame(cursor, record.Rejection.ProblemEvent);
            yield return null;
        }

        [UnityTest] public IEnumerator CapacityRejectionDoesNotReuseAnEarlierEmptyBranchReason()
        {
            int capacity = AudioRuntime.Instance.Pool.MaxVoices;
            try
            {
                AudioRuntime.Instance.RebuildPool(1, 0);
                SoundEvent empty = SoundEvent.CreateRuntime(); extras.Add(empty);
                SoundEvent root = Container(ContainerMode.Layered, SoundEventChild.Create(empty), SoundEventChild.Create(item), SoundEventChild.Create(item));
                Assert.IsFalse(AudioSystem.PlayTracked(root).IsAssigned);
                EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
                Assert.AreEqual(PlaybackOutcome.RejectedNoVoice, record.Outcome); Assert.AreEqual(PlaybackContentIssue.None, record.Rejection.ContentIssue);
                StringAssert.Contains("voice capacity", record.DescribeOutcome());
            }
            finally { AudioRuntime.Instance.RebuildPool(capacity, 0); }
            yield return null;
        }

        [UnityTest] public IEnumerator RejectionCapturesActualEmitterCountAndLimit()
        {
            item.MaxInstancesPerEmitter = 2; item.LimitPolicy = VoiceLimitPolicy.RejectNewest;
            AudioSystem.PlayTrackedAttached(item, emitter.transform); AudioSystem.PlayTrackedAttached(item, emitter.transform);
            Assert.IsFalse(AudioSystem.PlayTrackedAttached(item, emitter.transform).IsAssigned);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.IsFalse(record.HasGainCapture); Assert.AreSame(item, record.Event); Assert.AreEqual(2, record.Rejection.CurrentCount); Assert.AreEqual(2, record.Rejection.Limit);
            Assert.AreEqual("emitter", record.Rejection.Scope); StringAssert.Contains("2 instances", record.DescribeOutcome());
            yield return null;
        }
        [UnityTest] public IEnumerator CooldownCapturesTheRemainingTimeInsteadOfInventingACount()
        {
            item.CooldownSeconds = 1; AudioSystem.PlayTracked(item); AudioSystem.PlayTracked(item);
            EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.IsTrue(record.Rejection.HasCooldown); Assert.IsFalse(record.Rejection.HasInstanceLimit);
            Assert.Greater(record.Rejection.RemainingSeconds, 0); Assert.LessOrEqual(record.Rejection.RemainingSeconds, 1);
            yield return null;
        }
        [UnityTest] public IEnumerator AcceptedMuteRemainsAcceptedAndHasNoRejectionContext()
        {
            item.Volume = 0; AudioSystem.PlayTracked(item); EventDebugRecord record; AudioSystem.Debugger.TryGetLast(out record);
            Assert.IsTrue(record.HasGainCapture); Assert.AreEqual(PlaybackOutcome.Played, record.Outcome); Assert.AreEqual(0, record.FinalVolume);
            Assert.IsFalse(record.Rejection.HasInstanceLimit); Assert.IsFalse(record.Rejection.HasCooldown); Assert.AreSame(item, record.Event);
            yield return null;
        }
    }
}
