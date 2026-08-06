using System;

namespace AudioMW
{
    public struct Biquad
    {
        public double B0;
        public double B1;
        public double B2;
        public double A1;
        public double A2;

        private double x1;
        private double x2;
        private double y1;
        private double y2;

        public void Reset()
        {
            x1 = 0.0;
            x2 = 0.0;
            y1 = 0.0;
            y2 = 0.0;
        }

        public double Process(double input)
        {
            double output = B0 * input + B1 * x1 + B2 * x2 - A1 * y1 - A2 * y2;

            x2 = x1;
            x1 = input;
            y2 = y1;
            y1 = output;

            return output;
        }

        public static Biquad HighShelf(double sampleRate, double frequency, double gainDb, double q)
        {
            double a = Math.Pow(10.0, gainDb / 40.0);
            double w0 = 2.0 * Math.PI * frequency / sampleRate;
            double cos = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);
            double sqrtA = Math.Sqrt(a);

            double b0 = a * ((a + 1.0) + (a - 1.0) * cos + 2.0 * sqrtA * alpha);
            double b1 = -2.0 * a * ((a - 1.0) + (a + 1.0) * cos);
            double b2 = a * ((a + 1.0) + (a - 1.0) * cos - 2.0 * sqrtA * alpha);
            double a0 = (a + 1.0) - (a - 1.0) * cos + 2.0 * sqrtA * alpha;
            double a1 = 2.0 * ((a - 1.0) - (a + 1.0) * cos);
            double a2 = (a + 1.0) - (a - 1.0) * cos - 2.0 * sqrtA * alpha;

            return Normalize(b0, b1, b2, a0, a1, a2);
        }

        public static Biquad HighPass(double sampleRate, double frequency, double q)
        {
            double w0 = 2.0 * Math.PI * frequency / sampleRate;
            double cos = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);

            double b0 = (1.0 + cos) / 2.0;
            double b1 = -(1.0 + cos);
            double b2 = (1.0 + cos) / 2.0;
            double a0 = 1.0 + alpha;
            double a1 = -2.0 * cos;
            double a2 = 1.0 - alpha;

            return Normalize(b0, b1, b2, a0, a1, a2);
        }

        private static Biquad Normalize(double b0, double b1, double b2, double a0, double a1, double a2)
        {
            Biquad filter = new Biquad();
            filter.B0 = b0 / a0;
            filter.B1 = b1 / a0;
            filter.B2 = b2 / a0;
            filter.A1 = a1 / a0;
            filter.A2 = a2 / a0;
            return filter;
        }
    }
}
