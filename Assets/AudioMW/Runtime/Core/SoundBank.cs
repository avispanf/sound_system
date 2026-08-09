using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    [CreateAssetMenu(fileName = "BANK_NewBank", menuName = "AudioMW/Sound Bank", order = 20)]
    public sealed class SoundBank : ScriptableObject
    {
        [SerializeField] private SoundEvent[] events = new SoundEvent[0];
        [SerializeField] private bool loadOnStartup;
        [SerializeField] private ClipReference[] streamedClips = new ClipReference[0];

        [System.NonSerialized] private bool loaded;
        [System.NonSerialized] private readonly List<AudioClip> loadedClips = new List<AudioClip>();

        public SoundEvent[] Events
        {
            get { return events; }
            set { events = value ?? new SoundEvent[0]; }
        }

        public ClipReference[] StreamedClips
        {
            get { return streamedClips; }
            set { streamedClips = value ?? new ClipReference[0]; }
        }

        public bool HasStreamedClips
        {
            get { return streamedClips != null && streamedClips.Length > 0; }
        }

        public int PendingStreamCount { get; private set; }

        public bool IsStreamingComplete
        {
            get { return PendingStreamCount == 0; }
        }

        public bool LoadOnStartup
        {
            get { return loadOnStartup; }
            set { loadOnStartup = value; }
        }

        public bool IsLoaded
        {
            get { return loaded; }
        }

        public int LoadedClipCount
        {
            get { return loadedClips.Count; }
        }

        public void Load()
        {
            if (loaded)
            {
                return;
            }

            loadedClips.Clear();

            for (int i = 0; i < events.Length; i++)
            {
                CollectClips(events[i], loadedClips);
            }

            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClip clip = loadedClips[i];
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }
            }

            loaded = true;
        }

        public void LoadStreamed(IAudioAssetLoader loader)
        {
            if (loader == null || !HasStreamedClips)
            {
                return;
            }

            for (int i = 0; i < streamedClips.Length; i++)
            {
                ClipReference reference = streamedClips[i];

                if (reference == null || !reference.IsAddressed || reference.IsResolved)
                {
                    continue;
                }

                if (!loader.CanLoad(reference.Address))
                {
                    continue;
                }

                ClipReference captured = reference;
                PendingStreamCount++;

                loader.LoadAsync(reference.Address, (address, clip) =>
                {
                    captured.SetResolved(clip);
                    PendingStreamCount = Mathf.Max(0, PendingStreamCount - 1);
                });
            }
        }

        public void UnloadStreamed(IAudioAssetLoader loader)
        {
            if (!HasStreamedClips)
            {
                return;
            }

            for (int i = 0; i < streamedClips.Length; i++)
            {
                ClipReference reference = streamedClips[i];

                if (reference == null)
                {
                    continue;
                }

                if (loader != null && reference.IsAddressed)
                {
                    loader.Release(reference.Address);
                }

                reference.ClearResolved();
            }

            PendingStreamCount = 0;
        }

        public void Unload()
        {
            if (!loaded)
            {
                return;
            }

            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClip clip = loadedClips[i];
                if (clip != null && clip.loadState == AudioDataLoadState.Loaded)
                {
                    clip.UnloadAudioData();
                }
            }

            loadedClips.Clear();
            loaded = false;
        }

        public bool IsFullyLoaded()
        {
            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClip clip = loadedClips[i];
                if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
                {
                    return false;
                }
            }

            return loaded;
        }

        public static void CollectClips(SoundEvent soundEvent, List<AudioClip> target)
        {
            if (soundEvent == null || target == null)
            {
                return;
            }

            AudioClip[] clips = soundEvent.Clips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && !target.Contains(clips[i]))
                {
                    target.Add(clips[i]);
                }
            }

            BlendLayer[] layers = soundEvent.BlendLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null && layers[i].Clip != null && !target.Contains(layers[i].Clip))
                {
                    target.Add(layers[i].Clip);
                }
            }
        }

        public static SoundBank CreateRuntime(params SoundEvent[] soundEvents)
        {
            SoundBank bank = CreateInstance<SoundBank>();
            bank.events = soundEvents ?? new SoundEvent[0];
            return bank;
        }
    }
}
