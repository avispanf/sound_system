using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class ParameterTests
    {
        [Test]
        public void NormalizeMapsRangeToUnitInterval()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(20f, 120f, 20f);

            Assert.AreEqual(0f, parameter.Normalize(20f), 0.0001f);
            Assert.AreEqual(0.5f, parameter.Normalize(70f), 0.0001f);
            Assert.AreEqual(1f, parameter.Normalize(120f), 0.0001f);
        }

        [Test]
        public void NormalizeHandlesDegenerateRange()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(5f, 5f, 5f);
            Assert.AreEqual(0f, parameter.Normalize(5f), 0.0001f);
        }

        [Test]
        public void ClampRespectsBounds()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(-1f, 1f, 0f);

            Assert.AreEqual(-1f, parameter.Clamp(-50f), 0.0001f);
            Assert.AreEqual(1f, parameter.Clamp(50f), 0.0001f);
            Assert.AreEqual(0.25f, parameter.Clamp(0.25f), 0.0001f);
        }

        [Test]
        public void StoreReturnsDefaultWhenUnset()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 10f, 7f);
            ParameterStore store = new ParameterStore();

            Assert.IsFalse(store.Has(parameter));
            Assert.AreEqual(7f, store.Get(parameter), 0.0001f);
        }

        [Test]
        public void StoreClampsOnSet()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 10f, 0f);
            ParameterStore store = new ParameterStore();

            store.Set(parameter, 999f);

            Assert.AreEqual(10f, store.Get(parameter), 0.0001f);
        }

        [Test]
        public void StoreTryGetReportsPresence()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0.5f);
            ParameterStore store = new ParameterStore();

            float value;
            Assert.IsFalse(store.TryGet(parameter, out value));

            store.Set(parameter, 0.25f);

            Assert.IsTrue(store.TryGet(parameter, out value));
            Assert.AreEqual(0.25f, value, 0.0001f);
        }

        [Test]
        public void StoreIgnoresNullParameter()
        {
            ParameterStore store = new ParameterStore();
            store.Set(null, 1f);

            Assert.AreEqual(0, store.Count);
            Assert.AreEqual(0f, store.Get(null), 0.0001f);
        }

        [Test]
        public void BindingEvaluatesCurveOverNormalizedValue()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 100f, 0f);
            ParameterBinding binding = ParameterBinding.CreateRuntime(
                parameter,
                ParameterTarget.Volume,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.AreEqual(0f, binding.Evaluate(0f), 0.0001f);
            Assert.AreEqual(0.5f, binding.Evaluate(50f), 0.0001f);
            Assert.AreEqual(1f, binding.Evaluate(100f), 0.0001f);
        }

        [Test]
        public void BindingClampsInputBeyondRange()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);
            ParameterBinding binding = ParameterBinding.CreateRuntime(
                parameter,
                ParameterTarget.Pitch,
                AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.AreEqual(1f, binding.Evaluate(17f), 0.0001f);
            Assert.AreEqual(0f, binding.Evaluate(-17f), 0.0001f);
        }

        [Test]
        public void InvalidBindingIsNeutral()
        {
            ParameterBinding binding = ParameterBinding.CreateRuntime(null, ParameterTarget.Volume, null);

            Assert.IsFalse(binding.IsValid);
            Assert.AreEqual(1f, binding.Evaluate(123f), 0.0001f);
        }
    }
}
