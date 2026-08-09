using UnityEngine;

namespace AudioMW
{
    public sealed class Voice
    {
        private readonly AudioSource source;
        private readonly Transform cachedTransform;

        private SoundEvent currentEvent;
        private Transform attachTarget;
        private bool active;
        private float baseVolume = 1f;
        private float basePitch = 1f;
        private readonly ParameterStore localParameters = new ParameterStore();
        private readonly System.Collections.Generic.List<Voice> followers = new System.Collections.Generic.List<Voice>();
        private BlendLayer blendLayer;
        private float lastVolumeMultiplier = 1f;
        private float lastPitchMultiplier = 1f;
        private float lastBlendWeight = 1f;

        public event System.Action<Voice> Released;
        private float startTime;

        public Voice(AudioSource source)
        {
            this.source = source;
            cachedTransform = source.transform;
        }

        public AudioSource Source
        {
            get { return source; }
        }

        public bool IsActive
        {
            get { return active; }
        }

        public float StartTime
        {
            get { return startTime; }
        }

        public System.Collections.Generic.IReadOnlyList<Voice> Followers
        {
            get { return followers; }
        }

        public float BaseVolume
        {
            get { return baseVolume; }
        }

        public float BasePitch
        {
            get { return basePitch; }
        }

        public float LastVolumeMultiplier
        {
            get { return lastVolumeMultiplier; }
        }

        public float LastPitchMultiplier
        {
            get { return lastPitchMultiplier; }
        }

        public float LastBlendWeight
        {
            get { return lastBlendWeight; }
        }

        public BlendLayer BlendLayer
        {
            get { return blendLayer; }
            set { blendLayer = value; }
        }

        public ParameterStore LocalParameters
        {
            get { return localParameters; }
        }

        public SoundEvent CurrentEvent
        {
            get { return currentEvent; }
        }

        public void Play(SoundEvent soundEvent, PlaybackParameters parameters, Vector3 position, Transform attach)
        {
            currentEvent = soundEvent;
            attachTarget = attach;
            active = true;
            startTime = Time.unscaledTime;

            cachedTransform.position = attach != null ? attach.position : position;

            baseVolume = parameters.Volume;
            basePitch = parameters.Pitch;

            source.clip = parameters.Clip;
            source.volume = parameters.Volume;
            source.pitch = parameters.Pitch;
            source.loop = soundEvent.Loop;
            source.outputAudioMixerGroup = soundEvent.MixerGroup;
            if (soundEvent.AttenuationPreset != null)
            {
                soundEvent.AttenuationPreset.ApplyTo(source);
            }
            else
            {
                source.spatialBlend = soundEvent.SpatialBlend;
                source.minDistance = soundEvent.MinDistance;
                source.maxDistance = soundEvent.MaxDistance;
                source.rolloffMode = soundEvent.RolloffMode;
            }

            source.priority = soundEvent.Priority;
            ApplyParameters();
            source.Play();
        }

        public void Stop()
        {
            for (int i = 0; i < followers.Count; i++)
            {
                followers[i].Stop();
            }

            followers.Clear();

            if (!active)
            {
                return;
            }

            source.Stop();
            Release();
        }

        public void SetLocalParameter(SoundParameter parameter, float value)
        {
            if (parameter == null)
            {
                return;
            }

            localParameters.Set(parameter, value);
            ApplyParameters();

            for (int i = 0; i < followers.Count; i++)
            {
                followers[i].SetLocalParameter(parameter, value);
            }
        }

        public void ClearLocalParameters()
        {
            localParameters.Clear();
            ApplyParameters();

            for (int i = 0; i < followers.Count; i++)
            {
                followers[i].ClearLocalParameters();
            }
        }

        public bool IsGroupPlaying
        {
            get
            {
                if (active)
                {
                    return true;
                }

                for (int i = 0; i < followers.Count; i++)
                {
                    if (followers[i].IsActive)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void AddFollower(Voice follower)
        {
            if (follower != null && follower != this)
            {
                followers.Add(follower);
            }
        }

        public bool Tick()
        {
            if (!active)
            {
                return false;
            }

            if (attachTarget != null)
            {
                cachedTransform.position = attachTarget.position;
            }
            else if (source.spatialBlend > 0f && currentEvent != null && HadAttachTarget())
            {
                source.Stop();
                Release();
                return false;
            }

            ApplyParameters();

            if (!source.loop && !source.isPlaying)
            {
                Release();
                return false;
            }

            return true;
        }

        public void ApplyParameters()
        {
            if (currentEvent == null)
            {
                return;
            }

            if (!currentEvent.HasParameterBindings)
            {
                lastVolumeMultiplier = 1f;
                lastPitchMultiplier = 1f;
                lastBlendWeight = EvaluateBlendWeight();
                source.volume = Mathf.Clamp01(baseVolume * lastBlendWeight);
                return;
            }

            float volumeMultiplier = 1f;
            float pitchMultiplier = 1f;
            ParameterBinding[] bindings = currentEvent.ParameterBindings;

            for (int i = 0; i < bindings.Length; i++)
            {
                ParameterBinding binding = bindings[i];
                if (binding == null || !binding.IsValid)
                {
                    continue;
                }

                float raw;
                if (!localParameters.TryGet(binding.Parameter, out raw))
                {
                    raw = AudioRuntime.Exists
                        ? AudioRuntime.Instance.GlobalParameters.Get(binding.Parameter)
                        : binding.Parameter.DefaultValue;
                }

                float evaluated = binding.Evaluate(raw);

                if (binding.Target == ParameterTarget.Volume)
                {
                    volumeMultiplier *= evaluated;
                }
                else
                {
                    pitchMultiplier *= evaluated;
                }
            }

            lastVolumeMultiplier = volumeMultiplier;
            lastPitchMultiplier = pitchMultiplier;
            lastBlendWeight = EvaluateBlendWeight();

            source.volume = Mathf.Clamp01(baseVolume * volumeMultiplier * lastBlendWeight);
            source.pitch = Mathf.Clamp(basePitch * pitchMultiplier, 0.01f, 3f);
        }

        private float EvaluateBlendWeight()
        {
            if (blendLayer == null || currentEvent == null || currentEvent.BlendParameter == null)
            {
                return 1f;
            }

            SoundParameter parameter = currentEvent.BlendParameter;
            float raw;
            if (!localParameters.TryGet(parameter, out raw))
            {
                raw = AudioRuntime.Exists
                    ? AudioRuntime.Instance.GlobalParameters.Get(parameter)
                    : parameter.DefaultValue;
            }

            return blendLayer.EvaluateWeight(parameter.Normalize(parameter.Clamp(raw)));
        }

        private bool HadAttachTarget()
        {
            return !ReferenceEquals(attachTarget, null);
        }

        private void Release()
        {
            active = false;
            localParameters.Clear();
            blendLayer = null;

            System.Action<Voice> handler = Released;
            Released = null;
            if (handler != null)
            {
                handler(this);
            }

            currentEvent = null;
            attachTarget = null;
            source.clip = null;
            source.outputAudioMixerGroup = null;
        }
    }
}
