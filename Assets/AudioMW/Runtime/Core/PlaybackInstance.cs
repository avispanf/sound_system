using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    internal sealed class PlaybackInstance
    {
        private sealed class Slot
        {
            public readonly PlaybackPlan.Layer Layer = new PlaybackPlan.Layer();
            public VirtualVoice VirtualCache;
            public Voice Voice;
            public AudioClipLifetime Lifetime;
            public VirtualVoice Virtual;
        }

        private readonly AudioRuntime runtime;
        private bool stopping;
        private readonly Stack<Slot> freeSlots = new Stack<Slot>();
        private readonly List<Slot> slots = new List<Slot>();
        private readonly ParameterStore parameters = new ParameterStore();
        public SoundEvent Event { get; private set; }
        public Transform Attach { get; private set; }
        public float Started { get; private set; }
        public Voice Primary { get; private set; }
        public int HandleId;
        private float occlusionVolume = 1f;
        private float occlusionCutoff = 22000f;

        public PlaybackInstance(AudioRuntime runtime) { this.runtime = runtime; }

        public void Initialize(SoundEvent soundEvent, Transform attach, ParameterStore initial)
        {
            Event = soundEvent; Attach = attach; Started = Time.unscaledTime; HandleId = 0;
            parameters.CopyFrom(initial);
        }

        public bool NeedsListener
        {
            get
            {
                if (Event == null) return false;
                foreach (Slot slot in slots)
                    if (slot.Virtual != null || (slot.Voice != null && slot.Voice.IsActive && slot.Layer.Event.AllowVirtualization && slot.Voice.Source.spatialBlend > 0)) return true;
                return false;
            }
        }

        public bool IsPlaying
        {
            get
            {
                if (Event == null) return false;
                foreach (Slot slot in slots)
                    if ((slot.Voice != null && slot.Voice.IsActive) || slot.Virtual != null) return true;
                return false;
            }
        }

        public int VirtualCount
        {
            get { int count = 0; foreach (Slot slot in slots) if (slot.Virtual != null) count++; return count; }
        }

        public int ReservedCount
        {
            get { int count = 0; foreach (Slot slot in slots) if (slot.Voice != null) count++; return count; }
        }

        public float Volume
        {
            get { float value = 0; foreach (Slot slot in slots) if (slot.Voice != null && slot.Voice.IsActive) value = Mathf.Max(value, slot.Voice.Source.volume); return value; }
        }

        public Voice ActiveVoice
        {
            get { foreach (Slot slot in slots) if (slot.Voice != null && slot.Voice.IsActive) return slot.Voice; return null; }
        }

        public void Start(PlaybackPlan plan, VoicePool pool, Vector3 position)
        {
            foreach (PlaybackPlan.Layer layer in plan.Layers)
            {
                Voice voice = pool.AcquireFree();
                Slot slot = freeSlots.Count > 0 ? freeSlots.Pop() : new Slot();
                slot.Layer.CopyFrom(layer);
                slot.Lifetime = AudioClipLifetime.RetainPlayback(layer.Parameters.Clip);
                slot.Voice = voice;
                slots.Add(slot);
                Configure(slot, position, 0f);
                if (Primary == null) Primary = voice;
            }
            RefreshFollowers();
        }

        private void Configure(Slot slot, Vector3 position, float offset)
        {
            Voice voice = slot.Voice;
            voice.Owner = this;
            voice.Bindings = slot.Layer.Bindings;
            voice.BlendLayer = slot.Layer.Blend;
            voice.LocalParameters.CopyFrom(parameters);
            voice.HandleId = HandleId;
            voice.Play(slot.Layer.Event, slot.Layer.Parameters, position, Attach, offset);
            voice.ApplyOcclusionSelf(occlusionVolume, occlusionCutoff);
        }

        public void ApplyOcclusion(float volume, float cutoff)
        {
            occlusionVolume = volume;
            occlusionCutoff = cutoff;
            foreach (Slot slot in slots) if (slot.Voice != null) slot.Voice.ApplyOcclusionSelf(volume, cutoff);
        }

        public void SetParameter(SoundParameter parameter, float value)
        {
            if (parameter == null) return;
            parameters.Set(parameter, value);
            ApplyParameters();
        }

        public void ClearParameters()
        {
            parameters.Clear();
            ApplyParameters();
        }

        private void ApplyParameters()
        {
            foreach (Slot slot in slots)
            {
                if (slot.Voice == null) continue;
                slot.Voice.LocalParameters.CopyFrom(parameters);
                slot.Voice.ApplyParameters();
            }
        }

        public void Stop()
        {
            if (stopping) return;
            stopping = true;
            runtime.BeginOperation();
            try
            {
                foreach (Slot slot in slots)
                {
                    if (slot.Voice != null)
                    {
                        slot.Voice.Owner = null;
                        slot.Voice.ClearFollowers();
                        slot.Voice.Stop();
                        slot.Voice = null;
                    }
                    if (slot.Virtual != null) slot.Virtual.Clear();
                    slot.Virtual = null;
                    ReleaseLifetime(slot);
                    slot.Layer.Clear();
                    freeSlots.Push(slot);
                }
                slots.Clear();
                parameters.Clear();
                Event = null; Attach = null; Primary = null;
                occlusionVolume = 1f; occlusionCutoff = 22000f;
            }
            finally
            {
                stopping = false;
                runtime.EndOperation();
            }
        }

        private static void ReleaseLifetime(Slot slot)
        {
            AudioClipLifetime lifetime = slot.Lifetime;
            slot.Lifetime = null;
            if (lifetime != null) lifetime.ReleasePlayback();
        }

        public void ClearVirtual()
        {
            foreach (Slot slot in slots) { if (slot.Virtual != null) slot.Virtual.Clear(); slot.Virtual = null; }
            if (!IsPlaying) Stop();
        }

        public void Tick(VoicePool pool, Transform listener, bool virtualizationEnabled, float delta)
        {
            if (Event == null) { Stop(); return; }
            foreach (Slot slot in slots)
            {
                Voice voice = slot.Voice;
                if (voice != null && !voice.IsActive && voice != Primary)
                {
                    voice.Owner = null;
                    voice.ClearFollowers();
                    slot.Voice = null;
                }
                if (voice != null && voice.IsActive && virtualizationEnabled && listener != null &&
                    slot.Layer.Event.AllowVirtualization && voice.Source.spatialBlend > 0f &&
                    VirtualizationPolicy.ShouldVirtualize(false, Vector3.Distance(voice.Source.transform.position, listener.position), voice.Source.maxDistance))
                {
                    if (slot.VirtualCache == null) slot.VirtualCache = new VirtualVoice();
                    slot.Virtual = slot.VirtualCache;
                    slot.Virtual.Configure(slot.Layer.Event, slot.Layer.Parameters, voice.Source.transform.position, Attach, voice.Source.maxDistance, voice.Source.time);
                    voice.Owner = null;
                    voice.ClearFollowers();
                    voice.Demote();
                    slot.Voice = null;
                    if (Primary == voice) Primary = null;
                    continue;
                }
                if (slot.Virtual == null) continue;
                float volumeMultiplier, pitchMultiplier;
                ParameterEvaluation.Evaluate(slot.Layer.Bindings, parameters, out volumeMultiplier, out pitchMultiplier);
                slot.Virtual.Advance((AudioListener.pause && !slot.Layer.Event.IgnorePause ? 0 : delta) * Mathf.Clamp(slot.Layer.Parameters.Pitch * pitchMultiplier, 0.01f, 3f));
                if (slot.Virtual.IsExpired || slot.Virtual.TargetLost)
                {
                    slot.Virtual.Clear();
                    slot.Virtual = null;
                    continue;
                }
                if (virtualizationEnabled && (listener == null || !slot.Virtual.ShouldBecomeReal(listener.position, VirtualizationPolicy.DefaultHysteresis))) continue;
                slot.Voice = pool.AcquireFree();
                if (slot.Voice == null) continue;
                Configure(slot, slot.Virtual.CurrentPosition, slot.Virtual.PlaybackOffset);
                slot.Virtual.Clear();
                slot.Virtual = null;
                if (Primary == null) Primary = slot.Voice;
            }
            foreach (Slot slot in slots)
                if (slot.Virtual == null && (slot.Voice == null || !slot.Voice.IsActive)) ReleaseLifetime(slot);
            if (!IsPlaying) Stop();
            else RefreshFollowers();
        }

        private void RefreshFollowers()
        {
            if (Primary == null) return;
            Primary.ClearFollowers();
            foreach (Slot slot in slots)
                if (slot.Voice != null && slot.Voice != Primary && slot.Voice.IsActive) Primary.AddFollower(slot.Voice);
        }
    }
}
