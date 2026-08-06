using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class MixerRoutingTests
    {
        [Test]
        public void BindingMapsParameterToDecibelRange()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);
            MixerParameterBinding binding = MixerParameterBinding.CreateRuntime(
                parameter, "MusicVolume", AnimationCurve.Linear(0f, 0f, 1f, 1f), -80f, 0f);

            Assert.AreEqual(-80f, binding.Evaluate(0f), 0.01f);
            Assert.AreEqual(-40f, binding.Evaluate(0.5f), 0.01f);
            Assert.AreEqual(0f, binding.Evaluate(1f), 0.01f);
        }

        [Test]
        public void BindingClampsInputToParameterRange()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);
            MixerParameterBinding binding = MixerParameterBinding.CreateRuntime(
                parameter, "MusicVolume", AnimationCurve.Linear(0f, 0f, 1f, 1f), -80f, 0f);

            Assert.AreEqual(0f, binding.Evaluate(50f), 0.01f);
            Assert.AreEqual(-80f, binding.Evaluate(-50f), 0.01f);
        }

        [Test]
        public void InvertedCurveDucksOnRisingParameter()
        {
            SoundParameter duck = SoundParameter.CreateRuntime(0f, 1f, 0f);
            MixerParameterBinding binding = MixerParameterBinding.CreateRuntime(
                duck, "MusicVolume", AnimationCurve.Linear(0f, 1f, 1f, 0f), -12f, 0f);

            Assert.AreEqual(0f, binding.Evaluate(0f), 0.01f);
            Assert.AreEqual(-12f, binding.Evaluate(1f), 0.01f);
        }

        [Test]
        public void BindingWithoutExposedNameIsInvalid()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);

            Assert.IsFalse(MixerParameterBinding.CreateRuntime(parameter, null, null, -80f, 0f).IsValid);
            Assert.IsFalse(MixerParameterBinding.CreateRuntime(null, "Name", null, -80f, 0f).IsValid);
        }

        [Test]
        public void InvalidBindingReturnsUpperBound()
        {
            MixerParameterBinding binding = MixerParameterBinding.CreateRuntime(null, null, null, -80f, -3f);

            Assert.AreEqual(-3f, binding.Evaluate(0.5f), 0.01f);
        }

        [Test]
        public void ProfileWithoutMixerIsNotUsable()
        {
            MixerRoutingProfile profile = MixerRoutingProfile.CreateRuntime(null);

            Assert.IsFalse(profile.IsUsable);
            Assert.IsFalse(profile.HasExposedParameter("Anything"));
            Assert.AreEqual(0, profile.FindMissingExposedParameters().Count);
        }

        [Test]
        public void DirectorTracksProfilesWithoutDuplicates()
        {
            MixerDirector director = new MixerDirector();
            MixerRoutingProfile profile = MixerRoutingProfile.CreateRuntime(null);

            director.AddProfile(profile);
            director.AddProfile(profile);
            director.AddProfile(null);

            Assert.AreEqual(1, director.ProfileCount);

            director.RemoveProfile(profile);

            Assert.AreEqual(0, director.ProfileCount);
        }

        [Test]
        public void DirectorTickWithoutMixerWritesNothing()
        {
            MixerDirector director = new MixerDirector();
            director.AddProfile(MixerRoutingProfile.CreateRuntime(null));

            director.Tick();

            Assert.AreEqual(0, director.WritesLastTick);
            Assert.AreEqual(0, director.TotalWrites);
        }

        [Test]
        public void DirectorHandlesNullSnapshotBlends()
        {
            MixerDirector director = new MixerDirector();

            Assert.DoesNotThrow(() => director.TransitionTo(null, 1f));
            Assert.DoesNotThrow(() => director.BlendSnapshots(null, null, null, 1f));
        }
    }
}
