using System;
using System.Collections.Generic;

namespace AudioMW
{
    public struct LoudnessResult
    {
        public bool HasSignal;
        public double IntegratedLufs;
        public double MomentaryMaxLufs;
        public double ShortTermMaxLufs;
        public double SamplePeakDb;
        public double TruePeakDb;
        public double DurationSeconds;

        public static LoudnessResult Silent(double durationSeconds)
        {
            LoudnessResult result = new LoudnessResult();
            result.HasSignal = false;
            result.IntegratedLufs = double.NegativeInfinity;
            result.MomentaryMaxLufs = double.NegativeInfinity;
            result.ShortTermMaxLufs = double.NegativeInfinity;
            result.SamplePeakDb = double.NegativeInfinity;
            result.TruePeakDb = double.NegativeInfinity;
            result.DurationSeconds = durationSeconds;
            return result;
        }

        public double OffsetToTarget(double targetLufs)
        {
            return HasSignal ? targetLufs - IntegratedLufs : 0.0;
        }
    }

    public static class LoudnessMeter
    {
        public const double AbsoluteGateLufs = -70.0;
        public const double RelativeGateOffset = -10.0;
        public const double DefaultTargetLufs = -23.0;

        private const double ShelfFrequency = 1681.974450955533;
        private const double ShelfGainDb = 3.999843853973347;
        private const double ShelfQ = 0.7071752369554196;
        private const double HighPassFrequency = 38.13547087602444;
        private const double HighPassQ = 0.5003270373238773;

        private const double BlockSeconds = 0.4;
        private const double HopSeconds = 0.1;
        private const double ShortTermSeconds = 3.0;

        public static LoudnessResult Analyze(float[] interleaved, int channels, int sampleRate)
        {
            if (interleaved == null || interleaved.Length == 0 || channels <= 0 || sampleRate <= 0)
            {
                return LoudnessResult.Silent(0.0);
            }

            int frames = interleaved.Length / channels;
            double duration = (double)frames / sampleRate;

            if (frames == 0)
            {
                return LoudnessResult.Silent(0.0);
            }

            double samplePeak = 0.0;

            for (int i = 0; i < interleaved.Length; i++)
            {
                double magnitude = Math.Abs(interleaved[i]);

                if (magnitude > samplePeak)
                {
                    samplePeak = magnitude;
                }
            }

            double[] weightedSquares = new double[frames];

            for (int channel = 0; channel < channels; channel++)
            {
                Biquad shelf = Biquad.HighShelf(sampleRate, ShelfFrequency, ShelfGainDb, ShelfQ);
                Biquad highPass = Biquad.HighPass(sampleRate, HighPassFrequency, HighPassQ);
                shelf.Reset();
                highPass.Reset();

                for (int frame = 0; frame < frames; frame++)
                {
                    double sample = interleaved[frame * channels + channel];
                    double filtered = highPass.Process(shelf.Process(sample));
                    weightedSquares[frame] += filtered * filtered;
                }
            }

            int blockSize = (int)Math.Round(BlockSeconds * sampleRate);
            int hopSize = (int)Math.Round(HopSeconds * sampleRate);

            if (blockSize <= 0 || frames < blockSize)
            {
                return AnalyzeShortBuffer(weightedSquares, frames, samplePeak, duration, interleaved, channels, sampleRate);
            }

            List<double> blockMeans = new List<double>();

            for (int start = 0; start + blockSize <= frames; start += hopSize)
            {
                double sum = 0.0;

                for (int i = start; i < start + blockSize; i++)
                {
                    sum += weightedSquares[i];
                }

                blockMeans.Add(sum / blockSize);
            }

            LoudnessResult result = new LoudnessResult();
            result.DurationSeconds = duration;
            result.SamplePeakDb = ToDecibels(samplePeak);
            result.TruePeakDb = ToDecibels(TruePeakEstimator.Measure(interleaved, channels));

            result.IntegratedLufs = ComputeGatedLoudness(blockMeans);
            result.MomentaryMaxLufs = MaxWindowLoudness(weightedSquares, frames, sampleRate, BlockSeconds, HopSeconds);
            result.ShortTermMaxLufs = MaxWindowLoudness(weightedSquares, frames, sampleRate, ShortTermSeconds, HopSeconds);
            result.HasSignal = !double.IsNegativeInfinity(result.IntegratedLufs);

            return result;
        }

        private static LoudnessResult AnalyzeShortBuffer(double[] weightedSquares, int frames, double samplePeak, double duration, float[] interleaved, int channels, int sampleRate)
        {
            double sum = 0.0;

            for (int i = 0; i < frames; i++)
            {
                sum += weightedSquares[i];
            }

            double mean = sum / frames;
            double loudness = BlockLoudness(mean);

            LoudnessResult result = new LoudnessResult();
            result.DurationSeconds = duration;
            result.SamplePeakDb = ToDecibels(samplePeak);
            result.TruePeakDb = ToDecibels(TruePeakEstimator.Measure(interleaved, channels));
            result.IntegratedLufs = loudness;
            result.MomentaryMaxLufs = loudness;
            result.ShortTermMaxLufs = loudness;
            result.HasSignal = !double.IsNegativeInfinity(loudness) && loudness > AbsoluteGateLufs;

            return result;
        }

        private static double ComputeGatedLoudness(List<double> blockMeans)
        {
            double absoluteSum = 0.0;
            int absoluteCount = 0;

            for (int i = 0; i < blockMeans.Count; i++)
            {
                if (BlockLoudness(blockMeans[i]) > AbsoluteGateLufs)
                {
                    absoluteSum += blockMeans[i];
                    absoluteCount++;
                }
            }

            if (absoluteCount == 0)
            {
                return double.NegativeInfinity;
            }

            double relativeThreshold = BlockLoudness(absoluteSum / absoluteCount) + RelativeGateOffset;

            double gatedSum = 0.0;
            int gatedCount = 0;

            for (int i = 0; i < blockMeans.Count; i++)
            {
                double loudness = BlockLoudness(blockMeans[i]);

                if (loudness > AbsoluteGateLufs && loudness > relativeThreshold)
                {
                    gatedSum += blockMeans[i];
                    gatedCount++;
                }
            }

            if (gatedCount == 0)
            {
                return double.NegativeInfinity;
            }

            return BlockLoudness(gatedSum / gatedCount);
        }

        private static double MaxWindowLoudness(double[] weightedSquares, int frames, int sampleRate, double windowSeconds, double hopSeconds)
        {
            int windowSize = (int)Math.Round(windowSeconds * sampleRate);
            int hopSize = Math.Max(1, (int)Math.Round(hopSeconds * sampleRate));

            if (windowSize <= 0 || frames < windowSize)
            {
                return double.NegativeInfinity;
            }

            double best = double.NegativeInfinity;

            for (int start = 0; start + windowSize <= frames; start += hopSize)
            {
                double sum = 0.0;

                for (int i = start; i < start + windowSize; i++)
                {
                    sum += weightedSquares[i];
                }

                double loudness = BlockLoudness(sum / windowSize);

                if (loudness > best)
                {
                    best = loudness;
                }
            }

            return best;
        }

        public static double BlockLoudness(double meanSquare)
        {
            if (meanSquare <= 0.0)
            {
                return double.NegativeInfinity;
            }

            return -0.691 + 10.0 * Math.Log10(meanSquare);
        }

        public static double ToDecibels(double magnitude)
        {
            if (magnitude <= 0.0)
            {
                return double.NegativeInfinity;
            }

            return 20.0 * Math.Log10(magnitude);
        }
    }
}
