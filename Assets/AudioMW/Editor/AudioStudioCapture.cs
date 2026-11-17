using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public sealed class AudioStudioCapture : IDisposable
    {
        private ProfilerRecorder tick=ProfilerRecorder.StartNew(ProfilerCategory.Scripts,"AudioMW.Tick",1);
        private ProfilerRecorder frame=ProfilerRecorder.StartNew(ProfilerCategory.Internal,"Main Thread",1);
        private ProfilerRecorder gc=ProfilerRecorder.StartNew(ProfilerCategory.Memory,"GC Allocated In Frame",1);
        private double next;
        private int session;
        public bool Recording;
        public readonly List<AudioPerformanceSample> Samples=new List<AudioPerformanceSample>(600);
        public void Tick()
        {
            if(!Recording||!EditorApplication.isPlaying||EditorApplication.isPaused||!AudioRuntime.Exists||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.1;var runtime=AudioRuntime.Instance;
            if(session!=runtime.GetInstanceID()){Samples.Clear();session=runtime.GetInstanceID();}
            if(Samples.Count==600)Samples.RemoveAt(0);
            Samples.Add(new AudioPerformanceSample{Seconds=Time.realtimeSinceStartupAsDouble,Active=runtime.Pool.ActiveCount,Virtual=runtime.VirtualVoiceCount,Limit=runtime.Pool.MaxVoices,Requests=runtime.PlayRequests,Rejected=runtime.RejectedRequests,Steals=runtime.Pool.StealCount,TickMs=Read(tick,.000001),MainThreadMs=Read(frame,.000001),FrameGcBytes=Read(gc,1)});
        }
        private static double Read(ProfilerRecorder value,double scale)=>value.Valid&&value.Count>0?value.LastValue*scale:double.NaN;
        public string Csv(){var s=new StringBuilder("editor_seconds,sfx_physical,sfx_virtual,sfx_limit,session_requests,session_rejected,session_steals,tick_main_thread_ms,whole_editor_frame_ms,whole_editor_frame_gc_bytes\n");foreach(var row in Samples)s.AppendLine(row.CsvRow());return s.ToString();}
        public void Dispose(){tick.Dispose();frame.Dispose();gc.Dispose();}
    }
}
