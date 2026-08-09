using UnityEngine;

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
        private readonly System.Collections.Generic.List<VirtualVoice> virtualVoices = new System.Collections.Generic.List<VirtualVoice>();
        private Transform cachedListener;
        private bool virtualizationEnabled = true;

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
            get { return virtualVoices.Count; }
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

        public Voice Play(SoundEvent soundEvent, Vector3 position, Transform attach)
        {
            if (soundEvent == null)
            {
                rejectedRequests++;
                debugger.Record(null, PlaybackOutcome.RejectedNullEvent, null, position, attach != null);
                return null;
            }

            playRequests++;

            bool blend = soundEvent.IsBlendContainer;
            Voice result = blend
                ? PlayBlend(soundEvent, position, attach)
                : PlaySimple(soundEvent, position, attach);

            if (result == null)
            {
                rejectedRequests++;

                PlaybackOutcome outcome;
                if (blend)
                {
                    outcome = PlaybackOutcome.RejectedNoValidLayers;
                }
                else if (!soundEvent.HasClips)
                {
                    outcome = PlaybackOutcome.RejectedNoClips;
                }
                else
                {
                    outcome = PlaybackOutcome.RejectedNoVoice;
                }

                debugger.Record(soundEvent, outcome, null, position, attach != null);
            }
            else
            {
                debugger.Record(soundEvent, PlaybackOutcome.Played, result, position, attach != null);
            }

            return result;
        }

        private Voice PlaySimple(SoundEvent soundEvent, Vector3 position, Transform attach)
        {
            PlaybackParameters parameters = soundEvent.Resolve(rng);
            if (!parameters.IsValid)
            {
                return null;
            }

            Voice voice = pool.Acquire();
            if (voice == null)
            {
                return null;
            }

            voice.Play(soundEvent, parameters, position, attach);
            return voice;
        }

        private Voice PlayBlend(SoundEvent soundEvent, Vector3 position, Transform attach)
        {
            BlendLayer[] layers = soundEvent.BlendLayers;
            Voice primary = null;

            for (int i = 0; i < layers.Length; i++)
            {
                BlendLayer layer = layers[i];
                if (layer == null || !layer.IsValid)
                {
                    continue;
                }

                Voice voice = pool.Acquire();
                if (voice == null)
                {
                    break;
                }

                PlaybackParameters parameters = new PlaybackParameters
                {
                    Clip = layer.Clip,
                    Volume = soundEvent.Volume,
                    Pitch = soundEvent.Pitch,
                    IsValid = true
                };

                voice.BlendLayer = layer;
                voice.Play(soundEvent, parameters, position, attach);

                if (primary == null)
                {
                    primary = voice;
                }
                else
                {
                    primary.AddFollower(voice);
                }
            }

            return primary;
        }

        private void TickVirtualization()
        {
            if (!virtualizationEnabled)
            {
                return;
            }

            Transform listener = ResolveListener();

            if (listener == null)
            {
                return;
            }

            Vector3 listenerPosition = listener.position;
            float delta = Time.unscaledDeltaTime;

            DemoteDistantVoices(listenerPosition);
            PromoteNearVirtualVoices(listenerPosition, delta);
        }

        private void DemoteDistantVoices(Vector3 listenerPosition)
        {
            System.Collections.Generic.IReadOnlyList<Voice> voices = pool.Voices;

            for (int i = 0; i < voices.Count; i++)
            {
                Voice voice = voices[i];

                if (!voice.IsActive || voice.CurrentEvent == null || !voice.CurrentEvent.AllowVirtualization)
                {
                    continue;
                }

                if (voice.Source.spatialBlend <= 0f)
                {
                    continue;
                }

                float range = voice.Source.maxDistance;
                float distance = Vector3.Distance(voice.Source.transform.position, listenerPosition);

                if (!VirtualizationPolicy.ShouldVirtualize(false, distance, range))
                {
                    continue;
                }

                VirtualVoice virtualVoice = new VirtualVoice();
                virtualVoice.Configure(
                    voice.CurrentEvent,
                    voice.CurrentParameters,
                    voice.Source.transform.position,
                    voice.AttachTarget,
                    range,
                    voice.ElapsedSeconds);

                virtualVoices.Add(virtualVoice);
                voice.Demote();
            }
        }

        private void PromoteNearVirtualVoices(Vector3 listenerPosition, float delta)
        {
            for (int i = virtualVoices.Count - 1; i >= 0; i--)
            {
                VirtualVoice virtualVoice = virtualVoices[i];
                virtualVoice.Advance(delta);

                if (virtualVoice.IsExpired || virtualVoice.TargetLost)
                {
                    virtualVoices.RemoveAt(i);
                    continue;
                }

                if (!virtualVoice.ShouldBecomeReal(listenerPosition, VirtualizationPolicy.DefaultHysteresis))
                {
                    continue;
                }

                Voice voice = pool.Acquire();

                if (voice == null)
                {
                    continue;
                }

                voice.Play(
                    virtualVoice.Event,
                    virtualVoice.Parameters,
                    virtualVoice.CurrentPosition,
                    virtualVoice.Attach,
                    virtualVoice.PlaybackOffset);

                virtualVoices.RemoveAt(i);
            }
        }

        public void ClearVirtualVoices()
        {
            virtualVoices.Clear();
        }

        private Transform ResolveListener()
        {
            if (cachedListener != null)
            {
                return cachedListener;
            }

#if UNITY_2023_1_OR_NEWER
            AudioListener listener = FindFirstObjectByType<AudioListener>();
#else
            AudioListener listener = FindObjectOfType<AudioListener>();
#endif
            cachedListener = listener != null ? listener.transform : null;
            return cachedListener;
        }

        public void RebuildPool(int maxVoices, int prewarm)
        {
            pool.StopAll();
            pool.DestroyAll();
            pool = new VoicePool(transform, maxVoices);
            pool.Prewarm(prewarm);
        }

        public void StopAll()
        {
            pool.StopAll();
        }

        public void Shutdown()
        {
            pool.StopAll();
            pool.DestroyAll();
            music.Stop();
            voiceOver.Stop();
            mixing.Clear();
            globalParameters.Clear();
        }
    }
}
