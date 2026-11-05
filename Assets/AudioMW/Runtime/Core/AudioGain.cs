using UnityEngine;

namespace AudioMW
{
    public static class AudioGain
    {
        public static float ToDecibels(float linear)
        {
            return linear > 0f ? 20f * Mathf.Log10(linear) : float.NegativeInfinity;
        }

        public static float FromDecibels(float decibels)
        {
            if (decibels == 0f) return 1f;
            return float.IsNaN(decibels) || float.IsNegativeInfinity(decibels) ? 0f : Mathf.Pow(10f, decibels / 20f);
        }
    }
}
