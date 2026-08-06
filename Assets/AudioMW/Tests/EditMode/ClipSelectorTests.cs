using NUnit.Framework;

namespace AudioMW.Tests
{
    public sealed class ClipSelectorTests
    {
        [Test]
        public void EmptySetReturnsInvalidIndex()
        {
            ClipSelector selector = new ClipSelector();
            Assert.AreEqual(-1, selector.Next(ClipSelectionMode.Random, 0, new System.Random(1)));
        }

        [Test]
        public void SingleClipAlwaysReturnsZero()
        {
            ClipSelector selector = new ClipSelector();
            System.Random rng = new System.Random(1);

            for (int i = 0; i < 16; i++)
            {
                Assert.AreEqual(0, selector.Next(ClipSelectionMode.RandomNoRepeat, 1, rng));
            }
        }

        [Test]
        public void SequentialWrapsAround()
        {
            ClipSelector selector = new ClipSelector();
            System.Random rng = new System.Random(1);
            int[] expected = { 0, 1, 2, 0, 1, 2 };

            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], selector.Next(ClipSelectionMode.Sequential, 3, rng));
            }
        }

        [Test]
        public void RandomNoRepeatNeverRepeatsImmediately()
        {
            ClipSelector selector = new ClipSelector();
            System.Random rng = new System.Random(12345);
            int previous = selector.Next(ClipSelectionMode.RandomNoRepeat, 4, rng);

            for (int i = 0; i < 2000; i++)
            {
                int current = selector.Next(ClipSelectionMode.RandomNoRepeat, 4, rng);
                Assert.AreNotEqual(previous, current);
                previous = current;
            }
        }

        [Test]
        public void RandomNoRepeatStaysInRange()
        {
            ClipSelector selector = new ClipSelector();
            System.Random rng = new System.Random(777);

            for (int i = 0; i < 2000; i++)
            {
                int index = selector.Next(ClipSelectionMode.RandomNoRepeat, 5, rng);
                Assert.GreaterOrEqual(index, 0);
                Assert.Less(index, 5);
            }
        }

        [Test]
        public void RandomNoRepeatCoversAllIndices()
        {
            ClipSelector selector = new ClipSelector();
            System.Random rng = new System.Random(2024);
            bool[] seen = new bool[4];

            for (int i = 0; i < 500; i++)
            {
                seen[selector.Next(ClipSelectionMode.RandomNoRepeat, 4, rng)] = true;
            }

            for (int i = 0; i < seen.Length; i++)
            {
                Assert.IsTrue(seen[i]);
            }
        }

        [Test]
        public void ResetClearsHistory()
        {
            ClipSelector selector = new ClipSelector();
            System.Random rng = new System.Random(5);
            selector.Next(ClipSelectionMode.Sequential, 3, rng);
            selector.Next(ClipSelectionMode.Sequential, 3, rng);
            selector.Reset();

            Assert.AreEqual(-1, selector.LastIndex);
            Assert.AreEqual(0, selector.Next(ClipSelectionMode.Sequential, 3, rng));
        }
    }
}
