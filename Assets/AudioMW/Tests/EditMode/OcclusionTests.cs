using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class OcclusionTests
    {
        [Test]
        public void FullyBlockedReadsAsOne()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.SetTargetFromHits(3, 3);

            Assert.AreEqual(1f, sampler.Target, 0.0001f);
        }

        [Test]
        public void PartiallyBlockedReadsAsFraction()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.SetTargetFromHits(1, 4);

            Assert.AreEqual(0.25f, sampler.Target, 0.0001f);
        }

        [Test]
        public void HitsAreClampedToSampleCount()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.SetTargetFromHits(99, 3);

            Assert.AreEqual(1f, sampler.Target, 0.0001f);

            sampler.SetTargetFromHits(-5, 3);

            Assert.AreEqual(0f, sampler.Target, 0.0001f);
        }

        [Test]
        public void ZeroSamplesReadsAsOpen()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.SetTargetFromHits(2, 0);

            Assert.AreEqual(0f, sampler.Target, 0.0001f);
        }

        [Test]
        public void AdvanceMovesGraduallyTowardsTarget()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.Reset(0f);
            sampler.SetTargetFromHits(1, 1);

            float afterOneStep = sampler.Advance(0.1f, 1f);

            Assert.Greater(afterOneStep, 0f);
            Assert.Less(afterOneStep, 1f);
        }

        [Test]
        public void AdvanceReachesTargetEventually()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.Reset(0f);
            sampler.SetTargetFromHits(1, 1);

            for (int i = 0; i < 40; i++)
            {
                sampler.Advance(0.1f, 1f);
            }

            Assert.AreEqual(1f, sampler.Current, 0.001f);
        }

        [Test]
        public void ZeroSmoothingSnapsImmediately()
        {
            OcclusionSampler sampler = new OcclusionSampler();
            sampler.Reset(0f);
            sampler.SetTargetFromHits(1, 1);

            Assert.AreEqual(1f, sampler.Advance(0.016f, 0f), 0.0001f);
        }

        [Test]
        public void VolumeMultiplierFallsWithOcclusion()
        {
            OcclusionSettings settings = OcclusionSettings.CreateRuntime(3, 0.7f, 22000f, 900f);

            Assert.AreEqual(1f, settings.VolumeMultiplierFor(0f), 0.0001f);
            Assert.AreEqual(0.3f, settings.VolumeMultiplierFor(1f), 0.0001f);
            Assert.AreEqual(0.65f, settings.VolumeMultiplierFor(0.5f), 0.0001f);
        }

        [Test]
        public void CutoffInterpolatesLogarithmically()
        {
            OcclusionSettings settings = OcclusionSettings.CreateRuntime(3, 0.5f, 20000f, 500f);

            Assert.AreEqual(20000f, settings.CutoffFor(0f), 1f);
            Assert.AreEqual(500f, settings.CutoffFor(1f), 1f);

            float mid = settings.CutoffFor(0.5f);

            Assert.AreEqual(Mathf.Sqrt(20000f * 500f), mid, 50f);
        }

        [Test]
        public void OccludedCutoffNeverExceedsOpenCutoff()
        {
            OcclusionSettings settings = OcclusionSettings.CreateRuntime(3, 0.5f, 800f, 5000f);

            Assert.LessOrEqual(settings.OccludedCutoff, settings.OpenCutoff);
        }

        [Test]
        public void SampleCountIsClamped()
        {
            Assert.AreEqual(1, OcclusionSettings.CreateRuntime(-4, 0.5f, 20000f, 500f).SampleCount);
            Assert.AreEqual(9, OcclusionSettings.CreateRuntime(40, 0.5f, 20000f, 500f).SampleCount);
        }

        [Test]
        public void FirstSampleHasNoOffset()
        {
            Assert.AreEqual(Vector3.zero, OcclusionSampler.SampleOffset(0, 3, 1f, Vector3.forward));
        }

        [Test]
        public void OffsetsSitOnTheSpreadRadius()
        {
            for (int i = 1; i < 5; i++)
            {
                Vector3 offset = OcclusionSampler.SampleOffset(i, 5, 2f, Vector3.forward);

                Assert.AreEqual(2f, offset.magnitude, 0.001f);
            }
        }

        [Test]
        public void OffsetsArePerpendicularToTheRay()
        {
            Vector3 direction = new Vector3(1f, 2f, 3f);

            for (int i = 1; i < 4; i++)
            {
                Vector3 offset = OcclusionSampler.SampleOffset(i, 4, 1.5f, direction);

                Assert.AreEqual(0f, Vector3.Dot(offset.normalized, direction.normalized), 0.001f);
            }
        }

        [Test]
        public void DegenerateDirectionStillProducesOffsets()
        {
            Vector3 offset = OcclusionSampler.SampleOffset(1, 3, 1f, Vector3.zero);

            Assert.AreEqual(1f, offset.magnitude, 0.001f);
        }
    }
}
