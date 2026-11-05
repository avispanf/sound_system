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
            get
            {
                if (streamedClips != null && streamedClips.Length > 0) return true;
                foreach (ClipReference reference in CollectReferences()) if (reference != null && reference.IsAddressed) return true;
                return false;
            }
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
            if (AudioRuntime.Exists && AudioRuntime.Instance.IsStopping) return;
            if (loaded)
            {
                return;
            }

            loadedClips.Clear();

            for (int i = 0; i < events.Length; i++)
            {
                CollectClips(events[i], loadedClips);
            }

            foreach (ClipReference reference in streamedClips)
                if (reference != null && reference.DirectClip != null && !loadedClips.Contains(reference.DirectClip)) loadedClips.Add(reference.DirectClip);

            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClip clip = loadedClips[i];
                if (clip != null && clip.loadState == AudioDataLoadState.Unloaded)
                {
                    clip.LoadAudioData();
                }
            }

            foreach (AudioClip clip in loadedClips)
            {
                int count; clipUsers.TryGetValue(clip, out count); clipUsers[clip] = count + 1;
            }
            loaded = true;
            activeBanks.Add(this);
        }

        private sealed class Request
        {
            public IAudioAssetLoader Loader;
            public string Address;
            public bool Active = true;
            public bool Pending = true;
            public AudioClip Clip;
            public readonly List<ClipReference> References = new List<ClipReference>();
        }
        [System.NonSerialized] private readonly Dictionary<string, Request> requests = new Dictionary<string, Request>(System.StringComparer.Ordinal);
        private static readonly HashSet<SoundBank> activeBanks = new HashSet<SoundBank>();
        private static readonly Dictionary<AudioClip, int> clipUsers = new Dictionary<AudioClip, int>();

        public void LoadStreamed(IAudioAssetLoader loader)
        {
            if (AudioRuntime.Exists && AudioRuntime.Instance.IsStopping) return;
            if (loader == null) return;
            List<ClipReference> references = CollectReferences();
            IAudioAssetLoader previousLoader = null;
            foreach (Request previous in requests.Values) { previousLoader = previous.Loader; break; }
            if (previousLoader != null && !ReferenceEquals(previousLoader, loader)) UnloadStreamed(null);
            for (int i = 0; i < references.Count; i++)
            {
                ClipReference reference = references[i];
                if (reference == null || !reference.IsAddressed || !loader.CanLoad(reference.Address)) continue;
                Request request;
                if (requests.TryGetValue(reference.Address, out request))
                {
                    if (!request.References.Contains(reference)) request.References.Add(reference);
                    if (!request.Pending && request.Clip != null) reference.ResolveFor(request, request.Clip);
                    continue;
                }
                request = new Request { Loader = loader, Address = reference.Address };
                request.References.Add(reference);
                requests.Add(request.Address, request);
                activeBanks.Add(this);
                PendingStreamCount++;
                Request captured = request;
                try { loader.LoadAsync(request.Address, (address, clip) => Complete(captured, clip)); }
                catch
                {
                    if (captured.Active) RemoveRequest(captured);
                    throw;
                }
            }
        }

        private void Complete(Request request, AudioClip clip)
        {
            if (this == null || !request.Active || !request.Pending) return;
            request.Pending = false;
            PendingStreamCount--;
            if (clip == null) { RemoveRequest(request); return; }
            request.Clip = clip;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            foreach (ClipReference reference in request.References)
                if (reference.IsAddressed && reference.Address == request.Address) reference.ResolveFor(request, clip);
        }

        private void RemoveRequest(Request request)
        {
            if (!request.Active) return;
            request.Active = false;
            requests.Remove(request.Address);
            if (request.Pending) { PendingStreamCount--; request.Pending = false; }
            foreach (ClipReference reference in request.References) reference.ReleaseFor(request);
            request.References.Clear(); request.Clip = null;
            if (!loaded && requests.Count == 0) activeBanks.Remove(this);
            request.Loader.Release(request.Address);
        }

        public void UnloadStreamed(IAudioAssetLoader loader)
        {
            Request[] owned = new Request[requests.Count]; requests.Values.CopyTo(owned, 0);
            foreach (Request request in owned) RemoveRequest(request);
        }

        private void OnDestroy() { UnloadStreamed(null); Unload(); }

        public static void UnloadAll()
        {
            SoundBank[] banks = new SoundBank[activeBanks.Count]; activeBanks.CopyTo(banks);
            foreach (SoundBank bank in banks) if (bank != null) { bank.UnloadStreamed(null); bank.Unload(); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { UnloadAll(); activeBanks.Clear(); clipUsers.Clear(); }

        public void Unload()
        {
            if (!loaded)
            {
                return;
            }

            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClip clip = loadedClips[i];
                int count;
                if (!ReferenceEquals(clip, null) && clipUsers.TryGetValue(clip, out count))
                {
                    if (count > 1) clipUsers[clip] = count - 1;
                    else
                    {
                        clipUsers.Remove(clip);
                        if (clip != null && clip.loadState == AudioDataLoadState.Loaded) clip.UnloadAudioData();
                    }
                }
            }

            loadedClips.Clear();
            loaded = false;
            if (requests.Count == 0) activeBanks.Remove(this);
        }

        public bool IsFullyLoaded()
        {
            if (!loaded || PendingStreamCount > 0) return false;
            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClip clip = loadedClips[i];
                if (clip != null && clip.loadState != AudioDataLoadState.Loaded)
                {
                    return false;
                }
            }

            foreach (ClipReference reference in CollectReferences()) if (reference != null && reference.IsAddressed && (!reference.IsResolved || reference.Clip.loadState != AudioDataLoadState.Loaded)) return false;
            return loaded;
        }

        public static void CollectClips(SoundEvent soundEvent, List<AudioClip> target)
        {
            if (soundEvent == null || target == null)
            {
                return;
            }

            HashSet<SoundEvent> visited = new HashSet<SoundEvent>();
            HashSet<AudioClip> known = new HashSet<AudioClip>(target);
            Stack<SoundEvent> pending = new Stack<SoundEvent>(); pending.Push(soundEvent);
            while (pending.Count > 0)
            {
                SoundEvent item = pending.Pop();
                if (item == null || !visited.Add(item)) continue;
                foreach (AudioClip clip in item.Clips) if (clip != null && known.Add(clip)) target.Add(clip);
                foreach (ClipReference reference in item.ClipReferences)
                    if (reference != null && reference.DirectClip != null && known.Add(reference.DirectClip)) target.Add(reference.DirectClip);
                foreach (BlendLayer layer in item.BlendLayers)
                    if (layer != null && layer.Clip != null && known.Add(layer.Clip)) target.Add(layer.Clip);
                for (int i = item.Children.Length - 1; i >= 0; i--)
                    if (item.Children[i] != null && item.Children[i].Event != null) pending.Push(item.Children[i].Event);
            }
        }

        private List<ClipReference> CollectReferences()
        {
            List<ClipReference> result = new List<ClipReference>(streamedClips);
            HashSet<SoundEvent> visited = new HashSet<SoundEvent>();
            Stack<SoundEvent> pending = new Stack<SoundEvent>();
            foreach (SoundEvent item in events) if (item != null) pending.Push(item);
            while (pending.Count > 0)
            {
                SoundEvent item = pending.Pop(); if (item == null || !visited.Add(item)) continue;
                foreach (ClipReference reference in item.ClipReferences) if (reference != null) result.Add(reference);
                foreach (SoundEventChild child in item.Children) if (child != null && child.Event != null) pending.Push(child.Event);
            }
            return result;
        }

        public static SoundBank CreateRuntime(params SoundEvent[] soundEvents)
        {
            SoundBank bank = CreateInstance<SoundBank>();
            bank.events = soundEvents ?? new SoundEvent[0];
            return bank;
        }
    }
}
