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

        private readonly List<Channel> pendingChannels = new List<Channel>();
        private readonly List<AudioSource> stingerSources = new List<AudioSource>();
        private MusicTrack pendingTrack;
        private double pendingStartDspTime;
        private readonly List<double> markerBeats = new List<double>();
        private int lastMarkerIndex = -1;

        public event Action<int> BeatTick;
        public event Action<int> BarTick;
        public event Action<MusicMarker> MarkerReached;

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

        public bool IsTransitionPending
        {
            get { return pendingTrack != null; }
        }

        public double PendingStartDspTime
        {
            get { return pendingStartDspTime; }
        }

        public MusicTrack PendingTrack
        {
            get { return pendingTrack; }
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

            BuildChannels(track, channels);
            RebuildMarkers(track);

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

            CancelPendingTransition();
            ClearChannels();

            RebuildMarkers(null);
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

        public double LoopBeats
        {
            get
            {
                if (bodyDuration <= 0.0 || !clock.IsRunning)
                {
                    return 0.0;
                }

                return bodyDuration / clock.SecondsPerBeat;
            }
        }

        public double GetNextMarkerTime(double dspTime)
        {
            return clock.GetNextMarkerTime(dspTime, markerBeats, LoopBeats);
        }

        private void RebuildMarkers(MusicTrack track)
        {
            markerBeats.Clear();
            lastMarkerIndex = -1;

            if (track == null || !track.HasMarkers)
            {
                return;
            }

            MusicMarker[] source = track.Markers;

            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] != null)
                {
                    markerBeats.Add(source[i].PositionInBeats(track.BeatsPerBar));
                }
            }
        }

        private void DispatchMarkers(double now)
        {
            if (currentTrack == null || !currentTrack.HasMarkers || MarkerReached == null)
            {
                return;
            }

            double loopBeats = LoopBeats;

            if (loopBeats <= 0.0)
            {
                return;
            }

            double position = clock.GetPosition(now) / clock.SecondsPerBeat;
            double withinLoop = position - Math.Floor(position / loopBeats) * loopBeats;

            MusicMarker[] source = currentTrack.Markers;
            int best = -1;
            double bestBeat = -1.0;

            for (int i = 0; i < source.Length && i < markerBeats.Count; i++)
            {
                double beat = markerBeats[i];

                if (beat <= withinLoop && beat > bestBeat)
                {
                    bestBeat = beat;
                    best = i;
                }
            }

            if (best >= 0 && best != lastMarkerIndex && source[best] != null)
            {
                lastMarkerIndex = best;
                MarkerReached(source[best]);
            }
        }

        public void Tick()
        {
            if (!playing || currentTrack == null)
            {
                return;
            }

            double now = AudioSettings.dspTime;

            if (pendingTrack != null && now >= pendingStartDspTime)
            {
                FinalizeTransition();
            }

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
            DispatchMarkers(now);
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

        public void TransitionTo(MusicTrack next, MusicQuantization quantization)
        {
            if (next == null || !next.HasContent)
            {
                return;
            }

            if (!playing || currentTrack == null)
            {
                Play(next, quantization);
                return;
            }

            CancelPendingTransition();

            double now = AudioSettings.dspTime;
            double boundary;

            if (quantization == MusicQuantization.Immediate)
            {
                boundary = now + StartOffset;
            }
            else if (quantization == MusicQuantization.Marker)
            {
                boundary = GetNextMarkerTime(now);
            }
            else
            {
                boundary = clock.GetNextBoundary(now, quantization);
            }

            for (int i = 0; i < channels.Count; i++)
            {
                Channel channel = channels[i];
                for (int s = 0; s < channel.Sources.Length; s++)
                {
                    if (channel.Sources[s].isPlaying)
                    {
                        channel.Sources[s].SetScheduledEndTime(boundary);
                    }
                }
            }

            if (introSource != null && introSource.isPlaying)
            {
                introSource.SetScheduledEndTime(boundary);
            }

            BuildChannels(next, pendingChannels);

            for (int i = 0; i < pendingChannels.Count; i++)
            {
                Channel channel = pendingChannels[i];
                AudioSource source = channel.Sources[channel.NextIndex];
                channel.NextIndex = (channel.NextIndex + 1) % channel.Sources.Length;
                source.clip = channel.Clip;
                source.PlayScheduled(boundary);
            }

            pendingTrack = next;
            pendingStartDspTime = boundary;
        }

        public void PlayStinger(AudioClip clip, MusicQuantization quantization, float volume = 1f)
        {
            if (clip == null)
            {
                return;
            }

            double now = AudioSettings.dspTime;
            double time;

            if (!clock.IsRunning || quantization == MusicQuantization.Immediate)
            {
                time = now + StartOffset;
            }
            else if (quantization == MusicQuantization.Marker)
            {
                time = GetNextMarkerTime(now);
            }
            else
            {
                time = clock.GetNextBoundary(now, quantization);
            }

            AudioSource source = AcquireStingerSource();
            source.outputAudioMixerGroup = currentTrack != null ? currentTrack.MixerGroup : null;
            source.volume = Mathf.Clamp01(volume);
            source.clip = clip;
            source.PlayScheduled(time);
        }

        private AudioSource AcquireStingerSource()
        {
            for (int i = 0; i < stingerSources.Count; i++)
            {
                if (!stingerSources[i].isPlaying)
                {
                    return stingerSources[i];
                }
            }

            AudioSource created = CreateSource("Music Stinger " + stingerSources.Count.ToString("D2"));
            stingerSources.Add(created);
            return created;
        }

        private void CancelPendingTransition()
        {
            if (pendingTrack == null)
            {
                return;
            }

            DestroyChannels(pendingChannels);
            pendingTrack = null;
            pendingStartDspTime = 0.0;
        }

        private void FinalizeTransition()
        {
            DestroyChannels(channels);

            for (int i = 0; i < pendingChannels.Count; i++)
            {
                channels.Add(pendingChannels[i]);
            }

            pendingChannels.Clear();

            if (introSource != null)
            {
                introSource.Stop();
                introSource.clip = null;
            }

            currentTrack = pendingTrack;
            clock.Configure(currentTrack.Tempo, currentTrack.BeatsPerBar);
            clock.Start(pendingStartDspTime);

            bodyDuration = currentTrack.BodyDuration;
            nextScheduleDspTime = pendingStartDspTime + bodyDuration;
            lastBeatIndex = -1;
            lastBarIndex = -1;

            pendingTrack = null;
            pendingStartDspTime = 0.0;
        }

        private static void DestroyChannels(List<Channel> target)
        {
            for (int i = 0; i < target.Count; i++)
            {
                Channel channel = target[i];

                for (int s = 0; s < channel.Sources.Length; s++)
                {
                    if (channel.Sources[s] != null)
                    {
                        channel.Sources[s].Stop();
                        UnityEngine.Object.Destroy(channel.Sources[s].gameObject);
                    }
                }
            }

            target.Clear();
        }

        private void BuildChannels(MusicTrack track, List<Channel> target)
        {
            if (track.LoopClip != null)
            {
                target.Add(CreateChannel(null, track.LoopClip, track, "Music Base"));
            }

            if (track.HasLayers)
            {
                MusicLayer[] layers = track.Layers;

                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null && layers[i].IsValid)
                    {
                        target.Add(CreateChannel(layers[i], layers[i].Clip, track, "Music Layer " + i.ToString("D2")));
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
            DestroyChannels(channels);
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
