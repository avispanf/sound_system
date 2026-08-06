using System.Collections.Generic;

namespace AudioMW
{
    public sealed class ParameterStore
    {
        private readonly Dictionary<SoundParameter, float> values = new Dictionary<SoundParameter, float>();

        public int Count
        {
            get { return values.Count; }
        }

        public bool Has(SoundParameter parameter)
        {
            return parameter != null && values.ContainsKey(parameter);
        }

        public float Get(SoundParameter parameter)
        {
            if (parameter == null)
            {
                return 0f;
            }

            float value;
            return values.TryGetValue(parameter, out value) ? value : parameter.DefaultValue;
        }

        public bool TryGet(SoundParameter parameter, out float value)
        {
            value = 0f;
            return parameter != null && values.TryGetValue(parameter, out value);
        }

        public void Set(SoundParameter parameter, float value)
        {
            if (parameter == null)
            {
                return;
            }

            values[parameter] = parameter.Clamp(value);
        }

        public void Remove(SoundParameter parameter)
        {
            if (parameter != null)
            {
                values.Remove(parameter);
            }
        }

        public void Clear()
        {
            values.Clear();
        }
    }
}
