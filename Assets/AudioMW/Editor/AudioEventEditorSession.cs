using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow
    {
        [SerializeField] private Transform auditionTarget;
        private GameObject auditionHost;
        private MusicPlayer legacyPlayer;
        private MusicArrangementPlayer arrangementPlayer;
        private SoundHandle auditionHandle;
        private readonly ParameterStore auditionParameters=new ParameterStore();
        private readonly Dictionary<SoundParameter,LiveParameterLease> liveLeases=new Dictionary<SoundParameter,LiveParameterLease>();
        private AudioStudioCapture capture;
        private double lastRefresh,lastSessionUiRefresh;
        private int liveIdentity;
        private Label transportPosition;
        private Label auditionStatus;
        private ToolbarButton muteButton, pauseButton;
        private ToolbarButton enterPlayButton;
        private PopupField<string> auditionMode;
        private Scene auditionScene;
        [SerializeField] private string pendingAuditionGuid = "";
        private double AuditionPosition
        {
            get
            {
                if(arrangementPlayer!=null&&(arrangementPlayer.IsPlaying||arrangementPlayer.IsPaused))return arrangementPlayer.Position;
                if(legacyPlayer==null||!legacyPlayer.IsPlaying)return CurrentDocument?.Cursor??0;
                return LegacyTimelinePosition(legacyPlayer.CurrentTrack,legacyPlayer.Clock.GetPosition(AudioSettings.dspTime));
            }
        }
        public static double LegacyTimelinePosition(MusicTrack track,double elapsed)
        {
            double value=Math.Max(0,elapsed);
            if(track==null)return value;
            double intro=MusicTrack.GetExactDuration(track.IntroClip),body=track.BodyDuration;
            return track.Loop&&body>0&&value>=intro?intro+(value-intro)%body:Math.Min(value,intro+body);
        }
        private void BuildTransport(VisualElement parent)
        {
            var transport=new Toolbar{name="studio-v2-transport"};
            transport.Add(new ToolbarButton(ReturnToEntry){text="↤",name="studio-return",tooltip="Return to entry · Home. Legacy music restarts; arrangements seek."});
            transport.Add(new ToolbarButton(PlayAudition){text="▶ Play event",name="studio-play"});
            pauseButton=new ToolbarButton(ToggleAuditionPause){text="Ⅱ Pause",name="studio-pause"};transport.Add(pauseButton);
            transport.Add(new ToolbarButton(StopAudition){text="■ Stop",tooltip="Stop only this audition session · Space"});
            transportPosition=new Label("Stopped"){name="studio-transport-position"};transport.Add(transportPosition);
            auditionMode=new PopupField<string>(new List<string>{"Author","Audition","Live"},mode);auditionMode.RegisterValueChangedCallback(e=>{if(mode=="Live")ReleaseLive();StopAudition();mode=e.newValue;notice="";});transport.Add(auditionMode);
            var target=new ObjectField("Emitter position"){name="studio-emitter",objectType=typeof(Transform),allowSceneObjects=true,value=auditionTarget};target.style.width=260;target.RegisterValueChangedCallback(e=>auditionTarget=e.newValue as Transform);transport.Add(target);
            parent.Add(transport);
            var status=new VisualElement{name="studio-audition-status"};
            auditionStatus=new Label{name="studio-audition-message"};status.Add(auditionStatus);
            enterPlayButton=new ToolbarButton(()=>
            {
                if(!(CurrentAsset is SoundEvent)||mode=="Live")return;
                pendingAuditionGuid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(CurrentAsset));
                EditorApplication.isPlaying=true;
            }){text="Enter Play Mode & audition",name="studio-enter-play"};status.Add(enterPlayButton);
            muteButton=new ToolbarButton(()=>EditorUtility.audioMasterMute=!EditorUtility.audioMasterMute){name="studio-audio-mute",tooltip="Unity Editor audio output. This does not change event gains or saved defaults."};status.Add(muteButton);
            sessionLabel=new Label();sessionLabel.tooltip="Connection to the gameplay runtime. Music can audition separately in Edit Mode.";status.Add(sessionLabel);parent.Add(status);
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
            if(mode=="Live"){notice="Live observes the game. Choose Audition to start an owned playback session.";return;}
            if(EditorApplication.isCompiling||EditorApplication.isPaused){notice="Wait for compilation or resume Play Mode before auditioning.";return;}
            if(arrangementPlayer!=null&&arrangementPlayer.IsPaused){arrangementPlayer.Resume();notice="";return;}
            if(CurrentAsset is AudioClip&&EditorAudioPreview.IsPaused){EditorAudioPreview.TogglePause();notice="";return;}
            StopAudition();notice="";
            if(CurrentAsset is AudioClip source){EditorAudioPreview.Play(source);return;}
            mode="Audition";auditionMode?.SetValueWithoutNotify(mode);
            if(CurrentAsset is SoundEvent sound)
            {
                if(!EditorApplication.isPlaying){notice="Sound event audition uses the game runtime. Enter Play Mode to hear containers, routing and spatial effects.";return;}
                var listeners=UnityEngine.Object.FindObjectsOfType<AudioListener>().Where(x=>x.isActiveAndEnabled).ToArray();
                if(listeners.Length!=1){notice="Exactly one active AudioListener is required for event audition.";return;}
                if(!AudioRuntime.Exists){notice="AudioMW runtime is unavailable in this scene.";return;}
                Vector3 position=auditionTarget!=null?auditionTarget.position:listeners[0].transform.position;
                auditionHandle=AudioSystem.PlayTrackedAtPosition(sound,position,auditionParameters);
                if(!auditionHandle.IsAssigned)notice="Event request rejected. Open Issues for the runtime reason and event validation.";
                return;
            }
            if(!(CurrentAsset is MusicArrangement)&&!(CurrentAsset is MusicTrack)){notice="Select a sound event, music document or audio source to play.";return;}
            try
            {
                auditionHost=new GameObject("AudioMW Studio audition"){hideFlags=HideFlags.HideAndDontSave};
                if(!EditorApplication.isPlaying)
                {
                    auditionScene=EditorSceneManager.NewPreviewScene();
                    SceneManager.MoveGameObjectToScene(auditionHost,auditionScene);
                }
                if(CurrentAsset is MusicArrangement arrangement)
                {
                    arrangementPlayer=auditionHost.AddComponent<MusicArrangementPlayer>();arrangementPlayer.Arrangement=arrangement;
                    arrangementPlayer.Parameters.CopyFrom(auditionParameters);
                    foreach(var lane in arrangement.Lanes)arrangementPlayer.SetLaneAudition(lane.Id,mutedLanes.Contains(lane.Id),soloLanes.Contains(lane.Id));
                    double cursor=CurrentDocument?.Cursor??arrangement.Entry;
                    if(!arrangementPlayer.Play(cursor>=arrangement.Exit?arrangement.Entry:Math.Max(arrangement.Entry,cursor)))
                    {
                        notice=arrangement.Validate();StopAudition();
                    }
                }
                else if(CurrentAsset is MusicTrack music)
                {
                    legacyPlayer=new MusicPlayer(auditionHost.transform,auditionParameters);legacyPlayer.Play(music);
                    if(!legacyPlayer.IsPlaying){notice="Music track has no playable content. Check its clips in Deck.";StopAudition();}
                }
            }
            catch(Exception exception){StopAudition();notice="Audition failed: "+exception.Message;}
        }
        private void StopAudition()
        {
            pendingAuditionGuid="";
            auditionHandle.Stop();auditionHandle=SoundHandle.None;legacyPlayer?.Destroy();legacyPlayer=null;
            if(arrangementPlayer!=null){auditionParameters.CopyFrom(arrangementPlayer.Parameters);arrangementPlayer.Stop();}arrangementPlayer=null;
            if(auditionHost!=null)DestroyImmediate(auditionHost);auditionHost=null;
            if(auditionScene.IsValid())EditorSceneManager.ClosePreviewScene(auditionScene);auditionScene=default;
            EditorAudioPreview.Stop();
        }
        private void ToggleAuditionPause()
        {
            if(EditorAudioPreview.IsPlaying||EditorAudioPreview.IsPaused){EditorAudioPreview.TogglePause();return;}
            if(arrangementPlayer==null)return;
            if(arrangementPlayer.IsPaused)arrangementPlayer.Resume();else arrangementPlayer.Pause();
        }
        private void ToggleTransport()
        {
            if(EditorAudioPreview.IsPlaying||EditorAudioPreview.IsLoading||auditionHandle.IsPlaying||legacyPlayer!=null&&legacyPlayer.IsPlaying||arrangementPlayer!=null&&arrangementPlayer.IsPlaying)StopAudition();
            else if(EditorAudioPreview.IsPaused)EditorAudioPreview.TogglePause();
            else PlayAudition();
        }
        private void ReturnToEntry()
        {
            if(EditorAudioPreview.CurrentClip!=null&&(EditorAudioPreview.IsPlaying||EditorAudioPreview.IsPaused))
            {
                var source=EditorAudioPreview.CurrentClip;bool paused=EditorAudioPreview.IsPaused;EditorAudioPreview.Play(source);if(paused)EditorAudioPreview.TogglePause();return;
            }
            double entry=CurrentAsset is MusicArrangement score?score.Entry:0;
            if(CurrentDocument!=null){CurrentDocument.Cursor=entry;CurrentDocument.Scroll.x=0;}
            if(arrangementPlayer!=null&&(arrangementPlayer.IsPlaying||arrangementPlayer.IsPaused))arrangementPlayer.Seek(entry);
            else if(legacyPlayer!=null&&legacyPlayer.IsPlaying)PlayAudition();
        }
        public static string FormatTransportTime(double seconds,bool musical,double tempo,int beatsPerBar)
        {
            seconds=Math.Max(0,seconds);
            if(musical&&tempo>0&&beatsPerBar>0)
            {
                double beat=seconds*tempo/60;return (1+(long)(beat/beatsPerBar))+":"+(1+(int)(beat%beatsPerBar)).ToString("00")+" · Bars:Beats";
            }
            return ((long)seconds/60).ToString("00")+":"+(seconds%60).ToString("00.000")+" · s";
        }
        private bool IsTransportKeyBlocked(UnityEngine.UIElements.IEventHandler target)
        {
            if(EditorGUIUtility.editingTextField)return true;
            for(var element=target as VisualElement;element!=null;element=element.parent)
                if(element is TextElement||element is TextField||element.ClassListContains("unity-base-field"))return true;
            return false;
        }
        private void ReleaseLive(){foreach(var lease in liveLeases.Values)lease.Restore();liveLeases.Clear();}
        private void UpdateSession()
        {
            legacyPlayer?.Tick();if(!EditorApplication.isPlaying)arrangementPlayer?.Tick();capture?.Tick();
            double now=EditorApplication.timeSinceStartup;
            if(now-lastSessionUiRefresh<1.0/30)return;lastSessionUiRefresh=now;
            int identity=EditorApplication.isPlaying&&AudioRuntime.Exists?AudioRuntime.Instance.GetInstanceID():0;
            if(identity!=liveIdentity){ReleaseLive();liveIdentity=identity;}
            if(sessionLabel!=null)sessionLabel.text=identity==0?"Game: offline":EditorApplication.isPaused?"Game: paused":"Game: connected";
            if(muteButton!=null){muteButton.text=EditorUtility.audioMasterMute?"Output muted · Unmute":"Output on · Mute";muteButton.EnableInClassList("studio-v2-active",EditorUtility.audioMasterMute);}
            if(enterPlayButton!=null)enterPlayButton.style.display=CurrentAsset is SoundEvent&&!EditorApplication.isPlaying&&mode!="Live"?DisplayStyle.Flex:DisplayStyle.None;
            if(pauseButton!=null)
            {
                bool paused=EditorAudioPreview.IsPaused||arrangementPlayer!=null&&arrangementPlayer.IsPaused;
                pauseButton.text=paused?"▶ Resume":"Ⅱ Pause";
                pauseButton.SetEnabled(EditorAudioPreview.CanPause&&(EditorAudioPreview.IsPlaying||EditorAudioPreview.IsPaused)||arrangementPlayer!=null&&(arrangementPlayer.IsPlaying||arrangementPlayer.IsPaused));
                pauseButton.tooltip=legacyPlayer!=null?"Legacy MusicTrack supports Play / Stop / Restart. Pause is available for editable arrangements and dry sources.":"Pause this source or arrangement only. No global audio pause.";
            }
            if(auditionStatus!=null)
            {
                string state=EditorAudioPreview.LastError!=""?EditorAudioPreview.LastError:notice!=""?notice
                    :EditorAudioPreview.IsPaused?"Source (dry) · paused"
                    :arrangementPlayer!=null&&arrangementPlayer.IsPaused?"Music audition · paused · snapshot retained"
                    :EditorAudioPreview.IsLoading?"Loading source…"
                    :EditorAudioPreview.IsPlaying?"Source (dry) · "+EditorAudioPreview.CurrentClip.name
                    :arrangementPlayer!=null&&arrangementPlayer.IsPlaying||legacyPlayer!=null&&legacyPlayer.IsPlaying?"Music audition · runtime DSP scheduler"
                    :auditionHandle.IsPlaying?"Event audition · "+(auditionTarget!=null?"selected emitter":"listener position")
                    :"Ready · Play a source or audition an event";
                auditionStatus.text=state;auditionStatus.tooltip=state;
            }
            var play=rootVisualElement.Q<ToolbarButton>("studio-play");if(play!=null)play.text=CurrentAsset is AudioClip&&EditorAudioPreview.IsPaused||arrangementPlayer!=null&&arrangementPlayer.IsPaused?"▶ Resume":CurrentAsset is AudioClip?"▶ Play source (dry)":"▶ Play event";
            if(transportPosition!=null)
            {
                bool sourceActive=EditorAudioPreview.IsPlaying||EditorAudioPreview.IsPaused;
                bool musicActive=arrangementPlayer!=null&&(arrangementPlayer.IsPlaying||arrangementPlayer.IsPaused)||legacyPlayer!=null&&legacyPlayer.IsPlaying;
                double tempo=CurrentAsset is MusicArrangement score?score.Tempo:CurrentAsset is MusicTrack track?track.Tempo:120;
                int meter=CurrentAsset is MusicArrangement a?a.BeatsPerBar:CurrentAsset is MusicTrack m?m.BeatsPerBar:4;
                transportPosition.text=sourceActive||musicActive?FormatTransportTime(sourceActive?EditorAudioPreview.Position:AuditionPosition,bars&&!sourceActive,tempo,meter):auditionHandle.IsPlaying?"Playing":"Stopped";
            }
            if(EditorAudioPreview.IsPlaying||arrangementPlayer!=null&&arrangementPlayer.IsPlaying||legacyPlayer!=null&&legacyPlayer.IsPlaying||auditionHandle.IsPlaying)canvas?.MarkDirtyRepaint();
            if(now-lastRefresh>.25){lastRefresh=now;RefreshDocuments();UpdateCompactLayout();canvas?.MarkDirtyRepaint();Repaint();}
        }
        private void RefreshDock()
        {
            if(bottom==null)return;bottom.Clear();bool open=bottomTab!="";bottom.style.display=open?DisplayStyle.Flex:DisplayStyle.None;bottom.style.height=open?210:0;deck.style.display=DisplayStyle.Flex;deck.style.minHeight=open?28:160;deck.style.height=open?28:(position.height<850?200:240);deckScroll.style.display=open?DisplayStyle.None:DisplayStyle.Flex;
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
