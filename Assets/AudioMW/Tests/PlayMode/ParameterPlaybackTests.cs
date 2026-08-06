using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class ParameterPlaybackTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopAll();
        }

        [UnityTest]
        public IEnumerator GlobalParameterDrivesVoiceVolume()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 1f);
            SoundEvent soundEvent = MakeEvent(parameter, ParameterTarget.Volume);
            soundEvent.Volume = 1f;

            AudioSystem.SetParameter(parameter, 1f);
            Voice voice = AudioSystem.Play(soundEvent);

            Assert.IsNotNull(voice);
            Assert.AreEqual(1f, voice.Source.volume, 0.01f);

            AudioSystem.SetParameter(parameter, 0.25f);

            yield return null;
            yield return null;

            Assert.AreEqual(0.25f, voice.Source.volume, 0.01f);
        }

        [UnityTest]
        public IEnumerator LocalParameterOverridesGlobal()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 1f);
            SoundEvent soundEvent = MakeEvent(parameter, ParameterTarget.Volume);
            soundEvent.Volume = 1f;

            AudioSystem.SetParameter(parameter, 1f);
            Voice voice = AudioSystem.Play(soundEvent);

            AudioSystem.SetVoiceParameter(voice, parameter, 0.5f);

            yield return null;

            Assert.AreEqual(0.5f, voice.Source.volume, 0.01f);
        }

        [UnityTest]
        public IEnumerator ParameterDrivesPitch()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);
            SoundEvent soundEvent = MakeEvent(parameter, ParameterTarget.Pitch);
            soundEvent.ParameterBindings = new[]
            {
                ParameterBinding.CreateRuntime(parameter, ParameterTarget.Pitch, AnimationCurve.Linear(0f, 1f, 1f, 2f))
            };

            AudioSystem.SetParameter(parameter, 0f);
            Voice voice = AudioSystem.Play(soundEvent);

            yield return null;

            Assert.AreEqual(1f, voice.Source.pitch, 0.01f);

            AudioSystem.SetParameter(parameter, 1f);

            yield return null;
            yield return null;

            Assert.AreEqual(2f, voice.Source.pitch, 0.01f);
        }

        [UnityTest]
        public IEnumerator LocalParametersClearOnRelease()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 1f, 1f);
            SoundEvent soundEvent = MakeEvent(parameter, ParameterTarget.Volume);

            Voice voice = AudioSystem.Play(soundEvent);
            AudioSystem.SetVoiceParameter(voice, parameter, 0.1f);

            Assert.AreEqual(1, voice.LocalParameters.Count);

            voice.Stop();

            Assert.AreEqual(0, voice.LocalParameters.Count);

            yield return null;
        }

        [UnityTest]
        public IEnumerator ShutdownDestroysPool()
        {
            SoundEvent soundEvent = MakeEvent(null, ParameterTarget.Volume);
            AudioSystem.Play(soundEvent);

            Assert.Greater(AudioRuntime.Instance.Pool.TotalCount, 0);

            AudioSystem.Shutdown();

            Assert.AreEqual(0, AudioRuntime.Instance.Pool.TotalCount);
            Assert.AreEqual(0, AudioSystem.ActiveVoiceCount);

            yield return null;
        }

        private static SoundEvent MakeEvent(SoundParameter parameter, ParameterTarget target)
        {
            SoundEvent soundEvent = SoundEvent.CreateRuntime(MakeSine(2f));
            soundEvent.SpatialBlend = 0f;
            soundEvent.Volume = 0.5f;
            soundEvent.Loop = true;

            if (parameter != null)
            {
                soundEvent.ParameterBindings = new[]
                {
                    ParameterBinding.CreateRuntime(parameter, target, AnimationCurve.Linear(0f, 0f, 1f, 1f))
                };
            }

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
