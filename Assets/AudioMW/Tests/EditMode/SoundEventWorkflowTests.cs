using System;
using System.Collections;
using System.Collections.Generic;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class SoundEventWorkflowTests
    {
        [Test]
        public void SpatialModelKeepsScaleAndCustomSourceAxisExplicit()
        {
            Assert.AreEqual(1, AudioInspectionData.DistanceModel(0,2,10,1,AudioRolloffMode.Linear,null));
            Assert.AreEqual(.5f, AudioInspectionData.DistanceModel(6,2,10,1,AudioRolloffMode.Linear,null), .0001f);
            Assert.AreEqual(0, AudioInspectionData.DistanceModel(10,2,10,1,AudioRolloffMode.Linear,null));
            Assert.AreEqual(1, AudioInspectionData.DistanceModel(100,2,10,0,AudioRolloffMode.Linear,null));
            Assert.AreEqual(.02f, AudioInspectionData.DistanceModel(100,2,10,1,AudioRolloffMode.Logarithmic,null), .0001f);
            Assert.AreEqual(.8f, AudioInspectionData.DistanceModel(2,2,10,1,AudioRolloffMode.Custom,AnimationCurve.Linear(0,1,1,0)), .0001f);
        }

        [Test]
        public void RoutingRetainsRepeatedLeavesAndExplainsCycles()
        {
            var leaf = Event("RouteLeaf",Clip("LeafClip")); var root = Event("RouteRoot",Clip("Unused")); root.ContainerMode=ContainerMode.Layered;
            root.Children=new[] { SoundEventChild.Create(leaf),SoundEventChild.Create(leaf),SoundEventChild.Create(root) };
            var routes=AudioInspectionData.Routes(root);Assert.AreEqual(3,routes.Count);
            Assert.AreEqual(leaf,routes[0].Owner);Assert.AreEqual(leaf,routes[1].Owner);
            StringAssert.Contains("no mixer group",routes[0].Path);StringAssert.Contains("cycle",routes[2].Path);
        }

        [Test]
        public void ParameterResponsesDoNotWriteDefaultsOrInventScope()
        {
            var parameter=SoundParameter.CreateRuntime(-1,1,.25f);AssetDatabase.CreateAsset(parameter,folder+"/ResponseParameter.asset");
            var item=Event("BoundEvent",Clip("BoundClip"));item.ParameterBindings=new[] { ParameterBinding.CreateRuntime(parameter,ParameterTarget.Volume,AnimationCurve.Linear(0,0,1,1)) };
            var uses=AudioInspectionData.ParameterUses(parameter);Assert.AreEqual(1,uses.Count);Assert.AreEqual(item,uses[0].Asset);
            Assert.AreEqual(.5f,uses[0].Evaluate(0),.0001f);Assert.AreEqual(.25f,parameter.DefaultValue);
        }

        [Test]
        public void ParentSpatialWorkspaceDoesNotClaimOneCurve()
        {
            var item=Event("SpatialParent",Clip("Unused"));item.ContainerMode=ContainerMode.Layered;
            var window=AudioInspectionWindow.Open(AudioInspectionWindow.Workspace.Spatial);
            try { window.SelectAsset(item);Assert.IsNull(window.rootVisualElement.Q<Label>("spatial-context"));bool explained=false;window.rootVisualElement.Query<Label>().ForEach(label=>{if(label.text!=null&&label.text.Contains("no single attenuation"))explained=true;});Assert.IsTrue(explained); }
            finally { window.Close(); }
        }

        [Test]
        public void EventContextSeparatesDryPreviewFromUnavailableRuntime()
        {
            SoundEvent item = ScriptableObject.CreateInstance<SoundEvent>();
            try
            {
                var pane = new AudioEventContextPane(item);
                Assert.IsFalse(pane.Q<Button>("play-event").enabledSelf);
                StringAssert.Contains("Play Mode", pane.Q<Label>("audition-status").text);
                StringAssert.Contains("No AudioMixerGroup", pane.Q<Label>("event-route").text);
                Assert.IsNull(pane.Q<ObjectField>("audition-emitter").value);
            }
            finally { Object.DestroyImmediate(item); }
        }

        [Test]
        public void SelectionLabelsPreserveSerializedAlgorithms()
        {
            Assert.AreEqual("No immediate repeat", AudioEventContextPane.SelectionLabel(ClipSelectionMode.RandomNoRepeat));
            Assert.AreEqual("Shuffle bag", AudioEventContextPane.SelectionLabel(ClipSelectionMode.ShuffleBag));
            Assert.AreEqual(0, (int)ContainerMode.Simple); Assert.AreEqual(1, (int)ClipSelectionMode.RandomNoRepeat);
        }

        [Test]
        public void DiagnosticReasonsDistinguishAcceptedSilenceFromRejection()
        {
            Assert.AreEqual("Source volume is zero", EventDebuggerWindow.Reason(new EventDebugRecord { Outcome = PlaybackOutcome.Played, HasGainCapture = true, FinalVolume = 0 }));
            Assert.AreEqual("Accepted · gain capture unavailable", EventDebuggerWindow.Reason(new EventDebugRecord { Outcome = PlaybackOutcome.Played }));
            Assert.AreEqual("Playback request accepted", EventDebuggerWindow.Reason(new EventDebugRecord { Outcome = PlaybackOutcome.Played, HasGainCapture = true, FinalVolume = 0.5f }));
            StringAssert.Contains("2", EventDebuggerWindow.Reason(new EventDebugRecord { Outcome = PlaybackOutcome.RejectedInstanceLimit, Rejection = new PlaybackRejectionInfo { HasInstanceLimit = true, CurrentCount = 2, Limit = 2, Scope = "emitter" } }));
        }

        [Test]
        public void MusicOutsidePlayModeExplainsUnavailableRuntimeAndDisablesRequests()
        {
            MusicStatePreviewWindow window = ScriptableObject.CreateInstance<MusicStatePreviewWindow>();
            try
            {
                window.CreateGUI();
                Assert.IsNotNull(window.rootVisualElement.Q("workspace-navigation"));
                bool explained = false;
                window.rootVisualElement.Query<Label>().ForEach(label => { if (!string.IsNullOrEmpty(label.text) && label.text.Contains("Unavailable outside Play Mode")) explained = true; });
                Assert.IsTrue(explained);
                StringAssert.Contains("Unavailable", window.rootVisualElement.Q<Label>("music-timing").text);
                Assert.IsNotNull(window.rootVisualElement.Q<ScrollView>("music-content"));
            }
            finally { Object.DestroyImmediate(window); }
        }

        [UnityTest] public IEnumerator ChildRowsPreserveStateAcrossModeChangesAndSupportGainUndo()
        {
            SoundEvent child = Event("Child", Clip("Clip"));
            SoundEvent parent = Event("Parent", Clip("Unused"));
            parent.ContainerMode = ContainerMode.Layered;
            parent.Children = new[] { SoundEventChild.Create(child, 7, 0.5f) };
            SerializedObject serialized = new SerializedObject(parent);
            VisualElement row = new SoundEventChildDrawer().CreatePropertyGUI(serialized.FindProperty("children").GetArrayElementAtIndex(0));
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            window.rootVisualElement.Add(row); row.Bind(serialized);
            yield return null; yield return null;
            Assert.AreEqual(DisplayStyle.None, row.Q("child-state").style.display.value);
            DecibelGainField gain = row.Q<DecibelGainField>("child-gain");
            Undo.IncrementCurrentGroup(); gain.value = AudioGain.FromDecibels(-12);
            yield return null; Undo.FlushUndoRecordObjects();
            Assert.AreEqual(-12, parent.Children[0].GainDb, 0.001f);
            Undo.PerformUndo(); yield return null;
            Assert.AreEqual(0.5f, parent.Children[0].Gain, 0.001f);
            serialized.Update(); serialized.FindProperty("containerMode").enumValueIndex = (int)ContainerMode.Switch; serialized.ApplyModifiedProperties();
            yield return null; yield return null;
            double until = EditorApplication.timeSinceStartup + 3;
            while (row.Q("child-state").style.display.value != DisplayStyle.Flex && EditorApplication.timeSinceStartup < until) yield return null;
            Assert.AreEqual(DisplayStyle.Flex, row.Q("child-state").style.display.value);
            Assert.AreEqual(7, parent.Children[0].State); Assert.AreEqual(child, parent.Children[0].Event);
            window.Close();
        }

        [UnityTest] public IEnumerator ChildGainMultiEditChangesOnlyGainAndUndoRestoresBothValues()
        {
            SoundEvent child = Event("Child", Clip("Clip"));
            SoundEvent a = Event("A", Clip("A")); SoundEvent b = Event("B", Clip("B"));
            a.ContainerMode = ContainerMode.Layered; b.ContainerMode = ContainerMode.Switch;
            a.Children = new[] { SoundEventChild.Create(child, 1, 0.5f) };
            b.Children = new[] { SoundEventChild.Create(child, 2, 0.25f) };
            SerializedObject serialized = new SerializedObject(new Object[] { a, b });
            VisualElement row = new SoundEventChildDrawer().CreatePropertyGUI(serialized.FindProperty("children").GetArrayElementAtIndex(0));
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            window.rootVisualElement.Add(row); row.Bind(serialized);
            yield return null; yield return null;
            Assert.AreEqual(DisplayStyle.Flex, row.Q("child-state").style.display.value);
            DecibelGainField gain = row.Q<DecibelGainField>("child-gain"); Assert.IsTrue(gain.showMixedValue);
            Undo.IncrementCurrentGroup(); gain.value = 1;
            yield return null; Undo.FlushUndoRecordObjects();
            Assert.AreEqual(1, a.Children[0].Gain); Assert.AreEqual(1, b.Children[0].Gain);
            Assert.AreEqual(1, a.Children[0].State); Assert.AreEqual(2, b.Children[0].State);
            Undo.PerformUndo(); yield return null;
            Assert.AreEqual(0.5f, a.Children[0].Gain); Assert.AreEqual(0.25f, b.Children[0].Gain);
            window.Close();
        }

        [Test] public void InlineWeightExpansionPreservesImplicitDefaultsAndUndo()
        {
            SoundEvent item = Event("Weights", Clip("A"), Clip("B"), Clip("C"));
            item.ClipWeights = new[] { 0.5f }; SerializedObject serialized = new SerializedObject(item);
            Undo.IncrementCurrentGroup(); ClipWeightEditor.SetWeight(serialized, 2, 4); Undo.FlushUndoRecordObjects();
            CollectionAssert.AreEqual(new[] { 0.5f, 1, 4 }, item.ClipWeights);
            Undo.PerformUndo(); CollectionAssert.AreEqual(new[] { 0.5f }, item.ClipWeights);
            Undo.PerformRedo(); CollectionAssert.AreEqual(new[] { 0.5f, 1, 4 }, item.ClipWeights);
        }

        [Test] public void InlineWeightsIncludeAddressSlotsAndDoNotRewriteLegacyValuesOnDisplay()
        {
            SoundEvent item = Event("Weights", Clip("Direct"));
            item.ClipReferences = new[] { ClipReference.Addressed("audio/step") };
            item.ClipWeights = new[] { -2f, 3f, 9f };
            ClipWeightEditor pane = new ClipWeightEditor(new SerializedObject(item));
            StringAssert.Contains("audio/step", pane.Q<FloatField>("weight-1").label);
            CollectionAssert.AreEqual(new[] { -2f, 3f, 9f }, item.ClipWeights);
            ClipWeightEditor.SetWeight(new SerializedObject(item), 1, float.PositiveInfinity);
            CollectionAssert.AreEqual(new[] { -2f, 0f, 9f }, item.ClipWeights);
        }

        [Test] public void InlineWeightsKeepNativeMultiEditAndRejectInvalidSlots()
        {
            SoundEvent a = Event("A", Clip("A")); SoundEvent b = Event("B", Clip("B"), Clip("C"));
            SerializedObject mixed = new SerializedObject(new Object[] { a, b });
            ClipWeightEditor pane = new ClipWeightEditor(mixed);
            Assert.IsNotNull(pane.Q<PropertyField>()); Assert.IsNull(pane.Q<FloatField>());
            ClipWeightEditor.SetWeight(mixed, 0, 7);
            ClipWeightEditor.SetWeight(new SerializedObject(a), 4, 7);
            Assert.AreEqual(0, a.ClipWeights.Length); Assert.AreEqual(0, b.ClipWeights.Length);
        }

        [UnityTest] public IEnumerator InlineWeightFieldWritesTheCorrectSourceSlot()
        {
            SoundEvent item = Event("Weights", Clip("A"), Clip("B"));
            SerializedObject serialized = new SerializedObject(item);
            ClipWeightEditor pane = new ClipWeightEditor(serialized);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            window.rootVisualElement.Add(pane);
            yield return null; yield return null;
            FloatField field = pane.Q<FloatField>("weight-1");
            Assert.IsTrue(field.enabledInHierarchy);
            field.value = 0;
            CollectionAssert.AreEqual(new[] { 1f, 0f }, item.ClipWeights);
            window.Close();
        }

        [UnityTest] public IEnumerator MusicRefreshTracksAddedRemovedRenamedAndReplacedStates()
        {
            MusicStatePreviewWindow window = ScriptableObject.CreateInstance<MusicStatePreviewWindow>();
            MusicStateProfile profile = ScriptableObject.CreateInstance<MusicStateProfile>(); temporary.Add(profile);
            try
            {
                window.Show(); window.CreateGUI();
                yield return null; yield return null;
                profile.States = new[] { new MusicState { Name = "Calm" } };
                window.rootVisualElement.Q<ObjectField>().value = profile;
                VisualElement states = window.rootVisualElement.Q("music-states");
                Assert.AreEqual(1, states.Query<Button>("request-state").ToList().Count);
                Button first = states.Q<Button>("request-state");
                profile.States[0].Name = "Explore"; window.RefreshView();
                StringAssert.StartsWith("Explore", first.text);
                profile.States = new[] { profile.States[0], new MusicState { Name = "Combat" } }; window.RefreshView();
                Assert.AreEqual(2, states.Query<Button>("request-state").ToList().Count);
                profile.States = new[] { new MusicState { Name = "Replacement" } }; window.RefreshView();
                Assert.AreEqual(1, states.Query<Button>("request-state").ToList().Count);
                StringAssert.StartsWith("Replacement", states.Q<Button>("request-state").text);
                profile.States = new MusicState[0]; window.RefreshView();
                Assert.AreEqual(0, states.Query<Button>("request-state").ToList().Count);
                Assert.IsFalse(window.rootVisualElement.Q<Button>("stop-scene-music").enabledSelf);
                Assert.IsFalse(window.rootVisualElement.Q<Button>("cancel-pending-music").enabledSelf);
            }
            finally { window.Close(); }
        }

        [Test] public void DiagnosticsEmptyViewShowsAnActionInsteadOfAnEmptyTable()
        {
            EventDebuggerWindow window = ScriptableObject.CreateInstance<EventDebuggerWindow>();
            try
            {
                window.CreateGUI();
                Assert.AreEqual(DisplayStyle.None, window.rootVisualElement.Q<ListView>().style.display.value);
                StringAssert.Contains("Play Mode", window.rootVisualElement.Q<Label>("diagnostics-empty").text);
                Assert.IsFalse(window.rootVisualElement.Q<Button>("open-event-settings").enabledSelf);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [Test] public void DiagnosticBankStatusReadsLiveReadinessWithoutStartingLoads()
        {
            SoundBank bank = SoundBank.CreateRuntime(); temporary.Add(bank); bank.name = "Steps";
            Assert.AreEqual("Steps · Not loaded", EventDebuggerWindow.DescribeBankState(bank));
            Assert.IsFalse(bank.IsLoaded);
            bank.Load();
            Assert.AreEqual("Steps · Ready", EventDebuggerWindow.DescribeBankState(bank));
            bank.Unload();
            Assert.AreEqual("Steps · Not loaded", EventDebuggerWindow.DescribeBankState(bank));
            Assert.AreEqual("Bank no longer exists", EventDebuggerWindow.DescribeBankState(null));
        }

        [Test] public void NestedValidationIdentifiesTheExactLabelledChildPath()
        {
            SoundEvent empty = Event("Empty", Clip("Temporary")); empty.Clips = new AudioClip[0];
            SoundEvent inner = Event("Inner", Clip("Unused")); inner.ContainerMode = ContainerMode.Layered;
            inner.Children = new[] { SoundEventChild.Create(Event("Good", Clip("A"))), SoundEventChild.Create(empty) }; inner.Children[1].Label = "Tail";
            SoundEvent root = Event("Root", Clip("Unused")); root.ContainerMode = ContainerMode.Layered; root.Children = new[] { SoundEventChild.Create(inner) };
            string message = SoundEventValidation.Describe(root);
            StringAssert.StartsWith("Child 1 / Child 2 (Tail):", message);
            StringAssert.Contains("Add audio clips", message);
        }

        [Test] public void ValidationDistinguishesSharedChildrenFromActualCycles()
        {
            SoundEvent child = Event("Shared", Clip("A")); SoundEvent root = Event("Root", Clip("Unused")); root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(child), SoundEventChild.Create(child) };
            Assert.AreEqual("", SoundEventValidation.Describe(root));
            root.Children[1].Event = root;
            StringAssert.Contains("Child 2: Circular child reference", SoundEventValidation.Describe(root));
        }

        [Test] public void SwitchSummaryCountsEveryMatchingPlayableRowWithoutChangingParameters()
        {
            SoundEvent leaf = Event("Leaf", Clip("A")); SoundEvent item = Event("Switch", Clip("Unused")); item.ContainerMode = ContainerMode.Switch;
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 4, 1.6f); temporary.Add(parameter); item.SwitchParameter = parameter;
            item.Children = new[] { SoundEventChild.Create(leaf, 2), SoundEventChild.Create(leaf, 2), SoundEventChild.Create(null, 2), SoundEventChild.Create(leaf, 1) };
            string summary = SoundEventValidation.DescribeSwitch(item);
            StringAssert.Contains("Default state 2", summary); StringAssert.Contains("2 matching", summary); StringAssert.Contains("All matching children", summary);
            Assert.AreEqual(1.6f, parameter.DefaultValue);
        }

        [Test] public void SwitchSummaryExplainsUnmatchedDefaultsAndMissingParameter()
        {
            SoundEvent item = Event("Switch", Clip("Unused")); item.ContainerMode = ContainerMode.Switch;
            StringAssert.Contains("Assign a state parameter", SoundEventValidation.DescribeSwitch(item));
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 3, 0); temporary.Add(parameter); item.SwitchParameter = parameter;
            item.Children = new[] { SoundEventChild.Create(Event("Leaf", Clip("A")), 3) };
            StringAssert.Contains("No child starts", SoundEventValidation.DescribeSwitch(item));
        }

        [UnityTest] public IEnumerator ParentInspectorRefreshesWhenOnlyItsChildAssetChanges()
        {
            SoundEvent child = Event("Child", Clip("A")); SoundEvent parent = Event("Parent", Clip("Unused"));
            parent.ContainerMode = ContainerMode.Layered; parent.Children = new[] { SoundEventChild.Create(child) };
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(parent); temporary.Add(editor);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            VisualElement root = editor.CreateInspectorGUI(); window.rootVisualElement.Add(root); root.Bind(editor.serializedObject);
            yield return null; yield return null;
            int dirty = EditorUtility.GetDirtyCount(parent);
            SerializedObject nested = new SerializedObject(child); nested.FindProperty("clips").arraySize = 0; nested.ApplyModifiedProperties();
            HelpBox message = root.Q<HelpBox>("event-authoring-diagnostics");
            double until = EditorApplication.timeSinceStartup + 3;
            while (string.IsNullOrEmpty(message.text) && EditorApplication.timeSinceStartup < until) yield return null;
            StringAssert.Contains("Child 1", message.text); StringAssert.Contains("Add audio clips", message.text);
            Assert.AreEqual(dirty, EditorUtility.GetDirtyCount(parent)); window.Close();
        }

        [UnityTest] public IEnumerator SwitchInspectorRefreshesWhenOnlyTheParameterDefaultChanges()
        {
            SoundEvent child = Event("Child", Clip("A")); SoundEvent parent = Event("Parent", Clip("Unused"));
            SoundParameter parameter = SoundParameter.CreateRuntime(0, 4, 0); temporary.Add(parameter);
            parent.ContainerMode = ContainerMode.Switch; parent.SwitchParameter = parameter; parent.Children = new[] { SoundEventChild.Create(child, 2) };
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(parent); temporary.Add(editor);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            VisualElement root = editor.CreateInspectorGUI(); window.rootVisualElement.Add(root); root.Bind(editor.serializedObject);
            yield return null; yield return null;
            int dirty = EditorUtility.GetDirtyCount(parent);
            SerializedObject nested = new SerializedObject(parameter); nested.FindProperty("defaultValue").floatValue = 1.6f; nested.ApplyModifiedProperties();
            HelpBox message = root.Q<HelpBox>("switch-default-summary"); double until = EditorApplication.timeSinceStartup + 3;
            while (!message.text.Contains("Default state 2") && EditorApplication.timeSinceStartup < until) yield return null;
            StringAssert.Contains("Default state 2", message.text); StringAssert.Contains("1 matching", message.text);
            Assert.AreEqual(dirty, EditorUtility.GetDirtyCount(parent)); window.Close();
        }

        [Test] public void DiagnosticSettingsRouteToTheActualProblemAssetWithRootFallback()
        {
            SoundEvent root = Event("Root", Clip("A")); SoundEvent child = Event("Child", Clip("B"));
            EventDebugRecord record = new EventDebugRecord { Event = root, Rejection = new PlaybackRejectionInfo { ProblemEvent = child } };
            Assert.AreSame(child, EventDebuggerWindow.SettingsEvent(record));
            record.Rejection = default(PlaybackRejectionInfo); Assert.AreSame(root, EventDebuggerWindow.SettingsEvent(record));
            record.Event = null; Assert.IsNull(EventDebuggerWindow.SettingsEvent(record));
        }

        private string folder;
        private readonly List<Object> temporary = new List<Object>();
        private readonly List<AudioBrowserWindow> browsers = new List<AudioBrowserWindow>();
        private Object[] previousSelection;
        [SetUp] public void SetUp()
        {
            previousSelection = Selection.objects;
            folder = "Assets/AudioMWWorkflow_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        }
        [TearDown] public void TearDown()
        {
            foreach (AudioBrowserWindow browser in browsers) if (browser != null) { browser.FollowSelection(); browser.Close(); }
            browsers.Clear(); EditorAudioPreview.Stop();
            foreach (Object item in temporary) if (item != null && !AssetDatabase.Contains(item)) Object.DestroyImmediate(item);
            temporary.Clear(); AssetDatabase.DeleteAsset(folder); Selection.objects = previousSelection;
        }
        private AudioClip Clip(string name)
        {
            string path = folder + "/" + name + ".wav";
            using (System.IO.BinaryWriter writer = new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                const int bytes = 4410 * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(44100); writer.Write(88200);
                writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                for (int i = 0; i < 4410; i++) writer.Write((short)(Math.Sin(i * Math.PI * 880 / 44100) * 2000));
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path); clip.LoadAudioData(); return clip;
        }
        private SoundEvent Event(string name, params AudioClip[] clips)
        {
            SoundEvent item = SoundEventAuthoring.CreateAsset(clips, SoundEventPreset.OneShot, folder + "/" + name + ".asset");
            return item;
        }
        private AudioBrowserWindow Browser()
        {
            AudioBrowserWindow browser = ScriptableObject.CreateInstance<AudioBrowserWindow>(); browsers.Add(browser);
            browser.ShowUtility(); browser.CreateGUI(); return browser;
        }
        [TestCase(SoundEventPreset.OneShot)]
        [TestCase(SoundEventPreset.Footsteps)]
        [TestCase(SoundEventPreset.UI)]
        [TestCase(SoundEventPreset.Ambience)]
        public void PresetsRemainEditableSoundEvents(SoundEventPreset preset)
        {
            AudioClip[] clips = { Clip("Step_D"), Clip("Step_C"), Clip("Step_B"), Clip("Step_A") };
            SoundEvent item = SoundEventAuthoring.Create(clips, preset); temporary.Add(item);
            Assert.AreEqual("Step_A", item.Clips[0].name); Assert.AreEqual(4, item.Clips.Length);
            Assert.AreEqual(ClipSelectionMode.ShuffleBag, item.SelectionMode);
            Assert.AreEqual(preset == SoundEventPreset.UI ? 0 : 1, item.SpatialBlend);
            Assert.AreEqual(preset == SoundEventPreset.Ambience, item.Loop);
            if (preset == SoundEventPreset.Footsteps) { Assert.AreEqual(2, item.MaxInstancesPerEmitter); Assert.AreEqual(-2, item.VolumeRandomDbMin); Assert.AreEqual(2, item.VolumeRandomDbMax); }
            Assert.Throws<ArgumentException>(() => SoundEventAuthoring.Create(new AudioClip[0], preset));
        }
        [Test] public void SourceCatalogPreservesEveryBranchAndStopsCycles()
        {
            SoundEvent leaf = Event("Leaf", Clip("One"), Clip("Two"));
            SoundEvent root = Event("Root", Clip("Unused")); root.ContainerMode = ContainerMode.Layered;
            root.Children = new[] { SoundEventChild.Create(leaf), SoundEventChild.Create(root), SoundEventChild.Create(leaf) };
            List<AudioSourceEntry> sources = AudioSourceCatalog.Collect(root);
            Assert.AreEqual(4, sources.Count); Assert.AreEqual(leaf.Clips[1], sources[1].Clip);
        }
        [Test] public void UnresolvedAddressKeepsItsIdentityAndFindsItsActualBank()
        {
            SoundEvent item = Event("Addressed", Clip("Direct")); item.Clips = new AudioClip[0];
            item.ClipReferences = new[] { ClipReference.Addressed("audio/step") };
            SoundBank bank = SoundBank.CreateRuntime(item); AssetDatabase.CreateAsset(bank, folder + "/Bank.asset");
            AudioSourceEntry source = AudioSourceCatalog.Collect(item)[0];
            StringAssert.Contains("audio/step", source.Name); StringAssert.Contains("Sound Bank", source.PreviewIssue);
            CollectionAssert.Contains(AudioSourceCatalog.BanksFor(item), bank); Assert.IsFalse(item.ClipReferences[0].IsResolved);
        }
        [Test] public void BrowserSelectsNativeInspectorAndSwitchesTheSourceMetadata()
        {
            AudioClip a = Clip("First"); AudioClip b = Clip("Second"); SoundEvent item = Event("Event", a, b);
            AudioBrowserWindow browser = Browser(); Assert.IsTrue(browser.SelectAsset(item));
            Assert.AreEqual(item, Selection.activeObject); Assert.AreEqual(a, browser.SelectedSource);
            browser.rootVisualElement.Q<AudioSourcePreviewPane>().Select(1);
            Assert.AreEqual(b, browser.SelectedSource); Assert.AreEqual(b.name, browser.rootVisualElement.Q<Label>("source-name").text);
            Assert.AreEqual(WaveformCache.Get(b), browser.rootVisualElement.Q<Image>("source-waveform").image);
        }
        [Test] public void EmptySearchClearsOldDetailsAndOffersAReset()
        {
            SoundEvent item = Event("Visible", Clip("Clip")); AudioBrowserWindow browser = Browser(); browser.SelectAsset(item);
            browser.SearchQuery = "NoSuchAudio_" + Guid.NewGuid().ToString("N");
            Assert.IsNull(browser.SelectedAsset); Assert.IsNull(browser.rootVisualElement.Q("asset-title"));
            Assert.IsNotNull(browser.rootVisualElement.Q<Button>("reset-filters"));
            browser.SearchQuery = ""; Assert.AreEqual(item, browser.SelectedAsset);
        }
        [Test] public void BrowserStateRestoresFromUnitySerialization()
        {
            SoundEvent item = Event("State", Clip("A"), Clip("B")); AudioBrowserWindow browser = Browser();
            browser.SearchQuery = "State"; browser.FilterMask = 1; browser.SelectAsset(item);
            browser.rootVisualElement.Q<AudioSourcePreviewPane>().Select(1);
            AudioBrowserWindow restored = ScriptableObject.CreateInstance<AudioBrowserWindow>(); browsers.Add(restored); restored.ShowUtility();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(browser), restored); restored.CreateGUI();
            Assert.AreEqual("State", restored.SearchQuery); Assert.AreEqual(1, restored.FilterMask);
            Assert.AreEqual(item, restored.SelectedAsset); Assert.AreEqual(item.Clips[1], restored.SelectedSource);
        }
        [Test] public void PinningKeepsBrowserSelectionIndependentAndFollowClosesOnlyTheOwnedInspector()
        {
            SoundEvent a = Event("Pinned", Clip("A")); SoundEvent b = Event("Selected", Clip("B"));
            AudioBrowserWindow browser = Browser(); browser.SelectAsset(a); browser.PinSelection();
            Assert.AreEqual(a, browser.PinnedAsset); browser.SelectAsset(b);
            Assert.AreEqual(b, browser.SelectedAsset); Assert.AreEqual(b, Selection.activeObject); Assert.AreEqual(a, browser.PinnedAsset);
            StringAssert.Contains(a.name, browser.rootVisualElement.Q<Label>("inspector-status").text);
            browser.FollowSelection(); Assert.IsNull(browser.PinnedAsset); Assert.AreEqual(b, Selection.activeObject);
        }
        [Test] public void RepeatedPinPreservesTheOwnedNativeInspector()
        {
            SoundEvent item = Event("Reuse", Clip("A")); AudioBrowserWindow browser = Browser(); browser.SelectAsset(item);
            browser.PinSelection();
            EditorWindow first = (EditorWindow)new SerializedObject(browser).FindProperty("pinnedInspector").objectReferenceValue;
            Assert.IsNotNull(first);
            browser.PinSelection();
            EditorWindow second = (EditorWindow)new SerializedObject(browser).FindProperty("pinnedInspector").objectReferenceValue;
            Assert.AreEqual(first, second); Assert.AreEqual(item, browser.PinnedAsset);
        }
        [UnityTest] public IEnumerator InspectorKeepsGainVisibleAndResetUsesNativeUndo()
        {
            SoundEvent item = Event("Inspector", Clip("A")); item.VolumeDb = -12;
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(item); temporary.Add(editor);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            VisualElement root = editor.CreateInspectorGUI(); window.rootVisualElement.Add(root); root.Bind(editor.serializedObject);
            yield return null; yield return null;
            DecibelGainField gain = root.Q<DecibelGainField>(); Assert.IsNull(gain.GetFirstAncestorOfType<Foldout>());
            Button reset = gain.Q<Button>("gain-reset");
            Undo.IncrementCurrentGroup();
            using (NavigationSubmitEvent submit = NavigationSubmitEvent.GetPooled()) { submit.target = reset; reset.SendEvent(submit); }
            yield return null; Undo.FlushUndoRecordObjects(); Assert.AreEqual(1, item.Volume);
            Undo.PerformUndo(); yield return null; Assert.AreEqual(-12, item.VolumeDb, 0.0001f);
            window.Close();
        }
        [UnityTest] public IEnumerator DiagnosticNavigationRevealsTheActualLimitsAndCooldownField()
        {
            SoundEvent item = Event("Navigate", Clip("A"));
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(item); temporary.Add(editor);
            EditorWindow window = ScriptableObject.CreateInstance<EditorWindow>(); temporary.Add(window); window.Show();
            VisualElement root = editor.CreateInspectorGUI(); window.rootVisualElement.Add(root); root.Bind(editor.serializedObject);
            yield return null; yield return null;
            bool revealed = false; double until = EditorApplication.timeSinceStartup + 3;
            while (!revealed && EditorApplication.timeSinceStartup < until)
            {
                revealed = AudioWorkspace.RevealSection(root, "Playback limits", "cooldownSeconds");
                yield return null;
            }
            Assert.IsTrue(revealed);
            Assert.IsTrue(root.Q<Foldout>("section-Playback limits").value);
            Assert.IsNotNull(root.Q("cooldownSeconds").Q<FloatField>());
            window.Close();
        }
        [Test] public void PreviewOwnershipEndsOnSourceChangeAndWindowClose()
        {
            SoundEvent item = Event("Preview", Clip("A"), Clip("B")); AudioBrowserWindow browser = Browser(); browser.SelectAsset(item);
            EditorAudioPreview.Play(item.Clips[0]); Assert.AreEqual(item.Clips[0], EditorAudioPreview.CurrentClip);
            browser.rootVisualElement.Q<AudioSourcePreviewPane>().Select(1); Assert.IsNull(EditorAudioPreview.CurrentClip);
            EditorAudioPreview.Play(item.Clips[1]); browser.Close(); Assert.IsNull(EditorAudioPreview.CurrentClip);
        }
        [Test] public void SummariesUseCurrentValuesAndMixedState()
        {
            SoundEvent a = Event("One", Clip("A")); SoundEvent b = Event("Two", Clip("B"));
            SerializedObject single = new SerializedObject(a);
            Assert.AreEqual("Off", SoundEventSummaries.Get(single, "Variation"));
            Assert.AreEqual("Unlimited", SoundEventSummaries.Get(single, "Playback limits"));
            a.MaxInstancesPerEmitter = 2; single.Update(); Assert.AreEqual("2 / emitter", SoundEventSummaries.Get(single, "Playback limits"));
            SerializedObject mixed = new SerializedObject(new Object[] { a, b }); Assert.AreEqual("Mixed", SoundEventSummaries.Get(mixed, "Playback limits"));
        }
        [Test] public void EmitterAssignmentAndComponentCreationAreUndoable()
        {
            SoundEvent item = Event("Bind", Clip("A")); GameObject go = new GameObject("Workflow emitter"); temporary.Add(go);
            SoundEmitter emitter = SoundEventAuthoring.AssignEmitter(item, go); Assert.AreEqual(item, emitter.Event);
            Undo.PerformUndo(); Assert.IsNull(go.GetComponent<SoundEmitter>());
            Undo.PerformRedo(); Assert.AreEqual(item, go.GetComponent<SoundEmitter>().Event);
        }
        [Test] public void EmitterAssignmentRecordsPrefabOverridesAndRestoresThePreviousEvent()
        {
            SoundEvent a = Event("Original", Clip("A")); SoundEvent b = Event("Replacement", Clip("B"));
            GameObject original = new GameObject("Emitter prefab"); temporary.Add(original); SoundEventAuthoring.AssignEmitter(a, original);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(original, folder + "/Emitter.prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab); temporary.Add(instance);
            SoundEmitter emitter = SoundEventAuthoring.AssignEmitter(b, instance);
            SerializedObject serialized = new SerializedObject(emitter); Assert.IsTrue(serialized.FindProperty("soundEvent").prefabOverride);
            Undo.PerformUndo(); Assert.AreEqual(a, emitter.Event); serialized.Update(); Assert.IsFalse(serialized.FindProperty("soundEvent").prefabOverride);
        }
    }
}
