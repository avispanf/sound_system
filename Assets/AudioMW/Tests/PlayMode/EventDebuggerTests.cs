using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class EventDebuggerTests
    {
        [SetUp]
        public void SetUp()
        {
            AudioSystem.Shutdown();
            AudioSystem.Debugger.Clear();
            AudioSystem.Debugger.Enabled = true;
            AudioSystem.Debugger.Capacity = EventDebugger.DefaultCapacity;
        }

        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
            AudioSystem.Debugger.Clear();
        }

        [UnityTest]
        public IEnumerator SuccessfulPlayIsRecorded()
        {
            SoundEvent soundEvent = MakeEvent("Footstep");

            AudioSystem.Play(soundEvent);

            EventDebugRecord record;
            Assert.IsTrue(AudioSystem.Debugger.TryGetLast(out record));
            Assert.AreEqual(PlaybackOutcome.Played, record.Outcome);
            Assert.AreEqual("Footstep", record.EventName);

            yield return null;
        }

        [UnityTest]
        public IEnumerator NullEventIsRecordedWithReason()
        {
            AudioSystem.Play(null);

            EventDebugRecord record;
            AudioSystem.Debugger.TryGetLast(out record);

            Assert.AreEqual(PlaybackOutcome.RejectedNullEvent, record.Outcome);
            StringAssert.Contains("no event", record.DescribeOutcome());

            yield return null;
        }

        [UnityTest]
        public IEnumerator EventWithoutClipsIsRecordedWithReason()
        {
            SoundEvent empty = SoundEvent.CreateRuntime();
            empty.name = "Empty";

            AudioSystem.Play(empty);

            EventDebugRecord record;
            AudioSystem.Debugger.TryGetLast(out record);

            Assert.AreEqual(PlaybackOutcome.RejectedNoClips, record.Outcome);
            StringAssert.Contains("no usable clips", record.DescribeOutcome());

            yield return null;
        }

        [UnityTest]
        public IEnumerator VolumeChainIsCaptured()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 1f);
            SoundEvent soundEvent = MakeEvent("Ducked");
            soundEvent.Volume = 0.8f;
            soundEvent.ParameterBindings = new[]
            {
                ParameterBinding.CreateRuntime(parameter, ParameterTarget.Volume, AnimationCurve.Linear(0f, 0f, 1f, 1f))
            };

            AudioSystem.SetParameter(parameter, 0.5f);
            AudioSystem.Play(soundEvent);

            EventDebugRecord record;
            AudioSystem.Debugger.TryGetLast(out record);

            Assert.AreEqual(0.8f, record.BaseVolume, 0.01f);
            Assert.AreEqual(0.5f, record.ParameterMultiplier, 0.01f);
            Assert.AreEqual(0.4f, record.FinalVolume, 0.02f);
            StringAssert.Contains("base", record.DescribeVolumeChain());

            yield return null;
        }

        [UnityTest]
        public IEnumerator CapacityTrimsOldestRecords()
        {
            AudioSystem.Debugger.Capacity = 4;
            SoundEvent soundEvent = MakeEvent("Spam");

            for (int i = 0; i < 10; i++)
            {
                AudioSystem.Play(soundEvent);
            }

            Assert.AreEqual(4, AudioSystem.Debugger.Count);

            yield return null;
        }

        [UnityTest]
        public IEnumerator DisabledDebuggerRecordsNothing()
        {
            AudioSystem.Debugger.Enabled = false;

            AudioSystem.Play(MakeEvent("Ignored"));

            Assert.AreEqual(0, AudioSystem.Debugger.Count);

            AudioSystem.Debugger.Enabled = true;

            yield return null;
        }

        [UnityTest]
        public IEnumerator FilterByNameAndRejections()
        {
            AudioSystem.Play(MakeEvent("Gunshot"));
            AudioSystem.Play(null);

            List<EventDebugRecord> byName = AudioSystem.Debugger.Filter("gun", false);
            List<EventDebugRecord> rejections = AudioSystem.Debugger.Filter(null, true);

            Assert.AreEqual(1, byName.Count);
            Assert.AreEqual(1, rejections.Count);
            Assert.AreEqual(PlaybackOutcome.RejectedNullEvent, rejections[0].Outcome);

            yield return null;
        }

        [UnityTest]
        public IEnumerator OutcomeCountsAreReported()
        {
            AudioSystem.Play(MakeEvent("One"));
            AudioSystem.Play(MakeEvent("Two"));
            AudioSystem.Play(null);

            Assert.AreEqual(2, AudioSystem.Debugger.CountWithOutcome(PlaybackOutcome.Played));
            Assert.AreEqual(1, AudioSystem.Debugger.CountWithOutcome(PlaybackOutcome.RejectedNullEvent));

            yield return null;
        }

        private static SoundEvent MakeEvent(string eventName)
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(1f));
            soundEvent.name = eventName;
            soundEvent.SpatialBlend = 0f;
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

            AudioClip clip = AudioClip.Create("AudioMW_DebugSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
