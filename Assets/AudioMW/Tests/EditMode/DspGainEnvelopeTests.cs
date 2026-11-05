using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class DspGainEnvelopeTests
    {
        [Test]
        public void LinearCrossfadePreservesUnityGain()
        {
            var incoming = new DspGainEnvelope(10, 2);
            var outgoing = new DspGainEnvelope(outStart: 10, outSeconds: 2);
            for (int i = 0; i <= 100; i++)
                Assert.AreEqual(1, incoming.Evaluate(10 + i * 0.02) + outgoing.Evaluate(10 + i * 0.02), 0.00001);
        }

        [Test]
        public void EqualPowerPreservesSumOfSquares()
        {
            var incoming = new DspGainEnvelope(10, 2, MusicFadeCurve.EqualPower);
            var outgoing = new DspGainEnvelope(outStart: 10, outSeconds: 2, outCurve: MusicFadeCurve.EqualPower);
            for (int i = 0; i <= 100; i++)
            {
                float a = incoming.Evaluate(10 + i * 0.02), b = outgoing.Evaluate(10 + i * 0.02);
                Assert.AreEqual(1, a * a + b * b, 0.00001);
            }
            Assert.AreEqual(0, incoming.Evaluate(9));
            Assert.AreEqual(0, outgoing.Evaluate(13), 0.000001);
        }

        [Test]
        public void BufferProcessingUsesSampleTimeAndSameGainForEveryChannel()
        {
            var envelope = new DspGainEnvelope(1, 1);
            float[] samples = { 1, -1, 1, -1, 1, -1, 1, -1 };
            envelope.Process(samples, 2, 1, 4);
            CollectionAssert.AreEqual(new[] { 0f, 0f, 0.25f, -0.25f, 0.5f, -0.5f, 0.75f, -0.75f }, samples);
        }

        [Test]
        public void CancellingFadeOutPreservesTheOriginalFadeIn()
        {
            var envelope = new DspGainEnvelope(10, 2).WithFadeOut(11, 2, MusicFadeCurve.Linear);
            Assert.AreEqual(0.5625f, envelope.Evaluate(11.5), 0.00001);
            Assert.AreEqual(0.75f, envelope.WithoutFadeOut().Evaluate(11.5), 0.00001);
        }

        [Test]
        public void ZeroDurationCutChangesAtItsBoundary()
        {
            var envelope = new DspGainEnvelope(outStart: 2);
            Assert.AreEqual(1, envelope.Evaluate(1.999));
            Assert.AreEqual(0, envelope.Evaluate(2));
        }
    }
}
