using UnityEngine;

namespace AudioMW
{
    public sealed class OcclusionSampler
    {
        private float current;
        private float target;

        public float Current
        {
            get { return current; }
        }

        public float Target
        {
            get { return target; }
        }

        public void Reset(float value)
        {
            current = Mathf.Clamp01(value);
            target = current;
        }

        public void SetTargetFromHits(int blockedCount, int totalSamples)
        {
            if (totalSamples <= 0)
            {
                target = 0f;
                return;
            }

            target = Mathf.Clamp01((float)Mathf.Clamp(blockedCount, 0, totalSamples) / totalSamples);
        }

        public float Advance(float deltaSeconds, float smoothingSeconds)
        {
            if (smoothingSeconds <= 0f)
            {
                current = target;
                return current;
            }

            current = Mathf.MoveTowards(current, target, deltaSeconds / smoothingSeconds);
            return current;
        }

        public static Vector3 SampleOffset(int index, int total, float spread, Vector3 forward)
        {
            if (index <= 0 || total <= 1 || spread <= 0f)
            {
                return Vector3.zero;
            }

            Vector3 right = Vector3.Cross(forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward, Vector3.up);

            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            right = right.normalized;
            Vector3 up = Vector3.Cross(right, forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward).normalized;

            float angle = (index - 1) * (2f * Mathf.PI / Mathf.Max(1, total - 1));
            return (right * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * spread;
        }
    }
}
