using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class MusicClockTests
    {
        [Test]
        public void SecondsPerBeatFollowsTempo()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);

            Assert.AreEqual(0.5, clock.SecondsPerBeat, 1e-9);
            Assert.AreEqual(2.0, clock.SecondsPerBar, 1e-9);
        }

        [Test]
        public void TempoAndSignatureAreGuarded()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(-10.0, 0);

            Assert.AreEqual(1.0, clock.Tempo, 1e-9);
            Assert.AreEqual(1, clock.BeatsPerBar);
        }

        [Test]
        public void BeatIndexAdvancesWithTime()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);
            clock.Start(100.0);

            Assert.AreEqual(0, clock.GetBeatIndex(100.0));
            Assert.AreEqual(0, clock.GetBeatIndex(100.4));
            Assert.AreEqual(1, clock.GetBeatIndex(100.5));
            Assert.AreEqual(4, clock.GetBeatIndex(102.0));
        }

        [Test]
        public void BarIndexAdvancesEveryFourBeats()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);
            clock.Start(0.0);

            Assert.AreEqual(0, clock.GetBarIndex(0.0));
            Assert.AreEqual(0, clock.GetBarIndex(1.9));
            Assert.AreEqual(1, clock.GetBarIndex(2.0));
            Assert.AreEqual(2, clock.GetBarIndex(4.0));
        }

        [Test]
        public void BeatInBarWrapsWithinSignature()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 3);
            clock.Start(0.0);

            Assert.AreEqual(0, clock.GetBeatInBar(0.0));
            Assert.AreEqual(1, clock.GetBeatInBar(0.5));
            Assert.AreEqual(2, clock.GetBeatInBar(1.0));
            Assert.AreEqual(0, clock.GetBeatInBar(1.5));
        }

        [Test]
        public void NextBeatBoundaryIsStrictlyAhead()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);
            clock.Start(10.0);

            Assert.AreEqual(10.5, clock.GetNextBoundary(10.0, MusicQuantization.Beat), 1e-9);
            Assert.AreEqual(10.5, clock.GetNextBoundary(10.2, MusicQuantization.Beat), 1e-9);
            Assert.AreEqual(11.0, clock.GetNextBoundary(10.5, MusicQuantization.Beat), 1e-9);
        }

        [Test]
        public void NextBarBoundaryUsesSignature()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);
            clock.Start(0.0);

            Assert.AreEqual(2.0, clock.GetNextBoundary(0.0, MusicQuantization.Bar), 1e-9);
            Assert.AreEqual(4.0, clock.GetNextBoundary(2.0, MusicQuantization.Bar), 1e-9);
            Assert.AreEqual(4.0, clock.GetNextBoundary(3.9, MusicQuantization.Bar), 1e-9);
        }

        [Test]
        public void BoundaryAtOrAfterKeepsExactHits()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(120.0, 4);
            clock.Start(0.0);

            Assert.AreEqual(2.0, clock.GetBoundaryAtOrAfter(2.0, MusicQuantization.Bar), 1e-9);
            Assert.AreEqual(4.0, clock.GetBoundaryAtOrAfter(2.1, MusicQuantization.Bar), 1e-9);
        }

        [Test]
        public void ImmediateQuantizationReturnsInputTime()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(90.0, 4);
            clock.Start(0.0);

            Assert.AreEqual(3.33, clock.GetNextBoundary(3.33, MusicQuantization.Immediate), 1e-9);
            Assert.AreEqual(3.33, clock.GetBoundaryAtOrAfter(3.33, MusicQuantization.Immediate), 1e-9);
        }

        [Test]
        public void ClockDoesNotDriftOverManyBars()
        {
            MusicClock clock = new MusicClock();
            clock.Configure(128.0, 4);
            clock.Start(0.0);

            double barLength = clock.SecondsPerBar;

            for (int bar = 0; bar < 2000; bar++)
            {
                double expected = bar * barLength;
                Assert.AreEqual(bar, clock.GetBarIndex(expected + 1e-9));
            }
        }

        [Test]
        public void StopClearsRunningState()
        {
            MusicClock clock = new MusicClock();
            clock.Start(5.0);

            Assert.IsTrue(clock.IsRunning);

            clock.Stop();

            Assert.IsFalse(clock.IsRunning);
        }
    }
}
