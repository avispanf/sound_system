using UnityEngine;

namespace AudioMW
{
    public sealed class VirtualVoice
    {
        private float elapsed;

        public SoundEvent Event { get; private set; }
        public PlaybackParameters Parameters { get; private set; }
        public Transform Attach { get; private set; }
        public Vector3 Position { get; private set; }
        public bool Loop { get; private set; }
        public float AudibleRange { get; private set; }
        public int HandleId { get; set; }

        public float Elapsed
        {
            get { return elapsed; }
        }

        public float ClipLength
        {
            get { return Parameters.Clip != null ? Parameters.Clip.length : 0f; }
        }

        public bool IsExpired
        {
            get { return VirtualizationPolicy.IsExpired(elapsed, ClipLength, Loop); }
        }

        public float PlaybackOffset
        {
            get { return VirtualizationPolicy.PlaybackOffset(elapsed, ClipLength, Loop); }
        }

        public Vector3 CurrentPosition
        {
            get { return Attach != null ? Attach.position : Position; }
        }

        public bool TargetLost
        {
            get { return !ReferenceEquals(Attach, null) && Attach == null; }
        }

        public void Configure(SoundEvent soundEvent, PlaybackParameters parameters, Vector3 position, Transform attach, float audibleRange, float startElapsed)
        {
            Event = soundEvent;
            Parameters = parameters;
            Position = position;
            Attach = attach;
            Loop = soundEvent != null && soundEvent.Loop;
            AudibleRange = Mathf.Max(0f, audibleRange);
            elapsed = Mathf.Max(0f, startElapsed);
        }

        public void Advance(float deltaSeconds)
        {
            elapsed += Mathf.Max(0f, deltaSeconds);
        }

        public bool ShouldBecomeReal(Vector3 listenerPosition, float hysteresis)
        {
            float distance = Vector3.Distance(CurrentPosition, listenerPosition);
            return !VirtualizationPolicy.ShouldVirtualize(true, distance, AudibleRange, hysteresis);
        }

        public void Clear()
        {
            Event = null;
            Attach = null;
            elapsed = 0f;
        }
    }
}
