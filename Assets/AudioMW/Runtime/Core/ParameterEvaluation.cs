namespace AudioMW
{
    internal static class ParameterEvaluation
    {
        public static void Evaluate(ParameterBinding[] bindings, ParameterStore local, out float volume, out float pitch)
        {
            volume = 1f;
            pitch = 1f;
            if (bindings == null) return;
            foreach (ParameterBinding binding in bindings)
            {
                if (binding == null || !binding.IsValid) continue;
                float raw;
                if (!local.TryGet(binding.Parameter, out raw))
                    raw = AudioRuntime.Exists ? AudioRuntime.Instance.GlobalParameters.Get(binding.Parameter) : binding.Parameter.DefaultValue;
                if (binding.Target == ParameterTarget.Volume) volume *= binding.Evaluate(raw);
                else pitch *= binding.Evaluate(raw);
            }
        }
    }
}
