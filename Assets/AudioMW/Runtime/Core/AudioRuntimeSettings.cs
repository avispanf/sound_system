using UnityEngine;

namespace AudioMW
{
    public static class AudioRuntimeSettings
    {
        public const int FallbackMaxVoices = 32;

        private static int maxVoices = FallbackMaxVoices;

        public static int MaxVoices
        {
            get { return maxVoices; }
            set { maxVoices = Mathf.Clamp(value, 1, 512); }
        }

        public static AudioTierConfig ActiveTier { get; set; }

        public static void Reset()
        {
            maxVoices = FallbackMaxVoices;
            ActiveTier = null;
        }
    }
}
