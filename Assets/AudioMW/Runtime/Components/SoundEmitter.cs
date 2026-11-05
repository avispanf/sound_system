using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Sound Emitter")]
    public sealed class SoundEmitter : MonoBehaviour
    {
        [SerializeField] private SoundEvent soundEvent;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private bool attachToTransform = true;
        [SerializeField] private bool stopOnDisable = true;

        private readonly System.Collections.Generic.List<SoundHandle> playing = new System.Collections.Generic.List<SoundHandle>();
        private readonly ParameterStore parameters = new ParameterStore();

        public void SetParameter(SoundParameter parameter, float value)
        {
            parameters.Set(parameter, value);
            for (int i = playing.Count - 1; i >= 0; i--)
            {
                if (!playing[i].IsPlaying) playing.RemoveAt(i);
                else playing[i].SetParameter(parameter, value);
            }
        }

        public SoundEvent Event
        {
            get { return soundEvent; }
            set { soundEvent = value; }
        }

        private void OnEnable()
        {
            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            if (stopOnDisable)
            {
                Stop();
            }
        }

        public void Play()
        {
            if (soundEvent == null)
            {
                return;
            }

            playing.RemoveAll(handle => !handle.IsPlaying);
            SoundHandle handle = attachToTransform
                ? AudioSystem.PlayTrackedAttached(soundEvent, transform, parameters)
                : AudioSystem.PlayTrackedAtPosition(soundEvent, transform.position, parameters);
            if (handle.IsAssigned) playing.Add(handle);
        }

        public void Stop()
        {
            foreach (SoundHandle handle in playing) handle.Stop();
            playing.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (soundEvent == null)
            {
                return;
            }

            AttenuationPreset preset = soundEvent.AttenuationPreset;
            float min = preset != null ? preset.MinDistance : soundEvent.MinDistance;
            float max = preset != null ? preset.MaxDistance : soundEvent.MaxDistance;
            float blend = preset != null ? preset.SpatialBlend : soundEvent.SpatialBlend;

            if (blend <= 0f)
            {
                return;
            }

            Gizmos.color = new Color(0.2f, 0.9f, 0.6f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, min);

            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, max);
        }
    }
}
