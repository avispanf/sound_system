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
    public sealed class DecibelAuthoringTests
    {
        private readonly List<Object> assets = new List<Object>();
        private EditorWindow window;
        private string legacyPath;
        [TearDown] public void TearDown()
        {
            if (window != null) window.Close();
            if (legacyPath != null) AssetDatabase.DeleteAsset(legacyPath);
            legacyPath = null;
            foreach (Object asset in assets) if (asset != null) Object.DestroyImmediate(asset);
            assets.Clear();
        }
        private SoundEvent Event(float volume = 1f)
        {
            SoundEvent item = SoundEvent.CreateRuntime(); assets.Add(item); item.Volume = volume; return item;
        }
        private AudioClip Clip()
        {
            AudioClip clip = AudioClip.Create("Gain test", 4410, 1, 44100, false); assets.Add(clip); return clip;
        }
        [TestCase(0f, 1f)]
        [TestCase(-6.0206f, 0.5f)]
        [TestCase(-20f, 0.1f)]
        [TestCase(-80f, 0.0001f)]
        [TestCase(-120f, 0.000001f)]
        [TestCase(6.0206f, 2f)]
        public void DecibelsDescribeAmplitudeRatios(float decibels, float linear)
        {
            Assert.AreEqual(linear, AudioGain.FromDecibels(decibels), linear * 0.00001f);
            Assert.AreEqual(decibels, AudioGain.ToDecibels(linear), 0.0001f);
        }
        [Test] public void SilenceAndInvalidInputCannotCreateNaNVolume()
        {
            Assert.AreEqual(float.NegativeInfinity, AudioGain.ToDecibels(0));
            Assert.AreEqual(0, AudioGain.FromDecibels(float.NegativeInfinity));
            SoundEvent item = Event(); item.VolumeDb = float.NaN; Assert.AreEqual(0, item.Volume);
            item.VolumeDb = float.PositiveInfinity; Assert.AreEqual(1, item.Volume);
        }
        [Test] public void GainAliasesRetainTheLinearSerializationContract()
        {
            SoundEvent item = Event(); item.VolumeDb = -6.0206f;
            SerializedObject serialized = new SerializedObject(item);
            Assert.AreEqual(0.5f, serialized.FindProperty("volume").floatValue, 0.00001f);
            MusicTrack track = MusicTrack.CreateRuntime(null, null, 120, 4); assets.Add(track); track.VolumeDb = -20;
            VoiceLine line = VoiceLine.CreateRuntime(null, "", ""); assets.Add(line); line.VolumeDb = -20;
            SoundEventChild child = SoundEventChild.Create(item); child.GainDb = -20;
            Assert.AreEqual(0.1f, track.Volume, 0.00001f); Assert.AreEqual(0.1f, line.Volume, 0.00001f);
            Assert.AreEqual(0.1f, child.Gain, 0.00001f);
        }
        [Test] public void LegacyDataWithoutNewFieldsKeepsAdditiveOffsetsAndRandomSequence()
        {
            SoundEvent item = ScriptableObject.CreateInstance<SoundEvent>(); assets.Add(item);
            SerializedObject authored = new SerializedObject(item);
            authored.FindProperty("volume").floatValue = 0.4f;
            authored.FindProperty("volumeRandomMin").floatValue = -0.1f;
            authored.FindProperty("volumeRandomMax").floatValue = 0.2f;
            authored.ApplyModifiedPropertiesWithoutUndo();
            legacyPath = "Assets/AudioMWLegacy_" + Guid.NewGuid().ToString("N") + ".asset";
            AssetDatabase.CreateAsset(item, legacyPath); assets.Remove(item); AssetDatabase.SaveAssets();
            string yaml = System.IO.File.ReadAllText(legacyPath);
            yaml = System.Text.RegularExpressions.Regex.Replace(yaml, @"^  (volumeVariationMode|volumeRandomDbMin|volumeRandomDbMax):.*\r?\n", "", System.Text.RegularExpressions.RegexOptions.Multiline);
            System.IO.File.WriteAllText(legacyPath, yaml);
            AssetDatabase.ImportAsset(legacyPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            item = AssetDatabase.LoadAssetAtPath<SoundEvent>(legacyPath);
            item.Clips = new[] { Clip() }; item.SelectionMode = ClipSelectionMode.Sequential;
            Assert.AreEqual(VolumeVariationMode.LinearOffsets, item.VolumeVariationMode);
            System.Random actual = new System.Random(41); System.Random expected = new System.Random(41);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(0.4f - 0.1f + (float)expected.NextDouble() * 0.3f, item.Resolve(actual).Volume, 0.000001f);
        }
        [Test] public void DecibelVariationIsUniformInDecibelsAndRelativeToTheBaseGain()
        {
            SoundEvent item = Event(); item.Clips = new[] { Clip() }; item.SelectionMode = ClipSelectionMode.Sequential;
            item.VolumeDb = -12; item.VolumeRandomDbMin = -3; item.VolumeRandomDbMax = 3;
            System.Random actual = new System.Random(99); System.Random expected = new System.Random(99);
            for (int i = 0; i < 100; i++)
                Assert.AreEqual(-15f + (float)expected.NextDouble() * 6f, AudioGain.ToDecibels(item.Resolve(actual).Volume), 0.00001f);
        }
        [Test] public void SwitchingUnitsKeepsIndependentAuthoredRanges()
        {
            SoundEvent item = Event(0.4f); item.Clips = new[] { Clip() };
            SerializedObject serialized = new SerializedObject(item);
            serialized.FindProperty("volumeRandomMin").floatValue = -0.1f;
            serialized.FindProperty("volumeRandomMax").floatValue = -0.1f; serialized.ApplyModifiedProperties();
            item.VolumeRandomDbMin = item.VolumeRandomDbMax = -6.0206f;
            Assert.AreEqual(0.2f, item.Resolve(new System.Random(1)).Volume, 0.00001f);
            item.VolumeVariationMode = VolumeVariationMode.LinearOffsets;
            Assert.AreEqual(0.3f, item.Resolve(new System.Random(1)).Volume, 0.00001f);
            item.VolumeVariationMode = VolumeVariationMode.Decibels;
            Assert.AreEqual(0.2f, item.Resolve(new System.Random(1)).Volume, 0.00001f);
        }
        [Test] public void VariationCannotBoostAboveUnityOrUnmuteASilentBase()
        {
            SoundEvent item = Event(0.5f); item.Clips = new[] { Clip() };
            item.VolumeRandomDbMin = item.VolumeRandomDbMax = 24;
            Assert.AreEqual(1, item.Resolve(new System.Random(1)).Volume);
            item.Volume = 0; Assert.AreEqual(0, item.Resolve(new System.Random(1)).Volume);
        }
        [Test] public void FieldPreservesQuietAndSilentLevelsUntilAnActualEdit()
        {
            DecibelGainField field = new DecibelGainField("Gain");
            field.SetValueWithoutNotify(0.000001f);
            Assert.AreEqual(0.000001f, field.value); Assert.AreEqual("-120", field.Q<TextField>().value);
            field.SetValueWithoutNotify(0); Assert.AreEqual("−∞", field.Q<TextField>().value);
            float value; Assert.IsTrue(DecibelGainField.TryParse("-inf", out value)); Assert.AreEqual(0, value);
            Assert.IsFalse(DecibelGainField.TryParse("NaN", out value));
            Assert.IsTrue(DecibelGainField.TryParse("-20", out value)); Assert.AreEqual(0.1f, value, 0.00001f);
        }
        private DecibelGainField BoundField(SerializedObject serialized)
        {
            window = ScriptableObject.CreateInstance<EditorWindow>(); window.position = new Rect(100, 100, 320, 160); window.Show();
            DecibelGainField field = new DecibelGainField("Gain"); window.rootVisualElement.Add(field);
            field.BindProperty(serialized.FindProperty("volume")); return field;
        }
        [UnityTest] public IEnumerator MultiEditAndUndoRedoUseTheOriginalLinearValues()
        {
            SoundEvent a = Event(0.25f); SoundEvent b = Event(0.75f);
            SerializedObject serialized = new SerializedObject(new Object[] { a, b });
            DecibelGainField field = BoundField(serialized); yield return null; yield return null;
            Assert.IsTrue(field.showMixedValue); Assert.AreEqual("—", field.Q<TextField>().value);
            Undo.IncrementCurrentGroup(); field.Q<Slider>().value = -20; yield return null;
            Undo.FlushUndoRecordObjects(); Assert.AreEqual(0.1f, a.Volume, 0.00001f); Assert.AreEqual(a.Volume, b.Volume);
            Undo.PerformUndo(); yield return null;
            Assert.AreEqual(0.25f, a.Volume); Assert.AreEqual(0.75f, b.Volume);
            Undo.PerformRedo(); yield return null;
            Assert.AreEqual(0.1f, a.Volume, 0.00001f); Assert.AreEqual(a.Volume, b.Volume);
        }
        [UnityTest] public IEnumerator MixedSelectionCanBeSetToItsFirstExistingValueThroughText()
        {
            SoundEvent a = Event(0.1f); SoundEvent b = Event(0.8f);
            SerializedObject serialized = new SerializedObject(new Object[] { a, b });
            DecibelGainField field = BoundField(serialized); yield return null; yield return null;
            Assert.IsTrue(field.showMixedValue);
            field.Q<TextField>().value = "-20"; yield return null;
            Assert.AreEqual(0.1f, a.Volume, 0.00001f); Assert.AreEqual(0.1f, b.Volume, 0.00001f);
        }
        [UnityTest] public IEnumerator BindingAndRebindingDoNotModifyExistingAssetValues()
        {
            SoundEvent item = Event(0.000001f); SerializedObject serialized = new SerializedObject(item);
            DecibelGainField field = BoundField(serialized); yield return null; yield return null;
            Assert.AreEqual(0.000001f, item.Volume); Assert.AreEqual("-120", field.Q<TextField>().value);
            field.Unbind(); item.Volume = 0; serialized.Update(); field.BindProperty(serialized.FindProperty("volume"));
            yield return null; yield return null;
            Assert.AreEqual(0, item.Volume); Assert.AreEqual("−∞", field.Q<TextField>().value);
        }
    }
}
