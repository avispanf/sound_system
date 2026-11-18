using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using AudioMW.Editor;

namespace AudioMW.Tests
{
    public class StudioMixerTests
    {
        private string folder;
        private AudioMixer mixer;
        private AudioMixerSnapshot snapshot;
        private StudioMixerChannel music, ambience;

        [SetUp]
        public void Setup()
        {
            folder = "Assets/MixerTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            Assert.IsTrue(AssetDatabase.CopyAsset(AudioMixPlanner.TemplatePath,folder+"/Main.mixer"));
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(folder+"/Main.mixer");
            snapshot = AssetDatabase.LoadAllAssetsAtPath(folder+"/Main.mixer").OfType<AudioMixerSnapshot>().Single();
            var channels = StudioMixerChannel.Read(mixer);
            music = channels.Single(c=>c.ExposedGain=="AudioMW_MusicDuckDb");
            ambience = channels.Single(c=>c.ExposedGain=="AudioMW_AmbienceDuckDb");
            Assert.AreSame(mixer.FindMatchingGroups("Master/Music").First(c=>c.name=="Music"),music.Parent);
            CollectionAssert.Contains(music.Effects,"Attenuation");
        }

        [TearDown]
        public void Cleanup() { AssetDatabase.DeleteAsset(folder); }

        [Test]
        public void SnapshotEditsPreserveOtherChannelsAndReachUnityMixer()
        {
            Assert.IsTrue(ambience.Level(snapshot,out _, -7));
            Undo.IncrementCurrentGroup();
            Assert.IsTrue(music.Level(snapshot,out _, -12.5f));
            Assert.IsTrue(music.Level(snapshot,out var value));
            Assert.AreEqual(-12.5f,value);
            Undo.FlushUndoRecordObjects(); Undo.PerformUndo();
            Assert.IsTrue(music.Level(snapshot,out value)); Assert.AreEqual(0,value);
            Undo.PerformRedo();
            Assert.IsFalse(music.Level(snapshot,out _,float.NaN));
            AssetDatabase.SaveAssetIfDirty(snapshot);
            AssetDatabase.ImportAsset(folder+"/Main.mixer",ImportAssetOptions.ForceUpdate);
            mixer=AssetDatabase.LoadAssetAtPath<AudioMixer>(folder+"/Main.mixer");
            Assert.IsTrue(mixer.GetFloat("AudioMW_MusicDuckDb",out value));Assert.AreEqual(-12.5f,value);
            Assert.IsTrue(mixer.GetFloat("AudioMW_AmbienceDuckDb",out value));Assert.AreEqual(-7f,value);
        }

        [Test]
        public void GroupSwitchesAreIndependentAndUndoable()
        {
            Assert.IsTrue(music.SetSwitch(false,true));
            Assert.IsTrue(ambience.SetSwitch(true,true));
            Undo.IncrementCurrentGroup(); Assert.IsTrue(music.SetSwitch(false,false));
            Undo.FlushUndoRecordObjects();Undo.PerformUndo();
            var channels=StudioMixerChannel.Read(mixer);
            Assert.AreEqual(true,channels.Single(c=>c.Group==music.Group).Mute);
            Assert.AreEqual(false,channels.Single(c=>c.Group==music.Group).Solo);
            Assert.AreEqual(true,channels.Single(c=>c.Group==ambience.Group).Solo);
        }

        [Test]
        public void ReroutesOnlyRequestedLeafOrStableTrackAndPreservesInheritance()
        {
            var score=ScriptableObject.CreateInstance<MusicArrangement>();
            var a=new ArrangementLane();var b=new ArrangementLane();
            score.Lanes.Add(a);score.Lanes.Add(b);
            AssetDatabase.CreateAsset(score,folder+"/Score.asset");
            Assert.IsTrue(StudioMixerRoutes.Assign(score,null,music.Group));
            score.Lanes.Reverse();
            Assert.IsTrue(StudioMixerRoutes.Assign(score,b.Id,ambience.Group));
            Assert.IsNull(a.Output);Assert.AreSame(ambience.Group,b.Output);Assert.AreSame(music.Group,score.Output);
            Assert.IsFalse(StudioMixerRoutes.Assign(score,"deleted",null));
            Assert.IsTrue(StudioMixerRoutes.Assign(score,b.Id,null));Assert.IsNull(b.Output);
            var parent=ScriptableObject.CreateInstance<SoundEvent>();parent.ContainerMode=ContainerMode.Layered;
            AssetDatabase.CreateAsset(parent,folder+"/Parent.asset");
            Assert.IsFalse(StudioMixerRoutes.Assign(parent,null,music.Group));
            parent.ContainerMode=ContainerMode.Simple;
            Assert.IsTrue(StudioMixerRoutes.Assign(parent,null,music.Group));Assert.AreSame(music.Group,parent.MixerGroup);
        }

        [Test]
        public void MasterMayFeedAnotherMixerButNotItselfOrAFeedbackLoop()
        {
            Assert.IsTrue(AssetDatabase.CopyAsset(AudioMixPlanner.TemplatePath,folder+"/Other.mixer"));
            var other=AssetDatabase.LoadAssetAtPath<AudioMixer>(folder+"/Other.mixer");
            var target=other.FindMatchingGroups("Master/SFX").Single();
            Assert.IsTrue(StudioMixerRoutes.Connect(mixer,target));
            Assert.AreSame(target,mixer.outputAudioMixerGroup);
            Assert.IsFalse(StudioMixerRoutes.Connect(other,ambience.Group));
            Assert.IsFalse(StudioMixerRoutes.Connect(mixer,music.Group));
            Assert.IsTrue(StudioMixerRoutes.Connect(mixer,null));Assert.IsNull(mixer.outputAudioMixerGroup);
        }
    }
}
