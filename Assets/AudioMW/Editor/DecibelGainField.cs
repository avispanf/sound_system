using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class DecibelGainField : BaseField<float>
    {
        public const float FaderMinimumDb = -80f;
        private readonly Slider fader;
        private readonly TextField number;

        public DecibelGainField(string label) : this(label, new VisualElement()) { }

        private DecibelGainField(string label, VisualElement input) : base(label + " (dB)", input)
        {
            AddToClassList(alignedFieldUssClassName);
            input.style.flexDirection = FlexDirection.Row;
            fader = new Slider(FaderMinimumDb, 0f) { name = "gain-fader" };
            fader.style.flexGrow = 1;
            fader.style.minWidth = 32;
            number = new TextField { name = "gain-number", isDelayed = true };
            number.style.width = 56;
            number.style.flexShrink = 0;
            input.Add(fader);
            input.Add(number);
            Button reset = new Button(() => value = 1f) { text = "Reset", name = "gain-reset", tooltip = "Reset gain to 0 dB." };
            reset.style.width = 38;
            reset.style.flexShrink = 0;
            input.Add(reset);
            tooltip = "0 dB keeps the original level. Type -inf for silence. The fader spans -80 to 0 dB; lower levels can be typed.";
            fader.tooltip = number.tooltip = tooltip;
            fader.RegisterValueChangedCallback(evt =>
            {
                evt.StopPropagation();
                value = AudioGain.FromDecibels(evt.newValue);
            });
            number.RegisterValueChangedCallback(evt =>
            {
                evt.StopPropagation();
                float linear;
                if (TryParse(evt.newValue, out linear)) value = linear;
                Refresh();
            });
            Refresh();
        }

        public override void SetValueWithoutNotify(float newValue)
        {
            base.SetValueWithoutNotify(newValue);
            Refresh();
        }

        protected override void UpdateMixedValueContent() { Refresh(); }

        private void Refresh()
        {
            if (fader == null || number == null) return;
            float decibels = AudioGain.ToDecibels(value);
            fader.SetValueWithoutNotify(Mathf.Clamp(decibels, FaderMinimumDb, 0f));
            fader.showMixedValue = showMixedValue;
            number.SetValueWithoutNotify(showMixedValue ? "—" : Format(value));
        }

        public static string Format(float linear)
        {
            float decibels = AudioGain.ToDecibels(linear);
            return float.IsNegativeInfinity(decibels) ? "−∞" : decibels.ToString("0.##", CultureInfo.CurrentCulture);
        }

        public static bool TryParse(string text, out float linear)
        {
            linear = 0f;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string normalized = text.Trim().ToLowerInvariant();
            if (normalized == "-inf" || normalized == "-infinity" || normalized == "−∞" || normalized == "-∞" || normalized == "mute") return true;
            float decibels;
            if ((!float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out decibels) &&
                !float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decibels)) || float.IsNaN(decibels)) return false;
            linear = AudioGain.FromDecibels(Mathf.Min(0f, decibels));
            return true;
        }
    }
}
