using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public static class WaveformCache
    {
        private const int Width = 512;
        private const int Height = 96;
        private const int MaxEntries = 64;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static readonly List<string> Order = new List<string>();

        public static void Clear()
        {
            foreach (KeyValuePair<string, Texture2D> pair in Cache)
            {
                if (pair.Value != null)
                {
                    Object.DestroyImmediate(pair.Value);
                }
            }

            Cache.Clear();
            Order.Clear();
        }

        public static Texture2D Get(AudioClip clip)
        {
            if (clip == null)
            {
                return null;
            }

            string path = AssetDatabase.GetAssetPath(clip);
            string key = AssetDatabase.AssetPathToGUID(path);

            if (string.IsNullOrEmpty(key))
            {
                key = clip.GetInstanceID().ToString();
            }

            Texture2D cached;

            if (Cache.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            Texture2D texture = Render(clip);
            Cache[key] = texture;
            Order.Add(key);

            while (Order.Count > MaxEntries)
            {
                string oldest = Order[0];
                Order.RemoveAt(0);

                Texture2D stale;

                if (Cache.TryGetValue(oldest, out stale))
                {
                    if (stale != null)
                    {
                        Object.DestroyImmediate(stale);
                    }

                    Cache.Remove(oldest);
                }
            }

            return texture;
        }

        private static Texture2D Render(AudioClip clip)
        {
            Texture2D texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;

            Color background = new Color(0f, 0f, 0f, 0f);
            Color foreground = EditorGUIUtility.isProSkin
                ? new Color(0.42f, 0.62f, 0.88f, 1f)
                : new Color(0.20f, 0.42f, 0.72f, 1f);

            Color[] pixels = new Color[Width * Height];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = background;
            }

            float[] peaks = ComputePeaks(clip, Width);

            if (peaks != null)
            {
                int middle = Height / 2;

                for (int x = 0; x < Width; x++)
                {
                    int half = Mathf.Clamp(Mathf.RoundToInt(peaks[x] * (Height / 2 - 2)), 1, Height / 2 - 1);

                    for (int y = middle - half; y <= middle + half; y++)
                    {
                        if (y >= 0 && y < Height)
                        {
                            pixels[y * Width + x] = foreground;
                        }
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static float[] ComputePeaks(AudioClip clip, int buckets)
        {
            if (clip.loadType != AudioClipLoadType.DecompressOnLoad || clip.samples <= 0)
            {
                return null;
            }

            float[] data = new float[clip.samples * clip.channels];

            if (!clip.GetData(data, 0))
            {
                return null;
            }

            float[] peaks = new float[buckets];
            int perBucket = Mathf.Max(1, data.Length / buckets);

            for (int bucket = 0; bucket < buckets; bucket++)
            {
                int start = bucket * perBucket;
                float peak = 0f;

                for (int i = start; i < start + perBucket && i < data.Length; i++)
                {
                    float magnitude = Mathf.Abs(data[i]);

                    if (magnitude > peak)
                    {
                        peak = magnitude;
                    }
                }

                peaks[bucket] = peak;
            }

            return peaks;
        }
    }
}
