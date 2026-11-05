using System;
using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "SFX_NewEvent", menuName = "AudioMW/Sound Event", order = 0)]
    public sealed class SoundEvent : ScriptableObject
    {
        [SerializeField] private ClipReference[] clipReferences = new ClipReference[0];
        public ClipReference[] ClipReferences { get { return clipReferences; } set { clipReferences = value ?? new ClipReference[0]; ResetSelection(); } }
        public int TotalClipCount { get { return clips.Length + clipReferences.Length; } }
        public bool HasUnresolvedReferences
        {
            get { foreach (ClipReference reference in clipReferences) if (reference != null && reference.IsAddressed && (!reference.IsResolved || reference.Clip.loadState != AudioDataLoadState.Loaded)) return true; return false; }
        }
        public AudioClip GetClip(int index)
        {
            if (index < 0 || index >= TotalClipCount) return null;
            if (index < clips.Length) return clips[index];
            ClipReference reference = clipReferences[index - clips.Length];
            return reference != null ? reference.Clip : null;
        }

        [SerializeField] private AudioClip[] clips = new AudioClip[0];
        [SerializeField] private ClipSelectionMode selectionMode = ClipSelectionMode.RandomNoRepeat;
        [SerializeField] private AudioMixerGroup mixerGroup;
        [SerializeField] private bool loop;
        [SerializeField] private bool ignorePause;
        public bool IgnorePause { get { return ignorePause; } set { ignorePause = value; } }

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
        [SerializeField] private AttenuationPreset attenuationPreset;
        [SerializeField] private bool allowVirtualization;
        [SerializeField] private ParameterBinding[] parameterBindings = new ParameterBinding[0];
        [SerializeField] private ContainerMode containerMode = ContainerMode.Simple;
        [SerializeField] private SoundParameter blendParameter;
        [SerializeField] private BlendLayer[] blendLayers = new BlendLayer[0];

        [SerializeField] private float[] clipWeights = new float[0];
        [SerializeField] private SoundEventChild[] children = new SoundEventChild[0];
        [SerializeField] private SoundParameter switchParameter;
        [SerializeField, Min(0)] private int maxInstances;
        [SerializeField, Min(0)] private int maxInstancesPerEmitter;
        [SerializeField, Min(0f)] private float cooldownSeconds;
        [SerializeField] private VoiceLimitPolicy limitPolicy = VoiceLimitPolicy.StealOldest;
        [SerializeField] private ConcurrencyGroup concurrencyGroup;

        [NonSerialized] private ClipSelector selector;

        public float[] ClipWeights { get { return clipWeights; } set { clipWeights = value ?? new float[0]; ResetSelection(); } }
        public SoundEventChild[] Children { get { return children; } set { children = value ?? new SoundEventChild[0]; } }
        public SoundParameter SwitchParameter { get { return switchParameter; } set { switchParameter = value; } }
        public int MaxInstances { get { return Mathf.Max(0, maxInstances); } set { maxInstances = Mathf.Max(0, value); } }
        public int MaxInstancesPerEmitter { get { return Mathf.Max(0, maxInstancesPerEmitter); } set { maxInstancesPerEmitter = Mathf.Max(0, value); } }
        public float CooldownSeconds { get { return Mathf.Max(0f, cooldownSeconds); } set { cooldownSeconds = Mathf.Max(0f, value); } }
        public VoiceLimitPolicy LimitPolicy { get { return limitPolicy; } set { limitPolicy = value; } }
        public ConcurrencyGroup ConcurrencyGroup { get { return concurrencyGroup; } set { concurrencyGroup = value; } }


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

        public ContainerMode ContainerMode
        {
            get { return containerMode; }
            set { containerMode = value; }
        }

        public SoundParameter BlendParameter
        {
            get { return blendParameter; }
            set { blendParameter = value; }
        }

        public BlendLayer[] BlendLayers
        {
            get { return blendLayers; }
            set { blendLayers = value ?? new BlendLayer[0]; }
        }

        public bool IsBlendContainer
        {
            get { return containerMode == ContainerMode.Blend && blendLayers != null && blendLayers.Length > 0; }
        }

        public bool HasParameterBindings
        {
            get { return parameterBindings != null && parameterBindings.Length > 0; }
        }

        public bool AllowVirtualization
        {
            get { return allowVirtualization; }
            set { allowVirtualization = value; }
        }

        public AttenuationPreset AttenuationPreset
        {
            get { return attenuationPreset; }
            set { attenuationPreset = value; }
        }

        public bool HasClips
        {
            get { return TotalClipCount > 0; }
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
            if (!HasClips || HasUnresolvedReferences)
            {
                return PlaybackParameters.Invalid;
            }

            if (selector == null)
            {
                selector = new ClipSelector();
            }

            int index = selector.Next(selectionMode, TotalClipCount, rng, clipWeights);
            if (index < 0 || GetClip(index) == null)
            {
                return PlaybackParameters.Invalid;
            }

            float volumeOffset = Lerp(volumeRandomMin, volumeRandomMax, rng);
            float pitchOffset = Lerp(pitchRandomMin, pitchRandomMax, rng);

            return new PlaybackParameters
            {
                Clip = GetClip(index),
                Volume = Mathf.Clamp01(volume + volumeOffset),
                Pitch = Mathf.Clamp(pitch + pitchOffset, 0.01f, 3f),
                IsValid = true
            };
        }

        internal float ResolveVolume(System.Random rng) { return Mathf.Clamp01(volume + Lerp(volumeRandomMin, volumeRandomMax, rng)); }
        internal float ResolvePitch(System.Random rng) { return Mathf.Clamp(pitch + Lerp(pitchRandomMin, pitchRandomMax, rng), 0.01f, 3f); }

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
