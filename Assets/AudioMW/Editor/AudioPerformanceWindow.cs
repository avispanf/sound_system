using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public struct AudioPerformanceSample
    {
        public double Seconds;
        public int Active, Virtual, Limit, Requests, Rejected, Steals;
        public double TickMs, MainThreadMs, FrameGcBytes;
        public static string CsvValue(double value) { return double.IsNaN(value) ? "" : value.ToString("0.######", CultureInfo.InvariantCulture); }
        public string CsvRow()
        {
            return string.Join(",", CsvValue(Seconds), Active, Virtual, Limit, Requests, Rejected, Steals, CsvValue(TickMs), CsvValue(MainThreadMs), CsvValue(FrameGcBytes));
        }
    }

    public sealed class AudioPerformanceWindow : EditorWindow
    {
        private ProfilerRecorder tick, mainThread, gc;
        private readonly List<AudioPerformanceSample> samples = new List<AudioPerformanceSample>(600);
        private Label state, voices, cpu, allocations, totals;
        private Button export;
        private bool recording = true;
        private double nextSample;
        private IMGUIContainer graph;
        public IReadOnlyList<AudioPerformanceSample> Samples => samples;
        [MenuItem("Window/AudioMW/Panels/Diagnostics/Performance")]
        public static void Open()
        {
            var window = GetWindow<AudioPerformanceWindow>(); window.titleContent = new GUIContent("Performance"); window.minSize = new Vector2(360, 340); window.Show();
        }
        private void OnEnable()
        {
            tick = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "AudioMW.Tick", 1);
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            EditorApplication.update += UpdateSample;
        }
        private void OnDisable() { EditorApplication.update -= UpdateSample; tick.Dispose(); mainThread.Dispose(); gc.Dispose(); }
        public void CreateGUI()
        {
            var root = rootVisualElement; root.Clear(); AudioWorkspace.Style(root); root.AddToClassList("audiomw-performance"); root.Add(AudioWorkspace.Navigation("Performance"));
            var content = new ScrollView(ScrollViewMode.Vertical) { horizontalScrollerVisibility = ScrollerVisibility.Hidden }; content.AddToClassList("audiomw-preview"); root.Add(content);
            content.Add(new Label("Performance") { name = "performance-heading" });
            state = new Label(); content.Add(state);
            var toolbar = new Toolbar();
            var capture = new ToolbarToggle { text = "Capture", value = recording }; capture.RegisterValueChangedCallback(e => recording = e.newValue); toolbar.Add(capture);
            toolbar.Add(new ToolbarButton(() => { samples.Clear(); Refresh(); }) { text = "Clear" });
            export = new ToolbarButton(Export) { text = "Export CSV" }; toolbar.Add(export); content.Add(toolbar);
            voices = Metric(content, "Voices"); cpu = Metric(content, "CPU"); allocations = Metric(content, "Allocations"); totals = Metric(content, "Requests");
            graph = new IMGUIContainer(DrawGraph) { name = "voice-history" }; graph.style.height = 120; content.Add(graph);
            content.Add(new Label("Physical voice history · up to 600 samples, requested at 10 Hz. Editor stalls and pauses extend the time span; short spikes can be missed. Counts cover AudioMW SFX voices; music and VO use separate sources."));
            content.Add(new Label("AudioMW.Tick measures main-thread maintenance only, not audio DSP. Main Thread and GC cover the whole Editor frame, including this UI. Missing counters are unavailable, not zero. Use a Development Player and Unity Profiler on the target device for CPU, DSP, audio memory and streaming budgets."));
            content.Add(new Button(() => EditorApplication.ExecuteMenuItem("Window/Analysis/Profiler")) { text = "Open Unity Profiler" });
            content.Add(new Button(LiveTweakWindow.Open) { text = "Review Play Mode edits" });
            Refresh();
        }
        private static Label Metric(VisualElement root, string title)
        {
            var row = new VisualElement(); row.AddToClassList("audiomw-metric-row"); row.Add(new Label(title)); var value = new Label(); value.AddToClassList("audiomw-metric-value"); row.Add(value); root.Add(row); return value;
        }
        private static double Read(ProfilerRecorder recorder, double scale)
        {
            return recorder.Valid && recorder.Count > 0 ? recorder.LastValue * scale : double.NaN;
        }
        private void UpdateSample()
        {
            if (EditorApplication.timeSinceStartup < nextSample) return;
            nextSample = EditorApplication.timeSinceStartup + .1;
            if (recording && EditorApplication.isPlaying && !EditorApplication.isPaused && AudioRuntime.Exists)
            {
                var runtime = AudioRuntime.Instance;
                if (samples.Count == 600) samples.RemoveAt(0);
                samples.Add(new AudioPerformanceSample { Seconds = Time.realtimeSinceStartupAsDouble, Active = runtime.Pool.ActiveCount, Virtual = runtime.VirtualVoiceCount, Limit = runtime.Pool.MaxVoices, Requests = runtime.PlayRequests, Rejected = runtime.RejectedRequests, Steals = runtime.Pool.StealCount, TickMs = Read(tick, .000001), MainThreadMs = Read(mainThread, .000001), FrameGcBytes = Read(gc, 1) });
            }
            Refresh();
        }
        private static string Format(double value, string unit) { return double.IsNaN(value) ? "Unavailable" : value.ToString("0.###") + unit; }
        private void Refresh()
        {
            if (state == null) return;
            bool live = EditorApplication.isPlaying && !EditorApplication.isPaused && AudioRuntime.Exists && recording;
            state.text = live ? "Recording · local Editor · 10 Hz" : "Not recording · enter Play Mode with AudioMW and enable Capture";
            export.SetEnabled(samples.Count > 0);
            if (samples.Count == 0) { voices.text = cpu.text = allocations.text = totals.text = "No samples"; graph?.MarkDirtyRepaint(); return; }
            var sample = samples[samples.Count - 1];
            voices.text = sample.Active + " physical / " + sample.Limit + " limit · " + sample.Virtual + " virtual";
            cpu.text = "Tick " + Format(sample.TickMs, " ms") + " · whole frame " + Format(sample.MainThreadMs, " ms");
            allocations.text = Format(sample.FrameGcBytes, " B / sampled Editor frame");
            totals.text = sample.Requests + " plays · " + sample.Rejected + " rejected · " + sample.Steals + " steals · session counters";
            graph?.MarkDirtyRepaint();
        }
        private void DrawGraph()
        {
            Rect rect = GUILayoutUtility.GetRect(20, 110, GUILayout.ExpandWidth(true)); EditorGUI.DrawRect(rect, new Color(.10f,.11f,.12f));
            if (samples.Count < 2) return;
            float maximum = 1; foreach (var sample in samples) maximum = Mathf.Max(maximum, sample.Limit, sample.Active + sample.Virtual);
            Handles.BeginGUI(); var old = Handles.color; Handles.color = new Color(.55f,.78f,.42f);
            for (int i = 1; i < samples.Count; i++)
            {
                Vector3 from = new Vector3(rect.x + rect.width * (i-1) / 599f, rect.yMax - rect.height * samples[i-1].Active / maximum);
                Vector3 to = new Vector3(rect.x + rect.width * i / 599f, rect.yMax - rect.height * samples[i].Active / maximum); Handles.DrawLine(from,to);
            }
            Handles.color = old; Handles.EndGUI();
        }
        private void Export()
        {
            string path = EditorUtility.SaveFilePanel("Export performance samples", "", "audiomw-editor-performance.csv", "csv");
            if (string.IsNullOrEmpty(path)) return;
            var csv = new StringBuilder("editor_seconds,sfx_physical,sfx_virtual,sfx_limit,session_requests,session_rejected,session_steals,tick_main_thread_ms,whole_editor_frame_ms,whole_editor_frame_gc_bytes\n");
            foreach (var sample in samples) csv.AppendLine(sample.CsvRow()); File.WriteAllText(path, csv.ToString());
        }
    }
}
