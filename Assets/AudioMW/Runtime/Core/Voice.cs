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
            source.spatialBlend = soundEvent.SpatialBlend;
            source.minDistance = soundEvent.MinDistance;
            source.maxDistance = soundEvent.MaxDistance;
            source.rolloffMode = soundEvent.RolloffMode;
            source.priority = soundEvent.Priority;
            ApplyParameters();
            source.Play();
        }

        public void Stop()
        {
            if (!active)
            {
                return;
            }

            source.Stop();
            Release();
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
            if (currentEvent == null || !currentEvent.HasParameterBindings)
            {
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

            source.volume = Mathf.Clamp01(baseVolume * volumeMultiplier);
            source.pitch = Mathf.Clamp(basePitch * pitchMultiplier, 0.01f, 3f);
        }

        private bool HadAttachTarget()
        {
            return !ReferenceEquals(attachTarget, null);
        }

        private void Release()
        {
            active = false;
            localParameters.Clear();
            currentEvent = null;
            attachTarget = null;
            source.clip = null;
            source.outputAudioMixerGroup = null;
        }
    }
}
