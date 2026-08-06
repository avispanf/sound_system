using System;

namespace AudioMW
{
    public sealed class ClipSelector
    {
        private int lastIndex = -1;
        private int cursor = -1;

        public int LastIndex
        {
            get { return lastIndex; }
        }

        public void Reset()
        {
            lastIndex = -1;
            cursor = -1;
        }

        public int Next(ClipSelectionMode mode, int count, Random rng)
        {
            if (count <= 0)
            {
                return -1;
            }

            if (count == 1)
            {
                lastIndex = 0;
                cursor = 0;
                return 0;
            }

            int index;

            switch (mode)
            {
                case ClipSelectionMode.Sequential:
                    cursor = (cursor + 1) % count;
                    index = cursor;
                    break;

                case ClipSelectionMode.RandomNoRepeat:
                    if (lastIndex < 0)
                    {
                        index = rng.Next(count);
                    }
                    else
                    {
                        index = rng.Next(count - 1);
                        if (index >= lastIndex)
                        {
                            index++;
                        }
                    }
                    break;

                default:
                    index = rng.Next(count);
                    break;
            }

            lastIndex = index;
            cursor = index;
            return index;
        }
    }
}
