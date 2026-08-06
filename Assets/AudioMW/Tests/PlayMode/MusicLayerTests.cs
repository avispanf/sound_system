using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AudioMW.Tests
{
    public sealed class MusicLayerTests
    {
        [TearDown]
        public void TearDown()
        {
            AudioSystem.StopMusic();
        }

        [Test]
        public void LayerWithoutParameterUsesDefaultWeight()
        {
            MusicLayer layer = MusicLayer.CreateRuntime("solo", null, null, null);

            Assert.AreEqual(1f, layer.EvaluateWeight(0f), 0.0001f);
            Assert.AreEqual(1f, layer.EvaluateWeight(99f), 0.0001f);
        }

        [Test]
        public void LayerWeightFollowsCurve()
        {
            SoundParameter parameter = SoundParameter.CreateRuntime(0f, 10f, 0f);
            MusicLayer layer = MusicLayer.CreateRuntime("strings", null, parameter, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            Assert.AreEqual(0f, layer.EvaluateWeight(0f), 0.0001f);
            Assert.AreEqual(0.5f, layer.EvaluateWeight(5f), 0.0001f);
            Assert.AreEqual(1f, layer.EvaluateWeight(10f), 0.0001f);
        }

        [Test]
        public void LayerWithoutClipIsInvalid()
        {
            Assert.IsFalse(MusicLayer.CreateRuntime("empty", null, null, null).IsValid);
        }

        [UnityTest]
        public IEnumerator TrackWithLayersBuildsChannelPerLayer()
        {
            SoundParameter parameter;
            MusicTrack track = MakeLayeredTrack(out parameter);

            AudioSystem.PlayMusic(track);

            Assert.AreEqual(2, AudioSystem.Music.ChannelCount);
            Assert.AreEqual("base", AudioSystem.Music.GetLayerName(0));
            Assert.AreEqual("high", AudioSystem.Music.GetLayerName(1));

            yield return null;
        }

        [UnityTest]
        public IEnumerator LayerWeightFollowsGlobalParameter()
        {
            SoundParameter parameter;
            MusicTrack track = MakeLayeredTrack(out parameter);

            AudioSystem.SetParameter(parameter, 0f);
            AudioSystem.PlayMusic(track);

            yield return null;

            Assert.AreEqual(0f, AudioSystem.Music.GetLayerWeight(1), 0.05f);

            AudioSystem.SetParameter(parameter, 1f);

            yield return new WaitForSeconds(0.5f);

            Assert.AreEqual(1f, AudioSystem.Music.GetLayerWeight(1), 0.05f);
        }

        [UnityTest]
        public IEnumerator LayerFadeIsGradual()
        {
            SoundParameter parameter;
            MusicTrack track = MakeLayeredTrack(out parameter);

            AudioSystem.SetParameter(parameter, 0f);
            AudioSystem.PlayMusic(track);

            yield return null;

            AudioSystem.SetParameter(parameter, 1f);

            yield return null;
            yield return null;

            float partial = AudioSystem.Music.GetLayerWeight(1);

            Assert.Greater(partial, 0f);
            Assert.Less(partial, 1f);
        }

        [UnityTest]
        public IEnumerator LayersStayScheduledTogether()
        {
            SoundParameter parameter;
            MusicTrack track = MakeLayeredTrack(out parameter);

            AudioSystem.PlayMusic(track);
            double firstSchedule = AudioSystem.Music.NextScheduleDspTime;

            yield return new WaitForSeconds(0.7f);

            Assert.IsTrue(AudioSystem.Music.IsPlaying);
            Assert.Greater(AudioSystem.Music.NextScheduleDspTime, firstSchedule);
            Assert.AreEqual(2, AudioSystem.Music.ChannelCount);
        }

        [UnityTest]
        public IEnumerator StopClearsChannels()
        {
            SoundParameter parameter;
            MusicTrack track = MakeLayeredTrack(out parameter);

            AudioSystem.PlayMusic(track);
            AudioSystem.StopMusic();

            Assert.AreEqual(0, AudioSystem.Music.ChannelCount);

            yield return null;
        }

        private static MusicTrack MakeLayeredTrack(out SoundParameter parameter)
        {
            parameter = SoundParameter.CreateRuntime(0f, 1f, 0f);

            MusicTrack track = MusicTrack.CreateRuntime(null, MakeSine(0.4f), 120.0, 4);
            track.Volume = 0.05f;
            track.Layers = new[]
            {
                MusicLayer.CreateRuntime("high", MakeSine(0.4f), parameter, AnimationCurve.Linear(0f, 0f, 1f, 1f), 0.25f)
            };

            return track;
        }

        private static AudioClip MakeSine(float seconds)
        {
            const int sampleRate = 44100;
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(sampleRate * seconds));
            float[] data = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                data[i] = Mathf.Sin(2f * Mathf.PI * 330f * i / sampleRate) * 0.2f;
            }

            AudioClip clip = AudioClip.Create("AudioMW_LayerSine", sampleCount, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
