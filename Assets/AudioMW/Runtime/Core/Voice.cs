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

            if (!source.loop && !source.isPlaying)
            {
                Release();
                return false;
            }

            return true;
        }

        private bool HadAttachTarget()
        {
            return !ReferenceEquals(attachTarget, null);
        }

        private void Release()
        {
            active = false;
            currentEvent = null;
            attachTarget = null;
            source.clip = null;
            source.outputAudioMixerGroup = null;
        }
    }
}
