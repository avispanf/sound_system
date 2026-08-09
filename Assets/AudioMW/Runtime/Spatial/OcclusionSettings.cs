using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class OcclusionSettings
    {
        [SerializeField] private LayerMask blockingLayers = ~0;
        [SerializeField, Range(1, 9)] private int sampleCount = 3;
        [SerializeField] private float sampleSpread = 0.6f;
        [SerializeField, Range(0f, 1f)] private float maxVolumeAttenuation = 0.7f;
        [SerializeField] private float openCutoff = 22000f;
        [SerializeField] private float occludedCutoff = 900f;
        [SerializeField] private float smoothingSeconds = 0.25f;
        [SerializeField] private float sampleInterval = 0.1f;

        public LayerMask BlockingLayers
        {
            get { return blockingLayers; }
        }

        public int SampleCount
        {
            get { return Mathf.Clamp(sampleCount, 1, 9); }
        }

        public float SampleSpread
        {
            get { return Mathf.Max(0f, sampleSpread); }
        }

        public float MaxVolumeAttenuation
        {
            get { return Mathf.Clamp01(maxVolumeAttenuation); }
        }

        public float OpenCutoff
        {
            get { return Mathf.Clamp(openCutoff, 10f, 22000f); }
        }

        public float OccludedCutoff
        {
            get { return Mathf.Clamp(occludedCutoff, 10f, OpenCutoff); }
        }

        public float SmoothingSeconds
        {
            get { return Mathf.Max(0f, smoothingSeconds); }
        }

        public float SampleInterval
        {
            get { return Mathf.Max(0f, sampleInterval); }
        }

        public float VolumeMultiplierFor(float occlusion)
        {
            return Mathf.Lerp(1f, 1f - MaxVolumeAttenuation, Mathf.Clamp01(occlusion));
        }

        public float CutoffFor(float occlusion)
        {
            float open = Mathf.Log(OpenCutoff);
            float closed = Mathf.Log(OccludedCutoff);
            return Mathf.Exp(Mathf.Lerp(open, closed, Mathf.Clamp01(occlusion)));
        }

        public static OcclusionSettings CreateRuntime(int samples, float attenuation, float openHz, float occludedHz)
        {
            return new OcclusionSettings
            {
                sampleCount = Mathf.Clamp(samples, 1, 9),
                maxVolumeAttenuation = Mathf.Clamp01(attenuation),
                openCutoff = openHz,
                occludedCutoff = occludedHz
            };
        }
    }
}
