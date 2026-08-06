using UnityEngine;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "PARAM_NewParameter", menuName = "AudioMW/Parameter", order = 10)]
    public sealed class SoundParameter : ScriptableObject
    {
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue = 1f;
        [SerializeField] private float defaultValue;
        [SerializeField] private bool isGlobal = true;

        public float MinValue
        {
            get { return minValue; }
        }

        public float MaxValue
        {
            get { return maxValue; }
        }

        public float DefaultValue
        {
            get { return Clamp(defaultValue); }
        }

        public bool IsGlobal
        {
            get { return isGlobal; }
        }

        public float Clamp(float value)
        {
            return Mathf.Clamp(value, minValue, maxValue);
        }

        public float Normalize(float value)
        {
            if (Mathf.Approximately(maxValue, minValue))
            {
                return 0f;
            }

            return Mathf.InverseLerp(minValue, maxValue, value);
        }

        public static SoundParameter CreateRuntime(float min, float max, float initial)
        {
            SoundParameter parameter = CreateInstance<SoundParameter>();
            parameter.minValue = min;
            parameter.maxValue = max;
            parameter.defaultValue = initial;
            return parameter;
        }
    }
}
