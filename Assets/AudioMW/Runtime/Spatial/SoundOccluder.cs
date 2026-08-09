using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Sound Occluder")]
    public sealed class SoundOccluder : MonoBehaviour
    {
        [SerializeField] private OcclusionSettings settings = new OcclusionSettings();
        [SerializeField] private SoundEvent soundEvent;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private SoundParameter occlusionParameter;

        private readonly OcclusionSampler sampler = new OcclusionSampler();
        private Voice voice;
        private float sampleTimer;

        public float Occlusion
        {
            get { return sampler.Current; }
        }

        public OcclusionSettings Settings
        {
            get { return settings; }
        }

        public Voice ActiveVoice
        {
            get { return voice; }
        }

        public SoundEvent Event
        {
            get { return soundEvent; }
            set { soundEvent = value; }
        }

        public SoundParameter OcclusionParameter
        {
            get { return occlusionParameter; }
            set { occlusionParameter = value; }
        }

        private void OnEnable()
        {
            sampler.Reset(0f);

            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play()
        {
            if (soundEvent == null)
            {
                return;
            }

            voice = AudioSystem.PlayAttached(soundEvent, transform);
        }

        public void Stop()
        {
            if (voice != null)
            {
                voice.ClearOcclusion();
                voice.Stop();
                voice = null;
            }
        }

        private void Update()
        {
            if (voice == null || !voice.IsActive)
            {
                return;
            }

            sampleTimer -= Time.deltaTime;

            if (sampleTimer <= 0f)
            {
                sampleTimer = settings.SampleInterval;
                Resample();
            }

            float occlusion = sampler.Advance(Time.deltaTime, settings.SmoothingSeconds);

            voice.ApplyOcclusion(settings.VolumeMultiplierFor(occlusion), settings.CutoffFor(occlusion));

            if (occlusionParameter != null)
            {
                voice.SetLocalParameter(occlusionParameter, occlusion);
            }
        }

        public void Resample()
        {
            Transform listener = FindListener();

            if (listener == null)
            {
                sampler.SetTargetFromHits(0, settings.SampleCount);
                return;
            }

            Vector3 origin = transform.position;
            Vector3 destination = listener.position;
            Vector3 direction = destination - origin;
            float distance = direction.magnitude;

            if (distance <= 0.001f)
            {
                sampler.SetTargetFromHits(0, settings.SampleCount);
                return;
            }

            int samples = settings.SampleCount;
            int blocked = 0;

            for (int i = 0; i < samples; i++)
            {
                Vector3 offset = OcclusionSampler.SampleOffset(i, samples, settings.SampleSpread, direction);
                Vector3 from = origin + offset;
                Vector3 to = destination + offset;
                Vector3 ray = to - from;

                if (Physics.Raycast(from, ray.normalized, ray.magnitude, settings.BlockingLayers, QueryTriggerInteraction.Ignore))
                {
                    blocked++;
                }
            }

            sampler.SetTargetFromHits(blocked, samples);
        }

        private static Transform FindListener()
        {
#if UNITY_2023_1_OR_NEWER
            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
#else
            AudioListener listener = Object.FindObjectOfType<AudioListener>();
#endif
            return listener != null ? listener.transform : null;
        }

        private void OnDrawGizmosSelected()
        {
            Transform listener = FindListener();

            if (listener == null)
            {
                return;
            }

            Gizmos.color = Color.Lerp(new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.5f, 0.3f), sampler.Current);
            Gizmos.DrawLine(transform.position, listener.position);
        }
    }
}
