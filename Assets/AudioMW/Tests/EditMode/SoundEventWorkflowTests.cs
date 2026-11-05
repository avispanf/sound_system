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
        public void DiagnosticReasonsDistinguishAcceptedSilenceFromRejection()
        {
            Assert.AreEqual("Source volume is zero", EventDebuggerWindow.Reason(new EventDebugRecord { Outcome = PlaybackOutcome.Played, HasGainCapture = true, FinalVolume = 0 }));
            Assert.AreEqual("Playback request accepted", EventDebuggerWindow.Reason(new EventDebugRecord { Outcome = PlaybackOutcome.Played }));
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
