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
            public ParameterBinding[] Bindings = System.Array.Empty<ParameterBinding>();
            public void SetBindings(List<ParameterBinding> source)
            {
                if (Bindings.Length != source.Count) Bindings = new ParameterBinding[source.Count];
                source.CopyTo(Bindings);
            }
            public void CopyFrom(Layer source)
            {
                Event = source.Event; Parameters = source.Parameters; Blend = source.Blend;
                if (Bindings.Length != source.Bindings.Length) Bindings = new ParameterBinding[source.Bindings.Length];
                System.Array.Copy(source.Bindings, Bindings, Bindings.Length);
            }
            public void Clear()
            {
                Event = null; Parameters = PlaybackParameters.Invalid; Blend = null;
                System.Array.Clear(Bindings, 0, Bindings.Length);
            }
        }

        public readonly List<Layer> Layers = new List<Layer>();
        public PlaybackOutcome Failure { get; private set; }
        public PlaybackRejectionInfo FailureInfo { get; private set; }
        private readonly Stack<Layer> freeLayers = new Stack<Layer>();
        private readonly HashSet<SoundEvent> path = new HashSet<SoundEvent>();
        private readonly List<ParameterBinding> bindings = new List<ParameterBinding>();

        public bool Build(SoundEvent root, System.Random random, ParameterStore local, ParameterStore global, int capacity)
        {
            Clear();
            Failure = root.ContainerMode == ContainerMode.Simple ? PlaybackOutcome.RejectedNoClips : PlaybackOutcome.RejectedNoValidLayers;
            return Visit(root, random, local, global, 1f, 1f, capacity) && Layers.Count > 0;
        }

        public void Clear()
        {
            foreach (Layer layer in Layers) { layer.Clear(); freeLayers.Push(layer); }
            Layers.Clear();
            FailureInfo = default(PlaybackRejectionInfo);
            path.Clear();
            bindings.Clear();
        }

        private void AddLayer(SoundEvent item, PlaybackParameters parameters, BlendLayer blend)
        {
            Layer layer = freeLayers.Count > 0 ? freeLayers.Pop() : new Layer();
            layer.Event = item; layer.Parameters = parameters; layer.Blend = blend;
            layer.SetBindings(bindings);
            Layers.Add(layer);
        }

        private bool Visit(SoundEvent item, System.Random random, ParameterStore local, ParameterStore global, float gain, float pitch, int capacity)
        {
            if (item == null) return true;
            if (path.Count >= 32) { Failure = PlaybackOutcome.RejectedContainerGraph; ContentFailure(item, PlaybackContentIssue.NestingLimit); return false; }
            if (!path.Add(item)) { Failure = PlaybackOutcome.RejectedContainerGraph; ContentFailure(item, PlaybackContentIssue.CircularReference); return false; }
            int bindingStart = bindings.Count;
            if (item.ParameterBindings != null) bindings.AddRange(item.ParameterBindings);
            bool valid = true;
            if (item.ContainerMode == ContainerMode.Simple)
            {
                if (item.HasUnresolvedReferences)
                {
                    Failure = PlaybackOutcome.RejectedClipNotLoaded;
                    ContentFailure(item, PlaybackContentIssue.None);
                    bindings.RemoveRange(bindingStart, bindings.Count - bindingStart);
                    path.Remove(item);
                    return false;
                }
                PlaybackParameters parameters = item.Resolve(random);
                if (parameters.IsValid)
                {
                    parameters.Volume *= gain;
                    parameters.Pitch = Mathf.Clamp(parameters.Pitch * pitch, 0.01f, 3f);
                    AddLayer(item, parameters, null);
                }
                else ContentFailure(item, PlaybackContentIssue.NoUsableSource);
            }
            else
            {
                gain *= item.ResolveVolume(random);
                pitch *= item.ResolvePitch(random);
                if (item.ContainerMode == ContainerMode.Blend)
                {
                    int before = Layers.Count;
                    foreach (BlendLayer blend in item.BlendLayers)
                    {
                        if (blend == null || !blend.IsValid) continue;
                        AddLayer(item, new PlaybackParameters
                        { Clip = blend.Clip, Volume = gain, Pitch = Mathf.Clamp(pitch, 0.01f, 3f), IsValid = true }, blend);
                        if (Layers.Count > capacity) { Failure = PlaybackOutcome.RejectedNoVoice; valid = false; break; }
                    }
                    if (Layers.Count == before) ContentFailure(item, PlaybackContentIssue.NoUsableSource);
                }
                else
                {
                    int matching = 0;
                    float raw = 0f;
                    if (item.ContainerMode == ContainerMode.Switch)
                    {
                        if (item.SwitchParameter == null) { valid = false; ContentFailure(item, PlaybackContentIssue.MissingSwitchParameter); }
                        else if (local == null || !local.TryGet(item.SwitchParameter, out raw)) raw = global.Get(item.SwitchParameter);
                    }
                    foreach (SoundEventChild child in item.Children)
                    {
                        if (!valid) break;
                        if (child == null || child.Event == null) continue;
                        if (item.ContainerMode == ContainerMode.Switch && child.State != Mathf.RoundToInt(raw)) continue;
                        matching++;
                        valid = Visit(child.Event, random, local, global, gain * child.Gain, pitch, capacity);
                    }
                    if (valid && matching == 0)
                    {
                        if (item.ContainerMode == ContainerMode.Switch) FailureInfo = new PlaybackRejectionInfo { ProblemEvent = item, ContentIssue = PlaybackContentIssue.NoSwitchMatch, SwitchValue = raw, SwitchState = Mathf.RoundToInt(raw) };
                        else ContentFailure(item, PlaybackContentIssue.NoUsableSource);
                    }
                }
            }
            bindings.RemoveRange(bindingStart, bindings.Count - bindingStart);
            path.Remove(item);
            if (Layers.Count > capacity) { Failure = PlaybackOutcome.RejectedNoVoice; ContentFailure(item, PlaybackContentIssue.None); return false; }
            return valid;
        }
        private void ContentFailure(SoundEvent item, PlaybackContentIssue issue)
        {
            FailureInfo = new PlaybackRejectionInfo { ProblemEvent = item, ContentIssue = issue };
        }
    }
}
