using System;
using UnityEngine;

namespace AudioMW
{
    public sealed class MusicPlayer
    {
        private const double ScheduleLookahead = 1.0;

        private readonly AudioSource[] sources = new AudioSource[2];
        private readonly MusicClock clock = new MusicClock();

        private MusicTrack currentTrack;
        private int nextSourceIndex;
        private double nextScheduleDspTime;
        private bool playing;
        private int lastBeatIndex = -1;
        private int lastBarIndex = -1;

        public event Action<int> BeatTick;
        public event Action<int> BarTick;

        public MusicClock Clock
        {
            get { return clock; }
        }

        public MusicTrack CurrentTrack
        {
            get { return currentTrack; }
        }

        public bool IsPlaying
        {
            get { return playing; }
        }

        public double NextScheduleDspTime
        {
            get { return nextScheduleDspTime; }
        }

        public MusicPlayer(Transform parent)
        {
            for (int i = 0; i < sources.Length; i++)
            {
                GameObject go = new GameObject("Music Source " + i.ToString("D2"));
                go.transform.SetParent(parent, false);

                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 0f;
                sources[i] = source;
            }
        }

        public void Play(MusicTrack track, MusicQuantization quantization = MusicQuantization.Immediate)
        {
            if (track == null || !track.HasContent)
            {
                return;
            }

            Stop();

            currentTrack = track;
            clock.Configure(track.Tempo, track.BeatsPerBar);

            double now = AudioSettings.dspTime;
            double startTime = quantization == MusicQuantization.Immediate || !clock.IsRunning
                ? now + 0.05
                : clock.GetNextBoundary(now, quantization);

            clock.Start(startTime);

            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].outputAudioMixerGroup = track.MixerGroup;
                sources[i].volume = track.Volume;
            }

            double cursor = startTime;

            if (track.IntroClip != null)
            {
                cursor = ScheduleClip(track.IntroClip, cursor);
            }

            if (track.LoopClip != null)
            {
                cursor = ScheduleClip(track.LoopClip, cursor);
            }

            nextScheduleDspTime = cursor;
            playing = true;
            lastBeatIndex = -1;
            lastBarIndex = -1;
        }

        public void Stop()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].Stop();
                sources[i].clip = null;
            }

            clock.Stop();
            playing = false;
            currentTrack = null;
            nextSourceIndex = 0;
            nextScheduleDspTime = 0.0;
        }

        public void SetVolume(float volume)
        {
            float clamped = Mathf.Clamp01(volume);

            for (int i = 0; i < sources.Length; i++)
            {
                sources[i].volume = clamped;
            }
        }

        public void Tick()
        {
            if (!playing || currentTrack == null)
            {
                return;
            }

            double now = AudioSettings.dspTime;

            if (currentTrack.Loop && currentTrack.LoopClip != null && now + ScheduleLookahead >= nextScheduleDspTime)
            {
                nextScheduleDspTime = ScheduleClip(currentTrack.LoopClip, nextScheduleDspTime);
            }

            if (!currentTrack.Loop && now >= nextScheduleDspTime)
            {
                Stop();
                return;
            }

            DispatchTicks(now);
        }

        private void DispatchTicks(double now)
        {
            if (now < clock.StartDspTime)
            {
                return;
            }

            int beat = clock.GetBeatIndex(now);
            if (beat != lastBeatIndex)
            {
                lastBeatIndex = beat;

                if (BeatTick != null)
                {
                    BeatTick(beat);
                }
            }

            int bar = clock.GetBarIndex(now);
            if (bar != lastBarIndex)
            {
                lastBarIndex = bar;

                if (BarTick != null)
                {
                    BarTick(bar);
                }
            }
        }

        private double ScheduleClip(AudioClip clip, double dspTime)
        {
            AudioSource source = sources[nextSourceIndex];
            nextSourceIndex = (nextSourceIndex + 1) % sources.Length;

            source.clip = clip;
            source.PlayScheduled(dspTime);

            return dspTime + MusicTrack.GetExactDuration(clip);
        }

        public void Destroy()
        {
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null)
                {
                    UnityEngine.Object.Destroy(sources[i].gameObject);
                    sources[i] = null;
                }
            }

            playing = false;
            currentTrack = null;
        }
    }
}
