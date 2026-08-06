using System;
using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class LoudnessMeterTests
    {
        private const int SampleRate = 48000;

        [Test]
        public void SilenceReportsNoSignal()
        {
            float[] buffer = new float[SampleRate * 2];
            LoudnessResult result = LoudnessMeter.Analyze(buffer, 1, SampleRate);

            Assert.IsFalse(result.HasSignal);
            Assert.IsTrue(double.IsNegativeInfinity(result.IntegratedLufs));
        }

        [Test]
        public void EmptyBufferIsHandled()
        {
            LoudnessResult result = LoudnessMeter.Analyze(new float[0], 1, SampleRate);

            Assert.IsFalse(result.HasSignal);
            Assert.AreEqual(0.0, result.DurationSeconds, 1e-9);
        }

        [Test]
        public void NullBufferIsHandled()
        {
            Assert.DoesNotThrow(() => LoudnessMeter.Analyze(null, 2, SampleRate));
        }

        [Test]
        public void StereoSineMatchesEbuReference()
        {
            float[] buffer = MakeSine(0.0766f, 1000f, 5f, 2);
            LoudnessResult result = LoudnessMeter.Analyze(buffer, 2, SampleRate);

            Assert.IsTrue(result.HasSignal);
            Assert.AreEqual(-23.0, result.IntegratedLufs, 1.5);
        }

        [Test]
        public void DoublingAmplitudeAddsSixDecibels()
        {
            LoudnessResult quiet = LoudnessMeter.Analyze(MakeSine(0.05f, 1000f, 4f, 1), 1, SampleRate);
            LoudnessResult loud = LoudnessMeter.Analyze(MakeSine(0.1f, 1000f, 4f, 1), 1, SampleRate);

            Assert.AreEqual(6.02, loud.IntegratedLufs - quiet.IntegratedLufs, 0.2);
        }

        [Test]
        public void StereoIsThreeDecibelsLouderThanMono()
        {
            LoudnessResult mono = LoudnessMeter.Analyze(MakeSine(0.1f, 1000f, 4f, 1), 1, SampleRate);
            LoudnessResult stereo = LoudnessMeter.Analyze(MakeSine(0.1f, 1000f, 4f, 2), 2, SampleRate);

            Assert.AreEqual(3.01, stereo.IntegratedLufs - mono.IntegratedLufs, 0.2);
        }

        [Test]
        public void LowFrequencyIsAttenuatedByKWeighting()
        {
            LoudnessResult low = LoudnessMeter.Analyze(MakeSine(0.1f, 40f, 4f, 1), 1, SampleRate);
            LoudnessResult mid = LoudnessMeter.Analyze(MakeSine(0.1f, 1000f, 4f, 1), 1, SampleRate);

            Assert.Less(low.IntegratedLufs, mid.IntegratedLufs - 5.0);
        }

        [Test]
        public void HighFrequencyIsBoostedByKWeighting()
        {
            LoudnessResult high = LoudnessMeter.Analyze(MakeSine(0.1f, 6000f, 4f, 1), 1, SampleRate);
            LoudnessResult mid = LoudnessMeter.Analyze(MakeSine(0.1f, 1000f, 4f, 1), 1, SampleRate);

            Assert.Greater(high.IntegratedLufs, mid.IntegratedLufs + 1.0);
        }

        [Test]
        public void SamplePeakMatchesAmplitude()
        {
            LoudnessResult result = LoudnessMeter.Analyze(MakeSine(0.5f, 1000f, 1f, 1), 1, SampleRate);

            Assert.AreEqual(-6.02, result.SamplePeakDb, 0.15);
        }

        [Test]
        public void TruePeakIsNotBelowSamplePeak()
        {
            LoudnessResult result = LoudnessMeter.Analyze(MakeSine(0.5f, 7000f, 1f, 1), 1, SampleRate);

            Assert.GreaterOrEqual(result.TruePeakDb, result.SamplePeakDb - 0.2);
        }

        [Test]
        public void GatingIgnoresLongSilence()
        {
            float[] tone = MakeSine(0.1f, 1000f, 3f, 1);
            float[] padded = new float[tone.Length + SampleRate * 10];
            Array.Copy(tone, padded, tone.Length);

            LoudnessResult toneOnly = LoudnessMeter.Analyze(tone, 1, SampleRate);
            LoudnessResult withSilence = LoudnessMeter.Analyze(padded, 1, SampleRate);

            Assert.AreEqual(toneOnly.IntegratedLufs, withSilence.IntegratedLufs, 1.0);
        }

        [Test]
        public void ShortTermIsUnavailableForShortClips()
        {
            LoudnessResult result = LoudnessMeter.Analyze(MakeSine(0.1f, 1000f, 1f, 1), 1, SampleRate);

            Assert.IsTrue(double.IsNegativeInfinity(result.ShortTermMaxLufs));
            Assert.IsFalse(double.IsNegativeInfinity(result.MomentaryMaxLufs));
        }

        [Test]
        public void OffsetToTargetIsSignedDifference()
        {
            LoudnessResult result = LoudnessMeter.Analyze(MakeSine(0.0766f, 1000f, 5f, 2), 2, SampleRate);

            double offset = result.OffsetToTarget(-16.0);

            Assert.AreEqual(-16.0 - result.IntegratedLufs, offset, 1e-9);
        }

        [Test]
        public void ClipShorterThanBlockStillReports()
        {
            LoudnessResult result = LoudnessMeter.Analyze(MakeSine(0.2f, 1000f, 0.1f, 1), 1, SampleRate);

            Assert.IsTrue(result.HasSignal);
            Assert.IsFalse(double.IsNegativeInfinity(result.IntegratedLufs));
        }

        private static float[] MakeSine(float amplitude, float frequency, float seconds, int channels)
        {
            int frames = (int)(SampleRate * seconds);
            float[] buffer = new float[frames * channels];

            for (int frame = 0; frame < frames; frame++)
            {
                float value = amplitude * (float)Math.Sin(2.0 * Math.PI * frequency * frame / SampleRate);

                for (int channel = 0; channel < channels; channel++)
                {
                    buffer[frame * channels + channel] = value;
                }
            }

            return buffer;
        }
    }
}
