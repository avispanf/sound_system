#if AUDIOMW_ADDRESSABLES
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace AudioMW
{
    public sealed class AddressableAudioAssetLoader : CachedAudioAssetLoader
    {
        private readonly Dictionary<string, AsyncOperationHandle<AudioClip>> handles = new Dictionary<string, AsyncOperationHandle<AudioClip>>(StringComparer.Ordinal);

        protected override void BeginLoad(string address, Action<AudioClip> onComplete)
        {
            AsyncOperationHandle<AudioClip> handle = Addressables.LoadAssetAsync<AudioClip>(address);
            handles.Add(address, handle);
            handle.Completed += operation => onComplete(operation.IsValid() && operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null);
        }

        protected override void EndLoad(string address)
        {
            AsyncOperationHandle<AudioClip> handle;
            if (!handles.TryGetValue(address, out handle)) return;
            handles.Remove(address);
            if (handle.IsValid()) Addressables.Release(handle);
        }
    }
}
#endif
