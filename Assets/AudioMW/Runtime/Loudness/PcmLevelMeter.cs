using System;

namespace AudioMW
{
    public sealed class PcmLevelMeter
    {
        public int Channels { get; }
        public int Rate { get; }
        public int Blocks { get; private set; }
        public double Momentary { get; private set; } = double.NaN;
        public double ShortTerm { get; private set; } = double.NaN;
        public readonly double[] Peak;
        public readonly double[] TruePeak;
        public readonly double[] MaxTruePeak;
        private readonly Biquad[] shelf, highPass;
        private readonly float[] pcm;
        private readonly int hop;
        private readonly double[] energy = new double[30];
        private readonly double[] gatedBlocks = new double[6000];
        private int energyHead, gateHead, gateCount, written;
        private double sum;

        public PcmLevelMeter(int channels, int rate)
        {
            if (channels < 1 || channels > 2 || rate < 1000) throw new ArgumentOutOfRangeException();
            Channels = channels;
            Rate = rate;
            hop = (int)Math.Round(rate * .1);
            pcm = new float[(hop + 11) * channels];
            Peak = new double[channels];
            TruePeak = new double[channels];
            MaxTruePeak = new double[channels];
            shelf = new Biquad[channels];
            highPass = new Biquad[channels];
            for (int c = 0; c < channels; c++)
            {
                shelf[c] = LoudnessMeter.CreateShelf(rate);
                highPass[c] = LoudnessMeter.CreateHighPass(rate);
                Peak[c] = TruePeak[c] = MaxTruePeak[c] = double.NegativeInfinity;
            }
        }

        public void Push(float[] samples, int frames)
        {
            if (samples == null || frames < 0 || frames > samples.Length / Channels) throw new ArgumentOutOfRangeException();
            for (int frame = 0; frame < frames; frame++)
            {
                for (int c = 0; c < Channels; c++)
                {
                    float value = samples[frame * Channels + c];
                    pcm[(11 + written) * Channels + c] = value;
                    double weighted = highPass[c].Process(shelf[c].Process(value));
                    sum += weighted * weighted;
                }
                if (++written == hop) FinishBlock();
            }
        }

        private void FinishBlock()
        {
            for (int c = 0; c < Channels; c++)
            {
                double peak = 0;
                for (int i = 11 * Channels + c; i < pcm.Length; i += Channels)
                    peak = Math.Max(peak, Math.Abs(pcm[i]));
                Peak[c] = LoudnessMeter.ToDecibels(peak);
                TruePeak[c] = LoudnessMeter.ToDecibels(Math.Max(peak, TruePeakEstimator.Measure(pcm, Channels, 5, hop, c)));
                MaxTruePeak[c] = Math.Max(MaxTruePeak[c], TruePeak[c]);
            }
            Array.Copy(pcm, hop * Channels, pcm, 0, 11 * Channels);
            energy[energyHead] = sum / hop;
            energyHead = (energyHead + 1) % energy.Length;
            Blocks++;
            if (Blocks >= 4)
            {
                double block = Mean(4);
                Momentary = LoudnessMeter.BlockLoudness(block);
                gatedBlocks[gateHead] = block;
                gateHead = (gateHead + 1) % gatedBlocks.Length;
                gateCount = Math.Min(gateCount + 1, gatedBlocks.Length);
            }
            if (Blocks >= 30) ShortTerm = LoudnessMeter.BlockLoudness(Mean(30));
            written = 0;
            sum = 0;
        }

        private double Mean(int blocks)
        {
            double total = 0;
            for (int i = 1; i <= blocks; i++)
                total += energy[(energyHead - i + energy.Length) % energy.Length];
            return total / blocks;
        }

        public double Integrated()
        {
            if (gateCount == 0) return double.NaN;
            double absolute = Math.Pow(10, (LoudnessMeter.AbsoluteGateLufs + .691) / 10);
            double mean = GatedMean(absolute);
            return LoudnessMeter.BlockLoudness(GatedMean(Math.Max(absolute, mean * .1)));
        }

        private double GatedMean(double threshold)
        {
            double total = 0;
            int count = 0;
            for (int i = 0; i < gateCount; i++)
                if (gatedBlocks[i] > threshold) { total += gatedBlocks[i]; count++; }
            return count > 0 ? total / count : 0;
        }
    }
}
