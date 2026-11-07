using System;
using System.Collections;
using System.IO;
using System.Linq;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class AudioMixPlannerTests
    {
        private string folder;
        private AudioMixRequest request;
        private SoundEvent sound;
        private GameObject target;
        [SetUp] public void SetUp()
        {
            folder = "Assets/MixTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            target = new GameObject("Mix owner"); EditorSceneManager.SaveScene(target.scene, folder + "/Game.unity");
            AudioClip clip = AudioClip.Create("Clip", 64, 1, 44100, false); AssetDatabase.CreateAsset(clip, folder + "/Clip.asset");
            sound = SoundEvent.CreateRuntime(clip); AssetDatabase.CreateAsset(sound, folder + "/Event.asset");
            AssetDatabase.SaveAssets();
            AudioProjectBrief brief = new AudioProjectBrief { Game = "Test", Genre = "Game", Audience = "Players", Platforms = "Desktop", SourceFolder = folder, ProfilePath = folder + "/Setup.asset" };
            AudioProjectSetup setup = AudioProjectPlanner.SaveReviewedPlan(AudioProjectPlanner.Analyse(brief), brief);
            request = new AudioMixRequest { Setup = setup, Folder = folder, Name = "Mix", SceneTarget = target, Routes = new[] { new AudioRouteChoice { Asset = sound, Bus = AudioMixBus.SFX } } };
        }
        [TearDown] public void TearDown()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); Undo.ClearAll(); AssetDatabase.DeleteAsset(folder);
        }
        [Test] public void TemplateHasIndependentDuckingAndNeutralDefaults()
        {
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixPlanner.TemplatePath);
            AudioMixPlanner.ValidateMixer(mixer);
            Assert.AreEqual(8, mixer.FindMatchingGroups("").Length);
            Assert.AreNotSame(AudioMixPlanner.Group(mixer, AudioMixBus.Music), AudioMixPlanner.Group(mixer, AudioMixBus.Ambience));
            Assert.IsTrue(mixer.GetFloat(AudioMixPlanner.MusicDuck, out float music)); Assert.AreEqual(0, music);
            Assert.IsTrue(mixer.GetFloat(AudioMixPlanner.AmbienceDuck, out float ambience)); Assert.AreEqual(0, ambience);
        }
        [Test] public void PreparingAndRejectingAreReadOnly()
        {
            string[] files = Directory.GetFiles(folder).OrderBy(value => value).ToArray(); byte[][] before = files.Select(File.ReadAllBytes).ToArray();
            AudioMixPlan plan = AudioMixPlanner.Prepare(request); plan.ReadChanges()[0].After = "Tampered";
            Assert.That(plan.ReadChanges()[0].After, Does.Not.Contain("Tampered"));
            AudioMixSetupWindow window = ScriptableObject.CreateInstance<AudioMixSetupWindow>();
            try { window.LoadDraft(request); window.Prepare(); Assert.IsTrue(window.rootVisualElement.Q<Button>("mix-approve").enabledSelf); window.Reject(); Assert.IsFalse(window.rootVisualElement.Q<Button>("mix-approve").enabledSelf); }
            finally { Object.DestroyImmediate(window); }
            Assert.AreEqual(files, Directory.GetFiles(folder).OrderBy(value => value).ToArray());
            for (int i = 0; i < files.Length; i++) CollectionAssert.AreEqual(before[i], File.ReadAllBytes(files[i]));
            Assert.IsNull(target.GetComponent<AudioMixScope>()); Assert.IsNull(sound.MixerGroup);
        }
        [Test] public void ApplyPersistsRoutesScopeAndRepeatIsANoOp()
        {
            AudioMixPlan plan = AudioMixPlanner.Prepare(request); AudioMixInstallation receipt = AudioMixPlanner.Apply(plan, request);
            Assert.IsNotNull(receipt); AudioMixProfile profile = AssetDatabase.LoadAssetAtPath<AudioMixProfile>(AudioMixPlanner.MixPath(request));
            Assert.AreSame(profile, target.GetComponent<AudioMixScope>().Profile); Assert.IsTrue(profile.IsUsable);
            Assert.AreEqual("SFX", sound.MixerGroup.name); Assert.AreSame(profile.Routing.Mixer, sound.MixerGroup.audioMixer);
            Assert.AreNotSame(AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixPlanner.TemplatePath), profile.Routing.Mixer);
            Assert.IsEmpty(AudioMixPlanner.Check(AudioMixPlanner.Prepare(request), request));
            Assert.IsTrue(AudioMixPlanner.Prepare(request).AlreadyApplied);
            Assert.AreSame(receipt, AudioMixPlanner.Apply(AudioMixPlanner.Prepare(request), request)); Assert.AreEqual(1, target.GetComponents<AudioMixScope>().Length);
            EditorSceneManager.OpenScene(folder + "/Game.unity");
            Assert.IsNotNull(Object.FindObjectOfType<AudioMixScope>());
        }
        [Test] public void ChangedInputsOrRequestRequireFreshApproval()
        {
            AudioMixPlan plan = AudioMixPlanner.Prepare(request); request.MusicDuckDb = -12;
            Assert.IsNotEmpty(AudioMixPlanner.Check(plan, request)); Assert.Throws<InvalidOperationException>(() => AudioMixPlanner.Apply(plan, request));
            plan = AudioMixPlanner.Prepare(request); sound.Volume = 0.6f; EditorUtility.SetDirty(sound);
            Assert.That(AudioMixPlanner.Check(plan, request), Does.Contain("unsaved audio")); AssetDatabase.SaveAssetIfDirty(sound);
            Assert.IsNotEmpty(AudioMixPlanner.Check(plan, request)); Assert.IsFalse(File.Exists(AudioMixPlanner.MixPath(request)));
        }
        [Test] public void ExistingOutputAndInvalidCategoryAreRejected()
        {
            File.WriteAllText(folder + "/Mix.mixer", "unrelated");
            Assert.Throws<InvalidOperationException>(() => AudioMixPlanner.Prepare(request)); File.Delete(folder + "/Mix.mixer");
            request.Routes[0].Bus = (AudioMixBus)999; Assert.Throws<ArgumentException>(() => AudioMixPlanner.Prepare(request));
        }
        [Test] public void ExistingRoutesNeedExplicitReplacementAndChangedReceiptIsPreserved()
        {
            sound.MixerGroup = AudioMixPlanner.Group(AssetDatabase.LoadAssetAtPath<AudioMixer>(AudioMixPlanner.TemplatePath), AudioMixBus.UI); EditorUtility.SetDirty(sound); AssetDatabase.SaveAssetIfDirty(sound);
            Assert.Throws<InvalidOperationException>(() => AudioMixPlanner.Prepare(request)); request.AllowReroute = true;
            AudioMixPlanner.Apply(AudioMixPlanner.Prepare(request), request);
            sound.Volume = 0.7f; EditorUtility.SetDirty(sound); AssetDatabase.SaveAssetIfDirty(sound);
            Assert.Throws<InvalidOperationException>(() => AudioMixPlanner.Prepare(request)); Assert.AreEqual(0.7f, sound.Volume);
        }
        [Test] public void FailureAfterSavingRoutesRestoresBytesAndRemovesGeneratedContent()
        {
            string path = AssetDatabase.GetAssetPath(sound); byte[] before = File.ReadAllBytes(path); byte[] scene = File.ReadAllBytes(target.scene.path);
            Assert.Throws<OperationCanceledException>(() => AudioMixPlanner.Apply(AudioMixPlanner.Prepare(request), request, stage => { if (stage == "Routes saved") throw new OperationCanceledException(); }));
            CollectionAssert.AreEqual(before, File.ReadAllBytes(path)); CollectionAssert.AreEqual(scene, File.ReadAllBytes(target.scene.path));
            Assert.IsNull(target.GetComponent<AudioMixScope>()); Assert.IsFalse(File.Exists(folder + "/Mix.mixer")); Assert.IsFalse(File.Exists(AudioMixPlanner.MixPath(request))); Assert.IsNull(sound.MixerGroup);
        }
        [Test] public void ReusePreservesMixerAndRemovesOnlyReviewedSourceDuckBinding()
        {
            string mixerPath = folder + "/Existing.mixer"; Assert.IsTrue(AssetDatabase.CopyAsset(AudioMixPlanner.TemplatePath, mixerPath));
            request.ExistingMixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            request.DuckParameter = SoundParameter.CreateRuntime(0, 1, 0); AssetDatabase.CreateAsset(request.DuckParameter, folder + "/Duck.asset");
            sound.ParameterBindings = new[] { ParameterBinding.CreateRuntime(request.DuckParameter, ParameterTarget.Volume, null), ParameterBinding.CreateRuntime(request.DuckParameter, ParameterTarget.Pitch, null) };
            request.Routes[0].Bus = AudioMixBus.Ambience; request.Routes[0].RemoveSourceDuck = true;
            EditorUtility.SetDirty(sound); AssetDatabase.SaveAssets(); byte[] before = File.ReadAllBytes(mixerPath);
            AudioMixPlanner.Apply(AudioMixPlanner.Prepare(request), request);
            CollectionAssert.AreEqual(before, File.ReadAllBytes(mixerPath)); Assert.AreEqual(1, sound.ParameterBindings.Length);
            Assert.AreEqual(ParameterTarget.Pitch, sound.ParameterBindings[0].Target); Assert.AreEqual("AmbienceDuck", sound.MixerGroup.name);
            Assert.IsFalse(File.Exists(folder + "/Mix.mixer"));
        }
        [Test] public void NonGlobalDuckAndUnsavedSceneCannotBeApproved()
        {
            request.DuckParameter = SoundParameter.CreateRuntime(0, 1, 0); AssetDatabase.CreateAsset(request.DuckParameter, folder + "/Duck.asset");
            SerializedObject parameter = new SerializedObject(request.DuckParameter); parameter.FindProperty("isGlobal").boolValue = false; parameter.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets();
            Assert.Throws<ArgumentException>(() => AudioMixPlanner.Prepare(request)); request.DuckParameter = null;
            EditorSceneManager.MarkSceneDirty(target.scene); Assert.Throws<InvalidOperationException>(() => AudioMixPlanner.Prepare(request));
        }
        [UnityTest] public IEnumerator RebuildingOrEditingWindowRevokesApproval()
        {
            AudioMixSetupWindow window = ScriptableObject.CreateInstance<AudioMixSetupWindow>();
            try
            {
                window.Show(); window.LoadDraft(request); window.Prepare(); yield return null;
                Assert.IsTrue(window.rootVisualElement.Q<Button>("mix-approve").enabledSelf);
                window.rootVisualElement.Query<FloatField>().First().value = -10;
                Assert.IsFalse(window.rootVisualElement.Q<Button>("mix-approve").enabledSelf);
                window.Prepare(); window.CreateGUI(); Assert.IsFalse(window.rootVisualElement.Q<Button>("mix-approve").enabledSelf);
            }
            finally { window.Close(); }
            Assert.IsNull(sound.MixerGroup);
        }
    }
}
