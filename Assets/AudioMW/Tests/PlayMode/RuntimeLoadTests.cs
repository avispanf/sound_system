using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;
using Unity.Profiling;

namespace AudioMW.Tests
{
    public sealed class RuntimeLoadTests
    {
        [Serializable]
        private sealed class Measurement
        {
            public string scenario;
            public string unity;
            public int iterations;
            public long allocatedBytes;
            public string allocationCounter;
            public double milliseconds;
        }

        [TestCase(ContainerMode.Simple)]
        [TestCase(ContainerMode.Layered)]
        [TestCase(ContainerMode.Switch)]
        public void WarmTriggerStopLoadRemainsBounded(ContainerMode mode)
        {
            AudioRuntime runtime = AudioRuntime.Instance;
            runtime.StopAll();
            runtime.RebuildPool(32, 32);
            bool debug = runtime.Debugger.Enabled;
            runtime.Debugger.Enabled = false;
            AudioClip clip = AudioTestUtil.MakeSine(1);
            SoundEvent leaf = SoundEvent.CreateRuntime(clip);
            leaf.Loop = true; leaf.SpatialBlend = 0;
            SoundEvent root = SoundEvent.CreateRuntime(clip);
            root.Loop = true; root.SpatialBlend = 0; root.ContainerMode = mode;
            SoundParameter state = SoundParameter.CreateRuntime(0, 1, 0);
            ParameterStore initial = new ParameterStore();
            initial.Set(state, 1);
            root.SwitchParameter = state;
            root.Children = new[] { SoundEventChild.Create(leaf, 0), SoundEventChild.Create(leaf, 1) };
            leaf.ParameterBindings = new[] { ParameterBinding.CreateRuntime(state, ParameterTarget.Volume, AnimationCurve.Linear(0, 0, 1, 1)) };
            try
            {
                for (int i = 0; i < 64; i++) AudioSystem.PlayTracked(root, initial).Stop();
                const int count = 2000;
                using (ProfilerRecorder recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1,
                    ProfilerRecorderOptions.StartImmediately))
                {
                long gcBefore = GC.GetAllocatedBytesForCurrentThread();
                long profilerBefore = recorder.CurrentValue;
                byte[] calibration = new byte[65536];
                GC.KeepAlive(calibration);
                long gcDelta = GC.GetAllocatedBytesForCurrentThread() - gcBefore;
                long profilerDelta = recorder.CurrentValue - profilerBefore;
                bool useGc = gcDelta >= 65536 && gcDelta <= 65800;
                bool useProfiler = recorder.Valid && profilerDelta >= 65536 && profilerDelta <= 65800;
                long beforeBytes = useGc ? GC.GetAllocatedBytesForCurrentThread() : recorder.CurrentValue;
                long beforeTime = System.Diagnostics.Stopwatch.GetTimestamp();
                int rejected = 0;
                for (int i = 0; i < count; i++)
                {
                    SoundHandle handle = AudioSystem.PlayTracked(root, initial);
                    if (!handle.IsAssigned) rejected++;
                    handle.Stop();
                }
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - beforeTime;
                long bytes = (useGc ? GC.GetAllocatedBytesForCurrentThread() : recorder.CurrentValue) - beforeBytes;
                if (!useGc && !useProfiler) bytes = -1;
                Measurement measurement = new Measurement { scenario = mode.ToString(), unity = Application.unityVersion, iterations = count, allocatedBytes = bytes, allocationCounter = useGc ? "GC.GetAllocatedBytesForCurrentThread" : useProfiler ? "GC Allocated In Frame (frame-wide)" : "unavailable", milliseconds = elapsed * 1000.0 / System.Diagnostics.Stopwatch.Frequency };
                File.WriteAllText(Path.Combine(Application.dataPath, "..", "runtime-load-" + mode + ".json"), JsonUtility.ToJson(measurement, true));
                Assert.AreEqual(0, rejected);
                Assert.AreEqual(0, runtime.TrackedHandleCount);
                Assert.AreEqual(0, runtime.Pool.ActiveCount);
                Assert.LessOrEqual(runtime.Pool.TotalCount, 32);
                }
            }
            finally
            {
                runtime.StopAll(); runtime.Debugger.Enabled = debug;
                Object.Destroy(root); Object.Destroy(leaf); Object.Destroy(state); Object.Destroy(clip);
            }
        }

        [Test]
        public void SaturatedCompositeBurstKeepsHandlesAndVoicesBounded()
        {
            AudioRuntime runtime = AudioRuntime.Instance;
            runtime.StopAll(); runtime.RebuildPool(32, 32);
            bool debug = runtime.Debugger.Enabled; runtime.Debugger.Enabled = false;
            AudioClip clip = AudioTestUtil.MakeSine(1);
            SoundEvent leaf = SoundEvent.CreateRuntime(clip);
            leaf.Loop = true; leaf.SpatialBlend = 0;
            SoundEvent root = SoundEvent.CreateRuntime();
            root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(leaf), SoundEventChild.Create(leaf) };
            SoundHandle[] handles = new SoundHandle[2000];
            try
            {
                for (int i = 0; i < handles.Length; i++) handles[i] = AudioSystem.PlayTracked(root);
                int alive = 0;
                foreach (SoundHandle handle in handles) if (handle.IsPlaying) alive++;
                Assert.AreEqual(16, alive);
                Assert.AreEqual(16, runtime.TrackedHandleCount);
                Assert.AreEqual(32, runtime.Pool.ActiveCount);
                Assert.AreEqual(32, runtime.Pool.TotalCount);
                runtime.StopAll();
                Assert.LessOrEqual(runtime.CachedInstanceCount, 32);
                foreach (SoundHandle handle in handles) Assert.IsFalse(handle.IsPlaying);
            }
            finally
            {
                runtime.StopAll(); runtime.Debugger.Enabled = debug;
                Object.Destroy(root); Object.Destroy(leaf); Object.Destroy(clip);
            }
        }
    }
}
