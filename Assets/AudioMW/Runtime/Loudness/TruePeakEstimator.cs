using System;

namespace AudioMW
{
    public static class TruePeakEstimator
    {
        public const int OversampleFactor = 4;
        private const int TapsPerPhase = 12;

        private static readonly double[][] Phases;

        static TruePeakEstimator()
        {
            Phases = new double[OversampleFactor][];

            for (int phase = 0; phase < OversampleFactor; phase++)
            {
                double[] taps = new double[TapsPerPhase];
                double fraction = (double)phase / OversampleFactor;
                double sum = 0.0;

                for (int tap = 0; tap < TapsPerPhase; tap++)
                {
                    double offset = tap - TapsPerPhase / 2 + 1 - fraction;
                    double pix = Math.PI * offset;
                    double sinc = Math.Abs(offset) < 1e-9 ? 1.0 : Math.Sin(pix) / pix;
                    double window = 0.54 - 0.46 * Math.Cos(2.0 * Math.PI * (tap + 0.5) / TapsPerPhase);

                    taps[tap] = sinc * window;
                    sum += taps[tap];
                }

                if (Math.Abs(sum) > 1e-12)
                {
                    for (int tap = 0; tap < TapsPerPhase; tap++)
                    {
                        taps[tap] /= sum;
                    }
                }

                Phases[phase] = taps;
            }
        }

        public static double Measure(float[] interleaved, int channels)
        {
            return Measure(interleaved, channels, 0, channels > 0 && interleaved != null ? interleaved.Length / channels : 0);
        }

        internal static double Measure(float[] interleaved, int channels, int first, int count, int channel)
        {
            double peak = 0;
            int frames = interleaved.Length / channels;
            for (int frame = first; frame < first + count; frame++)
            {
                for (int phase = 0; phase < OversampleFactor; phase++)
                {
                    double sum = 0;
                    double[] taps = Phases[phase];
                    int start = frame - TapsPerPhase / 2 + 1;
                    for (int tap = Math.Max(0, -start); tap < Math.Min(taps.Length, frames - start); tap++)
                        sum += taps[tap] * interleaved[(start + tap) * channels + channel];
                    peak = Math.Max(peak, Math.Abs(sum));
                }
            }
            return peak;
        }

        private static double Measure(float[] interleaved, int channels, int first, int count)
        {
            if (interleaved == null || interleaved.Length == 0 || channels <= 0)
            {
                return 0.0;
            }

            double peak = 0;
            for (int channel = 0; channel < channels; channel++)
                peak = Math.Max(peak, Measure(interleaved, channels, first, count, channel));

            return peak;
        }
    }
}
