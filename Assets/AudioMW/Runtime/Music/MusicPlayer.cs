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
            public readonly double[] Ends = new double[2];
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

        private sealed class Retiring
        {
            public readonly List<Channel> Channels = new List<Channel>();
            public MusicTrack Track;
            public AudioSource Intro;
            public double Next;
            public double Duration;
            public double End;
        }

        private readonly List<Retiring> retiring = new List<Retiring>();
        private double pendingFadeSeconds;
        private AudioSource pendingIntro;
        private double pendingNextSchedule;
        private float volume = 1f;
        private readonly Dictionary<AudioSource, double> stingerEnds = new Dictionary<AudioSource, double>();
        public float Volume { get { return volume; } }
        public int RetiringTrackCount { get { return retiring.Count; } }
        public double TransitionEndDspTime { get; private set; }
        public bool IsCrossfading { get { return retiring.Count > 0; } }
        public bool SupportsDspFades { get { return Application.platform != RuntimePlatform.WebGLPlayer; } }

        public float TransitionGain
        {
            get { return channels.Count > 0 ? channels[0].Sources[0].GetComponent<MusicDspGain>().Envelope.Evaluate(AudioSettings.dspTime) : 0f; }
        }

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
            introSource.GetComponent<MusicDspGain>().SetEnvelope(new DspGainEnvelope());
            clock.Configure(track.Tempo, track.BeatsPerBar);
            clock.Start(startTime);

            BuildChannels(track, channels);
            RebuildMarkers(track);

            double bodyStart = startTime;

            if (track.IntroClip != null)
            {
                introSource.outputAudioMixerGroup = track.MixerGroup;
                introSource.volume = track.Volume * volume;
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

            DiscardPending();
            foreach (Retiring item in retiring) { DestroyChannels(item.Channels); DestroySource(item.Intro); }
            retiring.Clear();
            foreach (AudioSource stinger in stingerSources) { stinger.Stop(); stinger.clip = null; }
            stingerEnds.Clear();
            TransitionEndDspTime = 0;
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
            this.volume = Mathf.Clamp01(volume);
            UpdateWeights();
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

            if (pendingTrack != null && pendingTrack.BodyDuration > 0)
                ScheduleAhead(pendingChannels, pendingTrack, ref pendingNextSchedule, pendingTrack.BodyDuration, now, double.PositiveInfinity);
            ScheduleAhead(channels, currentTrack, ref nextScheduleDspTime, bodyDuration, now,
                pendingTrack != null ? pendingStartDspTime + pendingFadeSeconds : double.PositiveInfinity);
            for (int i = retiring.Count - 1; i >= 0; i--)
            {
                Retiring item = retiring[i];
                if (now >= item.End)
                {
                    DestroyChannels(item.Channels);
                    DestroySource(item.Intro);
                    retiring.RemoveAt(i);
                }
                else ScheduleAhead(item.Channels, item.Track, ref item.Next, item.Duration, now, item.End);
            }
            double naturalEnd = bodyDuration > 0 ? nextScheduleDspTime : clock.StartDspTime + currentTrack.IntroDuration;
            if ((bodyDuration <= 0 || !currentTrack.Loop) && pendingTrack == null && retiring.Count == 0 && now >= naturalEnd)
            {
                Stop();
                return;
            }

            UpdateWeights();
            DispatchTicks(now);
            DispatchMarkers(now);
        }

        public void Destroy()
        {
            Stop();
            DestroySource(introSource);
            introSource = null;
            foreach (AudioSource stinger in stingerSources) DestroySource(stinger);
            stingerSources.Clear();
        }

        public void TransitionTo(MusicTrack next, MusicQuantization quantization, float fadeSeconds = 0f, MusicFadeCurve curve = MusicFadeCurve.Linear)
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

            double now = AudioSettings.dspTime;
            if (pendingTrack != null && now >= pendingStartDspTime) FinalizeTransition();
            if (next == pendingTrack) return;
            CancelTransition();
            if (next == currentTrack) return;
            double duration = SupportsDspFades && !float.IsNaN(fadeSeconds) && !float.IsInfinity(fadeSeconds) ? Mathf.Clamp(fadeSeconds, 0f, 30f) : 0;

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

            pendingTrack = next;
            pendingStartDspTime = boundary;
            pendingFadeSeconds = duration;

            SetFadeOut(channels, introSource, boundary, duration, curve, false);
            BuildChannels(next, pendingChannels);
            foreach (Channel channel in pendingChannels)
                foreach (AudioSource source in channel.Sources)
                    source.GetComponent<MusicDspGain>().SetEnvelope(new DspGainEnvelope(boundary, duration, curve));
            ScheduleChannels(pendingChannels, boundary, 0, now);
            pendingNextSchedule = boundary + next.BodyDuration;
            if (next.BodyDuration <= 0 && next.IntroClip != null)
            {
                pendingIntro = CreateSource("Music Pending Intro");
                pendingIntro.clip = next.IntroClip;
                pendingIntro.outputAudioMixerGroup = next.MixerGroup;
                pendingIntro.volume = next.Volume * volume;
                pendingIntro.GetComponent<MusicDspGain>().SetEnvelope(new DspGainEnvelope(boundary, duration, curve));
                pendingIntro.PlayScheduled(boundary);
            }
        }

        public bool CancelTransition()
        {
            if (pendingTrack == null) return false;
            if (AudioSettings.dspTime >= pendingStartDspTime) { FinalizeTransition(); return false; }
            DiscardPending();
            SetFadeOut(channels, introSource, 0, 0, MusicFadeCurve.Linear, true);
            return true;
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
            stingerEnds[source] = time + MusicTrack.GetExactDuration(clip);
        }

        private AudioSource AcquireStingerSource()
        {
            for (int i = 0; i < stingerSources.Count; i++)
            {
                double end;
                if (!stingerEnds.TryGetValue(stingerSources[i], out end) || AudioSettings.dspTime >= end)
                {
                    return stingerSources[i];
                }
            }

            AudioSource created = CreateSource("Music Stinger " + stingerSources.Count.ToString("D2"));
            stingerSources.Add(created);
            return created;
        }

        private void DiscardPending()
        {
            if (pendingTrack == null)
            {
                return;
            }

            DestroyChannels(pendingChannels);
            DestroySource(pendingIntro);
            pendingIntro = null;
            pendingTrack = null;
            pendingStartDspTime = 0.0;
        }

        private void FinalizeTransition()
        {
            if (pendingFadeSeconds > 0)
            {
                Retiring item = new Retiring { Track = currentTrack, Intro = introSource, Next = nextScheduleDspTime, Duration = bodyDuration, End = pendingStartDspTime + pendingFadeSeconds };
                item.Channels.AddRange(channels);
                channels.Clear();
                retiring.Add(item);
                introSource = CreateSource("Music Intro");
            }
            else
            {
                DestroyChannels(channels);
                introSource.Stop();
                introSource.clip = null;
            }
            if (pendingIntro != null)
            {
                DestroySource(introSource);
                introSource = pendingIntro;
                pendingIntro = null;
            }
            channels.AddRange(pendingChannels);
            pendingChannels.Clear();
            TransitionEndDspTime = pendingStartDspTime + pendingFadeSeconds;

            currentTrack = pendingTrack;
            RebuildMarkers(currentTrack);
            clock.Configure(currentTrack.Tempo, currentTrack.BeatsPerBar);
            clock.Start(pendingStartDspTime);

            bodyDuration = currentTrack.BodyDuration;
            nextScheduleDspTime = pendingNextSchedule;
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
                source.volume = track.Volume * channel.CurrentWeight * volume;
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
            source.gameObject.AddComponent<MusicDspGain>();
            return source;
        }

        private void ClearChannels()
        {
            DestroyChannels(channels);
        }

        private void ScheduleBody(double dspTime)
        {
            ScheduleChannels(channels, dspTime, 0, AudioSettings.dspTime);
        }

        private static void DestroySource(AudioSource source)
        {
            if (source == null) return;
            source.Stop();
            UnityEngine.Object.Destroy(source.gameObject);
        }

        private static void SetFadeOut(List<Channel> group, AudioSource intro, double start, double seconds, MusicFadeCurve curve, bool cancel)
        {
            foreach (Channel channel in group)
                foreach (AudioSource source in channel.Sources) SetFadeOut(source, start, seconds, curve, cancel);
            if (intro != null) SetFadeOut(intro, start, seconds, curve, cancel);
        }

        private static void SetFadeOut(AudioSource source, double start, double seconds, MusicFadeCurve curve, bool cancel)
        {
            MusicDspGain gain = source.GetComponent<MusicDspGain>();
            gain.SetEnvelope(cancel ? gain.Envelope.WithoutFadeOut() : gain.Envelope.WithFadeOut(start, seconds, curve));
        }

        private static bool ScheduleChannels(List<Channel> group, double start, double offset, double now)
        {
            foreach (Channel channel in group) if (channel.Ends[channel.NextIndex] > now) return false;
            foreach (Channel channel in group)
            {
                int index = channel.NextIndex;
                channel.NextIndex = (index + 1) % 2;
                AudioSource source = channel.Sources[index];
                double length = MusicTrack.GetExactDuration(channel.Clip);
                if (offset >= length) { channel.Ends[index] = start; continue; }
                source.clip = channel.Clip;
                source.timeSamples = Math.Min(channel.Clip.samples - 1, (int)(offset * channel.Clip.frequency));
                source.PlayScheduled(start);
                channel.Ends[index] = start + length - offset;
            }
            return true;
        }

        private static void ScheduleAhead(List<Channel> group, MusicTrack track, ref double next, double duration, double now, double end)
        {
            if (track == null || !track.Loop || duration <= 0 || group.Count == 0) return;
            if (next < now)
            {
                double offset = (now + StartOffset - next) % duration;
                if (ScheduleChannels(group, now + StartOffset, offset, now)) next = now + StartOffset + duration - offset;
            }
            while (next < end && next <= now + ScheduleLookahead)
            {
                if (!ScheduleChannels(group, next, 0, now)) break;
                next += duration;
            }
        }

        private void UpdateWeights()
        {
            if (currentTrack == null) return;
            UpdateGroupWeights(channels, currentTrack);
            if (pendingTrack != null) UpdateGroupWeights(pendingChannels, pendingTrack);
            foreach (Retiring item in retiring) UpdateGroupWeights(item.Channels, item.Track);
            if (introSource != null) introSource.volume = currentTrack.Volume * volume;
            foreach (Retiring item in retiring) if (item.Intro != null) item.Intro.volume = item.Track.Volume * volume;
        }

        private void UpdateGroupWeights(List<Channel> group, MusicTrack track)
        {
            float trackVolume = track.Volume * volume;
            float delta = Time.unscaledDeltaTime;

            for (int i = 0; i < group.Count; i++)
            {
                Channel channel = group[i];
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
