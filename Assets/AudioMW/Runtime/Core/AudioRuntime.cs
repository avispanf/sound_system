using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;

namespace AudioMW
{
    [DisallowMultipleComponent]
    public sealed class AudioRuntime : MonoBehaviour
    {
        public const int DefaultMaxVoices = 32;

        private static AudioRuntime instance;
        private static bool applicationQuitting;

        private VoicePool pool;
        private System.Random rng;
        private readonly ParameterStore globalParameters = new ParameterStore();
        private int playRequests;
        private int rejectedRequests;
        private MusicPlayer music;
        private VoiceOverDirector voiceOver;
        private readonly EventDebugger debugger = new EventDebugger();
        private readonly MixerDirector mixing = new MixerDirector();
        private readonly List<PlaybackInstance> instances = new List<PlaybackInstance>();
        private readonly Dictionary<int, PlaybackInstance> handles = new Dictionary<int, PlaybackInstance>();
        private readonly Dictionary<SoundEvent, float> lastStarted = new Dictionary<SoundEvent, float>();
        private Transform cachedListener;
        private bool virtualizationEnabled = true;
        private static readonly ProfilerMarker TickMarker = new ProfilerMarker("AudioMW.Tick");
        private static readonly ProfilerMarker PlayMarker = new ProfilerMarker("AudioMW.Play");
        private sealed class RequestContext
        {
            public PlaybackRejectionInfo Rejection;
            public readonly PlaybackPlan Plan = new PlaybackPlan();
            public readonly List<PlaybackInstance> Victims = new List<PlaybackInstance>();
        }
        private enum AdmissionScope { Event, Emitter, Group, Pool }
        private readonly Stack<PlaybackInstance> freeInstances = new Stack<PlaybackInstance>();
        public int CachedInstanceCount { get { return freeInstances.Count; } }
        private readonly Stack<RequestContext> requests = new Stack<RequestContext>();
        private readonly List<SoundEvent> expiredCooldowns = new List<SoundEvent>();
        private float nextCooldownSweep;
        public int CooldownEntryCount { get { return lastStarted.Count; } }
        private static int nextHandleId;
        private int operationDepth;
        private bool flushingReleases;
        private bool stopping;
        internal bool IsStopping { get { return stopping; } }
        private readonly Queue<ReleaseNotification> releases = new Queue<ReleaseNotification>();
        private struct ReleaseNotification
        {
            public Voice Voice;
            public System.Action<Voice> Handler;
        }

        internal void BeginOperation() { operationDepth++; }
        internal void EndOperation()
        {
            operationDepth--;
            if (operationDepth != 0 || flushingReleases) return;
            flushingReleases = true;
            try
            {
                while (releases.Count > 0)
                {
                    ReleaseNotification notification = releases.Dequeue();
                    notification.Handler(notification.Voice);
                }
            }
            finally { flushingReleases = false; }
        }

        internal void NotifyReleased(Voice voice, System.Action<Voice> handler)
        {
            releases.Enqueue(new ReleaseNotification { Voice = voice, Handler = handler });
            BeginOperation();
            EndOperation();
        }

        public static bool Exists
        {
            get { return instance != null; }
        }

        public static AudioRuntime Instance
        {
            get
            {
                if (instance == null && !applicationQuitting)
                {
                    Create();
                }

                return instance;
            }
        }

        public int VirtualVoiceCount
        {
            get { int count = 0; foreach (PlaybackInstance playback in instances) count += playback.VirtualCount; return count; }
        }

        public bool VirtualizationEnabled
        {
            get { return virtualizationEnabled; }
            set { virtualizationEnabled = value; }
        }

        public MixerDirector Mixing
        {
            get { return mixing; }
        }

        public EventDebugger Debugger
        {
            get { return debugger; }
        }

        public VoiceOverDirector VoiceOver
        {
            get { return voiceOver; }
        }

        public MusicPlayer Music
        {
            get { return music; }
        }

        public int PlayRequests
        {
            get { return playRequests; }
        }

        public int RejectedRequests
        {
            get { return rejectedRequests; }
        }

        public void ResetCounters()
        {
            playRequests = 0;
            rejectedRequests = 0;
            pool.ResetCounters();
        }

        public ParameterStore GlobalParameters
        {
            get { return globalParameters; }
        }

        public VoicePool Pool
        {
            get { return pool; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            applicationQuitting = false;
            Create();
        }

        private static void Create()
        {
            if (instance != null)
            {
                return;
            }

            GameObject go = new GameObject("[AudioMW Runtime]");
            instance = go.AddComponent<AudioRuntime>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            rng = new System.Random(unchecked(System.Environment.TickCount));
            pool = new VoicePool(transform, AudioRuntimeSettings.MaxVoices);
            music = new MusicPlayer(transform);
            voiceOver = new VoiceOverDirector(transform);
        }

        private void LateUpdate()
        {
            BeginOperation();
            using (TickMarker.Auto())
            {
                try
                {
                    SweepCooldowns();
                    pool.Tick();
                    music.Tick();
                    voiceOver.Tick();
                    mixing.Tick();
                    TickVirtualization();
                    AudioProfilerCounters.Sample(this);
                }
                finally { EndOperation(); }
            }
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                stopping = true;
                Shutdown();
                instance = null;
            }
        }

        public Voice Play(SoundEvent soundEvent, Vector3 position, Transform attach, ParameterStore initialParameters = null)
        {
            PlaybackInstance playback = StartPlayback(soundEvent, position, attach, initialParameters);
            return playback != null ? playback.Primary : null;
        }

        private PlaybackInstance StartPlayback(SoundEvent soundEvent, Vector3 position, Transform attach, ParameterStore initialParameters)
        {
            RequestContext request = requests.Count > 0 ? requests.Pop() : new RequestContext();
            BeginOperation();
            using (PlayMarker.Auto())
            {
                try
                {
                    if (stopping)
                    {
                        Reject(soundEvent, PlaybackOutcome.RejectedRuntimeStopping, position, attach);
                        return null;
                    }
                    if (soundEvent == null)
                    {
                        Reject(null, PlaybackOutcome.RejectedNullEvent, position, attach);
                        return null;
                    }
                    playRequests++;
                    CleanupInstances();
                    float last;
                    if (soundEvent.CooldownSeconds > 0f && lastStarted.TryGetValue(soundEvent, out last) && Time.unscaledTime - last < soundEvent.CooldownSeconds)
                    {
                        Reject(soundEvent, PlaybackOutcome.RejectedCooldown, position, attach, new PlaybackRejectionInfo { HasCooldown = true, RemainingSeconds = soundEvent.CooldownSeconds - (Time.unscaledTime - last) });
                        return null;
                    }
                    PlaybackPlan plan = request.Plan;
                    if (!plan.Build(soundEvent, rng, initialParameters, globalParameters, pool.MaxVoices))
                    {
                        Reject(soundEvent, plan.Failure, position, attach);
                        return null;
                    }
                    request.Rejection = default(PlaybackRejectionInfo);
                    List<PlaybackInstance> victims = request.Victims;
                    bool admitted = SelectVictims(victims, soundEvent, attach, AdmissionScope.Event, soundEvent.MaxInstances, soundEvent.LimitPolicy, request) &&
                    SelectVictims(victims, soundEvent, attach, AdmissionScope.Emitter, soundEvent.MaxInstancesPerEmitter, soundEvent.LimitPolicy, request);
                    ConcurrencyGroup group = soundEvent.ConcurrencyGroup;
                    if (admitted && group != null)
                    admitted = SelectVictims(victims, soundEvent, attach, AdmissionScope.Group, group.MaxInstances, group.Policy, request);
                    if (!admitted)
                    {
                        Reject(soundEvent, PlaybackOutcome.RejectedInstanceLimit, position, attach, request.Rejection);
                        return null;
                    }
                    int available = pool.FreeCount;
                    foreach (PlaybackInstance victim in victims) available += victim.ReservedCount;
                    while (available < plan.Layers.Count)
                    {
                        PlaybackInstance victim = ChooseVictim(victims, soundEvent, attach, AdmissionScope.Pool, soundEvent.LimitPolicy);
                        if (victim == null)
                        {
                            Reject(soundEvent, PlaybackOutcome.RejectedNoVoice, position, attach, new PlaybackRejectionInfo { HasVoiceCapacity = true, RequestedVoices = plan.Layers.Count, AvailableVoices = available, VoiceCapacity = pool.MaxVoices });
                            return null;
                        }
                        victims.Add(victim);
                        available += victim.ReservedCount;
                    }
                    foreach (PlaybackInstance victim in victims) { victim.Stop(); pool.RecordSteal(); }
                    CleanupInstances();
                    PlaybackInstance playback = freeInstances.Count > 0 ? freeInstances.Pop() : new PlaybackInstance(this);
                    playback.Initialize(soundEvent, attach, initialParameters);
                    playback.Start(plan, pool, position);
                    instances.Add(playback);
                    lastStarted[soundEvent] = Time.unscaledTime;
                    debugger.Record(soundEvent, PlaybackOutcome.Played, playback.Primary, position, attach != null);
                    return playback;
                }
                finally { request.Plan.Clear(); request.Victims.Clear(); requests.Push(request); EndOperation(); }
            }
        }

        private bool SelectVictims(List<PlaybackInstance> victims, SoundEvent root, Transform attach, AdmissionScope scope, int limit, VoiceLimitPolicy policy, RequestContext request)
        {
            if (limit <= 0) return true;
            int count = 0;
            foreach (PlaybackInstance item in instances) if (!victims.Contains(item) && Matches(item, root, attach, scope)) count++;
            while (count >= limit)
            {
                PlaybackInstance victim = ChooseVictim(victims, root, attach, scope, policy);
                if (victim == null)
                {
                    int actual = 0;
                    foreach (PlaybackInstance item in instances) if (Matches(item, root, attach, scope)) actual++;
                    request.Rejection = new PlaybackRejectionInfo { HasInstanceLimit = true, CurrentCount = actual, Limit = limit,
                        Scope = scope == AdmissionScope.Event ? "event" : scope == AdmissionScope.Emitter ? "emitter" : "concurrency group" };
                    return false;
                }
                victims.Add(victim);
                count--;
            }
            return true;
        }

        private PlaybackInstance ChooseVictim(List<PlaybackInstance> excluded, SoundEvent root, Transform attach, AdmissionScope scope, VoiceLimitPolicy policy)
        {
            if (policy == VoiceLimitPolicy.RejectNewest) return null;
            PlaybackInstance best = null;
            foreach (PlaybackInstance candidate in instances)
            {
                if (excluded.Contains(candidate) || !Matches(candidate, root, attach, scope)) continue;
                if (best == null ||
                (policy == VoiceLimitPolicy.StealOldest && candidate.Started < best.Started) ||
                (policy == VoiceLimitPolicy.StealQuietest && candidate.Volume < best.Volume) ||
                (policy == VoiceLimitPolicy.StealLowestPriority && candidate.Event.Priority > best.Event.Priority)) best = candidate;
            }
            return best;
        }

        private static bool Matches(PlaybackInstance candidate, SoundEvent root, Transform attach, AdmissionScope scope)
        {
            switch (scope)
            {
                case AdmissionScope.Event: return candidate.Event == root;
                case AdmissionScope.Emitter: return candidate.Event == root && candidate.Attach == attach;
                case AdmissionScope.Group: return candidate.Event != null && candidate.Event.ConcurrencyGroup == root.ConcurrencyGroup;
                default: return candidate.ReservedCount > 0;
            }
        }

        private void SweepCooldowns()
        {
            float now = Time.unscaledTime;
            if (now < nextCooldownSweep) return;
            nextCooldownSweep = now + 1f;
            foreach (KeyValuePair<SoundEvent, float> entry in lastStarted)
            if (entry.Key == null || now - entry.Value >= entry.Key.CooldownSeconds) expiredCooldowns.Add(entry.Key);
            foreach (SoundEvent key in expiredCooldowns) lastStarted.Remove(key);
            expiredCooldowns.Clear();
        }

        private void Reject(SoundEvent soundEvent, PlaybackOutcome outcome, Vector3 position, Transform attach, PlaybackRejectionInfo rejection = default(PlaybackRejectionInfo))
        {
            rejectedRequests++;
            debugger.Record(soundEvent, outcome, null, position, attach != null, rejection);
        }

        public SoundHandle PlayTracked(SoundEvent soundEvent, Vector3 position, Transform attach, ParameterStore initialParameters = null)
        {
            BeginOperation();
            try
            {
                PlaybackInstance playback = StartPlayback(soundEvent, position, attach, initialParameters);
                if (playback == null) return SoundHandle.None;
                do { nextHandleId = nextHandleId == int.MaxValue ? 1 : nextHandleId + 1; } while (handles.ContainsKey(nextHandleId));
                playback.HandleId = nextHandleId;
                playback.Primary.HandleId = nextHandleId;
                handles[nextHandleId] = playback;
                return new SoundHandle(nextHandleId);
            }
            finally { EndOperation(); }
        }

        public bool IsHandlePlaying(int id)
        {
            PlaybackInstance playback;
            return handles.TryGetValue(id, out playback) && playback.IsPlaying;
        }

        public bool IsHandleVirtual(int id)
        {
            PlaybackInstance playback;
            return handles.TryGetValue(id, out playback) && playback.VirtualCount > 0;
        }

        public Voice GetHandleVoice(int id)
        {
            PlaybackInstance playback;
            return handles.TryGetValue(id, out playback) ? playback.ActiveVoice : null;
        }

        public void SetHandleParameter(int id, SoundParameter parameter, float value)
        {
            PlaybackInstance playback;
            if (handles.TryGetValue(id, out playback)) playback.SetParameter(parameter, value);
        }

        public void StopHandle(int id)
        {
            BeginOperation();
            try
            {
                PlaybackInstance playback;
                if (handles.TryGetValue(id, out playback)) playback.Stop();
                handles.Remove(id);
                CleanupInstances();
            }
            finally { EndOperation(); }
        }

        public int TrackedHandleCount
        {
            get { CleanupInstances(); return handles.Count; }
        }

        public void ClearHandles()
        {
            foreach (PlaybackInstance playback in handles.Values) playback.HandleId = 0;
            handles.Clear();
        }

        private void TickVirtualization()
        {
            Transform listener = null;
            if (virtualizationEnabled)
            foreach (PlaybackInstance playback in instances)
            if (playback.NeedsListener) { listener = ResolveListener(); break; }
            foreach (PlaybackInstance playback in instances)
            playback.Tick(pool, listener, virtualizationEnabled, Time.unscaledDeltaTime);
            CleanupInstances();
        }

        private void CleanupInstances()
        {
            BeginOperation();
            try
            {
                for (int i = instances.Count - 1; i >= 0; i--)
                {
                    PlaybackInstance playback = instances[i];
                    if (playback.IsPlaying) continue;
                    playback.Stop();
                    if (playback.HandleId != 0) handles.Remove(playback.HandleId);
                    instances.RemoveAt(i);
                    if (freeInstances.Count < pool.MaxVoices) freeInstances.Push(playback);
                }
            }
            finally { EndOperation(); }
        }

        public void ClearVirtualVoices()
        {
            BeginOperation();
            try
            {
                foreach (PlaybackInstance playback in instances) playback.ClearVirtual();
                CleanupInstances();
            }
            finally { EndOperation(); }
        }

        private Transform ResolveListener()
        {
            if (cachedListener != null && cachedListener.gameObject.activeInHierarchy && cachedListener.GetComponent<AudioListener>().enabled)
            {
                return cachedListener;
            }

#if UNITY_2023_1_OR_NEWER
            AudioListener first = FindFirstObjectByType<AudioListener>();
            if (first == null) return null;
            if (first.isActiveAndEnabled) { cachedListener = first.transform; return cachedListener; }
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
            AudioListener first = FindObjectOfType<AudioListener>();
            if (first == null) return null;
            if (first.isActiveAndEnabled) { cachedListener = first.transform; return cachedListener; }
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
#endif
            cachedListener = null;
            foreach (AudioListener listener in listeners)
            if (listener.isActiveAndEnabled) { cachedListener = listener.transform; break; }
            return cachedListener;
        }

        public void RebuildPool(int maxVoices, int prewarm)
        {
            BeginOperation();
            try
            {
                StopAll();
                pool.DestroyAll();
                freeInstances.Clear();
                requests.Clear();
                pool = new VoicePool(transform, maxVoices);
                pool.Prewarm(prewarm);
            }
            finally { EndOperation(); }
        }

        public void StopAll()
        {
            BeginOperation();
            try
            {
                foreach (PlaybackInstance playback in instances)
                {
                    playback.Stop();
                    if (freeInstances.Count < pool.MaxVoices) freeInstances.Push(playback);
                }
                instances.Clear();
                handles.Clear();
                lastStarted.Clear();
                pool.StopAll();
            }
            finally { EndOperation(); }
        }

        public void Shutdown()
        {
            bool wasStopping = stopping;
            stopping = true;
            BeginOperation();
            try
            {
                StopAll();
                pool.DestroyAll();
                music.Stop();
                voiceOver.Reset();
                mixing.Clear();
                globalParameters.Clear();
                SoundBank.UnloadAll();
            }
            finally
            {
                try { EndOperation(); }
                finally { stopping = wasStopping; }
            }
        }
    }
}
