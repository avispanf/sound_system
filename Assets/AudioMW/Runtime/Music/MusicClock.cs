using System;

namespace AudioMW
{
    public sealed class MusicClock
    {
        private const double Epsilon = 1e-6;

        private double startDspTime;
        private double tempo = 120.0;
        private int beatsPerBar = 4;
        private bool running;

        public double Tempo
        {
            get { return tempo; }
        }

        public int BeatsPerBar
        {
            get { return beatsPerBar; }
        }

        public bool IsRunning
        {
            get { return running; }
        }

        public double StartDspTime
        {
            get { return startDspTime; }
        }

        public double SecondsPerBeat
        {
            get { return 60.0 / tempo; }
        }

        public double SecondsPerBar
        {
            get { return SecondsPerBeat * beatsPerBar; }
        }

        public void Configure(double bpm, int signatureBeatsPerBar)
        {
            tempo = Math.Max(1.0, bpm);
            beatsPerBar = Math.Max(1, signatureBeatsPerBar);
        }

        public void Start(double dspTime)
        {
            startDspTime = dspTime;
            running = true;
        }

        public void Stop()
        {
            running = false;
        }

        public double GetPosition(double dspTime)
        {
            return dspTime - startDspTime;
        }

        public double GetBeat(double dspTime)
        {
            return GetPosition(dspTime) / SecondsPerBeat;
        }

        public int GetBeatIndex(double dspTime)
        {
            return (int)Math.Floor(GetBeat(dspTime) + Epsilon);
        }

        public int GetBarIndex(double dspTime)
        {
            return (int)Math.Floor((GetBeat(dspTime) + Epsilon) / beatsPerBar);
        }

        public int GetBeatInBar(double dspTime)
        {
            int beat = GetBeatIndex(dspTime);
            int inBar = beat % beatsPerBar;
            return inBar < 0 ? inBar + beatsPerBar : inBar;
        }

        public double GetNextBoundary(double dspTime, MusicQuantization quantization)
        {
            if (quantization == MusicQuantization.Immediate)
            {
                return dspTime;
            }

            double interval = quantization == MusicQuantization.Bar ? SecondsPerBar : SecondsPerBeat;
            double position = GetPosition(dspTime);
            double steps = position / interval;
            double nextStep = Math.Floor(steps + Epsilon) + 1.0;

            return startDspTime + nextStep * interval;
        }

        public double GetNextMarkerTime(double dspTime, System.Collections.Generic.IReadOnlyList<double> markerBeats, double loopBeats)
        {
            if (markerBeats == null || markerBeats.Count == 0 || loopBeats <= 0.0)
            {
                return GetNextBoundary(dspTime, MusicQuantization.Bar);
            }

            double position = GetPosition(dspTime) / SecondsPerBeat;
            double cycle = Math.Floor(position / loopBeats);
            double best = double.MaxValue;

            for (int pass = 0; pass < 2; pass++)
            {
                double offset = (cycle + pass) * loopBeats;

                for (int i = 0; i < markerBeats.Count; i++)
                {
                    double beat = markerBeats[i];

                    if (beat < 0.0 || beat >= loopBeats)
                    {
                        continue;
                    }

                    double candidate = offset + beat;

                    if (candidate > position + Epsilon && candidate < best)
                    {
                        best = candidate;
                    }
                }

                if (best < double.MaxValue)
                {
                    break;
                }
            }

            if (best >= double.MaxValue)
            {
                return GetNextBoundary(dspTime, MusicQuantization.Bar);
            }

            return startDspTime + best * SecondsPerBeat;
        }

        public double GetBoundaryAtOrAfter(double dspTime, MusicQuantization quantization)
        {
            if (quantization == MusicQuantization.Immediate)
            {
                return dspTime;
            }

            double interval = quantization == MusicQuantization.Bar ? SecondsPerBar : SecondsPerBeat;
            double position = GetPosition(dspTime);
            double steps = position / interval;
            double rounded = Math.Round(steps);

            if (Math.Abs(steps - rounded) < Epsilon)
            {
                return startDspTime + rounded * interval;
            }

            return startDspTime + (Math.Floor(steps) + 1.0) * interval;
        }
    }
}
