using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Sound Scatterer")]
    public sealed class SoundScatterer : MonoBehaviour
    {
        [SerializeField] private SoundEvent soundEvent;
        [SerializeField] private bool playOnEnable = true;

        [SerializeField] private float minInterval = 2f;
        [SerializeField] private float maxInterval = 8f;

        [SerializeField] private float minRadius = 3f;
        [SerializeField] private float maxRadius = 20f;

        [SerializeField] private bool aroundListener = true;
        [SerializeField] private bool flatten = true;
        [SerializeField] private float verticalSpread = 2f;

        [SerializeField] private int maxConcurrent = 4;

        private float timer;
        private bool running;
        private readonly System.Collections.Generic.List<SoundHandle> playing = new System.Collections.Generic.List<SoundHandle>();
        private System.Random rng;

        public SoundEvent Event
        {
            get { return soundEvent; }
            set { soundEvent = value; }
        }

        public bool IsRunning
        {
            get { return running; }
        }

        public bool AroundListener
        {
            get { return aroundListener; }
            set { aroundListener = value; }
        }

        public int LiveCount
        {
            get { playing.RemoveAll(handle => !handle.IsPlaying); return playing.Count; }
        }

        public void Configure(float intervalMin, float intervalMax, float radiusMin, float radiusMax, int concurrent)
        {
            minInterval = Mathf.Max(0.01f, intervalMin);
            maxInterval = Mathf.Max(minInterval, intervalMax);
            minRadius = Mathf.Max(0f, radiusMin);
            maxRadius = Mathf.Max(minRadius, radiusMax);
            maxConcurrent = Mathf.Max(1, concurrent);
        }

        private void OnEnable()
        {
            rng = new System.Random(unchecked(GetInstanceID() * 31 + System.Environment.TickCount));

            if (playOnEnable)
            {
                StartScattering();
            }
        }

        private void OnDisable()
        {
            StopScattering();
        }

        public void StartScattering()
        {
            running = true;
            timer = NextInterval();
        }

        public void StopScattering()
        {
            running = false;
            foreach (SoundHandle handle in playing) handle.Stop();
            playing.Clear();
        }

        private void Update()
        {
            if (!running || soundEvent == null)
            {
                return;
            }

            timer -= Time.deltaTime;
            if (timer > 0f)
            {
                return;
            }

            timer = NextInterval();
            Spawn();
        }

        public Voice Spawn()
        {
            if (soundEvent == null || LiveCount >= maxConcurrent)
            {
                return null;
            }

            SoundHandle handle = AudioSystem.PlayTrackedAtPosition(soundEvent, NextPosition());
            if (handle.IsAssigned) playing.Add(handle);
            return handle.Voice;
        }

        public Vector3 NextPosition()
        {
            Vector3 origin = transform.position;

            if (aroundListener)
            {
                AudioListener listener = FindListener();
                if (listener != null)
                {
                    origin = listener.transform.position;
                }
            }

            double angle = rng.NextDouble() * System.Math.PI * 2.0;
            float radius = Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble());

            Vector3 offset = new Vector3(
                Mathf.Cos((float)angle) * radius,
                flatten ? 0f : ((float)rng.NextDouble() * 2f - 1f) * verticalSpread,
                Mathf.Sin((float)angle) * radius);

            return origin + offset;
        }

        private static AudioListener FindListener()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindFirstObjectByType<AudioListener>();
#else
            return Object.FindObjectOfType<AudioListener>();
#endif
        }

        private float NextInterval()
        {
            return Mathf.Lerp(minInterval, maxInterval, (float)rng.NextDouble());
        }
    }
}
