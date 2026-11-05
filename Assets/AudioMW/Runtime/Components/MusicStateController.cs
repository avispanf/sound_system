using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Music State Controller")]
    public sealed class MusicStateController : MonoBehaviour
    {
        [SerializeField] private MusicStateProfile profile;
        [SerializeField] private string initialState;
        [SerializeField] private bool playOnStart;

        public MusicStateProfile Profile { get { return profile; } set { profile = value; } }

        private void Start()
        {
            if (playOnStart) SetState(initialState);
        }

        public void SetState(string state)
        {
            TrySetState(state);
        }

        public bool TrySetState(string state)
        {
            return profile != null && profile.Request(AudioSystem.Music, state);
        }
    }
}
