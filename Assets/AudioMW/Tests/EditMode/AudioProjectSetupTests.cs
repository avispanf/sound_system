using System;
using System.Collections;
using System.IO;
using System.Linq;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class AudioProjectSetupTests
    {
        private string folder;
        private AudioProjectBrief brief;
        [SetUp] public void SetUp()
        {
            folder = "Assets/SetupTests_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring(7));
            brief = new AudioProjectBrief { Game = "Relay Field", Genre = "3D collection", Audience = "Indie players", Platforms = "macOS", SourceFolder = folder, ProfilePath = folder + "/Setup.asset" };
        }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(folder); }
        private SoundEvent Event(string name)
        {
            AudioClip clip = AudioClip.Create(name, 64, 1, 44100, false);
            AssetDatabase.CreateAsset(clip, folder + "/" + name + "Clip.asset");
            SoundEvent item = SoundEvent.CreateRuntime(clip);
            AssetDatabase.CreateAsset(item, folder + "/" + name + ".asset");
            AssetDatabase.SaveAssets(); return item;
        }
        [Test] public void AnalysisIsReadOnlyAndPlanCopiesCannotAlterApproval()
        {
            SoundEvent item = Event("Step");
            byte[] before = File.ReadAllBytes(AssetDatabase.GetAssetPath(item));
            string[] files = Directory.GetFiles(folder).OrderBy(value => value).ToArray();
            AudioProjectPlan plan = AudioProjectPlanner.Analyse(brief);
            AudioProjectReviewData copy = plan.Read(); copy.BriefJson = "changed"; copy.Inventory.Clear();
            Assert.AreEqual(files, Directory.GetFiles(folder).OrderBy(value => value).ToArray());
            Assert.AreEqual(before, File.ReadAllBytes(AssetDatabase.GetAssetPath(item)));
            Assert.IsFalse(EditorUtility.IsDirty(item)); Assert.IsFalse(File.Exists(brief.ProfilePath));
            Assert.IsTrue(plan.Read().Inventory.Any(value => value.Path == AssetDatabase.GetAssetPath(item)));
            Assert.IsEmpty(AudioProjectPlanner.Check(plan, brief));
        }
        [Test] public void BriefChangesInvalidateThePlan()
        {
            AudioProjectPlan plan = AudioProjectPlanner.Analyse(brief); brief.Audience = "New audience";
            Assert.That(AudioProjectPlanner.Check(plan, brief), Does.Contain("brief changed"));
            Assert.Throws<InvalidOperationException>(() => AudioProjectPlanner.SaveReviewedPlan(plan, brief));
            Assert.IsFalse(File.Exists(brief.ProfilePath));
        }
        [Test] public void AddedRemovedAndEditedAudioInvalidateThePlan()
        {
            SoundEvent item = Event("Step"); AudioProjectPlan plan = AudioProjectPlanner.Analyse(brief);
            Event("Added"); Assert.That(AudioProjectPlanner.Check(plan, brief), Does.Contain("inputs changed"));
            plan = AudioProjectPlanner.Analyse(brief); item.Volume = 0.3f; EditorUtility.SetDirty(item);
            Assert.That(AudioProjectPlanner.Check(plan, brief), Does.Contain("unsaved audio"));
            AssetDatabase.SaveAssetIfDirty(item);
            Assert.That(AudioProjectPlanner.Check(plan, brief), Does.Contain("inputs changed"));
            plan = AudioProjectPlanner.Analyse(brief); AssetDatabase.DeleteAsset(folder + "/Added.asset");
            Assert.That(AudioProjectPlanner.Check(plan, brief), Does.Contain("inputs changed"));
        }
        [Test] public void ApprovalWritesOnlyTheProfileAndPersistsReviewedEvidence()
        {
            SoundEvent item = Event("Step"); byte[] before = File.ReadAllBytes(AssetDatabase.GetAssetPath(item));
            AudioProjectPlan plan = AudioProjectPlanner.Analyse(brief);
            AudioProjectSetup saved = AudioProjectPlanner.SaveReviewedPlan(plan, brief);
            Assert.AreEqual(1, saved.Revision); Assert.AreEqual(brief.Game, saved.Brief.Game);
            Assert.AreEqual(plan.Json, saved.ReviewedPlanJson);
            Assert.AreEqual(before, File.ReadAllBytes(AssetDatabase.GetAssetPath(item)));
            Assert.AreEqual(32, saved.Brief.VoiceBudget);
            Assert.IsFalse(EditorUtility.IsDirty(saved));
            AudioProjectPlan repeat = AudioProjectPlanner.Analyse(brief);
            Assert.AreSame(saved, AudioProjectPlanner.SaveReviewedPlan(repeat, brief)); Assert.AreEqual(1, saved.Revision);
            brief.MixIntent = "Prioritize dialogue";
            AudioProjectPlanner.SaveReviewedPlan(AudioProjectPlanner.Analyse(brief), brief);
            Assert.AreEqual(2, saved.Revision); Assert.AreEqual("Prioritize dialogue", saved.Brief.MixIntent);
        }
        [Test] public void UpdatedRecommendationsRequireANewSavedRevision()
        {
            AudioProjectSetup setup = AudioProjectPlanner.SaveReviewedPlan(AudioProjectPlanner.Analyse(brief), brief);
            AudioProjectReviewData legacy = JsonUtility.FromJson<AudioProjectReviewData>(setup.ReviewedPlanJson);
            legacy.Recommendations[0].Reason = "Legacy recommendation";
            SerializedObject serialized = new SerializedObject(setup);
            serialized.FindProperty("reviewedPlanJson").stringValue = JsonUtility.ToJson(legacy, true);
            serialized.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssetIfDirty(setup);
            AudioProjectPlan review = AudioProjectPlanner.Analyse(brief);
            AudioProjectPlanner.SaveReviewedPlan(review, brief);
            Assert.AreEqual(2, setup.Revision); Assert.AreEqual(review.Json, setup.ReviewedPlanJson);
        }
        [Test] public void ChangingSavedDestinationAfterReviewRequiresNewAnalysis()
        {
            AudioProjectPlanner.SaveReviewedPlan(AudioProjectPlanner.Analyse(brief), brief);
            AudioProjectPlan review = AudioProjectPlanner.Analyse(brief);
            File.AppendAllText(brief.ProfilePath, "\n");
            Assert.That(AudioProjectPlanner.Check(review, brief), Does.Contain("destination changed"));
            Assert.Throws<InvalidOperationException>(() => AudioProjectPlanner.SaveReviewedPlan(review, brief));
        }
        [Test] public void DestinationConflictsAndUnsavedProfileEditsArePreserved()
        {
            SoundEvent item = Event("Step"); AudioProjectPlan plan = AudioProjectPlanner.Analyse(brief);
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(item), brief.ProfilePath);
            Assert.Throws<InvalidOperationException>(() => AudioProjectPlanner.SaveReviewedPlan(plan, brief));
            Assert.IsInstanceOf<SoundEvent>(AssetDatabase.LoadMainAssetAtPath(brief.ProfilePath));
            AssetDatabase.DeleteAsset(brief.ProfilePath);
            AudioProjectSetup setup = AudioProjectPlanner.SaveReviewedPlan(AudioProjectPlanner.Analyse(brief), brief);
            EditorUtility.SetDirty(setup);
            Assert.Throws<ArgumentException>(() => AudioProjectPlanner.Analyse(brief));
        }
        [TestCase("Assets/../Outside.asset")]
        [TestCase("Packages/Setup.asset")]
        [TestCase("Assets/Setup.json")]
        public void InvalidDestinationsCannotProducePlans(string path)
        {
            brief.ProfilePath = path; Assert.Throws<ArgumentException>(() => AudioProjectPlanner.Analyse(brief));
        }
        [Test] public void MissingIntentAndInvalidBudgetsRequireAnAnswer()
        {
            brief.Audience = ""; Assert.Throws<ArgumentException>(() => AudioProjectPlanner.Analyse(brief));
            brief.Audience = "Players"; brief.VoiceBudget = 0;
            Assert.Throws<ArgumentException>(() => AudioProjectPlanner.Analyse(brief));
        }
        [UnityTest] public IEnumerator RejectEditingAndRebuildingTheViewRequireFreshReview()
        {
            AudioProjectSetupWindow window = ScriptableObject.CreateInstance<AudioProjectSetupWindow>();
            try
            {
                window.Show(); yield return null; window.LoadDraft(brief); window.Analyse();
                Button approve = window.rootVisualElement.Q<Button>("setup-approve"); Assert.IsTrue(approve.enabledSelf);
                window.Reject(); Assert.IsFalse(approve.enabledSelf); Assert.IsFalse(File.Exists(brief.ProfilePath));
                window.Analyse(); Assert.IsTrue(approve.enabledSelf);
                window.rootVisualElement.Q<TextField>("setup-genre").value = "Changed";
                Assert.IsFalse(approve.enabledSelf); Assert.IsFalse(File.Exists(brief.ProfilePath));
                window.CreateGUI(); Assert.IsFalse(window.rootVisualElement.Q<Button>("setup-approve").enabledSelf);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }
}
