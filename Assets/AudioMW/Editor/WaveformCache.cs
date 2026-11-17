using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    [InitializeOnLoad]
    public static class WaveformCache
    {
        private sealed class Envelope
        {
            public int Frames, Channels, Stride;
            public readonly List<Vector2[]> Levels = new List<Vector2[]>();
            public Texture2D Texture;
        }
        private const int MaxEntries = 32;
        private static readonly Dictionary<int, Envelope> Cache = new Dictionary<int, Envelope>();
        private static readonly Queue<int> Order = new Queue<int>();
        static WaveformCache() { EditorApplication.projectChanged += Clear; AssemblyReloadEvents.beforeAssemblyReload += Clear; }
        public static void Clear()
        {
            foreach (var entry in Cache.Values) if (entry.Texture != null) Object.DestroyImmediate(entry.Texture);
            Cache.Clear(); Order.Clear();
        }
        private static Envelope Read(AudioClip clip)
        {
            if (clip == null || clip.samples <= 0 || clip.channels <= 0 || clip.loadType != AudioClipLoadType.DecompressOnLoad ) return null;
            if(clip.loadState==AudioDataLoadState.Unloaded){clip.LoadAudioData();return null;}
            if(clip.loadState!=AudioDataLoadState.Loaded)return null;
            int key = clip.GetInstanceID();
            if (Cache.TryGetValue(key, out var cached)) return cached;
            int stride = Math.Max(1, (int)Math.Ceiling(clip.samples / 65536.0));
            int buckets = (clip.samples + stride - 1) / stride;
            var envelope = new Envelope { Frames = clip.samples, Channels = clip.channels, Stride = stride };
            var values = new Vector2[buckets * clip.channels];
            for (int i = 0; i < values.Length; i++) values[i] = new Vector2(float.PositiveInfinity, float.NegativeInfinity);
            int blockFrames = Math.Min(16384, clip.samples);
            var data = new float[blockFrames * clip.channels];
            for (int offset = 0; offset < clip.samples; offset += blockFrames)
            {
                if (!clip.GetData(data, offset)) return null;
                int count = Math.Min(blockFrames, clip.samples - offset);
                for (int frame = 0; frame < count; frame++)
                    for (int channel = 0; channel < clip.channels; channel++)
                    {
                        int index = ((offset + frame) / stride) * clip.channels + channel;
                        float sample = data[frame * clip.channels + channel];
                        if (float.IsNaN(sample) || float.IsInfinity(sample)) sample = 0;
                        values[index].x = Mathf.Min(values[index].x, sample);
                        values[index].y = Mathf.Max(values[index].y, sample);
                    }
            }
            envelope.Levels.Add(values);
            while (buckets > 1)
            {
                int nextCount = (buckets + 1) / 2;
                var next = new Vector2[nextCount * clip.channels];
                for (int i = 0; i < nextCount; i++)
                    for (int channel = 0; channel < clip.channels; channel++)
                    {
                        var a = values[i * 2 * clip.channels + channel];
                        var b = values[Math.Min(i * 2 + 1, buckets - 1) * clip.channels + channel];
                        next[i * clip.channels + channel] = new Vector2(Mathf.Min(a.x, b.x), Mathf.Max(a.y, b.y));
                    }
                values = next; buckets = nextCount; envelope.Levels.Add(next);
            }
            Cache.Add(key, envelope); Order.Enqueue(key);
            while (Order.Count > MaxEntries)
            {
                int oldest = Order.Dequeue();
                if (Cache[oldest].Texture != null) Object.DestroyImmediate(Cache[oldest].Texture);
                Cache.Remove(oldest);
            }
            return envelope;
        }
        private static Vector2 Range(Envelope envelope, int channel, double start, double end)
        {
            double span = (end - start) / envelope.Stride;
            int level = Math.Min(envelope.Levels.Count - 1, Math.Max(0, (int)Math.Floor(Math.Log(Math.Max(1, span), 2))));
            double step = envelope.Stride * Math.Pow(2, level);
            var values = envelope.Levels[level];
            int count = values.Length / envelope.Channels;
            int first = Math.Max(0, Math.Min(count - 1, (int)Math.Floor(start / step)));
            int last = Math.Max(first, Math.Min(count - 1, (int)Math.Ceiling(end / step) - 1));
            var result = values[first * envelope.Channels + channel];
            for (int i = first + 1; i <= last; i++)
            {
                var value = values[i * envelope.Channels + channel];
                result.x = Mathf.Min(result.x, value.x); result.y = Mathf.Max(result.y, value.y);
            }
            return result;
        }
        public static Texture2D Get(AudioClip clip)
        {
            var envelope = Read(clip); if (envelope == null) return null;
            if (envelope.Texture != null) return envelope.Texture;
            const int width = 1024;
            int height = 64 * envelope.Channels;
            var pixels = new Color32[width * height];
            for (int channel = 0; channel < envelope.Channels; channel++)
                for (int x = 0; x < width; x++)
                {
                    var range = Range(envelope, channel, x * (double)envelope.Frames / width, (x + 1) * (double)envelope.Frames / width);
                    int center = height - channel * 64 - 32;
                    int low = Mathf.Clamp(center + Mathf.RoundToInt(range.x * 28), 0, height - 1);
                    int high = Mathf.Clamp(center + Mathf.RoundToInt(range.y * 28), 0, height - 1);
                    for (int y = low; y <= high; y++) pixels[y * width + x] = new Color32(128, 181, 188, 255);
                }
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point };
            texture.SetPixels32(pixels); texture.Apply(); envelope.Texture = texture; return texture;
        }
        public static bool Draw(AudioClip clip, Rect rect, double start = 0, double duration = -1, float verticalScale = 1)
        {
            var envelope = Read(clip); if (envelope == null) return false;
            if (Event.current.type != EventType.Repaint || rect.width <= 0 || rect.height <= 0) return true;
            if (duration < 0) duration = clip.samples / (double)clip.frequency - start;
            int columns = Math.Max(1, (int)Math.Ceiling(rect.width * EditorGUIUtility.pixelsPerPoint));
            float pixel = rect.width / columns, channelHeight = rect.height / envelope.Channels;
            for (int channel = 0; channel < envelope.Channels; channel++)
            {
                float center = rect.y + (channel + .5f) * channelHeight, scale = channelHeight * .43f;
                EditorGUI.DrawRect(new Rect(rect.x, center, rect.width, 1 / EditorGUIUtility.pixelsPerPoint), new Color(.25f, .34f, .36f));
                for (int x = 0; x < columns; x++)
                {
                    double a = (start + duration * x / columns) * clip.frequency;
                    double b = (start + duration * (x + 1) / columns) * clip.frequency;
                    var range = Range(envelope, channel, a, b);
                    float top = center - Mathf.Clamp(range.y * verticalScale, -1, 1) * scale;
                    float bottom = center - Mathf.Clamp(range.x * verticalScale, -1, 1) * scale;
                    EditorGUI.DrawRect(new Rect(rect.x + x * pixel, top, pixel, Mathf.Max(1 / EditorGUIUtility.pixelsPerPoint, bottom - top)), new Color(.50f, .71f, .74f));
                }
            }
            return true;
        }
    }
}
