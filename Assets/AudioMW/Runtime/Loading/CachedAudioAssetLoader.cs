using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public abstract class CachedAudioAssetLoader : IAudioAssetLoader
    {
        private sealed class Entry
        {
            public int Users;
            public bool Complete;
            public AudioClip Clip;
            public readonly List<Action<string, AudioClip>> Callbacks = new List<Action<string, AudioClip>>();
        }
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        public int RetainedAddressCount { get { return entries.Count; } }
        public virtual bool CanLoad(string address) { return !string.IsNullOrWhiteSpace(address); }

        public void LoadAsync(string address, Action<string, AudioClip> onComplete)
        {
            if (!CanLoad(address)) { if (onComplete != null) onComplete(address, null); return; }
            Entry entry;
            bool start = !entries.TryGetValue(address, out entry);
            if (start) { entry = new Entry(); entries.Add(address, entry); }
            entry.Users++;
            if (entry.Complete) { if (onComplete != null) onComplete(address, entry.Clip); return; }
            if (onComplete != null) entry.Callbacks.Add(onComplete);
            if (!start) return;
            Entry captured = entry;
            try { BeginLoad(address, clip => Complete(address, captured, clip)); }
            catch { Complete(address, captured, null); throw; }
        }

        private void Complete(string address, Entry entry, AudioClip clip)
        {
            Entry current;
            if (!entries.TryGetValue(address, out current) || !ReferenceEquals(entry, current) || entry.Complete) return;
            entry.Complete = true; entry.Clip = clip;
            Action<string, AudioClip>[] callbacks = entry.Callbacks.ToArray();
            entry.Callbacks.Clear();
            foreach (Action<string, AudioClip> callback in callbacks)
            {
                try { callback(address, clip); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        public void Release(string address)
        {
            Entry entry;
            if (address == null || !entries.TryGetValue(address, out entry)) return;
            if (--entry.Users > 0) return;
            entries.Remove(address);
            entry.Callbacks.Clear(); entry.Clip = null;
            EndLoad(address);
        }

        public void ReleaseAll()
        {
            string[] addresses = new string[entries.Count]; entries.Keys.CopyTo(addresses, 0);
            foreach (Entry entry in entries.Values) { entry.Callbacks.Clear(); entry.Clip = null; }
            entries.Clear();
            foreach (string address in addresses) EndLoad(address);
        }

        protected abstract void BeginLoad(string address, Action<AudioClip> onComplete);
        protected abstract void EndLoad(string address);
    }
}
