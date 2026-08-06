using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class ParameterBinding
    {
        [SerializeField] private SoundParameter parameter;
        [SerializeField] private ParameterTarget target = ParameterTarget.Volume;
        [SerializeField] private AnimationCurve curve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        public SoundParameter Parameter
        {
            get { return parameter; }
        }

        public ParameterTarget Target
        {
            get { return target; }
        }

        public bool IsValid
        {
            get { return parameter != null && curve != null && curve.length > 0; }
        }

        public float Evaluate(float rawValue)
        {
            if (!IsValid)
            {
                return 1f;
            }

            float normalized = parameter.Normalize(parameter.Clamp(rawValue));
            return curve.Evaluate(normalized);
        }

        public static ParameterBinding CreateRuntime(SoundParameter parameter, ParameterTarget target, AnimationCurve curve)
        {
            return new ParameterBinding
            {
                parameter = parameter,
                target = target,
                curve = curve ?? AnimationCurve.Linear(0f, 1f, 1f, 1f)
            };
        }
    }
}
