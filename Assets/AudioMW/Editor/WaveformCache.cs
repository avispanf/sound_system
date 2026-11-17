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
            public readonly List<Vector2[]> Energy = new List<Vector2[]>();
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
            var energy = new Vector2[values.Length];
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
                        energy[index] += new Vector2(sample * sample, 1);
                    }
            }
            envelope.Levels.Add(values); envelope.Energy.Add(energy);
            while (buckets > 1)
            {
                int nextCount = (buckets + 1) / 2;
                var next = new Vector2[nextCount * clip.channels];
                var nextEnergy = new Vector2[next.Length];
                for (int i = 0; i < nextCount; i++)
                    for (int channel = 0; channel < clip.channels; channel++)
                    {
                        var a = values[i * 2 * clip.channels + channel];
                        var b = values[Math.Min(i * 2 + 1, buckets - 1) * clip.channels + channel];
                        next[i * clip.channels + channel] = new Vector2(Mathf.Min(a.x, b.x), Mathf.Max(a.y, b.y));
                        nextEnergy[i * clip.channels + channel] = energy[i * 2 * clip.channels + channel];
                        if(i * 2 + 1 < buckets) nextEnergy[i * clip.channels + channel] += energy[(i * 2 + 1) * clip.channels + channel];
                    }
                values = next; energy = nextEnergy; buckets = nextCount; envelope.Levels.Add(next); envelope.Energy.Add(nextEnergy);
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
        private static float Rms(Envelope envelope,int channel,double start,double end)
        {
            double span=(end-start)/envelope.Stride;
            int level=Math.Min(envelope.Energy.Count-1,Math.Max(0,(int)Math.Floor(Math.Log(Math.Max(1,span),2))));
            double step=envelope.Stride*Math.Pow(2,level);var values=envelope.Energy[level];int count=values.Length/envelope.Channels;
            int first=Math.Max(0,Math.Min(count-1,(int)Math.Floor(start/step)));
            int last=Math.Max(first,Math.Min(count-1,(int)Math.Ceiling(end/step)-1));
            Vector2 sum=Vector2.zero;for(int i=first;i<=last;i++)sum+=values[i*envelope.Channels+channel];
            return sum.y>0?Mathf.Sqrt(sum.x/sum.y):0;
        }
        public static bool TryGetRange(AudioClip clip,int channel,double start,double duration,out Vector3 sample)
        {
            sample=Vector3.zero;var envelope=Read(clip);
            if(envelope==null||channel<0||channel>=envelope.Channels||!MusicArrangement.Finite(start)||!MusicArrangement.Finite(duration)||duration<=0)return false;
            double a=Math.Max(0,start*clip.frequency),b=Math.Min(envelope.Frames,(start+duration)*clip.frequency);
            if(b<=a)return false;
            var range=Range(envelope,channel,a,b);sample=new Vector3(range.x,range.y,Rms(envelope,channel,a,b));return true;
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
        public static bool Draw(AudioClip clip, Rect rect, double start = 0, double duration = -1, float verticalScale = 1, Color? tint = null, bool combineChannels = false)
        {
            var envelope=Read(clip);if(envelope==null)return false;
            if(Event.current.type!=EventType.Repaint||rect.width<=0||rect.height<=0)return true;
            if(duration<0)duration=clip.samples/(double)clip.frequency-start;
            if(verticalScale<=0)
            {
                float peak=0;foreach(var range in envelope.Levels[envelope.Levels.Count-1])peak=Mathf.Max(peak,Mathf.Abs(range.x),Mathf.Abs(range.y));
                verticalScale=peak>0?.9f/peak:1;
            }
            int columns=Math.Max(1,(int)Math.Ceiling(rect.width*EditorGUIUtility.pixelsPerPoint));
            int displayed=combineChannels?1:envelope.Channels;
            float pixel=rect.width/columns,channelHeight=rect.height/displayed,hairline=1/EditorGUIUtility.pixelsPerPoint;
            Color color=tint??new Color(.50f,.71f,.74f),peakColor=new Color(color.r,color.g,color.b,.6f);
            for(int channel=0;channel<displayed;channel++)
            {
                float center=rect.y+(channel+.5f)*channelHeight,scale=channelHeight*.43f;
                EditorGUI.DrawRect(new Rect(rect.x,center,rect.width,hairline),new Color(color.r,color.g,color.b,.20f));
                for(int x=0;x<columns;x++)
                {
                    double a=(start+duration*x/columns)*clip.frequency,b=(start+duration*(x+1)/columns)*clip.frequency;
                    var range=Range(envelope,channel,a,b);float rms=Rms(envelope,channel,a,b);
                    if(combineChannels)for(int other=1;other<envelope.Channels;other++)
                    {
                        var next=Range(envelope,other,a,b);range.x=Mathf.Min(range.x,next.x);range.y=Mathf.Max(range.y,next.y);
                        rms=Mathf.Max(rms,Rms(envelope,other,a,b));
                    }
                    float top=center-Mathf.Clamp(range.y*verticalScale,-1,1)*scale,bottom=center-Mathf.Clamp(range.x*verticalScale,-1,1)*scale;
                    EditorGUI.DrawRect(new Rect(rect.x+x*pixel,top,pixel,Mathf.Max(hairline,bottom-top)),peakColor);
                    float inner=rms*verticalScale*scale;
                    float upper=Mathf.Max(top,center-inner),lower=Mathf.Min(bottom,center+inner);
                    if(lower>upper)EditorGUI.DrawRect(new Rect(rect.x+x*pixel,upper,pixel,lower-upper),color);
                }
            }
            return true;
        }
    }
}
