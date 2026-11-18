using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class AttenuationPresetTests
    {
        [Test]
        public void RadiusAddsAngularWidthWithoutChangingGainOrOverridingWiderAuthoredSpread()
        {
            var preset = AttenuationPreset.CreateRuntime(1, 1, 10, AudioRolloffMode.Linear);
            try
            {
                float before = preset.EvaluateAttenuation(5);
                Assert.IsFalse(preset.UsesDistanceProcessing);
                preset.SourceRadius = 2;
                Assert.IsTrue(preset.UsesDistanceProcessing);
                Assert.AreEqual(90, preset.EvaluateSpread(2), .01f);
                Assert.AreEqual(180, preset.EvaluateSpread(0), .01f);
                Assert.Less(preset.EvaluateSpread(8), preset.EvaluateSpread(2));
                Assert.AreEqual(before, preset.EvaluateAttenuation(5));
                preset.DistanceSpread = true;
                preset.SpreadByDistance = AnimationCurve.Constant(0, 1, 230);
                Assert.AreEqual(230, preset.EvaluateSpread(2));
                preset.SourceRadius = -1;
                preset.DistanceSpread = false;
                Assert.IsFalse(preset.UsesDistanceProcessing);
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void FullVolumeInsideMinDistance()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 2f, 20f, AudioRolloffMode.Linear);

            Assert.AreEqual(1f, preset.EvaluateAttenuation(0f), 0.0001f);
            Assert.AreEqual(1f, preset.EvaluateAttenuation(2f), 0.0001f);
        }

        [Test]
        public void LinearRolloffReachesSilenceAtMaxDistance()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 0f, 10f, AudioRolloffMode.Linear);

            Assert.AreEqual(0f, preset.EvaluateAttenuation(10f), 0.0001f);
            Assert.AreEqual(0f, preset.EvaluateAttenuation(50f), 0.0001f);
        }

        [Test]
        public void LinearRolloffIsMonotonic()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 1f, 20f, AudioRolloffMode.Linear);
            float previous = preset.EvaluateAttenuation(1f);

            for (float d = 2f; d <= 20f; d += 0.5f)
            {
                float current = preset.EvaluateAttenuation(d);
                Assert.LessOrEqual(current, previous + 0.0001f);
                previous = current;
            }
        }

        [Test]
        public void LogarithmicRolloffStaysAboveZero()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 1f, 30f, AudioRolloffMode.Logarithmic);

            float far = preset.EvaluateAttenuation(30f);

            Assert.Greater(far, 0f);
            Assert.Less(far, 0.2f);
        }

        [Test]
        public void CustomCurveDrivesAttenuation()
        {
            AnimationCurve curve = AnimationCurve.Linear(0f, 1f, 1f, 0.25f);
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 0f, 10f, AudioRolloffMode.Custom, curve);

            Assert.AreEqual(1f, preset.EvaluateAttenuation(0f), 0.0001f);
            Assert.AreEqual(0.625f, preset.EvaluateAttenuation(5f), 0.01f);
            Assert.IsTrue(preset.UsesCustomCurve);
        }

        [Test]
        public void MaxDistanceNeverFallsBelowMin()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 15f, 5f, AudioRolloffMode.Linear);

            Assert.GreaterOrEqual(preset.MaxDistance, preset.MinDistance);
        }

        [Test]
        public void MinDistanceIsClampedAwayFromZero()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 0f, 10f, AudioRolloffMode.Linear);

            Assert.Greater(preset.MinDistance, 0f);
        }

        [Test]
        public void ApplyToNullSourceIsSafe()
        {
            AttenuationPreset preset = AttenuationPreset.CreateRuntime(1f, 1f, 10f, AudioRolloffMode.Linear);

            Assert.DoesNotThrow(() => preset.ApplyTo(null));
        }
    }
}
