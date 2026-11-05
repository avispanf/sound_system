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
        [System.NonSerialized] private bool manualLoad;
        [System.NonSerialized] private bool manualStreams;
        [System.NonSerialized] private int scopeCount;
        [System.NonSerialized] private int scopeGeneration;
        public int ScopeCount { get { return scopeCount; } }

        private sealed class Lease : System.IDisposable
        {
            private SoundBank bank;
            private readonly int generation;
            internal Lease(SoundBank bank) { this.bank = bank; generation = bank.scopeGeneration; }
            public void Dispose()
            {
                SoundBank owned = bank;
                bank = null;
                if (owned != null && generation == owned.scopeGeneration) owned.ReleaseScope();
            }
        }

        public System.IDisposable Acquire(IAudioAssetLoader loader = null)
        {
            if (AudioRuntime.Exists && AudioRuntime.Instance.IsStopping) return null;
            loader = loader ?? AudioSystem.AssetLoader;
            foreach (Request request in requests.Values)
                if (!ReferenceEquals(request.Loader, loader))
                    throw new System.InvalidOperationException("A scoped bank must keep its original audio asset loader until all owners release it.");
            Lease lease = new Lease(this);
            scopeCount++;
            try { EnsureLoaded(); EnsureStreamed(loader); return lease; }
            catch { lease.Dispose(); throw; }
        }

        private void ReleaseScope()
        {
            scopeCount--;
            if (scopeCount > 0) return;
            if (!manualStreams) UnloadRequests();
            if (!manualLoad) UnloadDirect();
        }
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
            manualLoad = true;
            EnsureLoaded();
        }

        private void EnsureLoaded()
        {
            if (loaded) return;
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
                AudioClipLifetime.RetainBank(clip);
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
            public AudioClipLifetime.StreamOwner Lifetime;
            public readonly List<ClipReference> References = new List<ClipReference>();
        }
        [System.NonSerialized] private readonly Dictionary<string, Request> requests = new Dictionary<string, Request>(System.StringComparer.Ordinal);
        private static readonly HashSet<SoundBank> activeBanks = new HashSet<SoundBank>();

        public void LoadStreamed(IAudioAssetLoader loader)
        {
            if (AudioRuntime.Exists && AudioRuntime.Instance.IsStopping) return;
            if (loader == null) return;
            if (scopeCount > 0)
                foreach (Request request in requests.Values)
                    if (!ReferenceEquals(request.Loader, loader))
                        throw new System.InvalidOperationException("Cannot replace the audio asset loader of a scoped bank.");
            manualStreams = true;
            EnsureStreamed(loader);
        }

        private void EnsureStreamed(IAudioAssetLoader loader)
        {
            List<ClipReference> references = CollectReferences();
            IAudioAssetLoader previousLoader = null;
            foreach (Request previous in requests.Values) { previousLoader = previous.Loader; break; }
            if (previousLoader != null && !ReferenceEquals(previousLoader, loader)) UnloadRequests();
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
            request.Lifetime = AudioClipLifetime.RetainStream(clip, request.Loader, request.Address);
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
            if (request.Lifetime != null) request.Lifetime.Release();
            else request.Loader.Release(request.Address);
            request.Lifetime = null;
        }

        public void UnloadStreamed(IAudioAssetLoader loader)
        {
            manualStreams = false;
            if (scopeCount == 0) UnloadRequests();
        }

        private void UnloadRequests()
        {
            Request[] owned = new Request[requests.Count]; requests.Values.CopyTo(owned, 0);
            foreach (Request request in owned) RemoveRequest(request);
        }

        private void ForceUnload()
        {
            scopeGeneration++;
            scopeCount = 0;
            manualStreams = false;
            manualLoad = false;
            UnloadRequests();
            UnloadDirect();
        }

        private void OnDestroy() { ForceUnload(); }

        public static void UnloadAll()
        {
            SoundBank[] banks = new SoundBank[activeBanks.Count]; activeBanks.CopyTo(banks);
            foreach (SoundBank bank in banks) if (bank != null) bank.ForceUnload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetSession() { UnloadAll(); activeBanks.Clear(); }

        public void Unload()
        {
            manualLoad = false;
            if (scopeCount == 0) UnloadDirect();
        }

        private void UnloadDirect()
        {
            if (!loaded)
            {
                return;
            }

            for (int i = 0; i < loadedClips.Count; i++)
            {
                AudioClipLifetime.ReleaseBank(loadedClips[i]);
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
