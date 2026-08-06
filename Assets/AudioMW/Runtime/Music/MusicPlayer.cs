using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public sealed class MusicPlayer
    {
        private const double ScheduleLookahead = 1.0;
        private const double StartOffset = 0.05;

        private sealed class Channel
        {
            public MusicLayer Layer;
            public AudioClip Clip;
            public readonly AudioSource[] Sources = new AudioSource[2];
            public int NextIndex;
            public float CurrentWeight = 1f;
        }

        private readonly Transform parent;
        private readonly MusicClock clock = new MusicClock();
        private readonly List<Channel> channels = new List<Channel>();

        private AudioSource introSource;
        private MusicTrack currentTrack;
        private double nextScheduleDspTime;
        private double bodyDuration;
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

        public int ChannelCount
        {
            get { return channels.Count; }
        }

        public MusicPlayer(Transform parent)
        {
            this.parent = parent;
            introSource = CreateSource("Music Intro");
        }

        public void Play(MusicTrack track, MusicQuantization quantization = MusicQuantization.Immediate)
        {
            if (track == null || !track.HasContent)
            {
                return;
            }

            bool clockWasRunning = clock.IsRunning;
            double now = AudioSettings.dspTime;
            double startTime = quantization == MusicQuantization.Immediate || !clockWasRunning
                ? now + StartOffset
                : clock.GetNextBoundary(now, quantization);

            Stop();

            currentTrack = track;
            clock.Configure(track.Tempo, track.BeatsPerBar);
            clock.Start(startTime);

            BuildChannels(track);

            double bodyStart = startTime;

            if (track.IntroClip != null)
            {
                introSource.outputAudioMixerGroup = track.MixerGroup;
                introSource.volume = track.Volume;
                introSource.clip = track.IntroClip;
                introSource.PlayScheduled(startTime);
                bodyStart += MusicTrack.GetExactDuration(track.IntroClip);
            }

            bodyDuration = track.BodyDuration;

            if (bodyDuration > 0.0)
            {
                ScheduleBody(bodyStart);
                nextScheduleDspTime = bodyStart + bodyDuration;
            }
            else
            {
                nextScheduleDspTime = bodyStart;
            }

            playing = true;
            lastBeatIndex = -1;
            lastBarIndex = -1;
        }

        public void Stop()
        {
            if (introSource != null)
            {
                introSource.Stop();
                introSource.clip = null;
            }

            ClearChannels();

            clock.Stop();
            playing = false;
            currentTrack = null;
            nextScheduleDspTime = 0.0;
            bodyDuration = 0.0;
        }

        public void SetVolume(float volume)
        {
            if (currentTrack != null)
            {
                currentTrack.Volume = volume;
            }
        }

        public float GetLayerWeight(int index)
        {
            return index >= 0 && index < channels.Count ? channels[index].CurrentWeight : 0f;
        }

        public string GetLayerName(int index)
        {
            if (index < 0 || index >= channels.Count)
            {
                return null;
            }

            return channels[index].Layer != null ? channels[index].Layer.Name : "base";
        }

        public void Tick()
        {
            if (!playing || currentTrack == null)
            {
                return;
            }

            double now = AudioSettings.dspTime;

            if (bodyDuration > 0.0)
            {
                if (currentTrack.Loop && now + ScheduleLookahead >= nextScheduleDspTime)
                {
                    ScheduleBody(nextScheduleDspTime);
                    nextScheduleDspTime += bodyDuration;
                }
                else if (!currentTrack.Loop && now >= nextScheduleDspTime)
                {
                    Stop();
                    return;
                }
            }

            UpdateWeights();
            DispatchTicks(now);
        }

        public void Destroy()
        {
            ClearChannels();

            if (introSource != null)
            {
                UnityEngine.Object.Destroy(introSource.gameObject);
                introSource = null;
            }

            playing = false;
            currentTrack = null;
        }

        private void BuildChannels(MusicTrack track)
        {
            if (track.LoopClip != null)
            {
                channels.Add(CreateChannel(null, track.LoopClip, track, "Music Base"));
            }

            if (track.HasLayers)
            {
                MusicLayer[] layers = track.Layers;

                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null && layers[i].IsValid)
                    {
                        channels.Add(CreateChannel(layers[i], layers[i].Clip, track, "Music Layer " + i.ToString("D2")));
                    }
                }
            }
        }

        private Channel CreateChannel(MusicLayer layer, AudioClip clip, MusicTrack track, string label)
        {
            Channel channel = new Channel();
            channel.Layer = layer;
            channel.Clip = clip;
            channel.CurrentWeight = layer != null ? ResolveWeight(layer) : 1f;

            for (int i = 0; i < channel.Sources.Length; i++)
            {
                AudioSource source = CreateSource(label + " " + i.ToString("D2"));
                source.outputAudioMixerGroup = track.MixerGroup;
                source.volume = track.Volume * channel.CurrentWeight;
                channel.Sources[i] = source;
            }

            return channel;
        }

        private AudioSource CreateSource(string label)
        {
            GameObject go = new GameObject(label);
            go.transform.SetParent(parent, false);

            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void ClearChannels()
        {
            for (int i = 0; i < channels.Count; i++)
            {
                Channel channel = channels[i];

                for (int s = 0; s < channel.Sources.Length; s++)
                {
                    if (channel.Sources[s] != null)
                    {
                        channel.Sources[s].Stop();
                        UnityEngine.Object.Destroy(channel.Sources[s].gameObject);
                    }
                }
            }

            channels.Clear();
        }

        private void ScheduleBody(double dspTime)
        {
            for (int i = 0; i < channels.Count; i++)
            {
                Channel channel = channels[i];
                AudioSource source = channel.Sources[channel.NextIndex];
                channel.NextIndex = (channel.NextIndex + 1) % channel.Sources.Length;

                source.clip = channel.Clip;
                source.PlayScheduled(dspTime);
            }
        }

        private void UpdateWeights()
        {
            float trackVolume = currentTrack.Volume;
            float delta = Time.unscaledDeltaTime;

            for (int i = 0; i < channels.Count; i++)
            {
                Channel channel = channels[i];
                float target = channel.Layer != null ? ResolveWeight(channel.Layer) : 1f;

                if (channel.Layer != null && channel.Layer.FadeSeconds > 0f)
                {
                    float step = delta / channel.Layer.FadeSeconds;
                    channel.CurrentWeight = Mathf.MoveTowards(channel.CurrentWeight, target, step);
                }
                else
                {
                    channel.CurrentWeight = target;
                }

                float volume = trackVolume * channel.CurrentWeight;

                for (int s = 0; s < channel.Sources.Length; s++)
                {
                    channel.Sources[s].volume = volume;
                }
            }
        }

        private static float ResolveWeight(MusicLayer layer)
        {
            if (layer.Parameter == null)
            {
                return layer.EvaluateWeight(0f);
            }

            float raw = AudioRuntime.Exists
                ? AudioRuntime.Instance.GlobalParameters.Get(layer.Parameter)
                : layer.Parameter.DefaultValue;

            return layer.EvaluateWeight(raw);
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
    }
}
