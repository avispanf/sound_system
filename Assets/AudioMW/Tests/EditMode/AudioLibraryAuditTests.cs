using System;
using System.Linq;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class AudioLibraryAuditTests
    {
        private string folder;
        [SetUp] public void SetUp()
        {
            folder = "Assets/AudioMWAudit_" + Guid.NewGuid().ToString("N"); AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        }
        [TearDown] public void TearDown() { AssetDatabase.DeleteAsset(folder); }
        private T Save<T>(T asset, string name) where T : Object
        {
            asset.name = name; AssetDatabase.CreateAsset(asset, folder + "/" + name + ".asset"); return asset;
        }
        private SoundEvent Leaf(string name)
        {
            AudioClip clip = Save(AudioClip.Create(name + " clip", 64, 1, 44100, false), name + " clip");
            return Save(SoundEvent.CreateRuntime(clip), name);
        }
        [Test] public void CleanLibraryScanDoesNotDirtyAssetsOrAdvanceSelection()
        {
            SoundEvent item = Leaf("Clean"); item.SelectionMode = ClipSelectionMode.Sequential;
            AudioClip second = Save(AudioClip.Create("Second", 64, 1, 44100, false), "Second"); item.Clips = new[] { item.Clips[0], second };
            Assert.AreSame(item.Clips[0], item.Resolve(new System.Random(1)).Clip);
            SoundBank bank = Save(SoundBank.CreateRuntime(item), "Bank");
            Save(SoundParameter.CreateRuntime(0, 1, 0), "Parameter");
            int dirty = EditorUtility.GetDirtyCount(item);
            AudioLibraryReport report = AudioLibraryAudit.Scan(folder);
            Assert.AreEqual(3, report.AssetsScanned); Assert.IsEmpty(report.Issues);
            Assert.AreEqual(dirty, EditorUtility.GetDirtyCount(item)); Assert.IsFalse(bank.IsLoaded);
            Assert.AreSame(second, item.Resolve(new System.Random(1)).Clip);
        }
        [Test] public void InvalidProfileAndRangeAreErrorsButEmptyEventAndVoiceAreWarnings()
        {
            Save(SoundEvent.CreateRuntime(), "Empty"); Save(VoiceLine.CreateRuntime(null, "", ""), "Voice");
            Save(ScriptableObject.CreateInstance<MusicStateProfile>(), "Profile");
            SoundParameter parameter = Save(SoundParameter.CreateRuntime(2, 1, 0), "Range");
            AudioLibraryReport report = AudioLibraryAudit.Scan(folder);
            Assert.AreEqual(2, report.Errors); Assert.AreEqual(2, report.Warnings);
            Assert.IsTrue(report.Issues.Any(issue => issue.Asset == parameter && issue.Rule == "Parameter range"));
        }
        [Test] public void BankOutsideTheScopeStillCoversNestedAddressedEventsWithoutLoading()
        {
            AssetDatabase.CreateFolder(folder, "Events");
            SoundEvent leaf = SoundEvent.CreateRuntime(); leaf.ClipReferences = new[] { ClipReference.Addressed("steps/grass") };
            AssetDatabase.CreateAsset(leaf, folder + "/Events/Leaf.asset");
            Assert.IsTrue(AudioLibraryAudit.Scan(folder + "/Events").Issues.Any(issue => issue.Rule == "Bank membership"));
            SoundEvent root = Save(SoundEvent.CreateRuntime(), "Root"); root.ContainerMode = ContainerMode.Layered; root.Children = new[] { SoundEventChild.Create(leaf) };
            SoundBank bank = Save(SoundBank.CreateRuntime(root), "Bank");
            Assert.IsFalse(AudioLibraryAudit.Scan(folder + "/Events").Issues.Any(issue => issue.Rule == "Bank membership"));
            Assert.IsFalse(bank.IsLoaded); Assert.IsFalse(leaf.ClipReferences[0].IsResolved);
        }
        [Test] public void SubassetsHaveDistinctExportableLocalIdsAndStableSnapshots()
        {
            SoundEvent root = Save(SoundEvent.CreateRuntime(), "Container"); root.ContainerMode = ContainerMode.Layered;
            SoundEvent child = SoundEvent.CreateRuntime(); child.name = "Child"; AssetDatabase.AddObjectToAsset(child, root); root.Children = new[] { SoundEventChild.Create(child) };
            AudioLibraryReport report = AudioLibraryAudit.Scan(folder);
            Assert.AreEqual(2, report.AssetsScanned);
            AudioLibraryIssue a = report.Issues.First(issue => issue.Asset == root); AudioLibraryIssue b = report.Issues.First(issue => issue.Asset == child);
            Assert.AreEqual(a.Guid, b.Guid); Assert.AreNotEqual(a.LocalId, b.LocalId);
            string json = JsonUtility.ToJson(report); Assert.IsFalse(json.Contains("instanceID"));
            AudioLibraryReport copy = JsonUtility.FromJson<AudioLibraryReport>(json);
            Assert.AreEqual(report.Issues.Count, copy.Issues.Count); Assert.AreEqual(json, JsonUtility.ToJson(AudioLibraryAudit.Scan(folder)));
        }
        [Test] public void WeightsAndEmptyBankSlotsAreReportedWithoutNormalizingLegacyData()
        {
            SoundEvent item = Leaf("Weighted"); item.SelectionMode = ClipSelectionMode.WeightedRandom; item.ClipWeights = new[] { -2f };
            Save(SoundBank.CreateRuntime((SoundEvent)null), "Bank");
            AudioLibraryReport report = AudioLibraryAudit.Scan(folder);
            Assert.IsTrue(report.Issues.Any(issue => issue.Rule == "Invalid weight"));
            Assert.IsTrue(report.Issues.Any(issue => issue.Rule == "Empty bank event"));
            Assert.AreEqual(-2, item.ClipWeights[0]);
        }
        [Test] public void FailedWindowScanClearsExportAndReportsIncompleteStatus()
        {
            AudioLibraryAuditWindow window = ScriptableObject.CreateInstance<AudioLibraryAuditWindow>();
            try
            {
                SerializedObject data = new SerializedObject(window); data.FindProperty("scope").stringValue = "Assets/MissingAudioMWAuditFolder"; data.ApplyModifiedPropertiesWithoutUndo();
                window.CreateGUI(); window.Scan();
                StringAssert.StartsWith("Scan could not complete", window.rootVisualElement.Q<Label>("audit-summary").text);
                Assert.IsFalse(window.rootVisualElement.Q<Button>("audit-export").enabledSelf);
                Assert.IsFalse(window.rootVisualElement.Q<Button>("audit-open").enabledSelf);
            }
            finally { Object.DestroyImmediate(window); }
        }

        [TestCase("Packages")]
        [TestCase("Assets/MissingAudioMWAuditFolder")]
        public void InvalidScopeIsRejected(string scope) { Assert.Throws<ArgumentException>(() => AudioLibraryAudit.Scan(scope)); }
    }
}
