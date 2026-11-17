using System.Collections;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace AudioMW.Tests
{
    public sealed class ProductionWorkflowTests
    {
        [Test]
        public void DistanceProcessingIsOptInAndClampsCurves()
        {
            var preset = AttenuationPreset.CreateRuntime(1, 1, 11, AudioRolloffMode.Linear);
            try
            {
                Assert.IsFalse(preset.UsesDistanceProcessing);
                Assert.AreEqual(22000, preset.EvaluateCutoff(6));
                preset.DistanceFiltering = true; preset.DistanceSpread = true;
                preset.CutoffByDistance = AnimationCurve.Linear(0,22000,1,2000);
                preset.SpreadByDistance = AnimationCurve.Linear(0,180,1,0);
                Assert.AreEqual(12000, preset.EvaluateCutoff(6), .01);
                Assert.AreEqual(90, preset.EvaluateSpread(6), .01);
                Assert.AreEqual(2000, preset.EvaluateCutoff(99), .01);
                preset.CutoffByDistance = AnimationCurve.Constant(0,1,-100);
                Assert.AreEqual(10, preset.EvaluateCutoff(6));
                preset.CutoffByDistance = new AnimationCurve();
                Assert.AreEqual(22000, preset.EvaluateCutoff(6));
            }
            finally { Object.DestroyImmediate(preset); }
        }
        [Test]
        public void LiveParameterRestoresUnsetAndPreservesGameplayReplacement()
        {
            var parameter = SoundParameter.CreateRuntime(0,1,.2f);
            try
            {
                var store = new ParameterStore();
                var lease = new LiveParameterLease(store, parameter); lease.Set(.8f);
                Assert.AreEqual(.8f,store.Get(parameter)); Assert.AreEqual(.2f,parameter.DefaultValue);
                Assert.IsTrue(lease.Restore()); Assert.IsFalse(store.Has(parameter));
                store.Set(parameter,.3f); lease = new LiveParameterLease(store,parameter); lease.Set(.9f);
                Assert.IsTrue(lease.Restore()); Assert.AreEqual(.3f,store.Get(parameter));
                lease = new LiveParameterLease(store,parameter); lease.Set(.9f); store.Set(parameter,.6f);
                Assert.IsFalse(lease.Restore()); Assert.AreEqual(.6f,store.Get(parameter));
            }
            finally { Object.DestroyImmediate(parameter); }
        }
        [Test]
        public void PerformanceCsvDoesNotInventMissingMeasurements()
        {
            var sample = new AudioPerformanceSample { Seconds = 1.5, Active = 3, Virtual = 2, Limit = 32, TickMs = double.NaN, MainThreadMs = 4.2, FrameGcBytes = double.NaN };
            Assert.AreEqual("1.5,3,2,32,0,0,0,,4.2,",sample.CsvRow());
        }
        [UnityTest]
        public IEnumerator StudioEmbedsNativeAuthoringAndPreservesUndo()
        {
            string path = "Assets/AudioMWStudioTest_" + System.Guid.NewGuid().ToString("N") + ".asset";
            var item = ScriptableObject.CreateInstance<SoundEvent>(); item.VolumeDb = -12;
            AssetDatabase.CreateAsset(item,path);
            var previous = Selection.activeObject;
            var window = ScriptableObject.CreateInstance<AudioBrowserWindow>(); window.ShowUtility(); window.CreateGUI();
            try
            {
                Assert.IsTrue(window.SelectAsset(item));
                yield return null; yield return null;
                Assert.AreEqual(item,Selection.activeObject);
                Assert.NotNull(window.rootVisualElement.Q<UnityEditor.UIElements.InspectorElement>());
                Assert.NotNull(window.rootVisualElement.Q("studio-container"));
                Assert.AreEqual(4,window.rootVisualElement.Q("studio-browser-tabs").Query<Button>().ToList().Count);
                var gain = window.rootVisualElement.Q<DecibelGainField>();
                Assert.NotNull(gain);
                Undo.IncrementCurrentGroup();
                var reset = gain.Q<Button>("gain-reset");
                using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = reset; reset.SendEvent(submit); }
                yield return null;
                Undo.FlushUndoRecordObjects(); Assert.AreEqual(0,item.VolumeDb,.01f);
                Undo.PerformUndo(); yield return null; yield return null;
                Assert.AreEqual(-12,item.VolumeDb,.01f);
                window.FilterMask = 8;
                Assert.IsNull(window.rootVisualElement.Q<UnityEditor.UIElements.InspectorElement>());
                window.FilterMask = 0; Assert.IsTrue(window.SelectAsset(item));
                Assert.NotNull(window.rootVisualElement.Q<UnityEditor.UIElements.InspectorElement>());
            }
            finally { window.Close(); Selection.activeObject = previous; AssetDatabase.DeleteAsset(path); }
        }
        [Test]
        public void AudioBinCollectsDirectClipWithoutChangingImport()
        {
            var clip = AudioClip.Create("Audio Bin",100,2,44100,false);
            try
            {
                var sources = AudioSourceCatalog.Collect(clip);
                Assert.AreEqual(1,sources.Count); Assert.AreSame(clip,sources[0].Clip);
                Assert.AreEqual(2,clip.channels);
            }
            finally { Object.DestroyImmediate(clip); }
        }
        [UnityTest]
        public IEnumerator ParameterWorkspaceStacksLibraryAt380()
        {
            var window = AudioInspectionWindow.Open(AudioInspectionWindow.Workspace.Parameters);
            try
            {
                window.position = new Rect(80,80,380,680);
                yield return null; yield return null; yield return null;
                var split = window.rootVisualElement.Q<TwoPaneSplitView>();
                Assert.AreEqual(TwoPaneSplitViewOrientation.Vertical,split.orientation);
                var detail = window.rootVisualElement.Q<ScrollView>("workspace-detail");
                Assert.LessOrEqual(detail.worldBound.xMax,window.rootVisualElement.worldBound.xMax + 1);
                Assert.Greater(detail.worldBound.width,300);
                window.position = new Rect(80,80,1100,680);
                yield return null; yield return null; yield return null;
                Assert.AreEqual(TwoPaneSplitViewOrientation.Horizontal,split.orientation);
            }
            finally { window.Close(); }
        }
        [UnityTest]
        public IEnumerator NativeObjectIconKeepsSquareGeometryAt300()
        {
            var track = ScriptableObject.CreateInstance<MusicTrack>();
            var clip = AudioClip.Create("Long source filename with several words",100,1,44100,false);
            var serialized = new SerializedObject(track); serialized.FindProperty("loopClip").objectReferenceValue = clip; serialized.ApplyModifiedPropertiesWithoutUndo();
            var before = EditorJsonUtility.ToJson(track); var window = AudioWorkspace.Pin(track);
            try
            {
                window.position = new Rect(80,80,300,720);
                yield return null; yield return null; yield return null;
                var field = window.rootVisualElement.Q("track-loopClip");
                var icon = field.Q(className:"unity-object-field-display__icon");
                Assert.NotNull(icon); Assert.AreEqual(16,icon.resolvedStyle.width,.1f); Assert.AreEqual(16,icon.resolvedStyle.height,.1f);
                var label = field.Q<Label>(className:"unity-object-field-display__label");
                Assert.AreEqual(WhiteSpace.NoWrap,label.resolvedStyle.whiteSpace);
                Assert.AreEqual(before,EditorJsonUtility.ToJson(track));
            }
            finally { window.Close(); Object.DestroyImmediate(track); Object.DestroyImmediate(clip); }
        }
    }
}
