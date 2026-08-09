using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    public static class SplinePath
    {
        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 target)
        {
            Vector3 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;

            if (lengthSquared <= 1e-8f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot(target - a, segment) / lengthSquared);
            return a + segment * t;
        }

        public static bool TryGetClosestPoint(IReadOnlyList<Vector3> points, bool closed, Vector3 target, out Vector3 closest, out float distance)
        {
            closest = Vector3.zero;
            distance = 0f;

            if (points == null || points.Count == 0)
            {
                return false;
            }

            if (points.Count == 1)
            {
                closest = points[0];
                distance = Vector3.Distance(target, closest);
                return true;
            }

            float best = float.MaxValue;
            int segments = closed ? points.Count : points.Count - 1;

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                Vector3 candidate = ClosestPointOnSegment(a, b, target);
                float candidateDistance = Vector3.Distance(target, candidate);

                if (candidateDistance < best)
                {
                    best = candidateDistance;
                    closest = candidate;
                }
            }

            distance = best;
            return true;
        }

        public static float Length(IReadOnlyList<Vector3> points, bool closed)
        {
            if (points == null || points.Count < 2)
            {
                return 0f;
            }

            float total = 0f;
            int segments = closed ? points.Count : points.Count - 1;

            for (int i = 0; i < segments; i++)
            {
                total += Vector3.Distance(points[i], points[(i + 1) % points.Count]);
            }

            return total;
        }

        public static Vector3 PointAtDistance(IReadOnlyList<Vector3> points, bool closed, float distance)
        {
            if (points == null || points.Count == 0)
            {
                return Vector3.zero;
            }

            if (points.Count == 1)
            {
                return points[0];
            }

            float total = Length(points, closed);

            if (total <= 0f)
            {
                return points[0];
            }

            float travelled = Mathf.Clamp(distance, 0f, total);
            int segments = closed ? points.Count : points.Count - 1;

            for (int i = 0; i < segments; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[(i + 1) % points.Count];
                float segmentLength = Vector3.Distance(a, b);

                if (travelled <= segmentLength || i == segments - 1)
                {
                    float t = segmentLength <= 0f ? 0f : travelled / segmentLength;
                    return Vector3.Lerp(a, b, Mathf.Clamp01(t));
                }

                travelled -= segmentLength;
            }

            return points[points.Count - 1];
        }
    }
}
