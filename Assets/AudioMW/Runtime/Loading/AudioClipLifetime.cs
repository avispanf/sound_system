using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    internal sealed class AudioClipLifetime
    {
        internal sealed class StreamOwner
        {
            internal AudioClipLifetime Lifetime;
            internal IAudioAssetLoader Loader;
            internal string Address;
            internal bool Owned = true;

            internal void Release()
            {
                if (!Owned) return;
                Owned = false;
                Lifetime.Collect();
            }
        }

        private static readonly Dictionary<AudioClip, AudioClipLifetime> clips = new Dictionary<AudioClip, AudioClipLifetime>();
        private const int CachedLifetimeLimit = 128;
        private static readonly Stack<AudioClipLifetime> free = new Stack<AudioClipLifetime>();
        private AudioClip clip;
        private readonly List<StreamOwner> streams = new List<StreamOwner>();
        private int banks;
        private int playback;
        private bool unloadData;
        private bool collecting;

        private static AudioClipLifetime Get(AudioClip clip)
        {
            AudioClipLifetime lifetime;
            if (!clips.TryGetValue(clip, out lifetime))
            {
                lifetime = free.Count > 0 ? free.Pop() : new AudioClipLifetime();
                lifetime.clip = clip;
                clips.Add(clip, lifetime);
            }
            return lifetime;
        }

        internal static void RetainBank(AudioClip clip)
        {
            if (clip == null) return;
            AudioClipLifetime lifetime = Get(clip);
            lifetime.banks++;
            lifetime.unloadData = true;
        }

        internal static void ReleaseBank(AudioClip clip)
        {
            AudioClipLifetime lifetime;
            if (ReferenceEquals(clip, null) || !clips.TryGetValue(clip, out lifetime) || lifetime.banks == 0) return;
            lifetime.banks--;
            lifetime.Collect();
        }

        internal static StreamOwner RetainStream(AudioClip clip, IAudioAssetLoader loader, string address)
        {
            AudioClipLifetime lifetime = Get(clip);
            StreamOwner owner = new StreamOwner { Lifetime = lifetime, Loader = loader, Address = address };
            lifetime.streams.Add(owner);
            return owner;
        }

        internal static AudioClipLifetime RetainPlayback(AudioClip clip)
        {
            if (clip == null) return null;
            AudioClipLifetime lifetime = Get(clip);
            lifetime.playback++;
            return lifetime;
        }

        internal void ReleasePlayback()
        {
            if (playback == 0) return;
            playback--;
            Collect();
        }

        private void Collect()
        {
            if (collecting || playback > 0 || banks > 0) return;
            collecting = true;
            try
            {
                while (playback == 0 && banks == 0)
                {
                    StreamOwner released = null;
                    for (int i = 0; i < streams.Count; i++)
                    {
                        if (streams[i].Owned) continue;
                        released = streams[i];
                        streams.RemoveAt(i);
                        break;
                    }
                    if (released == null) break;
                    released.Loader.Release(released.Address);
                }
                if (playback > 0 || banks > 0 || streams.Count > 0) return;
                clips.Remove(clip);
                if (unloadData && clip != null && clip.loadState == AudioDataLoadState.Loaded) clip.UnloadAudioData();
                clip = null;
                unloadData = false;
                if (free.Count < CachedLifetimeLimit) free.Push(this);
            }
            finally { collecting = false; }
        }
    }
}
