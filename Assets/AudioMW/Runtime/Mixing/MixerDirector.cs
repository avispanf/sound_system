using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    public sealed class MixerDirector
    {
        private readonly List<MixerRoutingProfile> profiles = new List<MixerRoutingProfile>();
        private readonly Dictionary<UnityEngine.Audio.AudioMixer, Dictionary<string, float>> lastWritten = new Dictionary<UnityEngine.Audio.AudioMixer, Dictionary<string, float>>();

        private int writesThisFrame;
        private int totalWrites;

        public int ProfileCount
        {
            get { return profiles.Count; }
        }

        public int WritesLastTick
        {
            get { return writesThisFrame; }
        }

        public int TotalWrites
        {
            get { return totalWrites; }
        }

        public void AddProfile(MixerRoutingProfile profile)
        {
            if (profile != null && !profiles.Contains(profile))
            {
                profiles.Add(profile);
            }
        }

        public void RemoveProfile(MixerRoutingProfile profile)
        {
            profiles.Remove(profile);
            if (profile != null && profile.Mixer != null) lastWritten.Remove(profile.Mixer);
        }

        internal bool ConflictsWith(MixerRoutingProfile requested)
        {
            foreach (MixerRoutingProfile current in profiles)
                if (current != null && current.Mixer == requested.Mixer)
                    foreach (MixerParameterBinding existing in current.Bindings)
                        foreach (MixerParameterBinding binding in requested.Bindings)
                            if (existing != null && existing.IsValid && binding != null && binding.IsValid && existing.ExposedName == binding.ExposedName) return true;
            return false;
        }

        public void Clear()
        {
            profiles.Clear();
            lastWritten.Clear();
            writesThisFrame = 0;
            totalWrites = 0;
        }

        public void Tick()
        {
            writesThisFrame = 0;

            for (int i = 0; i < profiles.Count; i++)
            {
                Apply(profiles[i]);
            }
        }

        public void Apply(MixerRoutingProfile profile)
        {
            if (profile == null || profile.Mixer == null)
            {
                return;
            }

            Dictionary<string, float> values;
            if (!lastWritten.TryGetValue(profile.Mixer, out values))
            {
                values = new Dictionary<string, float>(); lastWritten.Add(profile.Mixer, values);
            }
            MixerParameterBinding[] bindings = profile.Bindings;

            for (int i = 0; i < bindings.Length; i++)
            {
                MixerParameterBinding binding = bindings[i];

                if (binding == null || !binding.IsValid)
                {
                    continue;
                }

                float raw = AudioRuntime.Exists
                    ? AudioRuntime.Instance.GlobalParameters.Get(binding.Parameter)
                    : binding.Parameter.DefaultValue;

                float target = binding.Evaluate(raw);
                float previous;

                if (values.TryGetValue(binding.ExposedName, out previous) && Mathf.Approximately(previous, target))
                {
                    continue;
                }

                if (profile.Mixer.SetFloat(binding.ExposedName, target))
                {
                    values[binding.ExposedName] = target;
                    writesThisFrame++;
                    totalWrites++;
                }
            }
        }

        public void TransitionTo(AudioMixerSnapshot snapshot, float seconds)
        {
            if (snapshot != null)
            {
                snapshot.TransitionTo(Mathf.Max(0f, seconds));
            }
        }

        public void BlendSnapshots(AudioMixer mixer, AudioMixerSnapshot[] snapshots, float[] weights, float seconds)
        {
            if (mixer == null || snapshots == null || weights == null)
            {
                return;
            }

            if (snapshots.Length == 0 || snapshots.Length != weights.Length)
            {
                return;
            }

            mixer.TransitionToSnapshots(snapshots, weights, Mathf.Max(0f, seconds));
        }
    }
}
