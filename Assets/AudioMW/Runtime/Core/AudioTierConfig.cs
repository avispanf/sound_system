using UnityEngine;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "TIER_NewTier", menuName = "AudioMW/Tier Config", order = 70)]
    public sealed class AudioTierConfig : ScriptableObject
    {
        [SerializeField] private string tierName = "Standard 3D";
        [SerializeField, Range(1, 512)] private int maxVoices = 32;
        [SerializeField, Range(0, 128)] private int prewarmVoices;
        [SerializeField] private DspBufferPreset dspBuffer = DspBufferPreset.GoodLatency;
        [SerializeField] private int outputSampleRate = 48000;
        [SerializeField, Range(0, 128)] private int realVoiceCount = 32;
        [SerializeField] private bool spatialAudioEnabled = true;
        [SerializeField] private bool occlusionEnabled = true;
        [SerializeField] private bool customDspEnabled = true;
        [SerializeField] private bool applyOnBoot;

        public string TierName
        {
            get { return tierName; }
        }

        public int MaxVoices
        {
            get { return Mathf.Clamp(maxVoices, 1, 512); }
        }

        public int PrewarmVoices
        {
            get { return Mathf.Clamp(prewarmVoices, 0, MaxVoices); }
        }

        public DspBufferPreset DspBuffer
        {
            get { return dspBuffer; }
        }

        public int OutputSampleRate
        {
            get { return outputSampleRate <= 0 ? 48000 : outputSampleRate; }
        }

        public int RealVoiceCount
        {
            get { return Mathf.Clamp(realVoiceCount, 1, MaxVoices); }
        }

        public bool SpatialAudioEnabled
        {
            get { return spatialAudioEnabled; }
        }

        public bool OcclusionEnabled
        {
            get { return occlusionEnabled; }
        }

        public bool CustomDspEnabled
        {
            get { return customDspEnabled; }
        }

        public bool ApplyOnBoot
        {
            get { return applyOnBoot; }
            set { applyOnBoot = value; }
        }

        public int BufferLength
        {
            get { return BufferLengthFor(dspBuffer); }
        }

        public static int BufferLengthFor(DspBufferPreset preset)
        {
            switch (preset)
            {
                case DspBufferPreset.BestLatency:
                    return 256;

                case DspBufferPreset.BestPerformance:
                    return 1024;

                default:
                    return 512;
            }
        }

        public static AudioTierConfig CreateRuntime(string name, int voices, DspBufferPreset buffer)
        {
            AudioTierConfig config = CreateInstance<AudioTierConfig>();
            config.tierName = name;
            config.maxVoices = Mathf.Clamp(voices, 1, 512);
            config.realVoiceCount = config.maxVoices;
            config.dspBuffer = buffer;
            return config;
        }

        public static AudioTierConfig Mobile2D()
        {
            AudioTierConfig config = CreateRuntime("Mobile 2D", 16, DspBufferPreset.BestPerformance);
            config.outputSampleRate = 24000;
            config.realVoiceCount = 12;
            config.spatialAudioEnabled = false;
            config.occlusionEnabled = false;
            config.customDspEnabled = false;
            return config;
        }

        public static AudioTierConfig Standard3D()
        {
            AudioTierConfig config = CreateRuntime("Standard 3D", 32, DspBufferPreset.GoodLatency);
            config.outputSampleRate = 48000;
            config.realVoiceCount = 32;
            return config;
        }

        public static AudioTierConfig HighEnd()
        {
            AudioTierConfig config = CreateRuntime("High-End", 96, DspBufferPreset.BestLatency);
            config.outputSampleRate = 48000;
            config.realVoiceCount = 64;
            config.prewarmVoices = 24;
            return config;
        }
    }

    public static class AudioTierApplier
    {
        public static void Apply(AudioTierConfig config)
        {
            if (config == null)
            {
                return;
            }

            AudioRuntimeSettings.MaxVoices = config.MaxVoices;
            AudioRuntimeSettings.ActiveTier = config;

            ApplyAudioSettings(config);

            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.RebuildPool(config.MaxVoices, config.PrewarmVoices);
            }
        }

        public static bool ApplyAudioSettings(AudioTierConfig config)
        {
            if (config == null)
            {
                return false;
            }

            AudioConfiguration configuration = AudioSettings.GetConfiguration();

            if (configuration.dspBufferSize == config.BufferLength
                && configuration.sampleRate == config.OutputSampleRate
                && configuration.numRealVoices == config.RealVoiceCount
                && configuration.numVirtualVoices >= config.MaxVoices)
            {
                return true;
            }

            configuration.dspBufferSize = config.BufferLength;
            configuration.sampleRate = config.OutputSampleRate;
            configuration.numRealVoices = config.RealVoiceCount;
            configuration.numVirtualVoices = Mathf.Max(configuration.numVirtualVoices, config.MaxVoices);

            return AudioSettings.Reset(configuration);
        }
    }
}
