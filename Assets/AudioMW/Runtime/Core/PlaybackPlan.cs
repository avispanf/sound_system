using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    internal sealed class PlaybackPlan
    {
        internal sealed class Layer
        {
            public SoundEvent Event;
            public PlaybackParameters Parameters;
            public BlendLayer Blend;
            public ParameterBinding[] Bindings;
        }

        public readonly List<Layer> Layers = new List<Layer>();
        public PlaybackOutcome Failure { get; private set; }
        private readonly HashSet<SoundEvent> path = new HashSet<SoundEvent>();
        private readonly List<ParameterBinding> bindings = new List<ParameterBinding>();

        public bool Build(SoundEvent root, System.Random random, ParameterStore local, ParameterStore global, int capacity)
        {
            Layers.Clear();
            Failure = root.ContainerMode == ContainerMode.Simple ? PlaybackOutcome.RejectedNoClips : PlaybackOutcome.RejectedNoValidLayers;
            return Visit(root, random, local, global, 1f, 1f, capacity) && Layers.Count > 0;
        }

        private bool Visit(SoundEvent item, System.Random random, ParameterStore local, ParameterStore global, float gain, float pitch, int capacity)
        {
            if (item == null) return true;
            if (path.Count >= 32 || !path.Add(item)) { Failure = PlaybackOutcome.RejectedContainerGraph; return false; }
            int bindingStart = bindings.Count;
            if (item.ParameterBindings != null) bindings.AddRange(item.ParameterBindings);
            bool valid = true;
            if (item.ContainerMode == ContainerMode.Simple)
            {
                PlaybackParameters parameters = item.Resolve(random);
                if (parameters.IsValid)
                {
                    parameters.Volume *= gain;
                    parameters.Pitch = Mathf.Clamp(parameters.Pitch * pitch, 0.01f, 3f);
                    Layers.Add(new Layer { Event = item, Parameters = parameters, Bindings = bindings.ToArray() });
                }
            }
            else
            {
                gain *= item.ResolveVolume(random);
                pitch *= item.ResolvePitch(random);
                if (item.ContainerMode == ContainerMode.Blend)
                {
                    foreach (BlendLayer blend in item.BlendLayers)
                    {
                        if (blend == null || !blend.IsValid) continue;
                        Layers.Add(new Layer { Event = item, Blend = blend, Bindings = bindings.ToArray(), Parameters = new PlaybackParameters
                        { Clip = blend.Clip, Volume = gain, Pitch = Mathf.Clamp(pitch, 0.01f, 3f), IsValid = true } });
                        if (Layers.Count > capacity) { Failure = PlaybackOutcome.RejectedNoVoice; valid = false; break; }
                    }
                }
                else
                {
                    float raw = 0f;
                    if (item.ContainerMode == ContainerMode.Switch)
                    {
                        if (item.SwitchParameter == null) valid = false;
                        else if (local == null || !local.TryGet(item.SwitchParameter, out raw)) raw = global.Get(item.SwitchParameter);
                    }
                    foreach (SoundEventChild child in item.Children)
                    {
                        if (!valid) break;
                        if (child == null || child.Event == null) continue;
                        if (item.ContainerMode == ContainerMode.Switch && child.State != Mathf.RoundToInt(raw)) continue;
                        valid = Visit(child.Event, random, local, global, gain * child.Gain, pitch, capacity);
                    }
                }
            }
            bindings.RemoveRange(bindingStart, bindings.Count - bindingStart);
            path.Remove(item);
            if (Layers.Count > capacity) { Failure = PlaybackOutcome.RejectedNoVoice; return false; }
            return valid;
        }
    }
}
