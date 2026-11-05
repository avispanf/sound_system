using System;

namespace AudioMW
{
    public sealed class DspGainEnvelope
    {
        public readonly double InStart;
        public readonly double InSeconds;
        public readonly MusicFadeCurve InCurve;
        public readonly double OutStart;
        public readonly double OutSeconds;
        public readonly MusicFadeCurve OutCurve;

        public DspGainEnvelope(double inStart = 0, double inSeconds = 0, MusicFadeCurve inCurve = MusicFadeCurve.Linear,
            double outStart = double.PositiveInfinity, double outSeconds = 0, MusicFadeCurve outCurve = MusicFadeCurve.Linear)
        {
            InStart = inStart;
            InSeconds = Math.Max(0, inSeconds);
            InCurve = inCurve;
            OutStart = outStart;
            OutSeconds = Math.Max(0, outSeconds);
            OutCurve = outCurve;
        }

        public DspGainEnvelope WithFadeOut(double start, double seconds, MusicFadeCurve curve)
        {
            return new DspGainEnvelope(InStart, InSeconds, InCurve, start, seconds, curve);
        }

        public DspGainEnvelope WithoutFadeOut()
        {
            return new DspGainEnvelope(InStart, InSeconds, InCurve);
        }

        public float Evaluate(double time)
        {
            double incoming = InSeconds > 0 ? Clamp((time - InStart) / InSeconds) : time >= InStart ? 1 : 0;
            double outgoing = OutSeconds > 0 ? Clamp((time - OutStart) / OutSeconds) : time >= OutStart ? 1 : 0;
            if (InCurve == MusicFadeCurve.EqualPower) incoming = Math.Sin(incoming * Math.PI * 0.5);
            outgoing = OutCurve == MusicFadeCurve.EqualPower ? Math.Cos(outgoing * Math.PI * 0.5) : 1 - outgoing;
            return (float)(incoming * outgoing);
        }

        public void Process(float[] data, int channels, double blockStart, int sampleRate)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (channels <= 0 || sampleRate <= 0 || data.Length % channels != 0) throw new ArgumentOutOfRangeException();
            double step = 1.0 / sampleRate;
            for (int frame = 0, index = 0; index < data.Length; frame++)
            {
                float gain = Evaluate(blockStart + frame * step);
                for (int channel = 0; channel < channels; channel++, index++) data[index] *= gain;
            }
        }

        private static double Clamp(double value) { return Math.Max(0, Math.Min(1, value)); }
    }
}
