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
            if (interleaved == null || interleaved.Length == 0 || channels <= 0)
            {
                return 0.0;
            }

            int frames = interleaved.Length / channels;
            double peak = 0.0;

            for (int channel = 0; channel < channels; channel++)
            {
                for (int frame = 0; frame < frames; frame++)
                {
                    for (int phase = 0; phase < OversampleFactor; phase++)
                    {
                        double sum = 0.0;
                        double[] taps = Phases[phase];

                        for (int tap = 0; tap < taps.Length; tap++)
                        {
                            int sourceFrame = frame - TapsPerPhase / 2 + tap + 1;

                            if (sourceFrame < 0 || sourceFrame >= frames)
                            {
                                continue;
                            }

                            sum += taps[tap] * interleaved[sourceFrame * channels + channel];
                        }

                        double magnitude = Math.Abs(sum);

                        if (magnitude > peak)
                        {
                            peak = magnitude;
                        }
                    }
                }
            }

            return peak;
        }
    }
}
