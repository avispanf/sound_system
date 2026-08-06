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
        }

        private void LateUpdate()
        {
            pool.Tick();
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
                return null;
            }

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

        public void StopAll()
        {
            pool.StopAll();
        }

        public void Shutdown()
        {
            pool.StopAll();
            pool.DestroyAll();
            globalParameters.Clear();
        }
    }
}
