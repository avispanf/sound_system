using System;

namespace AudioMW
{
    public sealed class ClipSelector
    {
        private int lastIndex = -1;
        private int cursor = -1;
        private int[] bag = new int[0];
        private int remaining;

        public int LastIndex { get { return lastIndex; } }

        public void Reset()
        {
            lastIndex = -1;
            cursor = -1;
            remaining = 0;
        }

        public int Next(ClipSelectionMode mode, int count, Random rng, float[] weights = null)
        {
            if (count <= 0) return -1;
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            if (lastIndex >= count) Reset();
            int index;
            switch (mode)
            {
                case ClipSelectionMode.Sequential:
                    index = (cursor + 1) % count;
                    break;
                case ClipSelectionMode.RandomNoRepeat:
                    index = count == 1 || lastIndex < 0 ? rng.Next(count) : rng.Next(count - 1);
                    if (count > 1 && lastIndex >= 0 && index >= lastIndex) index++;
                    break;
                case ClipSelectionMode.WeightedRandom:
                    double total = 0;
                    for (int i = 0; i < count; i++) total += Weight(weights, i);
                    if (total <= 0) return -1;
                    double choice = rng.NextDouble() * total;
                    index = -1;
                    for (int i = 0; i < count; i++)
                    {
                        double weight = Weight(weights, i);
                        if (weight <= 0) continue;
                        index = i;
                        choice -= weight;
                        if (choice < 0) break;
                    }
                    break;
                case ClipSelectionMode.ShuffleBag:
                    if (bag.Length != count) { bag = new int[count]; remaining = 0; }
                    if (remaining == 0)
                    {
                        for (int i = 0; i < count; i++) bag[i] = i;
                        for (int i = count - 1; i > 0; i--)
                        {
                            int j = rng.Next(i + 1);
                            int saved = bag[i]; bag[i] = bag[j]; bag[j] = saved;
                        }
                        if (count > 1 && bag[count - 1] == lastIndex)
                        {
                            int j = rng.Next(count - 1);
                            int saved = bag[count - 1]; bag[count - 1] = bag[j]; bag[j] = saved;
                        }
                        remaining = count;
                    }
                    index = bag[--remaining];
                    break;
                default:
                    index = rng.Next(count);
                    break;
            }
            lastIndex = index;
            cursor = index;
            return index;
        }

        private static double Weight(float[] weights, int index)
        {
            float value = weights != null && index < weights.Length ? weights[index] : 1f;
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0 ? 0 : value;
        }
    }
}
