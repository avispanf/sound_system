using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("AudioMW/Sound Spline")]
    public sealed class SoundSpline : MonoBehaviour
    {
        [SerializeField] private SoundEvent soundEvent;
        [SerializeField] private Vector3[] localPoints = new Vector3[0];
        [SerializeField] private bool closed;
        [SerializeField] private bool playOnEnable = true;
        [SerializeField] private float followSmoothing = 0.15f;

        private readonly List<Vector3> worldPoints = new List<Vector3>();
        private Transform follower;
        private Voice voice;

        public SoundEvent Event
        {
            get { return soundEvent; }
            set { soundEvent = value; }
        }

        public bool Closed
        {
            get { return closed; }
            set { closed = value; }
        }

        public Voice ActiveVoice
        {
            get { return voice; }
        }

        public Transform Follower
        {
            get { return follower; }
        }

        public int PointCount
        {
            get { return localPoints != null ? localPoints.Length : 0; }
        }

        public float PathLength
        {
            get
            {
                RebuildWorldPoints();
                return SplinePath.Length(worldPoints, closed);
            }
        }

        public void SetLocalPoints(Vector3[] points)
        {
            localPoints = points ?? new Vector3[0];
            RebuildWorldPoints();
        }

        public Vector3 ClosestPointTo(Vector3 target)
        {
            RebuildWorldPoints();

            Vector3 closest;
            float distance;

            if (!SplinePath.TryGetClosestPoint(worldPoints, closed, target, out closest, out distance))
            {
                return transform.position;
            }

            return closest;
        }

        private void OnEnable()
        {
            EnsureFollower();

            if (playOnEnable)
            {
                Play();
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play()
        {
            if (soundEvent == null || PointCount == 0)
            {
                return;
            }

            EnsureFollower();
            follower.position = ClosestPointTo(ListenerPosition());
            voice = AudioSystem.PlayAttached(soundEvent, follower);
        }

        public void Stop()
        {
            if (voice != null)
            {
                voice.Stop();
                voice = null;
            }
        }

        private void LateUpdate()
        {
            if (voice == null || !voice.IsActive || follower == null)
            {
                return;
            }

            Vector3 target = ClosestPointTo(ListenerPosition());

            if (followSmoothing <= 0f)
            {
                follower.position = target;
                return;
            }

            follower.position = Vector3.Lerp(
                follower.position,
                target,
                1f - Mathf.Exp(-Time.deltaTime / followSmoothing));
        }

        private void EnsureFollower()
        {
            if (follower != null)
            {
                return;
            }

            GameObject go = new GameObject(name + " Spline Follow");
            go.transform.SetParent(transform, false);
            follower = go.transform;
        }

        private void RebuildWorldPoints()
        {
            worldPoints.Clear();

            if (localPoints == null)
            {
                return;
            }

            for (int i = 0; i < localPoints.Length; i++)
            {
                worldPoints.Add(transform.TransformPoint(localPoints[i]));
            }
        }

        private Vector3 ListenerPosition()
        {
#if UNITY_2023_1_OR_NEWER
            AudioListener listener = Object.FindFirstObjectByType<AudioListener>();
#else
            AudioListener listener = Object.FindObjectOfType<AudioListener>();
#endif
            return listener != null ? listener.transform.position : transform.position;
        }

        private void OnDrawGizmosSelected()
        {
            RebuildWorldPoints();

            if (worldPoints.Count == 0)
            {
                return;
            }

            Gizmos.color = new Color(0.3f, 0.8f, 1f);
            int segments = closed ? worldPoints.Count : worldPoints.Count - 1;

            for (int i = 0; i < segments; i++)
            {
                Gizmos.DrawLine(worldPoints[i], worldPoints[(i + 1) % worldPoints.Count]);
            }

            for (int i = 0; i < worldPoints.Count; i++)
            {
                Gizmos.DrawSphere(worldPoints[i], 0.15f);
            }

            if (Application.isPlaying && follower != null)
            {
                Gizmos.color = new Color(0.3f, 0.95f, 0.5f);
                Gizmos.DrawSphere(follower.position, 0.3f);
            }
        }
    }
}
