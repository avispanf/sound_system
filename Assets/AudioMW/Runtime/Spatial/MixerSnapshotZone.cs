using UnityEngine;
using UnityEngine.Audio;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Mixer Snapshot Zone")]
    [RequireComponent(typeof(Collider))]
    public sealed class MixerSnapshotZone : MonoBehaviour
    {
        [SerializeField] private AudioMixerSnapshot insideSnapshot;
        [SerializeField] private AudioMixerSnapshot outsideSnapshot;
        [SerializeField] private float transitionSeconds = 0.5f;
        [SerializeField] private string listenerTag = "MainCamera";
        [SerializeField] private bool requireTag = true;

        private bool occupied;

        public bool IsOccupied
        {
            get { return occupied; }
        }

        public AudioMixerSnapshot InsideSnapshot
        {
            get { return insideSnapshot; }
            set { insideSnapshot = value; }
        }

        public AudioMixerSnapshot OutsideSnapshot
        {
            get { return outsideSnapshot; }
            set { outsideSnapshot = value; }
        }

        public float TransitionSeconds
        {
            get { return Mathf.Max(0f, transitionSeconds); }
            set { transitionSeconds = Mathf.Max(0f, value); }
        }

        private void Reset()
        {
            Collider attached = GetComponent<Collider>();
            if (attached != null)
            {
                attached.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Matches(other))
            {
                return;
            }

            occupied = true;
            Apply(insideSnapshot);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!Matches(other))
            {
                return;
            }

            occupied = false;
            Apply(outsideSnapshot);
        }

        public void EnterManually()
        {
            occupied = true;
            Apply(insideSnapshot);
        }

        public void ExitManually()
        {
            occupied = false;
            Apply(outsideSnapshot);
        }

        private bool Matches(Collider other)
        {
            if (other == null)
            {
                return false;
            }

            if (!requireTag || string.IsNullOrEmpty(listenerTag))
            {
                return true;
            }

            return other.CompareTag(listenerTag);
        }

        private void Apply(AudioMixerSnapshot snapshot)
        {
            if (snapshot != null)
            {
                snapshot.TransitionTo(TransitionSeconds);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Collider attached = GetComponent<Collider>();
            if (attached == null)
            {
                return;
            }

            Gizmos.color = occupied ? new Color(0.2f, 0.9f, 0.4f, 0.25f) : new Color(0.3f, 0.6f, 1f, 0.18f);
            Bounds bounds = attached.bounds;
            Gizmos.DrawCube(bounds.center, bounds.size);
        }
    }
}
