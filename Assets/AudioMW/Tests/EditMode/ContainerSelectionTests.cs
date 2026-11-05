using System.Collections.Generic;
using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class ContainerSelectionTests
    {
        [Test]
        public void ShuffleBagVisitsEveryClipAndAvoidsBoundaryRepeats()
        {
            ClipSelector selector = new ClipSelector();
            System.Random random = new System.Random(42);
            int previous = -1;
            for (int cycle = 0; cycle < 20; cycle++)
            {
                HashSet<int> found = new HashSet<int>();
                for (int i = 0; i < 5; i++)
                {
                    int next = selector.Next(ClipSelectionMode.ShuffleBag, 5, random);
                    Assert.AreNotEqual(previous, next);
                    Assert.IsTrue(found.Add(next));
                    previous = next;
                }
            }
        }

        [Test]
        public void WeightedSelectionNeverChoosesDisabledClips()
        {
            ClipSelector selector = new ClipSelector();
            System.Random random = new System.Random(13);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(2, selector.Next(ClipSelectionMode.WeightedRandom, 5, random, new[] { 0f, -1f, 3f, float.NaN, float.PositiveInfinity }));
        }

        [Test]
        public void WeightedSelectionRespectsDistribution()
        {
            ClipSelector selector = new ClipSelector();
            System.Random random = new System.Random(8);
            int rare = 0;
            for (int i = 0; i < 10000; i++)
                if (selector.Next(ClipSelectionMode.WeightedRandom, 2, random, new[] { 1f, 9f }) == 0) rare++;
            Assert.That(rare, Is.InRange(850, 1150));
        }

        [Test]
        public void AllZeroWeightsRejectSelection()
        {
            Assert.AreEqual(-1, new ClipSelector().Next(ClipSelectionMode.WeightedRandom, 2, new System.Random(0), new[] { 0f, 0f }));
        }

        [Test]
        public void ResizingShuffleBagDoesNotReturnOldIndices()
        {
            ClipSelector selector = new ClipSelector();
            System.Random random = new System.Random(0);
            selector.Next(ClipSelectionMode.ShuffleBag, 8, random);
            for (int i = 0; i < 20; i++) Assert.That(selector.Next(ClipSelectionMode.ShuffleBag, 2, random), Is.InRange(0, 1));
        }
    }
}
