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

        public static SoundHandle PlayTracked(SoundEvent soundEvent)
        {
            return AudioRuntime.Instance.PlayTracked(soundEvent, Vector3.zero, null);
        }

        public static SoundHandle PlayTrackedAtPosition(SoundEvent soundEvent, Vector3 position)
        {
            return AudioRuntime.Instance.PlayTracked(soundEvent, position, null);
        }

        public static SoundHandle PlayTrackedAttached(SoundEvent soundEvent, Transform target)
        {
            Vector3 position = target != null ? target.position : Vector3.zero;
            return AudioRuntime.Instance.PlayTracked(soundEvent, position, target);
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
                voice.SetLocalParameter(parameter, value);
            }
        }

        private static IAudioAssetLoader assetLoader = NullAudioAssetLoader.Instance;

        public static IAudioAssetLoader AssetLoader
        {
            get { return assetLoader; }
            set { assetLoader = value ?? NullAudioAssetLoader.Instance; }
        }

        public static void LoadBank(SoundBank bank)
        {
            if (bank != null)
            {
                bank.Load();
                bank.LoadStreamed(assetLoader);
            }
        }

        public static void UnloadBank(SoundBank bank)
        {
            if (bank != null)
            {
                bank.UnloadStreamed(assetLoader);
                bank.Unload();
            }
        }

        public static void PlayMusic(MusicTrack track, MusicQuantization quantization = MusicQuantization.Immediate)
        {
            AudioRuntime.Instance.Music.Play(track, quantization);
        }

        public static void TransitionMusic(MusicTrack track, MusicQuantization quantization = MusicQuantization.Bar)
        {
            AudioRuntime.Instance.Music.TransitionTo(track, quantization);
        }

        public static void PlayStinger(AudioClip clip, MusicQuantization quantization = MusicQuantization.Beat, float volume = 1f)
        {
            AudioRuntime.Instance.Music.PlayStinger(clip, quantization, volume);
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

        public static bool PlayVoiceLine(VoiceLine line, VoiceOverMode mode = VoiceOverMode.Queue)
        {
            return AudioRuntime.Instance.VoiceOver.Play(line, mode);
        }

        public static void SkipVoiceLine()
        {
            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.VoiceOver.Skip();
            }
        }

        public static void StopVoiceOver()
        {
            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.VoiceOver.Stop();
            }
        }

        public static VoiceOverDirector VoiceOver
        {
            get { return AudioRuntime.Instance.VoiceOver; }
        }

        public static MixerDirector Mixing
        {
            get { return AudioRuntime.Instance.Mixing; }
        }

        public static void AddMixerRouting(MixerRoutingProfile profile)
        {
            AudioRuntime.Instance.Mixing.AddProfile(profile);
        }

        public static void RemoveMixerRouting(MixerRoutingProfile profile)
        {
            if (AudioRuntime.Exists)
            {
                AudioRuntime.Instance.Mixing.RemoveProfile(profile);
            }
        }

        public static EventDebugger Debugger
        {
            get { return AudioRuntime.Instance.Debugger; }
        }

        public static void ApplyTier(AudioTierConfig config)
        {
            AudioTierApplier.Apply(config);
        }

        public static AudioTierConfig ActiveTier
        {
            get { return AudioRuntimeSettings.ActiveTier; }
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
