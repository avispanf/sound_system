using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class VirtualizationTests
    {
        [Test]
        public void RealVoiceStaysRealInsideRange()
        {
            Assert.IsFalse(VirtualizationPolicy.ShouldVirtualize(false, 5f, 20f));
        }

        [Test]
        public void RealVoiceVirtualisesWellBeyondRange()
        {
            Assert.IsTrue(VirtualizationPolicy.ShouldVirtualize(false, 40f, 20f));
        }

        [Test]
        public void HysteresisPreventsFlappingAtTheBoundary()
        {
            const float range = 20f;
            float justOutside = range + 1f;

            Assert.IsFalse(VirtualizationPolicy.ShouldVirtualize(false, justOutside, range));
            Assert.IsTrue(VirtualizationPolicy.ShouldVirtualize(true, justOutside, range));
        }

        [Test]
        public void VirtualVoiceBecomesRealOnlyWellInsideRange()
        {
            const float range = 20f;

            Assert.IsTrue(VirtualizationPolicy.ShouldVirtualize(true, 18f, range));
            Assert.IsFalse(VirtualizationPolicy.ShouldVirtualize(true, 10f, range));
        }

        [Test]
        public void ZeroRangeNeverVirtualises()
        {
            Assert.IsFalse(VirtualizationPolicy.ShouldVirtualize(false, 1000f, 0f));
        }

        [Test]
        public void LoopOffsetWrapsWithinClipLength()
        {
            Assert.AreEqual(0.5f, VirtualizationPolicy.PlaybackOffset(2.5f, 2f, true), 0.0001f);
            Assert.AreEqual(0f, VirtualizationPolicy.PlaybackOffset(4f, 2f, true), 0.0001f);
        }

        [Test]
        public void OneShotOffsetClampsToClipLength()
        {
            Assert.AreEqual(1.5f, VirtualizationPolicy.PlaybackOffset(1.5f, 2f, false), 0.0001f);
            Assert.AreEqual(2f, VirtualizationPolicy.PlaybackOffset(9f, 2f, false), 0.0001f);
        }

        [Test]
        public void ZeroLengthClipOffsetsToZero()
        {
            Assert.AreEqual(0f, VirtualizationPolicy.PlaybackOffset(5f, 0f, true), 0.0001f);
        }

        [Test]
        public void NegativeElapsedIsTreatedAsZero()
        {
            Assert.AreEqual(0f, VirtualizationPolicy.PlaybackOffset(-3f, 2f, false), 0.0001f);
        }

        [Test]
        public void LoopsNeverExpire()
        {
            Assert.IsFalse(VirtualizationPolicy.IsExpired(9999f, 2f, true));
        }

        [Test]
        public void OneShotExpiresAfterItsLength()
        {
            Assert.IsFalse(VirtualizationPolicy.IsExpired(1.9f, 2f, false));
            Assert.IsTrue(VirtualizationPolicy.IsExpired(2f, 2f, false));
        }

        [Test]
        public void RemainingIsInfiniteForLoops()
        {
            Assert.IsTrue(float.IsPositiveInfinity(VirtualizationPolicy.RemainingSeconds(1f, 2f, true)));
        }

        [Test]
        public void RemainingCountsDownForOneShots()
        {
            Assert.AreEqual(0.5f, VirtualizationPolicy.RemainingSeconds(1.5f, 2f, false), 0.0001f);
            Assert.AreEqual(0f, VirtualizationPolicy.RemainingSeconds(5f, 2f, false), 0.0001f);
        }

        [Test]
        public void VirtualVoiceTracksElapsedTime()
        {
            VirtualVoice virtualVoice = new VirtualVoice();
            virtualVoice.Configure(null, new PlaybackParameters { IsValid = true }, Vector3.zero, null, 20f, 0f);

            virtualVoice.Advance(0.5f);
            virtualVoice.Advance(0.25f);

            Assert.AreEqual(0.75f, virtualVoice.Elapsed, 0.0001f);
        }

        [Test]
        public void VirtualVoiceUsesFixedPositionWithoutAttachment()
        {
            VirtualVoice virtualVoice = new VirtualVoice();
            Vector3 position = new Vector3(3f, 0f, 4f);
            virtualVoice.Configure(null, new PlaybackParameters { IsValid = true }, position, null, 20f, 0f);

            Assert.AreEqual(position, virtualVoice.CurrentPosition);
        }

        [Test]
        public void VirtualVoiceBecomesRealWhenListenerApproaches()
        {
            VirtualVoice virtualVoice = new VirtualVoice();
            virtualVoice.Configure(null, new PlaybackParameters { IsValid = true }, new Vector3(0f, 0f, 30f), null, 20f, 0f);

            Assert.IsFalse(virtualVoice.ShouldBecomeReal(Vector3.zero, VirtualizationPolicy.DefaultHysteresis));
            Assert.IsTrue(virtualVoice.ShouldBecomeReal(new Vector3(0f, 0f, 25f), VirtualizationPolicy.DefaultHysteresis));
        }
    }
}
