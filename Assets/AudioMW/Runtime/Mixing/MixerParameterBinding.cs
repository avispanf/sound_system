using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class MixerParameterBinding
    {
        [SerializeField] private SoundParameter parameter;
        [SerializeField] private string exposedName;
        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField] private float outputMin = -80f;
        [SerializeField] private float outputMax;

        public SoundParameter Parameter
        {
            get { return parameter; }
        }

        public string ExposedName
        {
            get { return exposedName; }
        }

        public float OutputMin
        {
            get { return outputMin; }
        }

        public float OutputMax
        {
            get { return outputMax; }
        }

        public bool IsValid
        {
            get
            {
                return parameter != null
                    && !string.IsNullOrEmpty(exposedName)
                    && curve != null
                    && curve.length > 0;
            }
        }

        public float Evaluate(float rawValue)
        {
            if (!IsValid)
            {
                return outputMax;
            }

            float normalized = parameter.Normalize(parameter.Clamp(rawValue));
            float shaped = Mathf.Clamp01(curve.Evaluate(normalized));
            return Mathf.Lerp(outputMin, outputMax, shaped);
        }

        public static MixerParameterBinding CreateRuntime(SoundParameter parameter, string exposedName, AnimationCurve curve, float min, float max)
        {
            return new MixerParameterBinding
            {
                parameter = parameter,
                exposedName = exposedName,
                curve = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f),
                outputMin = min,
                outputMax = max
            };
        }
    }
}
