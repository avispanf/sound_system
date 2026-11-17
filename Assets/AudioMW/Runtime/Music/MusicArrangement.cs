using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [Serializable]
    public sealed class ArrangementClip
    {
        public string Id = Guid.NewGuid().ToString("N");
        public AudioClip Source;
        public double Start;
        public double Trim;
        public double Duration = 1;
        public bool Repeat;
        public double FadeIn;
        public double FadeOut;
        [DecibelGain] public float Gain = 1;
    }

    [Serializable]
    public sealed class ArrangementLane
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Track";
        [DecibelGain] public float Gain = 1;
        public AudioMixerGroup Output;
        public SoundParameter Parameter;
        public AnimationCurve Weight = AnimationCurve.Linear(0, 0, 1, 1);
        public List<ArrangementClip> Clips = new List<ArrangementClip>();
    }

    [Serializable]
    public sealed class ArrangementMarker
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Cue";
        public double Seconds;
    }

    [CreateAssetMenu(fileName = "MUS_Arrangement", menuName = "AudioMW/Music Arrangement")]
    public sealed class MusicArrangement : ScriptableObject
    {
        public int Version = 1;
        public double Tempo = 120;
        public int BeatsPerBar = 4;
        public double Entry;
        public double Exit = 8;
        public bool Loop;
        public double LoopStart;
        public double LoopEnd = 8;
        [DecibelGain] public float Gain = 1;
        public AudioMixerGroup Output;
        public List<ArrangementLane> Lanes = new List<ArrangementLane>();
        public List<ArrangementMarker> Markers = new List<ArrangementMarker>();
        public double BeatSeconds => 60 / Tempo;
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public string Validate()
        {
            if (Version != 1) return "Unsupported arrangement version.";
            if (!Finite(Tempo) || Tempo <= 0 || BeatsPerBar < 1) return "Tempo and meter must be positive.";
            if (!Finite(Entry) || !Finite(Exit) || Entry < 0 || Exit <= Entry) return "Entry and exit must define a positive interval.";
            if (!Finite(Gain) || Gain < 0 || Gain > 1) return "Arrangement gain must be between silence and 0 dB.";
            if (Loop && (!Finite(LoopStart) || !Finite(LoopEnd) || LoopStart < Entry || LoopEnd > Exit || LoopEnd <= LoopStart)) return "Loop must be a positive interval inside entry / exit.";
            if (Lanes == null || Markers == null) return "Missing lane or cue collection.";
            var ids = new HashSet<string>();
            foreach (var lane in Lanes)
            {
                if (lane == null || string.IsNullOrEmpty(lane.Id) || !ids.Add(lane.Id) || lane.Clips == null) return "Invalid or duplicate lane identity.";
                if (!Finite(lane.Gain) || lane.Gain < 0 || lane.Gain > 1) return "Lane gain must be between silence and 0 dB.";
                foreach (var clip in lane.Clips)
                {
                    if (clip == null || string.IsNullOrEmpty(clip.Id) || !ids.Add(clip.Id)) return "Invalid or duplicate clip identity.";
                    if (clip.Source == null) return "A timeline clip has no source.";
                    double length = MusicTrack.GetExactDuration(clip.Source);
                    if (!Finite(clip.Start) || !Finite(clip.Trim) || !Finite(clip.Duration) || clip.Start < 0 || clip.Trim < 0 || clip.Trim >= length || clip.Duration <= 0 || !Finite(clip.Start + clip.Duration)) return "Invalid clip start, trim or duration.";
                    if (!clip.Repeat && clip.Trim + clip.Duration > length + 0.00001) return "Clip extends past its source; trim it or enable Repeat.";
                    if (!Finite(clip.FadeIn) || !Finite(clip.FadeOut) || clip.FadeIn < 0 || clip.FadeOut < 0 || clip.FadeIn + clip.FadeOut > clip.Duration) return "Fades must fit inside the clip.";
                    if (!Finite(clip.Gain) || clip.Gain < 0 || clip.Gain > 1) return "Clip gain must be between silence and 0 dB.";
                }
            }
            foreach (var marker in Markers)
                if (marker == null || string.IsNullOrEmpty(marker.Id) || !ids.Add(marker.Id) || !Finite(marker.Seconds) || marker.Seconds < Entry || marker.Seconds > Exit) return "Invalid cue identity or position.";
            return "";
        }
    }

    public struct ArrangementSegment
    {
        public string Key;
        public ArrangementLane Lane;
        public ArrangementClip Clip;
        public double Start, End, SourceOffset, ClipOrigin;
    }

    public static class ArrangementSchedule
    {
        public static double Position(MusicArrangement score, double initial, double elapsed)
        {
            double value = initial + Math.Max(0, elapsed);
            return score.Loop && value >= score.LoopEnd ? score.LoopStart + (value - score.LoopEnd) % (score.LoopEnd - score.LoopStart) : Math.Min(value, score.Exit);
        }

        public static List<ArrangementSegment> Window(MusicArrangement score, double initial, double from, double until)
        {
            var result = new List<ArrangementSegment>();
            if(score==null||!MusicArrangement.Finite(initial)||!MusicArrangement.Finite(from)||!MusicArrangement.Finite(until)||until<=from||score.Loop&&(!MusicArrangement.Finite(score.LoopEnd-score.LoopStart)||score.LoopEnd<=score.LoopStart))return result;
            double firstEnd = score.Loop ? score.LoopEnd : score.Exit;
            AddCycle(score, initial, firstEnd, 0, 0, from, until, result);
            if (!score.Loop) return result;
            double firstDuration = firstEnd - initial;
            double period = score.LoopEnd - score.LoopStart;
            long first = Math.Max(0, (long)Math.Floor((from - firstDuration) / period));
            long last = Math.Max(-1, (long)Math.Floor((until - firstDuration) / period));
            for (long cycle = first; cycle <= last; cycle++)
                AddCycle(score, score.LoopStart, score.LoopEnd, firstDuration + cycle * period, cycle + 1, from, until, result);
            return result;
        }

        private static void AddCycle(MusicArrangement score, double begin, double end, double origin, long cycle, double from, double until, List<ArrangementSegment> result)
        {
            foreach (var lane in score.Lanes) foreach (var clip in lane.Clips)
            {
                double lo = Math.Max(begin, clip.Start), hi = Math.Min(end, clip.Start + clip.Duration);
                if (hi <= lo || origin + hi - begin <= from || origin + lo - begin >= until) continue;
                double span = MusicTrack.GetExactDuration(clip.Source) - clip.Trim;
                if(!MusicArrangement.Finite(span)||span<=0)continue;
                long first = clip.Repeat ? Math.Max(0, (long)Math.Floor((Math.Max(lo, begin + from - origin) - clip.Start) / span)) : 0;
                for (long repetition = first; ; repetition++)
                {
                    double pieceStart = clip.Start + repetition * span;
                    double a = Math.Max(lo, pieceStart), b = Math.Min(hi, pieceStart + span);
                    double globalStart = origin + a - begin, globalEnd = origin + b - begin;
                    if (a >= hi || globalStart >= until) break;
                    if (b > a && globalEnd > from)
                        result.Add(new ArrangementSegment { Key = cycle + ":" + clip.Id + ":" + repetition, Lane = lane, Clip = clip, Start = globalStart, End = globalEnd, SourceOffset = clip.Trim + a - pieceStart, ClipOrigin = origin + clip.Start - begin });
                    if (!clip.Repeat) break;
                }
            }
        }
    }
}
