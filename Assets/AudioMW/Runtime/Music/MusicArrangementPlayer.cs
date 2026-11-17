using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Music Arrangement Player")]
    public sealed class MusicArrangementPlayer : MonoBehaviour
    {
        public MusicArrangement Arrangement;
        private MusicArrangement snapshot;
        private double anchor, initial;
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Stack<AudioSource> pool = new Stack<AudioSource>();
        private readonly Dictionary<string, double> scheduled = new Dictionary<string, double>();
        private readonly List<string> expired = new List<string>();
        private readonly HashSet<string> muted = new HashSet<string>();
        private readonly HashSet<string> solo = new HashSet<string>();
        public ParameterStore Parameters { get; } = new ParameterStore();
        public bool IsPlaying { get; private set; }
        public double Position => snapshot != null ? ArrangementSchedule.Position(snapshot, initial, AudioSettings.dspTime - anchor) : initial;
        public int ScheduledVoiceCount => voices.Count;
        public double StartDspTime => anchor;
        private sealed class Voice { public AudioSource Source; public ArrangementSegment Segment; }
        public bool Play(double position = -1)
        {
            Stop();
            if (!MusicArrangement.Finite(position) || Arrangement == null || !string.IsNullOrEmpty(Arrangement.Validate())) return false;
            snapshot = Instantiate(Arrangement); snapshot.hideFlags = HideFlags.HideAndDontSave;
            initial = position < 0 ? snapshot.Entry : Math.Max(snapshot.Entry, Math.Min(position, snapshot.Loop ? snapshot.LoopEnd : snapshot.Exit));
            if (snapshot.Loop && initial >= snapshot.LoopEnd) initial = snapshot.LoopStart;
            anchor = AudioSettings.dspTime + .1;
            IsPlaying = true; Tick(); return true;
        }
        public bool Seek(double position) { return MusicArrangement.Finite(position) && Play(position); }
        public void SetLaneAudition(string id, bool mute, bool isolate)
        {
            if (mute) muted.Add(id); else muted.Remove(id);
            if (isolate) solo.Add(id); else solo.Remove(id);
        }
        private void Update() { Tick(); }
        public void Tick()
        {
            if (!IsPlaying || snapshot == null) return;
            double now = AudioSettings.dspTime, elapsed = now - anchor;
            for (int i = voices.Count - 1; i >= 0; i--)
            {
                var voice = voices[i];
                if (anchor + voice.Segment.End <= now) { voice.Source.Stop(); voice.Source.clip = null; pool.Push(voice.Source); voices.RemoveAt(i); }
                else
                {
                    var lane = voice.Segment.Lane;
                    float weight = lane.Parameter != null && lane.Weight != null ? Mathf.Clamp01(lane.Weight.Evaluate(lane.Parameter.Normalize(Parameters.Get(lane.Parameter)))) : 1;
                    voice.Source.volume = muted.Contains(lane.Id) || solo.Count > 0 && !solo.Contains(lane.Id) ? 0 : snapshot.Gain * lane.Gain * voice.Segment.Clip.Gain * weight;
                }
            }
            expired.Clear(); foreach (var pair in scheduled) if (pair.Value < elapsed) expired.Add(pair.Key);
            foreach (var key in expired) scheduled.Remove(key);
            if (!snapshot.Loop && elapsed >= snapshot.Exit - initial) { Stop(); return; }
            foreach (var segment in ArrangementSchedule.Window(snapshot, initial, Math.Max(0, elapsed), Math.Max(0, elapsed) + .5))
            {
                if (scheduled.ContainsKey(segment.Key)) continue;
                double start = Math.Max(anchor + segment.Start, now + .02), end = anchor + segment.End;
                if (end <= start) continue;
                var source = GetSource(); source.clip = segment.Clip.Source; source.loop = false;
                source.outputAudioMixerGroup = segment.Lane.Output != null ? segment.Lane.Output : snapshot.Output;
                source.timeSamples = Math.Min(source.clip.samples - 1, (int)Math.Round((segment.SourceOffset + start - anchor - segment.Start) * source.clip.frequency));
                source.volume = 0;
                source.GetComponent<MusicDspGain>().SetEnvelope(new DspGainEnvelope(anchor + segment.ClipOrigin, segment.Clip.FadeIn, MusicFadeCurve.Linear, anchor + segment.ClipOrigin + segment.Clip.Duration - segment.Clip.FadeOut, segment.Clip.FadeOut));
                source.PlayScheduled(start); source.SetScheduledEndTime(end);
                voices.Add(new Voice { Source = source, Segment = segment }); scheduled[segment.Key] = segment.End;
                var lane = segment.Lane;
                float weight = lane.Parameter != null && lane.Weight != null ? Mathf.Clamp01(lane.Weight.Evaluate(lane.Parameter.Normalize(Parameters.Get(lane.Parameter)))) : 1;
                source.volume = muted.Contains(lane.Id) || solo.Count > 0 && !solo.Contains(lane.Id) ? 0 : snapshot.Gain * lane.Gain * segment.Clip.Gain * weight;
            }
        }
        private AudioSource GetSource()
        {
            if (pool.Count > 0) return pool.Pop();
            var go = new GameObject("Arrangement voice"); go.transform.SetParent(transform, false);
            var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; go.AddComponent<MusicDspGain>(); return source;
        }
        public void Stop()
        {
            if(snapshot!=null)initial=Position;
            IsPlaying = false;
            foreach (var voice in voices) { if (voice.Source == null) continue; voice.Source.Stop(); voice.Source.clip = null; pool.Push(voice.Source); }
            voices.Clear(); scheduled.Clear();
            if (snapshot != null) { if (Application.isPlaying) Destroy(snapshot); else DestroyImmediate(snapshot); snapshot = null; }
        }
        private void OnDisable() { Stop(); }
        private void OnDestroy()
        {
            Stop();
            while (pool.Count > 0) { var source = pool.Pop(); if (source != null) { if (Application.isPlaying) Destroy(source.gameObject); else DestroyImmediate(source.gameObject); } }
        }
    }
}
