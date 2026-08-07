using System;

namespace AudioMW.Editor
{
    public static class DemoSynth
    {
        public const int SampleRate = 48000;
        public const double Bpm = 120.0;
        public const int BeatsPerBar = 4;
        public const int Bars = 4;

        public static double SecondsPerBeat
        {
            get { return 60.0 / Bpm; }
        }

        public static double LoopSeconds
        {
            get { return SecondsPerBeat * BeatsPerBar * Bars; }
        }

        public static int LoopFrames
        {
            get { return (int)Math.Round(LoopSeconds * SampleRate); }
        }

        public static float[] BuildBass()
        {
            float[] buffer = new float[LoopFrames];
            double[] roots = { 55.00, 55.00, 73.42, 61.74 };

            for (int bar = 0; bar < Bars; bar++)
            {
                double frequency = roots[bar % roots.Length];

                for (int beat = 0; beat < BeatsPerBar; beat++)
                {
                    double start = (bar * BeatsPerBar + beat) * SecondsPerBeat;
                    AddNote(buffer, frequency, start, SecondsPerBeat * 0.85, 0.42f, 0.004, 0.18);
                }
            }

            return buffer;
        }

        public static float[] BuildPad()
        {
            float[] buffer = new float[LoopFrames];
            double[][] chords =
            {
                new[] { 220.00, 261.63, 329.63 },
                new[] { 220.00, 261.63, 329.63 },
                new[] { 293.66, 349.23, 440.00 },
                new[] { 246.94, 293.66, 369.99 }
            };

            for (int bar = 0; bar < Bars; bar++)
            {
                double[] chord = chords[bar % chords.Length];
                double start = bar * BeatsPerBar * SecondsPerBeat;
                double length = BeatsPerBar * SecondsPerBeat;

                for (int note = 0; note < chord.Length; note++)
                {
                    AddNote(buffer, chord[note], start, length, 0.16f, 0.35, 0.5);
                }
            }

            return buffer;
        }

        public static float[] BuildArp()
        {
            float[] buffer = new float[LoopFrames];
            double[][] chords =
            {
                new[] { 440.00, 523.25, 659.25, 523.25 },
                new[] { 440.00, 523.25, 659.25, 523.25 },
                new[] { 587.33, 698.46, 880.00, 698.46 },
                new[] { 493.88, 587.33, 739.99, 587.33 }
            };

            double step = SecondsPerBeat * 0.5;
            int stepsPerBar = BeatsPerBar * 2;

            for (int bar = 0; bar < Bars; bar++)
            {
                double[] chord = chords[bar % chords.Length];

                for (int i = 0; i < stepsPerBar; i++)
                {
                    double start = (bar * stepsPerBar + i) * step;
                    AddNote(buffer, chord[i % chord.Length], start, step * 0.8, 0.2f, 0.003, 0.09);
                }
            }

            return buffer;
        }

        public static float[] BuildPulse()
        {
            float[] buffer = new float[LoopFrames];

            for (int bar = 0; bar < Bars; bar++)
            {
                for (int beat = 0; beat < BeatsPerBar; beat++)
                {
                    double start = (bar * BeatsPerBar + beat) * SecondsPerBeat;
                    AddNoise(buffer, start, 0.06, beat == 0 ? 0.35f : 0.16f);
                }
            }

            return buffer;
        }

        public static float[] BuildStinger()
        {
            int frames = (int)(SampleRate * 1.6);
            float[] buffer = new float[frames];
            double[] notes = { 523.25, 659.25, 783.99, 1046.50 };

            for (int i = 0; i < notes.Length; i++)
            {
                AddNote(buffer, notes[i], i * 0.08, 1.2, 0.22f, 0.005, 0.9);
            }

            return buffer;
        }

        public static float[] BuildBlip(double frequency, double seconds)
        {
            int frames = (int)(SampleRate * seconds);
            float[] buffer = new float[frames];
            AddNote(buffer, frequency, 0.0, seconds * 0.9, 0.35f, 0.002, seconds * 0.5);
            return buffer;
        }

        public static float[] BuildImpact()
        {
            int frames = (int)(SampleRate * 0.9);
            float[] buffer = new float[frames];

            AddNote(buffer, 70.0, 0.0, 0.5, 0.5f, 0.002, 0.35);
            AddNoise(buffer, 0.0, 0.25, 0.3f);

            return buffer;
        }

        public static float[] BuildVoiceLine(double baseFrequency, double seconds)
        {
            int frames = (int)(SampleRate * seconds);
            float[] buffer = new float[frames];
            Random random = new Random(unchecked((int)(baseFrequency * 100)));

            double cursor = 0.0;

            while (cursor < seconds - 0.12)
            {
                double length = 0.09 + random.NextDouble() * 0.07;
                double frequency = baseFrequency * (0.85 + random.NextDouble() * 0.4);

                AddNote(buffer, frequency, cursor, length, 0.3f, 0.008, 0.05);
                AddNote(buffer, frequency * 2.0, cursor, length, 0.09f, 0.008, 0.05);

                cursor += length + 0.02 + random.NextDouble() * 0.04;
            }

            return buffer;
        }

        private static void AddNote(float[] buffer, double frequency, double startSeconds, double lengthSeconds, float amplitude, double attackSeconds, double releaseSeconds)
        {
            int start = (int)(startSeconds * SampleRate);
            int length = (int)(lengthSeconds * SampleRate);
            int attack = Math.Max(1, (int)(attackSeconds * SampleRate));
            int release = Math.Max(1, (int)(releaseSeconds * SampleRate));

            for (int i = 0; i < length; i++)
            {
                int index = start + i;

                if (index < 0 || index >= buffer.Length)
                {
                    continue;
                }

                double phase = 2.0 * Math.PI * frequency * i / SampleRate;
                double value = Math.Sin(phase) + 0.28 * Math.Sin(phase * 2.0) + 0.12 * Math.Sin(phase * 3.0);

                double envelope = 1.0;

                if (i < attack)
                {
                    envelope = (double)i / attack;
                }

                int fromEnd = length - i;

                if (fromEnd < release)
                {
                    envelope *= (double)fromEnd / release;
                }

                buffer[index] += (float)(value * envelope) * amplitude * 0.6f;
            }
        }

        private static void AddNoise(float[] buffer, double startSeconds, double lengthSeconds, float amplitude)
        {
            int start = (int)(startSeconds * SampleRate);
            int length = (int)(lengthSeconds * SampleRate);
            Random random = new Random(start + length);

            for (int i = 0; i < length; i++)
            {
                int index = start + i;

                if (index < 0 || index >= buffer.Length)
                {
                    continue;
                }

                double envelope = 1.0 - (double)i / length;
                double value = random.NextDouble() * 2.0 - 1.0;

                buffer[index] += (float)(value * envelope * envelope) * amplitude;
            }
        }

        public static float[] ToStereo(float[] mono)
        {
            float[] stereo = new float[mono.Length * 2];

            for (int i = 0; i < mono.Length; i++)
            {
                stereo[i * 2] = mono[i];
                stereo[i * 2 + 1] = mono[i];
            }

            return stereo;
        }
    }
}
