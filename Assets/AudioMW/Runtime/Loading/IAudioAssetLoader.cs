using System;
using UnityEngine;

namespace AudioMW
{
    public interface IAudioAssetLoader
    {
        bool CanLoad(string address);

        void LoadAsync(string address, Action<string, AudioClip> onComplete);

        void Release(string address);

        void ReleaseAll();
    }

    public sealed class NullAudioAssetLoader : IAudioAssetLoader
    {
        public static readonly NullAudioAssetLoader Instance = new NullAudioAssetLoader();

        public bool CanLoad(string address)
        {
            return false;
        }

        public void LoadAsync(string address, Action<string, AudioClip> onComplete)
        {
            if (onComplete != null)
            {
                onComplete(address, null);
            }
        }

        public void Release(string address)
        {
        }

        public void ReleaseAll()
        {
        }
    }
}
