using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class BlendContainerTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
        }

        [UnityTest]
        public IEnumerator BlendSpawnsVoicePerLayer()
        {
            SoundParameter parameter;
            SoundEvent soundEvent = MakeBlendEvent(out parameter);

            Voice primary = AudioSystem.Play(soundEvent);

            Assert.IsNotNull(primary);
            Assert.AreEqual(1, primary.Followers.Count);
            Assert.AreEqual(2, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator BlendCrossfadesByParameter()
        {
            SoundParameter parameter;
            SoundEvent soundEvent = MakeBlendEvent(out parameter);

            AudioSystem.SetParameter(parameter, 0f);
            Voice primary = AudioSystem.Play(soundEvent);
            Voice secondary = primary.Followers[0];

            yield return null;

            Assert.AreEqual(1f, primary.Source.volume, 0.02f);
            Assert.AreEqual(0f, secondary.Source.volume, 0.02f);

            AudioSystem.SetParameter(parameter, 1f);

            yield return null;
            yield return null;

            Assert.AreEqual(0f, primary.Source.volume, 0.02f);
            Assert.AreEqual(1f, secondary.Source.volume, 0.02f);
        }

        [UnityTest]
        public IEnumerator StoppingPrimaryStopsFollowers()
        {
            SoundParameter parameter;
            SoundEvent soundEvent = MakeBlendEvent(out parameter);

            Voice primary = AudioSystem.Play(soundEvent);
            Voice secondary = primary.Followers[0];

            primary.Stop();

            Assert.IsFalse(primary.IsActive);
            Assert.IsFalse(secondary.IsActive);
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator InvalidLayersAreSkipped()
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime();
            soundEvent.SpatialBlend = 0f;
            soundEvent.Volume = 1f;
            soundEvent.Loop = true;
            soundEvent.ContainerMode = ContainerMode.Blend;
            soundEvent.BlendParameter = SoundParameter.CreateRuntime(0f, 1f, 0f);
            soundEvent.BlendLayers = new[]
            {
                BlendLayer.CreateRuntime(null, null),
                BlendLayer.CreateRuntime(MakeSine(2f), AnimationCurve.Linear(0f, 1f, 1f, 1f))
            };

            Voice primary = AudioSystem.Play(soundEvent);

            Assert.IsNotNull(primary);
            Assert.AreEqual(0, primary.Followers.Count);
            Assert.AreEqual(1, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        [UnityTest]
        public IEnumerator LocalParameterReachesEveryLayer()
        {
            SoundParameter parameter;
            SoundEvent soundEvent = MakeBlendEvent(out parameter);

            AudioSystem.SetParameter(parameter, 0f);
            Voice primary = AudioSystem.Play(soundEvent);
            Voice secondary = primary.Followers[0];

            yield return null;

            AudioSystem.SetVoiceParameter(primary, parameter, 1f);

            yield return null;

            Assert.IsTrue(secondary.LocalParameters.Has(parameter));
            Assert.AreEqual(1f, secondary.LocalParameters.Get(parameter), 0.001f);
            Assert.AreEqual(0f, primary.Source.volume, 0.02f);
            Assert.AreEqual(1f, secondary.Source.volume, 0.02f);
        }

        [UnityTest]
        public IEnumerator LocalParameterIsIndependentPerInstance()
        {
            SoundParameter parameter;
            SoundEvent soundEvent = MakeBlendEvent(out parameter);

            AudioSystem.SetParameter(parameter, 0f);
            Voice first = AudioSystem.Play(soundEvent);
            Voice second = AudioSystem.Play(soundEvent);

            AudioSystem.SetVoiceParameter(first, parameter, 1f);

            yield return null;

            Assert.AreEqual(1f, first.Followers[0].Source.volume, 0.02f);
            Assert.AreEqual(0f, second.Followers[0].Source.volume, 0.02f);
        }

        [UnityTest]
        public IEnumerator GroupPlayingReflectsFollowers()
        {
            SoundParameter parameter;
            SoundEvent soundEvent = MakeBlendEvent(out parameter);

            Voice primary = AudioSystem.Play(soundEvent);

            Assert.IsTrue(primary.IsGroupPlaying);

            primary.Stop();

            Assert.IsFalse(primary.IsGroupPlaying);

            yield return null;
        }

        private static SoundEvent MakeBlendEvent(out SoundParameter parameter)
        {
            parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);

            SoundEvent soundEvent = SoundEvent.CreateRuntime();
            soundEvent.SpatialBlend = 0f;
            soundEvent.Volume = 1f;
            soundEvent.Loop = true;
            soundEvent.ContainerMode = ContainerMode.Blend;
            soundEvent.BlendParameter = parameter;
            soundEvent.BlendLayers = new[]
            {
                BlendLayer.CreateRuntime(MakeSine(2f), AnimationCurve.Linear(0f, 1f, 1f, 0f)),
                BlendLayer.CreateRuntime(MakeSine(2f), AnimationCurve.Linear(0f, 0f, 1f, 1f))
            };

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

            AudioClip clip = AudioClip.Create("AudioMW_TestSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
