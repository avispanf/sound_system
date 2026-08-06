using System;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class MusicLayer
    {
        [SerializeField] private string layerName = "Layer";
        [SerializeField] private AudioClip clip;
        [SerializeField] private SoundParameter parameter;
        [SerializeField] private AnimationCurve weightCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Range(0f, 1f)] private float defaultWeight = 1f;
        [SerializeField] private float fadeSeconds = 0.25f;

        public string Name
        {
            get { return layerName; }
        }

        public AudioClip Clip
        {
            get { return clip; }
        }

        public SoundParameter Parameter
        {
            get { return parameter; }
        }

        public float FadeSeconds
        {
            get { return Mathf.Max(0f, fadeSeconds); }
        }

        public bool IsValid
        {
            get { return clip != null; }
        }

        public float EvaluateWeight(float rawValue)
        {
            if (parameter == null || weightCurve == null || weightCurve.length == 0)
            {
                return Mathf.Clamp01(defaultWeight);
            }

            float normalized = parameter.Normalize(parameter.Clamp(rawValue));
            return Mathf.Clamp01(weightCurve.Evaluate(normalized));
        }

        public static MusicLayer CreateRuntime(string name, AudioClip clip, SoundParameter parameter, AnimationCurve curve, float fade = 0.25f)
        {
            return new MusicLayer
            {
                layerName = name,
                clip = clip,
                parameter = parameter,
                weightCurve = curve ?? AnimationCurve.Linear(0f, 0f, 1f, 1f),
                defaultWeight = 1f,
                fadeSeconds = fade
            };
        }
    }
}
