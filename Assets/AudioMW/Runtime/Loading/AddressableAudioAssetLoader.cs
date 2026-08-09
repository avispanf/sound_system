#if AUDIOMW_ADDRESSABLES
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AudioMW
{
    public sealed class AddressableAudioAssetLoader : IAudioAssetLoader
    {
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> handles =
            new Dictionary<string, AsyncOperationHandle<AudioClip>>();

        public bool CanLoad(string address)
        {
            return !string.IsNullOrEmpty(address);
        }

        public void LoadAsync(string address, Action<string, AudioClip> onComplete)
        {
            if (!CanLoad(address))
            {
                if (onComplete != null)
                {
                    onComplete(address, null);
                }

                return;
            }

            AsyncOperationHandle<AudioClip> existing;

            if (handles.TryGetValue(address, out existing) && existing.IsValid() && existing.IsDone)
            {
                if (onComplete != null)
                {
                    onComplete(address, existing.Result);
                }

                return;
            }

            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
            handles[address] = handle;

            handle.Completed += operation =>
            {
                AudioClip clip = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;

                if (clip == null)
                {
                    Debug.LogWarning("AudioMW could not load addressable audio clip: " + address);
                }

                if (onComplete != null)
                {
                    onComplete(address, clip);
                }
            };
        }

        public void Release(string address)
        {
            AsyncOperationHandle<AudioClip> handle;

            if (!handles.TryGetValue(address, out handle))
            {
                return;
            }

            handles.Remove(address);

            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        public void ReleaseAll()
        {
            foreach (KeyValuePair<string, AsyncOperationHandle<AudioClip>> pair in handles)
            {
                if (pair.Value.IsValid())
                {
                    Addressables.Release(pair.Value);
                }
            }

            handles.Clear();
        }
    }
}
#endif
