using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class BlendLayer
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private AnimationCurve weightCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        public AudioClip Clip
        {
            get { return clip; }
        }

        public bool IsValid
        {
            get { return clip != null && weightCurve != null && weightCurve.length > 0; }
        }

        public float EvaluateWeight(float normalizedValue)
        {
            if (weightCurve == null || weightCurve.length == 0)
            {
                return 1f;
            }

            return Mathf.Clamp01(weightCurve.Evaluate(Mathf.Clamp01(normalizedValue)));
        }

        public static BlendLayer CreateRuntime(AudioClip clip, AnimationCurve weightCurve)
        {
            return new BlendLayer
            {
                clip = clip,
                weightCurve = weightCurve ?? AnimationCurve.Linear(0f, 1f, 1f, 1f)
            };
        }
    }
}
