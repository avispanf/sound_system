using UnityEngine;

namespace AudioMW
{
    public static class VirtualizationPolicy
    {
        public const float DefaultHysteresis = 0.15f;

        public static bool ShouldVirtualize(bool currentlyVirtual, float distance, float audibleRange, float hysteresis = DefaultHysteresis)
        {
            if (audibleRange <= 0f)
            {
                return false;
            }

            float margin = Mathf.Max(0f, hysteresis) * audibleRange;

            if (currentlyVirtual)
            {
                return distance > audibleRange - margin;
            }

            return distance > audibleRange + margin;
        }

        public static float PlaybackOffset(float elapsedSeconds, float clipLength, bool loop)
        {
            if (clipLength <= 0f)
            {
                return 0f;
            }

            float elapsed = Mathf.Max(0f, elapsedSeconds);

            if (!loop)
            {
                return Mathf.Min(elapsed, clipLength);
            }

            return elapsed - Mathf.Floor(elapsed / clipLength) * clipLength;
        }

        public static bool IsExpired(float elapsedSeconds, float clipLength, bool loop)
        {
            if (loop)
            {
                return false;
            }

            return clipLength > 0f && elapsedSeconds >= clipLength;
        }

        public static float RemainingSeconds(float elapsedSeconds, float clipLength, bool loop)
        {
            if (loop || clipLength <= 0f)
            {
                return float.PositiveInfinity;
            }

            return Mathf.Max(0f, clipLength - Mathf.Max(0f, elapsedSeconds));
        }
    }
}
