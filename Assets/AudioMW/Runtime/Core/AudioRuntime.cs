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
            pool = new VoicePool(transform, DefaultMaxVoices);
            music = new MusicPlayer(transform);
            voiceOver = new VoiceOverDirector(transform);
        }

        private void LateUpdate()
        {
            pool.Tick();
            music.Tick();
            voiceOver.Tick();
            mixing.Tick();
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
