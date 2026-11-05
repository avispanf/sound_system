using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    internal sealed class PlaybackInstance
    {
        private sealed class Slot
        {
            public PlaybackPlan.Layer Layer;
            public Voice Voice;
            public VirtualVoice Virtual;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly ParameterStore parameters = new ParameterStore();
        public readonly SoundEvent Event;
        public readonly Transform Attach;
        public readonly float Started = Time.unscaledTime;
        public Voice Primary { get; private set; }
        public int HandleId;
        private float occlusionVolume = 1f;
        private float occlusionCutoff = 22000f;

        public PlaybackInstance(SoundEvent soundEvent, Transform attach, ParameterStore initial)
        {
            Event = soundEvent;
            Attach = attach;
            parameters.CopyFrom(initial);
        }

        public bool IsPlaying
        {
            get
            {
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
                Slot slot = new Slot { Layer = layer, Voice = voice };
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
            foreach (Slot slot in slots)
            {
                if (slot.Voice != null)
                {
                    slot.Voice.Owner = null;
                    slot.Voice.ClearFollowers();
                    slot.Voice.Stop();
                    slot.Voice = null;
                }
                slot.Virtual = null;
            }
            slots.Clear();
        }

        public void ClearVirtual()
        {
            foreach (Slot slot in slots) slot.Virtual = null;
            if (!IsPlaying) Stop();
        }

        public void Tick(VoicePool pool, Transform listener, bool virtualizationEnabled, float delta)
        {
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
                    slot.Virtual = new VirtualVoice();
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
                slot.Virtual.Advance(delta * Mathf.Clamp(slot.Layer.Parameters.Pitch * pitchMultiplier, 0.01f, 3f));
                if (slot.Virtual.IsExpired || slot.Virtual.TargetLost)
                {
                    slot.Virtual = null;
                    continue;
                }
                if (virtualizationEnabled && (listener == null || !slot.Virtual.ShouldBecomeReal(listener.position, VirtualizationPolicy.DefaultHysteresis))) continue;
                slot.Voice = pool.AcquireFree();
                if (slot.Voice == null) continue;
                Configure(slot, slot.Virtual.CurrentPosition, slot.Virtual.PlaybackOffset);
                slot.Virtual = null;
                if (Primary == null) Primary = slot.Voice;
            }
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
