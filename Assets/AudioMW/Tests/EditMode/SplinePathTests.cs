using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class SplinePathTests
    {
        [Test]
        public void ClosestPointOnSegmentProjectsPerpendicular()
        {
            Vector3 closest = SplinePath.ClosestPointOnSegment(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(3f, 5f, 0f));

            Assert.AreEqual(new Vector3(3f, 0f, 0f), closest);
        }

        [Test]
        public void ClosestPointOnSegmentClampsBeforeStart()
        {
            Vector3 closest = SplinePath.ClosestPointOnSegment(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(-8f, 2f, 0f));

            Assert.AreEqual(Vector3.zero, closest);
        }

        [Test]
        public void ClosestPointOnSegmentClampsAfterEnd()
        {
            Vector3 closest = SplinePath.ClosestPointOnSegment(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(40f, 2f, 0f));

            Assert.AreEqual(new Vector3(10f, 0f, 0f), closest);
        }

        [Test]
        public void DegenerateSegmentReturnsStart()
        {
            Vector3 point = new Vector3(2f, 2f, 2f);

            Assert.AreEqual(point, SplinePath.ClosestPointOnSegment(point, point, new Vector3(9f, 9f, 9f)));
        }

        [Test]
        public void EmptyPathHasNoClosestPoint()
        {
            Vector3 closest;
            float distance;

            Assert.IsFalse(SplinePath.TryGetClosestPoint(new List<Vector3>(), false, Vector3.zero, out closest, out distance));
            Assert.IsFalse(SplinePath.TryGetClosestPoint(null, false, Vector3.zero, out closest, out distance));
        }

        [Test]
        public void SinglePointPathReturnsThatPoint()
        {
            List<Vector3> points = new List<Vector3> { new Vector3(1f, 2f, 3f) };

            Vector3 closest;
            float distance;

            Assert.IsTrue(SplinePath.TryGetClosestPoint(points, false, Vector3.zero, out closest, out distance));
            Assert.AreEqual(points[0], closest);
            Assert.AreEqual(points[0].magnitude, distance, 0.001f);
        }

        [Test]
        public void ClosestPointPicksTheNearestSegment()
        {
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f)
            };

            Vector3 closest;
            float distance;

            SplinePath.TryGetClosestPoint(points, false, new Vector3(11f, 0f, 5f), out closest, out distance);

            Assert.AreEqual(new Vector3(10f, 0f, 5f), closest);
            Assert.AreEqual(1f, distance, 0.001f);
        }

        [Test]
        public void ClosedPathConsidersTheClosingSegment()
        {
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f),
                new Vector3(0f, 0f, 10f)
            };

            Vector3 open;
            Vector3 closedPoint;
            float openDistance;
            float closedDistance;

            Vector3 target = new Vector3(-2f, 0f, 5f);

            SplinePath.TryGetClosestPoint(points, false, target, out open, out openDistance);
            SplinePath.TryGetClosestPoint(points, true, target, out closedPoint, out closedDistance);

            Assert.Less(closedDistance, openDistance);
            Assert.AreEqual(new Vector3(0f, 0f, 5f), closedPoint);
        }

        [Test]
        public void LengthSumsSegments()
        {
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(3f, 4f, 0f)
            };

            Assert.AreEqual(7f, SplinePath.Length(points, false), 0.001f);
        }

        [Test]
        public void ClosedLengthAddsTheReturnSegment()
        {
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(3f, 0f, 0f),
                new Vector3(3f, 4f, 0f)
            };

            Assert.AreEqual(12f, SplinePath.Length(points, true), 0.001f);
        }

        [Test]
        public void ShortPathHasNoLength()
        {
            Assert.AreEqual(0f, SplinePath.Length(new List<Vector3> { Vector3.zero }, false), 0.001f);
            Assert.AreEqual(0f, SplinePath.Length(null, false), 0.001f);
        }

        [Test]
        public void PointAtDistanceWalksAlongThePath()
        {
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f),
                new Vector3(10f, 0f, 10f)
            };

            Assert.AreEqual(new Vector3(4f, 0f, 0f), SplinePath.PointAtDistance(points, false, 4f));
            Assert.AreEqual(new Vector3(10f, 0f, 3f), SplinePath.PointAtDistance(points, false, 13f));
        }

        [Test]
        public void PointAtDistanceClampsToTheEnds()
        {
            List<Vector3> points = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),
                new Vector3(10f, 0f, 0f)
            };

            Assert.AreEqual(new Vector3(0f, 0f, 0f), SplinePath.PointAtDistance(points, false, -5f));
            Assert.AreEqual(new Vector3(10f, 0f, 0f), SplinePath.PointAtDistance(points, false, 99f));
        }
    }
}
