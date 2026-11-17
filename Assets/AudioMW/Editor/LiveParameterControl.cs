using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class LiveParameterLease
    {
        private readonly ParameterStore store;
        private readonly SoundParameter parameter;
        private readonly bool hadValue;
        private readonly float original;
        private float last;
        private bool written;
        public LiveParameterLease(ParameterStore store, SoundParameter parameter)
        {
            this.store = store; this.parameter = parameter;
            hadValue = store.TryGet(parameter, out original);
        }
        public void Set(float value) { store.Set(parameter, value); last = store.Get(parameter); written = true; }
        public bool Restore()
        {
            if (!written) return true;
            written = false;
            if (!store.TryGet(parameter, out float current) || current != last) return false;
            if (hadValue) store.Set(parameter, original); else store.Remove(parameter);
            return true;
        }
    }

    public sealed class LiveParameterControl : VisualElement
    {
        private LiveParameterLease lease;
        public LiveParameterControl(SoundParameter parameter)
        {
            name = "live-parameter-control";
            AddToClassList("audiomw-runtime-card");
            var enable = new Toggle("Live global control") { name = "live-parameter-enable" };
            var slider = new Slider("Value", parameter.MinValue, parameter.MaxValue) { showInputField = true, name = "live-parameter-value" };
            var status = new Label { name = "live-parameter-status" };
            Add(enable); Add(slider); Add(status);
            slider.SetEnabled(false);
            enable.RegisterValueChangedCallback(e =>
            {
                if (!e.newValue)
                {
                    bool restored = lease == null || lease.Restore(); lease = null;
                    status.text = restored ? "Previous global value restored." : "Gameplay changed the value; its current value was preserved.";
                }
                slider.SetEnabled(e.newValue && EditorApplication.isPlaying && AudioRuntime.Exists);
            });
            slider.RegisterValueChangedCallback(e =>
            {
                if (!enable.value || !EditorApplication.isPlaying || !AudioRuntime.Exists || parameter == null) return;
                if (lease == null) lease = new LiveParameterLease(AudioRuntime.Instance.GlobalParameters, parameter);
                lease.Set(e.newValue);
                status.text = "Live global value · asset default unchanged. Local overrides take precedence.";
            });
            Add(new Label("Enable to audition global parameter changes in this Editor session. Disable or close to restore your previous value unless gameplay replaced it. This is not remote Player live update."));
            schedule.Execute(() =>
            {
                bool available = EditorApplication.isPlaying && AudioRuntime.Exists && parameter != null;
                enable.SetEnabled(available);
                slider.SetEnabled(available && enable.value);
                if (!available) { lease?.Restore(); lease = null; enable.SetValueWithoutNotify(false); status.text = "Unavailable · enter Play Mode with an AudioMW runtime."; }
                else if (!enable.value) slider.SetValueWithoutNotify(AudioRuntime.Instance.GlobalParameters.Get(parameter));
            }).Every(100);
            RegisterCallback<DetachFromPanelEvent>(_ => { lease?.Restore(); lease = null; });
        }
    }
}
