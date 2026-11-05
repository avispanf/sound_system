using UnityEngine;
using System.Collections.Generic;

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
        private int nextHandleId;

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
            pool.Tick();
            music.Tick();
            voiceOver.Tick();
            mixing.Tick();
            TickVirtualization();
            AudioProfilerCounters.Sample(this);
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
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
                Reject(soundEvent, PlaybackOutcome.RejectedCooldown, position, attach);
                return null;
            }
            PlaybackPlan plan = new PlaybackPlan();
            if (!plan.Build(soundEvent, rng, initialParameters, globalParameters, pool.MaxVoices))
            {
                Reject(soundEvent, plan.Failure, position, attach);
                return null;
            }
            List<PlaybackInstance> victims = new List<PlaybackInstance>();
            bool admitted = SelectVictims(victims, p => p.Event == soundEvent, soundEvent.MaxInstances, soundEvent.LimitPolicy) &&
                SelectVictims(victims, p => p.Event == soundEvent && p.Attach == attach, soundEvent.MaxInstancesPerEmitter, soundEvent.LimitPolicy);
            ConcurrencyGroup group = soundEvent.ConcurrencyGroup;
            if (admitted && group != null)
                admitted = SelectVictims(victims, p => p.Event.ConcurrencyGroup == group, group.MaxInstances, group.Policy);
            if (!admitted)
            {
                Reject(soundEvent, PlaybackOutcome.RejectedInstanceLimit, position, attach);
                return null;
            }
            int available = pool.FreeCount;
            foreach (PlaybackInstance victim in victims) available += victim.ReservedCount;
            while (available < plan.Layers.Count)
            {
                PlaybackInstance victim = ChooseVictim(victims, p => p.ReservedCount > 0, soundEvent.LimitPolicy);
                if (victim == null)
                {
                    Reject(soundEvent, PlaybackOutcome.RejectedNoVoice, position, attach);
                    return null;
                }
                victims.Add(victim);
                available += victim.ReservedCount;
            }
            foreach (PlaybackInstance victim in victims) { victim.Stop(); pool.RecordSteal(); }
            CleanupInstances();
            PlaybackInstance playback = new PlaybackInstance(soundEvent, attach, initialParameters);
            playback.Start(plan, pool, position);
            instances.Add(playback);
            lastStarted[soundEvent] = Time.unscaledTime;
            debugger.Record(soundEvent, PlaybackOutcome.Played, playback.Primary, position, attach != null);
            return playback;
        }

        private bool SelectVictims(List<PlaybackInstance> victims, System.Predicate<PlaybackInstance> match, int limit, VoiceLimitPolicy policy)
        {
            if (limit <= 0) return true;
            int count = 0;
            foreach (PlaybackInstance item in instances) if (!victims.Contains(item) && match(item)) count++;
            while (count >= limit)
            {
                PlaybackInstance victim = ChooseVictim(victims, match, policy);
                if (victim == null) return false;
                victims.Add(victim);
                count--;
            }
            return true;
        }

        private PlaybackInstance ChooseVictim(List<PlaybackInstance> excluded, System.Predicate<PlaybackInstance> match, VoiceLimitPolicy policy)
        {
            if (policy == VoiceLimitPolicy.RejectNewest) return null;
            PlaybackInstance best = null;
            foreach (PlaybackInstance candidate in instances)
            {
                if (excluded.Contains(candidate) || !match(candidate)) continue;
                if (best == null ||
                    (policy == VoiceLimitPolicy.StealOldest && candidate.Started < best.Started) ||
                    (policy == VoiceLimitPolicy.StealQuietest && candidate.Volume < best.Volume) ||
                    (policy == VoiceLimitPolicy.StealLowestPriority && candidate.Event.Priority > best.Event.Priority)) best = candidate;
            }
            return best;
        }

        private void Reject(SoundEvent soundEvent, PlaybackOutcome outcome, Vector3 position, Transform attach)
        {
            rejectedRequests++;
            debugger.Record(soundEvent, outcome, null, position, attach != null);
        }

        public SoundHandle PlayTracked(SoundEvent soundEvent, Vector3 position, Transform attach, ParameterStore initialParameters = null)
        {
            PlaybackInstance playback = StartPlayback(soundEvent, position, attach, initialParameters);
            if (playback == null) return SoundHandle.None;
            do { nextHandleId = nextHandleId == int.MaxValue ? 1 : nextHandleId + 1; } while (handles.ContainsKey(nextHandleId));
            playback.HandleId = nextHandleId;
            playback.Primary.HandleId = nextHandleId;
            handles[nextHandleId] = playback;
            return new SoundHandle(nextHandleId);
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
            PlaybackInstance playback;
            if (handles.TryGetValue(id, out playback)) playback.Stop();
            handles.Remove(id);
            CleanupInstances();
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
            Transform listener = ResolveListener();
            foreach (PlaybackInstance playback in instances)
                playback.Tick(pool, listener, virtualizationEnabled, Time.unscaledDeltaTime);
            CleanupInstances();
        }

        private void CleanupInstances()
        {
            for (int i = instances.Count - 1; i >= 0; i--)
            {
                PlaybackInstance playback = instances[i];
                if (playback.IsPlaying) continue;
                playback.Stop();
                if (playback.HandleId != 0) handles.Remove(playback.HandleId);
                instances.RemoveAt(i);
            }
        }

        public void ClearVirtualVoices()
        {
            foreach (PlaybackInstance playback in instances) playback.ClearVirtual();
            CleanupInstances();
        }

        private Transform ResolveListener()
        {
            if (cachedListener != null && cachedListener.gameObject.activeInHierarchy && cachedListener.GetComponent<AudioListener>().enabled)
            {
                return cachedListener;
            }

#if UNITY_2023_1_OR_NEWER
            AudioListener[] listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
#else
            AudioListener[] listeners = FindObjectsOfType<AudioListener>();
#endif
            cachedListener = null;
            foreach (AudioListener listener in listeners)
                if (listener.isActiveAndEnabled) { cachedListener = listener.transform; break; }
            return cachedListener;
        }

        public void RebuildPool(int maxVoices, int prewarm)
        {
            StopAll();
            pool.DestroyAll();
            pool = new VoicePool(transform, maxVoices);
            pool.Prewarm(prewarm);
        }

        public void StopAll()
        {
            foreach (PlaybackInstance playback in instances) playback.Stop();
            instances.Clear();
            handles.Clear();
            lastStarted.Clear();
            pool.StopAll();
        }

        public void Shutdown()
        {
            StopAll();
            pool.DestroyAll();
            music.Stop();
            voiceOver.Stop();
            mixing.Clear();
            globalParameters.Clear();
        }
    }
}
