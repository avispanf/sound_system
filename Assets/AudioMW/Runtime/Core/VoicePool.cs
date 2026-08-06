using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public sealed class VoicePool
    {
        private readonly List<Voice> voices = new List<Voice>();
        private readonly Transform parent;
        private readonly int maxVoices;

        public VoicePool(Transform parent, int maxVoices)
        {
            this.parent = parent;
            this.maxVoices = Mathf.Max(1, maxVoices);
        }

        public int MaxVoices
        {
            get { return maxVoices; }
        }

        public int TotalCount
        {
            get { return voices.Count; }
        }

        public int ActiveCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < voices.Count; i++)
                {
                    if (voices[i].IsActive)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public IReadOnlyList<Voice> Voices
        {
            get { return voices; }
        }

        public Voice Acquire()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                if (!voices[i].IsActive)
                {
                    return voices[i];
                }
            }

            if (voices.Count < maxVoices)
            {
                return CreateVoice();
            }

            return StealOldest();
        }

        public void Tick()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                voices[i].Tick();
            }
        }

        public void StopAll()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                voices[i].Stop();
            }
        }

        public void DestroyAll()
        {
            for (int i = 0; i < voices.Count; i++)
            {
                AudioSource source = voices[i].Source;
                if (source != null)
                {
                    Object.Destroy(source.gameObject);
                }
            }

            voices.Clear();
        }

        private Voice CreateVoice()
        {
            GameObject go = new GameObject("Voice " + voices.Count.ToString("D3"));
            go.transform.SetParent(parent, false);
            AudioSource source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            Voice voice = new Voice(source);
            voices.Add(voice);
            return voice;
        }

        private Voice StealOldest()
        {
            Voice oldest = null;
            float oldestTime = float.MaxValue;

            for (int i = 0; i < voices.Count; i++)
            {
                if (!voices[i].IsActive)
                {
                    continue;
                }

                if (voices[i].StartTime < oldestTime)
                {
                    oldestTime = voices[i].StartTime;
                    oldest = voices[i];
                }
            }

            if (oldest != null)
            {
                oldest.Stop();
            }

            return oldest;
        }
    }
}
