using System;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "SFX_NewEvent", menuName = "AudioMW/Sound Event", order = 0)]
    public sealed class SoundEvent : ScriptableObject
    {
        [SerializeField] private AudioClip[] clips = new AudioClip[0];
        [SerializeField] private ClipSelectionMode selectionMode = ClipSelectionMode.RandomNoRepeat;
        [SerializeField] private AudioMixerGroup mixerGroup;
        [SerializeField] private bool loop;

        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private float volumeRandomMin;
        [SerializeField] private float volumeRandomMax;

        [SerializeField, Range(0.01f, 3f)] private float pitch = 1f;
        [SerializeField] private float pitchRandomMin;
        [SerializeField] private float pitchRandomMax;

        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
        [SerializeField, Range(0, 256)] private int priority = 128;
        [SerializeField] private ParameterBinding[] parameterBindings = new ParameterBinding[0];

        [NonSerialized] private ClipSelector selector;

        public AudioClip[] Clips
        {
            get { return clips; }
            set { clips = value ?? new AudioClip[0]; ResetSelection(); }
        }

        public ClipSelectionMode SelectionMode
        {
            get { return selectionMode; }
            set { selectionMode = value; ResetSelection(); }
        }

        public AudioMixerGroup MixerGroup
        {
            get { return mixerGroup; }
            set { mixerGroup = value; }
        }

        public bool Loop
        {
            get { return loop; }
            set { loop = value; }
        }

        public float Volume
        {
            get { return volume; }
            set { volume = Mathf.Clamp01(value); }
        }

        public float Pitch
        {
            get { return pitch; }
            set { pitch = Mathf.Clamp(value, 0.01f, 3f); }
        }

        public float SpatialBlend
        {
            get { return spatialBlend; }
            set { spatialBlend = Mathf.Clamp01(value); }
        }

        public float MinDistance
        {
            get { return minDistance; }
            set { minDistance = Mathf.Max(0.01f, value); }
        }

        public float MaxDistance
        {
            get { return maxDistance; }
            set { maxDistance = Mathf.Max(minDistance, value); }
        }

        public AudioRolloffMode RolloffMode
        {
            get { return rolloffMode; }
            set { rolloffMode = value; }
        }

        public int Priority
        {
            get { return priority; }
            set { priority = Mathf.Clamp(value, 0, 256); }
        }

        public ParameterBinding[] ParameterBindings
        {
            get { return parameterBindings; }
            set { parameterBindings = value ?? new ParameterBinding[0]; }
        }

        public bool HasParameterBindings
        {
            get { return parameterBindings != null && parameterBindings.Length > 0; }
        }

        public bool HasClips
        {
            get { return clips != null && clips.Length > 0; }
        }

        public void ResetSelection()
        {
            if (selector != null)
            {
                selector.Reset();
            }
        }

        public PlaybackParameters Resolve(System.Random rng)
        {
            if (!HasClips)
            {
                return PlaybackParameters.Invalid;
            }

            if (selector == null)
            {
                selector = new ClipSelector();
            }

            int index = selector.Next(selectionMode, clips.Length, rng);
            if (index < 0 || clips[index] == null)
            {
                return PlaybackParameters.Invalid;
            }

            float volumeOffset = Lerp(volumeRandomMin, volumeRandomMax, rng);
            float pitchOffset = Lerp(pitchRandomMin, pitchRandomMax, rng);

            return new PlaybackParameters
            {
                Clip = clips[index],
                Volume = Mathf.Clamp01(volume + volumeOffset),
                Pitch = Mathf.Clamp(pitch + pitchOffset, 0.01f, 3f),
                IsValid = true
            };
        }

        private static float Lerp(float min, float max, System.Random rng)
        {
            if (Mathf.Approximately(min, max))
            {
                return min;
            }

            return min + (float)rng.NextDouble() * (max - min);
        }

        public static SoundEvent CreateRuntime(params AudioClip[] sourceClips)
        {
            SoundEvent instance = CreateInstance<SoundEvent>();
            instance.clips = sourceClips ?? new AudioClip[0];
            return instance;
        }
    }
}
