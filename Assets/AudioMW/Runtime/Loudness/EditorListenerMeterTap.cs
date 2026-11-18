#if UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;

namespace AudioMW
{
    [AddComponentMenu("")]
    public sealed class EditorListenerMeterTap : MonoBehaviour
    {
        private const int Slots = 32;
        private readonly float[][] buffers = new float[Slots][];
        private readonly int[] lengths = new int[Slots], widths = new int[Slots];
        private int read, write, discontinuities, ready;
        public int Discontinuities => Volatile.Read(ref discontinuities);

        private void Awake()
        {
            AudioSettings.GetDSPBufferSize(out int frames, out _);
            for (int i = 0; i < Slots; i++) buffers[i] = new float[Math.Max(frames, 1024) * 2];
            Volatile.Write(ref ready, 1);
        }

        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (Volatile.Read(ref ready) == 0) return;
            int next = (write + 1) % Slots;
            if (channels < 1 || channels > 2 || next == Volatile.Read(ref read) || data.Length > buffers[write].Length)
            {
                Interlocked.Increment(ref discontinuities);
                return;
            }
            Array.Copy(data, buffers[write], data.Length);
            widths[write] = channels;
            lengths[write] = data.Length / channels;
            Volatile.Write(ref write, next);
        }

        public bool TryRead(out float[] data, out int channels, out int frames)
        {
            data = null;
            channels = frames = 0;
            if (read == Volatile.Read(ref write)) return false;
            data = buffers[read]; channels = widths[read]; frames = lengths[read];
            return true;
        }

        public void Advance() => Volatile.Write(ref read, (read + 1) % Slots);
    }
}
#endif
