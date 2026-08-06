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

        public static void SetParameter(SoundParameter parameter, float value)
        {
            AudioRuntime.Instance.GlobalParameters.Set(parameter, value);
        }

        public static float GetParameter(SoundParameter parameter)
        {
            return AudioRuntime.Exists
                ? AudioRuntime.Instance.GlobalParameters.Get(parameter)
                : (parameter != null ? parameter.DefaultValue : 0f);
        }

        public static void SetVoiceParameter(Voice voice, SoundParameter parameter, float value)
        {
            if (voice != null)
            {
                voice.LocalParameters.Set(parameter, value);
                voice.ApplyParameters();
            }
        }

        public static void LoadBank(SoundBank bank)
        {
            if (bank != null)
            {
                bank.Load();
            }
        }

        public static void UnloadBank(SoundBank bank)
        {
            if (bank != null)
            {
                bank.Unload();
            }
        }

        public static void PlayMusic(MusicTrack track, MusicQuantization quantization = MusicQuantization.Immediate)
        {
            AudioRuntime.Instance.Music.Play(track, quantization);
        }

        public static void StopMusic()
        {
            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.Music.Stop();
            }
        }

        public static MusicPlayer Music
        {
            get { return AudioRuntime.Instance.Music; }
        }

        public static void Shutdown()
        {
            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.Shutdown();
            }
        }

        public static int ActiveVoiceCount
        {
            get { return AudioRuntime.Exists ? AudioRuntime.Instance.Pool.ActiveCount : 0; }
        }
    }
}
