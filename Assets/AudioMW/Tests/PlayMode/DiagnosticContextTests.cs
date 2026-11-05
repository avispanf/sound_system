using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class DiagnosticContextTests
    {
        private SoundEvent item;
        private AudioClip clip;
        private GameObject emitter;
        [SetUp] public void SetUp()
        {
            AudioSystem.Shutdown(); clip = AudioTestUtil.MakeSine(1); item = SoundEvent.CreateRuntime(clip); item.Loop = true;
            emitter = new GameObject("Diagnostic emitter");
        }
        [TearDown] public void TearDown() { AudioSystem.Shutdown(); Object.Destroy(item); Object.Destroy(clip); Object.Destroy(emitter); }
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
