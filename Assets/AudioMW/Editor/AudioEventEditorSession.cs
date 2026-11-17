using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow
    {
        private Transform auditionTarget;
        private GameObject auditionHost;
        private MusicPlayer legacyPlayer;
        private MusicArrangementPlayer arrangementPlayer;
        private SoundHandle auditionHandle;
        private readonly ParameterStore auditionParameters=new ParameterStore();
        private readonly Dictionary<SoundParameter,LiveParameterLease> liveLeases=new Dictionary<SoundParameter,LiveParameterLease>();
        private AudioStudioCapture capture;
        private double lastRefresh;
        private int liveIdentity;
        private Label transportPosition;
        private void BuildTransport(VisualElement parent)
        {
            var transport=new Toolbar{name="studio-v2-transport"};
            transport.Add(new ToolbarButton(PlayAudition){text="▶ Play event",name="studio-play"});transport.Add(new ToolbarButton(StopAudition){text="■ Stop"});
            transportPosition=new Label("0.000 s");transport.Add(transportPosition);
            var context=new PopupField<string>(new List<string>{"Author","Audition","Live"},mode);context.RegisterValueChangedCallback(e=>{if(mode=="Live")ReleaseLive();StopAudition();mode=e.newValue;notice="";});transport.Add(context);
            var target=new ObjectField("Emitter position"){name="studio-emitter",objectType=typeof(Transform),allowSceneObjects=true,value=auditionTarget};target.style.width=260;target.RegisterValueChangedCallback(e=>auditionTarget=e.newValue as Transform);transport.Add(target);
            sessionLabel=new Label();transport.Add(sessionLabel);parent.Add(transport);
            var local=new Toolbar{name="studio-view-tabs"};
            foreach(string label in new[]{"Container","Timeline","Parameters","Transitions"}){string next=label;local.Add(new ToolbarButton(()=>{view=next;RefreshTransport();canvas.MarkDirtyRepaint();}){text=label,name="view-"+label});}local.Add(new ToolbarSpacer{flex=true});local.Add(new ToolbarButton(()=>{overviewOpen=!overviewOpen;overview.style.display=overviewOpen?DisplayStyle.Flex:DisplayStyle.None;RefreshOverview();}){text="Overview"});parent.Add(local);
        }
        private void RefreshTransport()
        {
            foreach(string tab in new[]{"Container","Timeline","Parameters","Transitions"})rootVisualElement.Q<ToolbarButton>("view-"+tab)?.EnableInClassList("studio-v2-active",tab==view);
            bool music=CurrentAsset is MusicTrack||CurrentAsset is MusicArrangement||CurrentAsset is MusicStateProfile;
            foreach(string name in new[]{"Container","Timeline","Transitions"})
            {
                var button=rootVisualElement.Q<ToolbarButton>("view-"+name);
                if(button!=null)button.style.display=(name=="Container"?!music:name=="Transitions"?CurrentAsset is MusicTrack||CurrentAsset is MusicStateProfile:music)?DisplayStyle.Flex:DisplayStyle.None;
            }
            var emitter=rootVisualElement.Q<ObjectField>("studio-emitter");
            if(emitter!=null)emitter.style.display=CurrentAsset is SoundEvent?DisplayStyle.Flex:DisplayStyle.None;
        }
        private void PlayAudition()
        {
            if(mode=="Author"){notice="Choose Audition to start a session. Author edits assets only.";return;}
            if(mode=="Live"){notice="Live observes the game. Choose Audition to start an owned playback session.";return;}
            if(!EditorApplication.isPlaying||EditorApplication.isPaused){notice="Event audition requires local Play Mode.";return;}
            int listeners=0;foreach(var listener in UnityEngine.Object.FindObjectsOfType<AudioListener>())if(listener.isActiveAndEnabled)listeners++;
            if(listeners!=1){notice="Exactly one active AudioListener is required.";return;}
            StopAudition();EditorAudioPreview.Stop();notice="";
            if(CurrentAsset is SoundEvent sound)
            {
                if(!AudioRuntime.Exists||auditionTarget==null){notice="Choose an emitter position and start AudioMW in this scene.";return;}
                auditionHandle=AudioSystem.PlayTrackedAtPosition(sound,auditionTarget.position,auditionParameters);return;
            }
            auditionHost=new GameObject("AudioMW Studio audition"){hideFlags=HideFlags.HideAndDontSave};
            if(CurrentAsset is MusicArrangement arrangement){arrangementPlayer=auditionHost.AddComponent<MusicArrangementPlayer>();arrangementPlayer.Arrangement=arrangement;arrangementPlayer.Parameters.CopyFrom(auditionParameters);foreach(var lane in arrangement.Lanes)arrangementPlayer.SetLaneAudition(lane.Id,mutedLanes.Contains(lane.Id),soloLanes.Contains(lane.Id));if(!arrangementPlayer.Play())notice=arrangement.Validate();}
            else if(CurrentAsset is MusicTrack music){legacyPlayer=new MusicPlayer(auditionHost.transform,auditionParameters);legacyPlayer.Play(music);}
            else{notice="This document has no event audition path.";StopAudition();}
        }
        private void StopAudition()
        {
            auditionHandle.Stop();auditionHandle=SoundHandle.None;legacyPlayer?.Stop();legacyPlayer=null;
            if(arrangementPlayer!=null){auditionParameters.CopyFrom(arrangementPlayer.Parameters);arrangementPlayer.Stop();}arrangementPlayer=null;
            if(auditionHost!=null)DestroyImmediate(auditionHost);auditionHost=null;EditorAudioPreview.Stop();
        }
        private void ReleaseLive(){foreach(var lease in liveLeases.Values)lease.Restore();liveLeases.Clear();}
        private void UpdateSession()
        {
            legacyPlayer?.Tick();capture?.Tick();
            int identity=EditorApplication.isPlaying&&AudioRuntime.Exists?AudioRuntime.Instance.GetInstanceID():0;
            if(identity!=liveIdentity){ReleaseLive();liveIdentity=identity;}
            if(sessionLabel!=null)sessionLabel.text=identity==0?"Local: Disconnected":EditorApplication.isPaused?"Local: Paused":"Local: Connected · "+identity;
            if(transportPosition!=null)transportPosition.text=arrangementPlayer!=null?arrangementPlayer.Position.ToString("0.000")+" s":legacyPlayer!=null&&legacyPlayer.IsPlaying?"DSP music session":"Stopped";
            canvas?.MarkDirtyRepaint();
            if(EditorApplication.timeSinceStartup-lastRefresh>.25){lastRefresh=EditorApplication.timeSinceStartup;RefreshDocuments();Repaint();}
        }
        private void RefreshDock()
        {
            if(bottom==null)return;bottom.Clear();bool open=bottomTab!="";bottom.style.display=open?DisplayStyle.Flex:DisplayStyle.None;bottom.style.height=open?210:0;deck.style.display=DisplayStyle.Flex;deck.style.minHeight=open?28:160;deck.style.height=open?28:(position.height<850?180:240);deckScroll.style.display=open?DisplayStyle.None:DisplayStyle.Flex;
            if(open){var scroll=new ScrollView();scroll.style.flexGrow=1;scroll.Add(new IMGUIContainer(DrawDock));bottom.Add(scroll);}
        }
        private void DrawDock()
        {
            if(bottomTab=="Profiler")
            {
                if(capture==null)capture=new AudioStudioCapture();
                EditorGUILayout.BeginHorizontal();capture.Recording=GUILayout.Toggle(capture.Recording,"Record",EditorStyles.miniButton);if(GUILayout.Button("Clear"))capture.Samples.Clear();
                if(GUILayout.Button("Export CSV")){string path=EditorUtility.SaveFilePanel("Export capture","","audiomw-capture.csv","csv");if(path!="")System.IO.File.WriteAllText(path,capture.Csv());}EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Local Editor · bounded 600 samples / 10 Hz · paused Editor pauses capture · prior samples remain captured");
                if(capture.Samples.Count==0){EditorGUILayout.LabelField("No samples. Enter Play Mode with AudioMW and enable Record.");return;}
                var samples=capture.Samples;var s=samples[samples.Count-1];EditorGUILayout.LabelField((capture.Recording&&EditorApplication.isPlaying&&!EditorApplication.isPaused&&AudioRuntime.Exists?"Recording":"Captured / stale")+" · latest sample "+s.Seconds.ToString("0.00")+" s");EditorGUILayout.LabelField($"SFX {s.Active} physical / {s.Limit} · {s.Virtual} virtual · requests {s.Requests} / rejected {s.Rejected} / steals {s.Steals} (cumulative)");
                EditorGUILayout.LabelField("AudioMW tick "+AudioPerformanceSample.CsvValue(s.TickMs)+" ms · whole frame "+AudioPerformanceSample.CsvValue(s.MainThreadMs)+" ms · whole-frame GC "+AudioPerformanceSample.CsvValue(s.FrameGcBytes)+" B (blank = unavailable)");
                Rect r=GUILayoutUtility.GetRect(100,90,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(r,new Color(.10f,.11f,.12f));double span=Math.Max(.1,s.Seconds-samples[0].Seconds);float max=1;foreach(var row in samples)max=Mathf.Max(max,row.Limit);
                Handles.BeginGUI();Handles.color=new Color(.66f,.81f,.46f);for(int i=1;i<samples.Count;i++)if(samples[i].Seconds-samples[i-1].Seconds<.3)Handles.DrawLine(new Vector3(r.x+(float)((samples[i-1].Seconds-samples[0].Seconds)/span)*r.width,r.yMax-samples[i-1].Active/max*r.height),new Vector3(r.x+(float)((samples[i].Seconds-samples[0].Seconds)/span)*r.width,r.yMax-samples[i].Active/max*r.height));Handles.EndGUI();
            }
            else if(bottomTab=="Live changes")
            {
                EditorGUILayout.LabelField("Runtime global overrides · defaults remain unchanged",EditorStyles.boldLabel);
                SoundParameter release=null;foreach(var pair in liveLeases){EditorGUILayout.BeginHorizontal();EditorGUILayout.LabelField(pair.Key!=null?pair.Key.name:"Deleted parameter");if(GUILayout.Button("Release override"))release=pair.Key;EditorGUILayout.EndHorizontal();}if(release!=null){liveLeases[release].Restore();liveLeases.Remove(release);}
                EditorGUILayout.LabelField("Authored Play Mode changes",EditorStyles.boldLabel);foreach(string guid in LiveTweakTracker.ChangedGuids.ToArray()){var asset=AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid));EditorGUILayout.BeginHorizontal();if(GUILayout.Button(asset!=null?asset.name:guid))ShowDeck(asset);if(GUILayout.Button("Revert"))LiveTweakTracker.Revert(guid);if(GUILayout.Button("Keep"))LiveTweakTracker.Forget(guid);EditorGUILayout.EndHorizontal();}
            }
            else
            {
                if(CurrentAsset is SoundEvent sound)EditorGUILayout.HelpBox(SoundEventValidation.Describe(sound),MessageType.Info);
                if(CurrentAsset is MusicArrangement arrangement){string issue=arrangement.Validate();EditorGUILayout.HelpBox(issue==""?"Arrangement validation passed.":issue,issue==""?MessageType.Info:MessageType.Error);}
                DrawRequests();
            }
        }
        private void DrawLiveParameter(SoundParameter parameter)
        {
            EditorGUILayout.LabelField("Default "+parameter.DefaultValue+" · authored range "+parameter.MinValue+"…"+parameter.MaxValue);
            if(mode=="Live")
            {
                if(!EditorApplication.isPlaying||EditorApplication.isPaused||!AudioRuntime.Exists){EditorGUILayout.LabelField("Unavailable: Local runtime disconnected or paused.");return;}
                var store=AudioRuntime.Instance.GlobalParameters;bool owns=liveLeases.ContainsKey(parameter);EditorGUILayout.LabelField("Global scope · runtime "+store.Get(parameter)+" · owner "+(owns?"Studio override":"gameplay/default"));
                if(!owns){if(GUILayout.Button("Enable live override"))liveLeases[parameter]=new LiveParameterLease(store,parameter);}
                else{EditorGUILayout.LabelField("Apply now · global parameter store · local voice overrides take precedence");EditorGUI.BeginChangeCheck();float value=EditorGUILayout.Slider("Override",store.Get(parameter),parameter.MinValue,parameter.MaxValue);if(EditorGUI.EndChangeCheck())liveLeases[parameter].Set(value);if(GUILayout.Button("Save runtime value as default")){using(var authored=new SerializedObject(parameter)){authored.FindProperty("defaultValue").floatValue=store.Get(parameter);authored.ApplyModifiedProperties();}EditorUtility.SetDirty(parameter);}
                    if(GUILayout.Button("Release override")){liveLeases[parameter].Restore();liveLeases.Remove(parameter);}}
            }
            else if(mode=="Audition")
            {
                var store=arrangementPlayer!=null?arrangementPlayer.Parameters:auditionParameters;
                EditorGUI.BeginChangeCheck();float value=EditorGUILayout.Slider("Audition value",store.Get(parameter),parameter.MinValue,parameter.MaxValue);if(EditorGUI.EndChangeCheck()){store.Set(parameter,value);auditionHandle.SetParameter(parameter,value);}
            }
        }
    }
}
