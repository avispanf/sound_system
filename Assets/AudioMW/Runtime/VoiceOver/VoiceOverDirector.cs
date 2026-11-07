using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public sealed class VoiceOverDirector
    {
        private readonly AudioSource source;
        private sealed class QueuedLine
        {
            public VoiceLine Line;
            public AudioClip Clip;
            public AudioClipLifetime Lifetime;
            public QueuedLine(VoiceLine line)
            {
                Line = line;
                Clip = line.Clip;
                Lifetime = AudioClipLifetime.RetainPlayback(Clip);
            }
            public void Release()
            {
                AudioClipLifetime lifetime = Lifetime;
                Lifetime = null;
                if (lifetime != null) lifetime.ReleasePlayback();
            }
        }
        private readonly List<QueuedLine> queue = new List<QueuedLine>();
        private QueuedLine current;

        private VoiceLine currentLine;
        private float silenceTimer;
        private float duckValue;
        private float lastTrailingSilence;
        private bool justStarted;
        private bool resetting;

        private SoundParameter duckParameter;
        private float duckAttackSeconds = 0.2f;
        private float duckReleaseSeconds = 0.2f;

        public event Action<VoiceLine> LineStarted;
        public event Action<VoiceLine> LineFinished;
        public event Action<string> SubtitleChanged;

        public VoiceOverDirector(Transform parent)
        {
            GameObject go = new GameObject("Voice Over");
            go.transform.SetParent(parent, false);

            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
        }

        public bool IsSpeaking
        {
            get { return current != null; }
        }

        public VoiceLine CurrentLine
        {
            get { return currentLine; }
        }

        public int QueueLength
        {
            get { return queue.Count; }
        }

        public float DuckValue
        {
            get { return duckValue; }
        }

        public AudioSource Source
        {
            get { return source; }
        }

        public SoundParameter DuckParameter
        {
            get { return duckParameter; }
            set { duckParameter = value; }
        }

        public float DuckFadeSeconds
        {
            get { return duckReleaseSeconds; }
            set { duckAttackSeconds = duckReleaseSeconds = Mathf.Max(0f, value); }
        }

        public float DuckAttackSeconds { get => duckAttackSeconds; set => duckAttackSeconds = Mathf.Max(0, value); }
        public float DuckReleaseSeconds { get => duckReleaseSeconds; set => duckReleaseSeconds = Mathf.Max(0, value); }

        public bool Play(VoiceLine line, VoiceOverMode mode = VoiceOverMode.Queue)
        {
            if (resetting || source == null || line == null || !line.IsValid)
            {
                return false;
            }

            if (current != null && currentLine == null) FinishCurrent(true);
            if (current == null)
            {
                StartLine(new QueuedLine(line));
                return true;
            }

            switch (mode)
            {
                case VoiceOverMode.IgnoreIfBusy:
                    return false;

                case VoiceOverMode.Interrupt:
                    if (line.Priority < currentLine.Priority)
                    {
                        return false;
                    }

                    QueuedLine incoming = new QueuedLine(line);
                    FinishCurrent(true);
                    StartLine(incoming);
                    return true;

                default:
                    queue.Add(new QueuedLine(line));
                    return true;
            }
        }

        public void Skip()
        {
            if (current != null)
            {
                FinishCurrent(true);
            }
        }

        public void Stop()
        {
            ClearQueue();

            if (current != null)
            {
                FinishCurrent(true);
            }

            silenceTimer = 0f;
        }

        internal void Reset()
        {
            resetting = true;
            try
            {
                Stop();
                duckValue = 0;
                lastTrailingSilence = 0;
                justStarted = false;
                if (duckParameter != null && AudioRuntime.Exists) AudioRuntime.Instance.GlobalParameters.Set(duckParameter, 0);
            }
            finally { resetting = false; }
        }

        public void Tick()
        {
            if (AudioListener.pause && !source.ignoreListenerPause) return;
            float delta = Time.unscaledDeltaTime;

            if (current != null && (currentLine == null || (!justStarted && !source.isPlaying)))
            {
                FinishCurrent(currentLine == null);
                silenceTimer = lastTrailingSilence;
            }

            justStarted = false;

            if (currentLine == null && queue.Count > 0)
            {
                silenceTimer -= delta;

                if (silenceTimer <= 0f)
                {
                    QueuedLine next = queue[0];
                    queue.RemoveAt(0);
                    if (next.Line != null && next.Clip != null) StartLine(next);
                    else next.Release();
                }
            }

            UpdateDuck(delta);
        }

        public void Destroy()
        {
            ClearQueue();
            currentLine = null;
            if (source != null)
            {
                source.Stop();
                source.clip = null;
                UnityEngine.Object.Destroy(source.gameObject);
            }
            QueuedLine previous = current;
            current = null;
            if (previous != null) previous.Release();
        }

        private void ClearQueue()
        {
            QueuedLine[] owned = queue.ToArray();
            queue.Clear();
            foreach (QueuedLine line in owned) line.Release();
        }

        private void StartLine(QueuedLine queued)
        {
            VoiceLine line = queued.Line;
            current = queued;
            currentLine = line;

            source.clip = queued.Clip;
            source.volume = line.Volume;
            source.outputAudioMixerGroup = line.MixerGroup;
            source.Play();

            lastTrailingSilence = line.TrailingSilence;
            justStarted = true;

            if (LineStarted != null)
            {
                LineStarted(line);
            }

            if (SubtitleChanged != null)
            {
                SubtitleChanged(line.Subtitle);
            }
        }

        private void FinishCurrent(bool stopSource)
        {
            VoiceLine finished = currentLine;
            currentLine = null;

            if (stopSource)
            {
                source.Stop();
            }

            source.clip = null;
            QueuedLine previous = current;
            current = null;
            if (previous != null) previous.Release();

            if (LineFinished != null && finished != null)
            {
                LineFinished(finished);
            }

            if (SubtitleChanged != null)
            {
                SubtitleChanged(string.Empty);
            }
        }

        private void UpdateDuck(float delta)
        {
            float target = currentLine != null ? 1f : 0f;

            float seconds = target > duckValue ? duckAttackSeconds : duckReleaseSeconds;
            if (seconds > 0f)
            {
                duckValue = Mathf.MoveTowards(duckValue, target, delta / seconds);
            }
            else
            {
                duckValue = target;
            }

            if (duckParameter != null && AudioRuntime.Exists)
            {
                AudioRuntime.Instance.GlobalParameters.Set(duckParameter, duckValue);
            }
        }
    }
}
