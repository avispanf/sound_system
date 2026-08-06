using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public sealed class VoiceOverDirector
    {
        private readonly AudioSource source;
        private readonly List<VoiceLine> queue = new List<VoiceLine>();

        private VoiceLine currentLine;
        private float silenceTimer;
        private float duckValue;
        private float lastTrailingSilence;
        private bool justStarted;

        private SoundParameter duckParameter;
        private float duckFadeSeconds = 0.2f;

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
            get { return currentLine != null; }
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
            get { return duckFadeSeconds; }
            set { duckFadeSeconds = Mathf.Max(0f, value); }
        }

        public bool Play(VoiceLine line, VoiceOverMode mode = VoiceOverMode.Queue)
        {
            if (line == null || !line.IsValid)
            {
                return false;
            }

            if (currentLine == null)
            {
                StartLine(line);
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

                    FinishCurrent(true);
                    StartLine(line);
                    return true;

                default:
                    queue.Add(line);
                    return true;
            }
        }

        public void Skip()
        {
            if (currentLine != null)
            {
                FinishCurrent(true);
            }
        }

        public void Stop()
        {
            queue.Clear();

            if (currentLine != null)
            {
                FinishCurrent(true);
            }

            silenceTimer = 0f;
        }

        public void Tick()
        {
            float delta = Time.unscaledDeltaTime;

            if (currentLine != null && !justStarted && !source.isPlaying)
            {
                FinishCurrent(false);
                silenceTimer = lastTrailingSilence;
            }

            justStarted = false;

            if (currentLine == null && queue.Count > 0)
            {
                silenceTimer -= delta;

                if (silenceTimer <= 0f)
                {
                    VoiceLine next = queue[0];
                    queue.RemoveAt(0);
                    StartLine(next);
                }
            }

            UpdateDuck(delta);
        }

        public void Destroy()
        {
            queue.Clear();
            currentLine = null;

            if (source != null)
            {
                UnityEngine.Object.Destroy(source.gameObject);
            }
        }

        private void StartLine(VoiceLine line)
        {
            currentLine = line;

            source.clip = line.Clip;
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

            if (duckFadeSeconds > 0f)
            {
                duckValue = Mathf.MoveTowards(duckValue, target, delta / duckFadeSeconds);
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
