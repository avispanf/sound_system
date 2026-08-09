using System.Collections.Generic;
using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class MusicMarkerTests
    {
        [Test]
        public void PositionInBeatsFollowsSignature()
        {
            MusicMarker marker = MusicMarker.CreateRuntime("chorus", 2, 1);

            Assert.AreEqual(9.0, marker.PositionInBeats(4), 1e-9);
            Assert.AreEqual(7.0, marker.PositionInBeats(3), 1e-9);
        }

        [Test]
        public void NegativePositionsAreClamped()
        {
            MusicMarker marker = MusicMarker.CreateRuntime("bad", -5, -3);

            Assert.AreEqual(0, marker.Bar);
            Assert.AreEqual(0, marker.Beat);
        }

        [Test]
        public void NextMarkerPicksTheNearestAhead()
        {
            MusicClock clock = MakeClock();
            List<double> markers = new List<double> { 0.0, 4.0, 8.0 };

            Assert.AreEqual(2.0, clock.GetNextMarkerTime(0.5, markers, 16.0), 1e-6);
            Assert.AreEqual(4.0, clock.GetNextMarkerTime(2.5, markers, 16.0), 1e-6);
        }

        [Test]
        public void NextMarkerWrapsToTheFollowingLoop()
        {
            MusicClock clock = MakeClock();
            List<double> markers = new List<double> { 0.0, 4.0 };

            Assert.AreEqual(8.0, clock.GetNextMarkerTime(3.0, markers, 16.0), 1e-6);
        }

        [Test]
        public void MarkersOutsideTheLoopAreIgnored()
        {
            MusicClock clock = MakeClock();
            List<double> markers = new List<double> { 4.0, 99.0 };

            Assert.AreEqual(2.0, clock.GetNextMarkerTime(0.0, markers, 8.0), 1e-6);
        }

        [Test]
        public void EmptyMarkerSetFallsBackToBarBoundary()
        {
            MusicClock clock = MakeClock();

            double withMarkers = clock.GetNextMarkerTime(0.5, new List<double>(), 16.0);
            double barBoundary = clock.GetNextBoundary(0.5, MusicQuantization.Bar);

            Assert.AreEqual(barBoundary, withMarkers, 1e-9);
        }

        [Test]
        public void NullMarkerSetFallsBackToBarBoundary()
        {
            MusicClock clock = MakeClock();

            Assert.AreEqual(
                clock.GetNextBoundary(1.0, MusicQuantization.Bar),
                clock.GetNextMarkerTime(1.0, null, 16.0),
                1e-9);
        }

        [Test]
        public void ZeroLoopLengthFallsBackToBarBoundary()
        {
            MusicClock clock = MakeClock();
            List<double> markers = new List<double> { 4.0 };

            Assert.AreEqual(
                clock.GetNextBoundary(0.0, MusicQuantization.Bar),
                clock.GetNextMarkerTime(0.0, markers, 0.0),
                1e-9);
        }

        [Test]
        public void MarkerExactlyAtCursorIsSkipped()
        {
            MusicClock clock = MakeClock();
            List<double> markers = new List<double> { 4.0, 8.0 };

            Assert.AreEqual(4.0, clock.GetNextMarkerTime(2.0, markers, 16.0), 1e-6);
        }

        private static MusicClock MakeClock()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);
            clock.Start(0.0);
            return clock;
        }
    }
}
