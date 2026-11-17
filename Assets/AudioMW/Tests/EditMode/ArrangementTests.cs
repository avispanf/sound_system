using System.Linq;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEngine;

namespace AudioMW.Tests
{
    public sealed class ArrangementTests
    {
        private MusicArrangement score;
        private AudioClip source;
        [SetUp] public void SetUp()
        {
            source=AudioClip.Create("Arrangement fixture",48000,1,48000,false);
            score=ScriptableObject.CreateInstance<MusicArrangement>();score.Exit=8;
            score.Lanes.Add(new ArrangementLane());
        }
        [TearDown] public void TearDown(){Undo.ClearUndo(score);Object.DestroyImmediate(score);Object.DestroyImmediate(source);}
        private ArrangementClip Clip(double start=0,double trim=0,double duration=1,bool repeat=false)
        {
            var clip=new ArrangementClip{Source=source,Start=start,Trim=trim,Duration=duration,Repeat=repeat};score.Lanes[0].Clips.Add(clip);return clip;
        }
        [Test] public void SourceWeightsPreserveDefaultsAndUndo()
        {
            var item=ScriptableObject.CreateInstance<SoundEvent>();
            try
            {
                item.Clips=new[]{source,source,source};item.SelectionMode=ClipSelectionMode.WeightedRandom;
                Undo.IncrementCurrentGroup();AudioEventEditorWindow.SetSourceWeight(item,2,4);Undo.FlushUndoRecordObjects();
                CollectionAssert.AreEqual(new[]{1f,1f,4f},item.ClipWeights);
                Undo.PerformUndo();Assert.AreEqual(0,item.ClipWeights.Length);
                Undo.PerformRedo();CollectionAssert.AreEqual(new[]{1f,1f,4f},item.ClipWeights);
                item.SelectionMode=ClipSelectionMode.ShuffleBag;AudioEventEditorWindow.SetSourceWeight(item,1,9);
                Assert.AreEqual(1,item.ClipWeights[1]);
            }
            finally{Undo.ClearUndo(item);Object.DestroyImmediate(item);}
        }
        [Test] public void NestedSourceCatalogKeepsOwnerAndWeightIndexWithoutCycling()
        {
            var root=ScriptableObject.CreateInstance<SoundEvent>();var leaf=ScriptableObject.CreateInstance<SoundEvent>();
            try
            {
                root.ContainerMode=ContainerMode.Layered;root.Children=new[]{SoundEventChild.Create(leaf),SoundEventChild.Create(root)};
                leaf.Clips=new[]{source,source};var entries=AudioSourceCatalog.Collect(root);
                Assert.AreEqual(2,entries.Count);Assert.AreSame(leaf,entries[1].Owner);Assert.AreEqual(1,entries[1].SourceIndex);
                Assert.AreEqual(0,leaf.ClipWeights.Length);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(leaf);}
        }
        [Test] public void WaveformKeepsStereoPolarityAndLastFrame()
        {
            var stereo=AudioClip.Create("Stereo envelope",2051,2,48000,false);
            try
            {
                var pcm=new float[4102];for(int i=0;i<2051;i++){pcm[i*2]=.5f;pcm[i*2+1]=-.5f;}pcm[4100]=1;
                stereo.SetData(pcm,0);WaveformCache.Clear();var texture=WaveformCache.Get(stereo);
                Assert.IsNotNull(texture);Assert.AreEqual(128,texture.height);
                Assert.Greater(texture.GetPixel(100,110).a,0);Assert.AreEqual(0,texture.GetPixel(100,82).a);
                Assert.Greater(texture.GetPixel(100,18).a,0);Assert.AreEqual(0,texture.GetPixel(100,46).a);
                Assert.Greater(texture.GetPixel(1023,124).a,0);
            }
            finally{WaveformCache.Clear();Object.DestroyImmediate(stereo);}
        }
        [Test] public void FadeAuthoringClampsDurationAndUndoes()
        {
            var clip=Clip();Undo.IncrementCurrentGroup();ArrangementAuthoring.SetFades(score,clip.Id,.7,.9);Undo.FlushUndoRecordObjects();
            Assert.AreEqual(.7,clip.FadeIn,1e-9);Assert.AreEqual(.3,clip.FadeOut,1e-9);Assert.IsEmpty(score.Validate());
            Undo.PerformUndo();Assert.AreEqual(0,score.Lanes[0].Clips[0].FadeIn);Assert.AreEqual(0,score.Lanes[0].Clips[0].FadeOut);
            Undo.PerformRedo();Assert.AreEqual(.7,score.Lanes[0].Clips[0].FadeIn,1e-9);
        }
        [Test] public void SaveRootIncludesEditedChildAfterDeckNavigation()
        {
            string folder="Assets/StudioChild_"+System.Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            var root=ScriptableObject.CreateInstance<SoundEvent>();var child=ScriptableObject.CreateInstance<SoundEvent>();var window=ScriptableObject.CreateInstance<AudioEventEditorWindow>();
            try
            {
                AssetDatabase.CreateAsset(child,folder+"/Child.asset");root.ContainerMode=ContainerMode.Layered;root.Children=new[]{SoundEventChild.Create(child)};AssetDatabase.CreateAsset(root,folder+"/Root.asset");
                window.CreateGUI();window.OpenDocument(root);child.Volume=.25f;EditorUtility.SetDirty(child);
                var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
                typeof(AudioEventEditorWindow).GetMethod("TrackEdit",flags).Invoke(window,new object[]{child});
                string state=EditorJsonUtility.ToJson(window);EditorJsonUtility.FromJsonOverwrite(state,window);
                typeof(AudioEventEditorWindow).GetMethod("ShowDeck",flags).Invoke(window,new object[]{root});window.SaveDocument();
                Assert.IsFalse(EditorUtility.IsDirty(child));Assert.That(System.IO.File.ReadAllText(folder+"/Child.asset"),Does.Contain("volume: 0.25"));
            }
            finally{Object.DestroyImmediate(window);AssetDatabase.DeleteAsset(folder);}
        }
        [Test] public void TrimAndSeekPreserveSampleOffset()
        {
            Clip(2,.25,.75);var segment=ArrangementSchedule.Window(score,2.5,0,1).Single();
            Assert.AreEqual(.75,segment.SourceOffset,1e-9);Assert.AreEqual(0,segment.Start);Assert.AreEqual(.25,segment.End,1e-9);
        }
        [Test] public void RepeatUsesTrimmedSourceSpanWithoutOverlap()
        {
            Clip(0,.25,2,true);var segments=ArrangementSchedule.Window(score,0,0,3);
            Assert.AreEqual(3,segments.Count);Assert.AreEqual(.75,segments[0].End);Assert.AreEqual(.75,segments[1].Start);Assert.AreEqual(1.5,segments[2].Start);Assert.AreEqual(2,segments[2].End);
            Assert.IsTrue(segments.All(x=>x.SourceOffset==.25));Assert.AreEqual(3,segments.Select(x=>x.Key).Distinct().Count());
        }
        [Test] public void LoopWindowIncludesBothSidesOfBoundary()
        {
            Clip(0,0,8,true);score.Loop=true;score.LoopStart=2;score.LoopEnd=4;
            var segments=ArrangementSchedule.Window(score,0,3.8,4.2);
            Assert.AreEqual(2,segments.Count);Assert.AreEqual(4,segments[0].End);Assert.AreEqual(4,segments[1].Start);
            Assert.AreEqual(2.2,ArrangementSchedule.Position(score,0,4.2),1e-8);
            Assert.AreNotEqual(segments[0].Key,segments[1].Key);
        }
        [Test] public void FourLanesScheduleDistinctVoices()
        {
            for(int i=0;i<4;i++){if(i>0)score.Lanes.Add(new ArrangementLane());score.Lanes[i].Clips.Add(new ArrangementClip{Source=source,Start=i*.05,Duration=.5});}
            Assert.AreEqual(4,ArrangementSchedule.Window(score,0,0,.5).Count);
        }
        [Test] public void InvalidSourceBoundsAndIdentityAreRejected()
        {
            var clip=Clip(0,.5,1);Assert.That(score.Validate(),Does.Contain("past its source"));clip.Repeat=true;Assert.IsEmpty(score.Validate());
            score.Lanes[0].Clips.Add(new ArrangementClip{Id=clip.Id,Source=source});Assert.That(score.Validate(),Does.Contain("duplicate clip"));
        }
        [Test] public void AuthoringMoveDuplicateAndDeleteUndoPreserveReferences()
        {
            var clip=Clip(1);string id=clip.Id;ArrangementAuthoring.Move(score,new[]{id},-.5);Undo.FlushUndoRecordObjects();Assert.AreEqual(.5,score.Lanes[0].Clips[0].Start);
            Undo.PerformUndo();Assert.AreEqual(1,score.Lanes[0].Clips[0].Start);Undo.PerformRedo();Assert.AreEqual(.5,score.Lanes[0].Clips[0].Start);
            Undo.IncrementCurrentGroup();ArrangementAuthoring.Duplicate(score,new[]{id});Undo.FlushUndoRecordObjects();Assert.AreEqual(2,score.Lanes[0].Clips.Count);Assert.AreSame(source,score.Lanes[0].Clips[1].Source);Assert.AreNotEqual(id,score.Lanes[0].Clips[1].Id);
            Undo.PerformUndo();Assert.AreEqual(1,score.Lanes[0].Clips.Count);
            Undo.IncrementCurrentGroup();ArrangementAuthoring.Delete(score,new[]{id});Undo.FlushUndoRecordObjects();Assert.AreEqual(0,score.Lanes[0].Clips.Count);Undo.PerformUndo();Assert.AreEqual(id,score.Lanes[0].Clips[0].Id);
        }
        [Test] public void SavedAssetReopensWithStableIdentityAndPlacement()
        {
            string folder="Assets/ArrangementTest_"+System.Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            var clip=Clip(2,.25,.5);var copy=Object.Instantiate(score);
            try
            {
                using(var bytes=new System.IO.BinaryWriter(System.IO.File.Create(folder+"/Source.wav")))
                {
                    bytes.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));bytes.Write(96036);bytes.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));bytes.Write(16);bytes.Write((short)1);bytes.Write((short)1);bytes.Write(48000);bytes.Write(96000);bytes.Write((short)2);bytes.Write((short)16);bytes.Write(System.Text.Encoding.ASCII.GetBytes("data"));bytes.Write(96000);bytes.Write(new byte[96000]);
                }
                AssetDatabase.ImportAsset(folder+"/Source.wav");copy.Lanes[0].Clips[0].Source=AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"/Source.wav");
                AssetDatabase.CreateAsset(copy,folder+"/Arrangement.asset");AssetDatabase.SaveAssetIfDirty(copy);
                string guid=AssetDatabase.AssetPathToGUID(folder+"/Arrangement.asset");Resources.UnloadAsset(copy);
                var reopened=AssetDatabase.LoadAssetAtPath<MusicArrangement>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.AreEqual(clip.Id,reopened.Lanes[0].Clips[0].Id);Assert.AreEqual(AssetDatabase.AssetPathToGUID(folder+"/Source.wav"),AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(reopened.Lanes[0].Clips[0].Source)));Assert.AreEqual(.25,reopened.Lanes[0].Clips[0].Trim);Assert.IsEmpty(reopened.Validate());
            }
            finally{AssetDatabase.DeleteAsset(folder);}
        }
        [Test] public void BrowserChangesKeepDocumentAndZoom()
        {
            string folder="Assets/StudioDocumentTest_"+System.Guid.NewGuid().ToString("N");AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            var eventAsset=ScriptableObject.CreateInstance<SoundEvent>();AssetDatabase.CreateAsset(eventAsset,folder+"/Event.asset");
            var clip=Object.Instantiate(source);AssetDatabase.CreateAsset(clip,folder+"/Clip.asset");
            var window=ScriptableObject.CreateInstance<AudioEventEditorWindow>();
            try
            {
                window.CreateGUI();window.OpenDocument(eventAsset);
                using(var state=new SerializedObject(window)){state.FindProperty("documents").GetArrayElementAtIndex(0).FindPropertyRelative("Zoom").floatValue=123;state.ApplyModifiedPropertiesWithoutUndo();}
                window.SwitchBrowser(2);var browser=window.rootVisualElement.Q<TreeView>("studio-v2-library");
                int folderId=AssetDatabase.LoadAssetAtPath<Object>(folder).GetInstanceID();
                browser.ExpandItem(folderId);browser.SetSelectionById(clip.GetInstanceID());
                Assert.AreEqual(eventAsset,window.CurrentAsset);Assert.AreEqual(clip,window.DeckTarget);
                window.SwitchBrowser(1);window.SwitchBrowser(3);window.SwitchBrowser(2);Assert.IsTrue(browser.IsExpanded(folderId));window.SwitchBrowser(0);Assert.AreEqual(eventAsset,window.CurrentAsset);
                using(var state=new SerializedObject(window))Assert.AreEqual(123,state.FindProperty("documents").GetArrayElementAtIndex(0).FindPropertyRelative("Zoom").floatValue);
            }
            finally{Object.DestroyImmediate(window);AssetDatabase.DeleteAsset(folder);}
        }
        [Test] public void LegacyAdapterNeverMutatesOriginalTrack()
        {
            var track=ScriptableObject.CreateInstance<MusicTrack>();track.LoopClip=source;track.Loop=true;string before=EditorJsonUtility.ToJson(track);
            var converted=ArrangementAuthoring.FromLegacy(track);
            try{Assert.AreEqual(before,EditorJsonUtility.ToJson(track));Assert.AreSame(source,converted.Lanes[0].Clips[0].Source);Assert.AreEqual(1,converted.Exit);Assert.IsEmpty(converted.Validate());}finally{Object.DestroyImmediate(converted);Object.DestroyImmediate(track);}
        }
    }
}
