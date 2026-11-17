using UnityEngine;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "ATT_NewPreset", menuName = "AudioMW/Attenuation Preset", order = 50)]
    public sealed class AttenuationPreset : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;
        [SerializeField] private float minDistance = 1f;
        [SerializeField] private float maxDistance = 25f;
        [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;
        [SerializeField] private AnimationCurve customRolloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
        [SerializeField, Range(0f, 360f)] private float spread;
        [SerializeField, Range(0f, 5f)] private float dopplerLevel = 1f;

        [SerializeField, Range(-1f, 1f)] private float stereoPan;
        [SerializeField] private bool distanceFiltering;
        [SerializeField] private AnimationCurve cutoffByDistance = AnimationCurve.Linear(0f, 22000f, 1f, 1800f);
        [SerializeField] private bool distanceSpread;
        [SerializeField] private AnimationCurve spreadByDistance = AnimationCurve.Linear(0f, 180f, 1f, 0f);

        public float StereoPan { get { return stereoPan; } set { stereoPan = Mathf.Clamp(value, -1f, 1f); } }
        public bool DistanceFiltering { get { return distanceFiltering; } set { distanceFiltering = value; } }
        public bool DistanceSpread { get { return distanceSpread; } set { distanceSpread = value; } }
        public AnimationCurve CutoffByDistance { get { return cutoffByDistance; } set { cutoffByDistance = value; } }
        public AnimationCurve SpreadByDistance { get { return spreadByDistance; } set { spreadByDistance = value; } }
        public bool UsesDistanceProcessing { get { return spatialBlend > 0f && (distanceFiltering || distanceSpread); } }
        public float EvaluateCutoff(float distance) { return distanceFiltering ? EvaluateDistanceCurve(cutoffByDistance, distance, 22000f, 10f, 22000f) : 22000f; }
        public float EvaluateSpread(float distance) { return distanceSpread ? EvaluateDistanceCurve(spreadByDistance, distance, spread, 0f, 360f) : spread; }
        private float EvaluateDistanceCurve(AnimationCurve curve, float distance, float fallback, float low, float high)
        {
            if (curve == null || curve.length == 0) return fallback;
            float value = curve.Evaluate(Mathf.InverseLerp(MinDistance, MaxDistance, Mathf.Max(0f, distance)));
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, low, high);
        }

        public float SpatialBlend
        {
            get { return spatialBlend; }
        }

        public float MinDistance
        {
            get { return Mathf.Max(0.01f, minDistance); }
        }

        public float MaxDistance
        {
            get { return Mathf.Max(MinDistance, maxDistance); }
        }

        public AudioRolloffMode RolloffMode
        {
            get { return rolloffMode; }
        }

        public AnimationCurve CustomRolloff
        {
            get { return customRolloff; }
        }

        public float Spread
        {
            get { return spread; }
        }

        public float DopplerLevel
        {
            get { return dopplerLevel; }
        }

        public bool UsesCustomCurve
        {
            get { return rolloffMode == AudioRolloffMode.Custom && customRolloff != null && customRolloff.length > 0; }
        }

        public float EvaluateAttenuation(float distance)
        {
            float min = MinDistance;
            float max = MaxDistance;

            if (distance <= min)
            {
                return 1f;
            }

            if (distance >= max)
            {
                return rolloffMode == AudioRolloffMode.Logarithmic ? Evaluate(1f) : 0f;
            }

            float normalized = Mathf.InverseLerp(min, max, distance);
            return Evaluate(normalized);
        }

        private float Evaluate(float normalized)
        {
            switch (rolloffMode)
            {
                case AudioRolloffMode.Linear:
                    return Mathf.Clamp01(1f - normalized);

                case AudioRolloffMode.Custom:
                    return UsesCustomCurve ? Mathf.Clamp01(customRolloff.Evaluate(normalized)) : Mathf.Clamp01(1f - normalized);

                default:
                    float min = MinDistance;
                    float distance = Mathf.Lerp(min, MaxDistance, normalized);
                    return Mathf.Clamp01(min / Mathf.Max(min, distance));
            }
        }

        public void ApplyTo(AudioSource source)
        {
            if (source == null)
            {
                return;
            }

            source.spatialBlend = spatialBlend;
            source.minDistance = MinDistance;
            source.maxDistance = MaxDistance;
            source.spread = spread;
            source.panStereo = stereoPan;
            source.dopplerLevel = dopplerLevel;
            source.rolloffMode = rolloffMode;

            if (UsesCustomCurve)
            {
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, customRolloff);
            }
        }

        public static AttenuationPreset CreateRuntime(float blend, float min, float max, AudioRolloffMode mode, AnimationCurve curve = null)
        {
            AttenuationPreset preset = CreateInstance<AttenuationPreset>();
            preset.spatialBlend = Mathf.Clamp01(blend);
            preset.minDistance = min;
            preset.maxDistance = max;
            preset.rolloffMode = mode;

            if (curve != null)
            {
                preset.customRolloff = curve;
            }

            return preset;
        }
    }
}
