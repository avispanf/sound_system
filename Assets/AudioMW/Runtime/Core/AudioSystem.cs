using UnityEngine;

namespace AudioMW
{
    public static class AudioSystem
    {
        public static Voice Play(SoundEvent soundEvent)
        {
            return AudioRuntime.Instance.Play(soundEvent, Vector3.zero, null);
        }

        public static Voice PlayAtPosition(SoundEvent soundEvent, Vector3 position)
        {
            return AudioRuntime.Instance.Play(soundEvent, position, null);
        }

        public static Voice PlayAttached(SoundEvent soundEvent, Transform target)
        {
            Vector3 position = target != null ? target.position : Vector3.zero;
            return AudioRuntime.Instance.Play(soundEvent, position, target);
        }

        public static void StopAll()
        {
            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.StopAll();
            }
        }

        public static int ActiveVoiceCount
        {
            get { return AudioRuntime.Exists ? AudioRuntime.Instance.Pool.ActiveCount : 0; }
        }
    }
}
