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

        private Voice activeVoice;

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

            activeVoice = attachToTransform
                ? AudioSystem.PlayAttached(soundEvent, transform)
                : AudioSystem.PlayAtPosition(soundEvent, transform.position);
        }

        public void Stop()
        {
            if (activeVoice != null)
            {
                activeVoice.Stop();
                activeVoice = null;
            }
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
