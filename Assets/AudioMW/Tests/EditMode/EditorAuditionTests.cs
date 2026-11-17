using System;
using System.Linq;
using System.Reflection;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AudioMW.Tests
{
    public sealed class EditorAuditionTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private AudioEventEditorWindow window;
        private AudioClip clip;
        private string folder;
        private bool muted;
        private Object selection;
        [SetUp] public void SetUp()
        {
            muted=EditorUtility.audioMasterMute;EditorUtility.audioMasterMute=true;
            selection=Selection.activeObject;
            folder="Assets/EditorAudition_"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",folder.Substring(7));
            string path=folder+"/Source.wav";
            using(var writer=new System.IO.BinaryWriter(System.IO.File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(96036);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);
                writer.Write((short)1);writer.Write((short)1);writer.Write(48000);writer.Write(96000);
                writer.Write((short)2);writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(96000);writer.Write(new byte[96000]);
            }
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            clip=AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            window=ScriptableObject.CreateInstance<AudioEventEditorWindow>();window.CreateGUI();
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(window);EditorAudioPreview.Stop();
            AssetDatabase.DeleteAsset(folder);Selection.activeObject=selection;
            EditorUtility.audioMasterMute=muted;
        }
        private void Invoke(string method) => typeof(AudioEventEditorWindow).GetMethod(method,Private).Invoke(window,null);
        private T Field<T>(string name) => (T)typeof(AudioEventEditorWindow).GetField(name,Private).GetValue(window);
        private MusicArrangement OpenScore()
        {
            var score=ScriptableObject.CreateInstance<MusicArrangement>();score.Exit=1;
            score.Lanes.Add(new ArrangementLane());score.Lanes[0].Clips.Add(new ArrangementClip{Source=clip,Duration=1,FadeIn=.1,FadeOut=.1});
            AssetDatabase.CreateAsset(score,folder+"/Score.asset");window.OpenDocument(score);return score;
        }
        [Test] public void MusicPlayFromAuthorOwnsPreviewSceneAndStopCleansIt()
        {
            var score=OpenScore();int scenes=SceneManager.sceneCount;
            bool dirty=SceneManager.GetActiveScene().isDirty;
            Invoke("PlayAudition");
            Assert.AreEqual("Audition",Field<string>("mode"));
            var host=Field<GameObject>("auditionHost");Assert.IsNotNull(host,Field<string>("notice")+" · "+score.Validate());
            Scene preview=host.scene;Assert.IsTrue(EditorSceneManager.IsPreviewScene(preview));
            Assert.IsTrue(Field<MusicArrangementPlayer>("arrangementPlayer").IsPlaying);
            Assert.AreEqual(1,Field<MusicArrangementPlayer>("arrangementPlayer").ScheduledVoiceCount);
            Assert.AreEqual(scenes,SceneManager.sceneCount);Assert.AreEqual(dirty,SceneManager.GetActiveScene().isDirty);
            Assert.IsFalse(EditorUtility.IsDirty(score));
            Invoke("StopAudition");
            Assert.IsTrue(host==null);Assert.IsFalse(preview.IsValid());
            Assert.IsNull(Field<MusicArrangementPlayer>("arrangementPlayer"));
        }
        [Test] public void ReplacingUnpinnedDocumentStopsItsOwnedMusic()
        {
            var score=OpenScore();window.OpenDocument(score,false);
            var documents=(System.Collections.IList)typeof(AudioEventEditorWindow).GetField("documents",Private).GetValue(window);
            documents[0].GetType().GetField("Pinned").SetValue(documents[0],false);
            Invoke("PlayAudition");var host=Field<GameObject>("auditionHost");Assert.IsNotNull(host);
            window.OpenDocument(clip,false);Assert.IsTrue(host==null);Assert.AreEqual(clip,window.CurrentAsset);
        }
        [Test] public void SourceAndMusicAuditionAreExclusive()
        {
            var score=OpenScore();Invoke("PlayAudition");var host=Field<GameObject>("auditionHost");Assert.IsNotNull(host,Field<string>("notice"));
            window.OpenDocument(clip);Invoke("PlayAudition");
            Assert.IsTrue(host==null);Assert.AreEqual(clip,EditorAudioPreview.CurrentClip);
            window.OpenDocument(score);Invoke("PlayAudition");
            Assert.IsNull(EditorAudioPreview.CurrentClip);Assert.IsFalse(EditorAudioPreview.IsLoading);
        }
        [Test] public void InvalidArrangementLeavesNoOwnedPlayerAndExplainsFailure()
        {
            var score=OpenScore();score.Exit=0;Invoke("PlayAudition");
            Assert.IsNull(Field<GameObject>("auditionHost"));
            Assert.That(Field<string>("notice"),Does.Contain("Entry and exit"));
        }
        [Test] public void LiveModeNeverStartsOwnedAudition()
        {
            OpenScore();typeof(AudioEventEditorWindow).GetField("mode",Private).SetValue(window,"Live");
            Invoke("PlayAudition");Assert.IsNull(Field<GameObject>("auditionHost"));
            Assert.That(Field<string>("notice"),Does.Contain("Live observes"));
        }
        [Test] public void MissingSourceReportsErrorAndStopClearsIt()
        {
            EditorAudioPreview.Play(null);Assert.IsNotEmpty(EditorAudioPreview.LastError);
            Assert.IsFalse(EditorAudioPreview.IsPlaying);Assert.IsFalse(EditorAudioPreview.IsLoading);
            EditorAudioPreview.Stop();Assert.IsEmpty(EditorAudioPreview.LastError);
        }
        [Test] public void DspGainInitializesInEditModeAndLegacyDestroyRemovesSources()
        {
            var scene=EditorSceneManager.NewPreviewScene();
            var host=new GameObject("Legacy editor audition");SceneManager.MoveGameObjectToScene(host,scene);
            try
            {
                var player=new MusicPlayer(host.transform);
                var gain=host.GetComponentsInChildren<MusicDspGain>().Single();
                Assert.AreEqual(AudioSettings.outputSampleRate,typeof(MusicDspGain).GetField("sampleRate",Private).GetValue(gain));
                player.Destroy();Assert.AreEqual(0,host.transform.childCount);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        [Test] public void LegacyPlayheadWrapsBodyAfterIntro()
        {
            var track=ScriptableObject.CreateInstance<MusicTrack>();
            try
            {
                track.IntroClip=clip;track.LoopClip=clip;track.Loop=true;
                Assert.AreEqual(.5,AudioEventEditorWindow.LegacyTimelinePosition(track,.5),1e-6);
                Assert.AreEqual(1.25,AudioEventEditorWindow.LegacyTimelinePosition(track,3.25),1e-6);
                track.Loop=false;Assert.AreEqual(2,AudioEventEditorWindow.LegacyTimelinePosition(track,3.25),1e-6);
            }
            finally{Object.DestroyImmediate(track);}
        }
        [Test] public void TimelineDisplayFollowsBothEdgesAndKeepsTimeUnitsExplicit()
        {
            Assert.AreEqual(60,AudioEventEditorWindow.FittedLaneHeight(240,4));
            Assert.AreEqual(40,AudioEventEditorWindow.FittedLaneHeight(240,6));
            Assert.AreEqual(450,AudioEventEditorWindow.FollowTimelineScroll(0,5,100,200,1000));
            Assert.AreEqual(0,AudioEventEditorWindow.FollowTimelineScroll(450,.1,100,200,1000));
            Assert.AreEqual("2:01 · Bars:Beats",AudioEventEditorWindow.FormatTransportTime(2,true,120,4));
            Assert.AreEqual("01:01.250 · s",AudioEventEditorWindow.FormatTransportTime(61.25,false,120,4));
        }
        [Test] public void WaveformEnergyUsesActualSamplesIncludingOddTail()
        {
            var source=AudioClip.Create("Peak RMS fixture",5,2,48000,false);
            try
            {
                source.SetData(new[]{1f,-.5f,0f,-.5f,0f,-.5f,0f,-.5f,0f,-.5f},0);WaveformCache.Clear();
                Assert.IsTrue(WaveformCache.TryGetRange(source,0,0,5.0/48000,out var left));
                Assert.AreEqual(0,left.x);Assert.AreEqual(1,left.y);Assert.AreEqual(Mathf.Sqrt(.2f),left.z,1e-6);
                Assert.IsTrue(WaveformCache.TryGetRange(source,1,0,5.0/48000,out var right));
                Assert.AreEqual(-.5f,right.x);Assert.AreEqual(-.5f,right.y);Assert.AreEqual(.5f,right.z,1e-6);
                Assert.IsFalse(WaveformCache.TryGetRange(source,2,0,1,out _));
            }
            finally{WaveformCache.Clear();Object.DestroyImmediate(source);}
        }
        [Test] public void ArrangementPauseRetainsSnapshotAndStopDiscardsResume()
        {
            var score=OpenScore();Invoke("PlayAudition");var player=Field<MusicArrangementPlayer>("arrangementPlayer");
            Assert.IsTrue(player.Pause());Assert.IsTrue(player.IsPaused);Assert.IsFalse(player.IsPlaying);Assert.AreEqual(0,player.ScheduledVoiceCount);
            score.Lanes.Clear();Assert.IsTrue(player.Seek(.5));Assert.IsTrue(player.IsPaused);Assert.AreEqual(.5,player.Position,1e-6);
            Assert.IsTrue(player.Resume());Assert.AreEqual(1,player.ScheduledVoiceCount);
            player.Stop();Assert.IsFalse(player.IsPaused);Assert.IsFalse(player.Resume());
        }
        [Test] public void TransportGuardDoesNotCaptureTypedInput()
        {
            var method=typeof(AudioEventEditorWindow).GetMethod("IsTransportKeyBlocked",Private);
            var field=new UnityEngine.UIElements.TextField();var number=new UnityEngine.UIElements.DoubleField();var canvas=new UnityEngine.UIElements.IMGUIContainer();
            Assert.IsTrue((bool)method.Invoke(window,new object[]{field}));
            Assert.IsTrue((bool)method.Invoke(window,new object[]{number}));
            Assert.IsFalse((bool)method.Invoke(window,new object[]{canvas}));
        }
        [Test] public void LayerSelectionIgnoresMissingLegacySources()
        {
            var track=MusicTrack.CreateRuntime(null,null,120,4);
            track.Layers=new[]{MusicLayer.CreateRuntime("Missing",null,null,null),MusicLayer.CreateRuntime("Real",clip,null,null)};
            try{Assert.AreEqual(1,AudioEventEditorWindow.LegacyLayerIndex(track,0));Assert.AreEqual(-1,AudioEventEditorWindow.LegacyLayerIndex(track,1));}
            finally{Object.DestroyImmediate(track);}
        }
        [Test] public void SoundEventRequiresExplicitPlayModeAndDoesNotCreateEditorRuntime()
        {
            var sound=ScriptableObject.CreateInstance<SoundEvent>();sound.Clips=new[]{clip};
            AssetDatabase.CreateAsset(sound,folder+"/Event.asset");window.OpenDocument(sound);
            bool runtime=AudioRuntime.Exists;Invoke("PlayAudition");
            Assert.IsNull(Field<GameObject>("auditionHost"));Assert.AreEqual(runtime,AudioRuntime.Exists);
            Assert.That(Field<string>("notice"),Does.Contain("Enter Play Mode"));
        }
    }
}
