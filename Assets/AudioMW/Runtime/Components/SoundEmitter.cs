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
    }
}
