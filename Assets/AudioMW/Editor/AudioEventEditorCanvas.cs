using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow
    {
        private int dragKind;
        private string dragClip;
        private float dragOrigin,dragDelta;
        private double dragStart,dragTrim,dragDuration;
        private MusicStateProfile transitionProfile;
        private readonly HashSet<string> mutedLanes=new HashSet<string>();
        private readonly HashSet<string> soloLanes=new HashSet<string>();
        private void DrawCanvas()
        {
            if(CurrentAsset==null){EditorGUILayout.HelpBox("Open an event, or create one with New in the browser. Drop audio files to add references.",MessageType.Info);return;}
            if(notice!="")EditorGUILayout.HelpBox(notice,MessageType.Info);
            if(view=="Parameters"){DrawParameters();return;}
            if(view=="Transitions"){DrawTransitions();return;}
            if(CurrentAsset is MusicArrangement score){string issue=score.Validate();if(issue!=""){EditorGUILayout.HelpBox(issue+" Edit the selected properties in Deck.",MessageType.Error);if(GUILayout.Button("Arrangement properties")){selectedLane="";selectedClips.Clear();ShowDeck(score);}return;}DrawTimeline(score,false);return;}
            if(CurrentAsset is MusicTrack music)
            {
                EditorGUILayout.BeginHorizontal();EditorGUILayout.LabelField("Legacy projection · placement and trim fixed by MusicPlayer");
                if(GUILayout.Button("Create editable arrangement…",GUILayout.Width(205)))
                {
                    string path=EditorUtility.SaveFilePanelInProject("Create arrangement adapter",music.name+" Arrangement","asset","Creates a separate asset. Existing track and gameplay references stay unchanged. Layer smoothing differs: arrangement curves apply immediately; no automatic replacement.");
                    if(path!=""){var result=ArrangementAuthoring.FromLegacy(music);AssetDatabase.CreateAsset(result,path);Undo.RegisterCreatedObjectUndo(result,"Create arrangement adapter");OpenDocument(result);}
                }
                EditorGUILayout.EndHorizontal();
                int version=EditorUtility.GetDirtyCount(music);
                if(legacyProjection==null||projectionSource!=music||projectionVersion!=version){if(legacyProjection!=null)DestroyImmediate(legacyProjection);legacyProjection=ArrangementAuthoring.FromLegacy(music);legacyProjection.hideFlags=HideFlags.HideAndDontSave;projectionSource=music;projectionVersion=version;}
                DrawTimeline(legacyProjection,true);return;
            }
            DrawContainer();
        }
        private void DrawContainer()
        {
            var sources=AudioSourceCatalog.Collect(CurrentAsset);
            EditorGUILayout.BeginHorizontal();if(GUILayout.Button(CurrentAsset.name,EditorStyles.toolbarButton)){selectedClips.Clear();selectedLane="";ShowDeck(CurrentAsset);}GUILayout.FlexibleSpace();EditorGUILayout.LabelField(CurrentAsset is SoundEvent item?(item.ContainerMode==ContainerMode.Simple?"Single / Random":item.ContainerMode.ToString())+" · "+AudioEventContextPane.SelectionLabel(item.SelectionMode):CurrentAsset.GetType().Name,GUILayout.Width(220));EditorGUILayout.EndHorizontal();
            Rect area=new Rect(0,24,canvas.contentRect.width,Mathf.Max(100,canvas.contentRect.height-24));
            HandleContainerDrop(area);
            var doc=CurrentDocument;doc.Scroll=EditorGUILayout.BeginScrollView(doc.Scroll,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            foreach(var source in sources)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                if(GUILayout.Button(source.Name,EditorStyles.label,GUILayout.MinWidth(150))){ShowDeck(source.Clip);}
                EditorGUILayout.LabelField(source.Clip!=null?AudioSourcePreviewPane.SourceFormat(source.Clip):source.PreviewIssue,GUILayout.Width(170));
                if(source.Clip!=null&&GUILayout.Button("Preview source (dry)",GUILayout.Width(150))){StopAudition();EditorAudioPreview.Play(source.Clip);}
                EditorGUILayout.EndHorizontal();
                if(source.Clip!=null&&deckTarget==source.Clip){var texture=WaveformCache.Get(source.Clip);Rect wave=GUILayoutUtility.GetRect(100,70,GUILayout.ExpandWidth(true));if(texture!=null)GUI.DrawTexture(wave,texture,ScaleMode.StretchToFill);else GUI.Label(wave,"Waveform unavailable");}
            }
            if(sources.Count==0)EditorGUILayout.LabelField("No sources. Drop AudioClips here or edit the event in Deck.");
            EditorGUILayout.EndScrollView();
        }
        private void HandleContainerDrop(Rect area)
        {
            var e=Event.current;if(!area.Contains(e.mousePosition))return;
            var clips=DragAndDrop.objectReferences.OfType<AudioClip>().ToArray();if(clips.Length==0)return;
            if(e.type==EventType.DragUpdated){DragAndDrop.visualMode=CurrentAsset is SoundEvent?DragAndDropVisualMode.Copy:DragAndDropVisualMode.Rejected;e.Use();}
            if(e.type==EventType.DragPerform&&CurrentAsset is SoundEvent item)
            {
                if(item.ContainerMode!=ContainerMode.Simple){notice="Drop sources into a Simple/Random container. Child event fields are editable in Deck.";return;}
                Undo.RecordObject(item,"Add audio sources");item.Clips=item.Clips.Concat(clips).ToArray();EditorUtility.SetDirty(item);DragAndDrop.AcceptDrag();ShowDeck(item);e.Use();
            }
        }
        private void DrawTimeline(MusicArrangement score,bool legacy)
        {
            var doc=CurrentDocument;if(doc==null)return;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bars=GUILayout.Toggle(bars,"Bars:Beats",EditorStyles.toolbarButton,GUILayout.Width(90));if(!bars)GUILayout.Label("Seconds",GUILayout.Width(60));
            GUILayout.Label(score.Tempo+" BPM · "+score.BeatsPerBar+"/4",GUILayout.Width(120));snap=EditorGUILayout.Popup(snap,new[]{"Snap off","Beat","Bar"},GUILayout.Width(85));
            doc.Zoom=GUILayout.HorizontalSlider(doc.Zoom,10,240,GUILayout.Width(100));
            if(GUILayout.Button("Fit all",EditorStyles.toolbarButton))doc.Zoom=Mathf.Clamp((canvas.layout.width-200)/(float)Math.Max(1,score.Exit),10,240);
            using(new EditorGUI.DisabledScope(legacy))if(GUILayout.Button("+ Track",EditorStyles.toolbarButton)){Undo.RecordObject(score,"Add lane");score.Lanes.Add(new ArrangementLane{Name="Track "+(score.Lanes.Count+1)});EditorUtility.SetDirty(score);}
            EditorGUILayout.EndHorizontal();
            Rect viewport=GUILayoutUtility.GetRect(200,Mathf.Max(120,canvas.layout.height-85),GUILayout.ExpandWidth(true));
            float header=180,rowHeight=54,totalWidth=Mathf.Max(viewport.width-18,header+(float)score.Exit*doc.Zoom+40),height=70+Math.Max(4,score.Lanes.Count+(legacy?0:1))*rowHeight;
            if(Event.current.type==EventType.ScrollWheel&&viewport.Contains(Event.current.mousePosition))
            {
                if(Event.current.control||Event.current.command){float point=Event.current.mousePosition.x-viewport.x-header;float time=(point+doc.Scroll.x)/doc.Zoom;doc.Zoom=Mathf.Clamp(doc.Zoom*Mathf.Pow(1.1f,-Event.current.delta.y),10,240);doc.Scroll.x=Mathf.Max(0,time*doc.Zoom-point);Event.current.Use();}
                else if(Event.current.shift){doc.Scroll.x=Mathf.Max(0,doc.Scroll.x+Event.current.delta.y*16);Event.current.Use();}
            }
            if(!legacy&&viewport.Contains(Event.current.mousePosition))
            {
                float origin=viewport.x+header-doc.Scroll.x;
                for(int i=0;i<=score.Lanes.Count;i++)
                {
                    Rect drop=new Rect(viewport.x,viewport.y+68+i*rowHeight-doc.Scroll.y,viewport.width,rowHeight);
                    TimelineDrop(score,i<score.Lanes.Count?score.Lanes[i].Id:"",drop,origin,doc.Zoom);
                }
            }
            doc.Scroll=GUI.BeginScrollView(viewport,doc.Scroll,new Rect(0,0,totalWidth,height));
            EditorGUI.DrawRect(new Rect(0,0,totalWidth,height),new Color(.125f,.134f,.145f));
            double grid=snap==2?score.BeatSeconds*score.BeatsPerBar:score.BeatSeconds;while(grid*doc.Zoom<28)grid*=2;
            Handles.BeginGUI();Handles.color=new Color(.26f,.28f,.30f);
            for(double t=0;t<=score.Exit;t+=grid){float x=header+(float)t*doc.Zoom;Handles.DrawLine(new Vector3(x,24),new Vector3(x,height));GUI.Label(new Rect(x+3,0,80,22),bars?Musical(t,score):t.ToString("0.##"));}
            Handles.EndGUI();
            GUI.Label(new Rect(6,27,170,22),"Logic / cues");
            Cue(score.Loop&&score.Entry==score.LoopStart?"Entry / Loop in":"Entry",score.Entry,header,doc.Zoom);
            Cue(score.Loop&&score.Exit==score.LoopEnd?"Loop out / Exit":"Exit",score.Exit,header,doc.Zoom);
            if(score.Loop){if(score.LoopStart!=score.Entry)Cue("Loop in",score.LoopStart,header,doc.Zoom);if(score.LoopEnd!=score.Exit)Cue("Loop out",score.LoopEnd,header,doc.Zoom);}
            foreach(var cue in score.Markers)GUI.Label(new Rect(header+(float)cue.Seconds*doc.Zoom,45,120,20),new GUIContent("◆ "+cue.Name,cue.Seconds+" s"),EditorStyles.miniLabel);
            for(int i=0;i<score.Lanes.Count;i++)
            {
                var lane=score.Lanes[i];float y=68+i*rowHeight;Rect laneHeader=new Rect(0,y,header-4,26);
                if(selectedLane==(legacy?"legacy:"+i:lane.Id))EditorGUI.DrawRect(new Rect(0,y,header-4,rowHeight-2),new Color(.22f,.29f,.21f));
                if(GUI.Button(laneHeader,new GUIContent(lane.Name,lane.Name),EditorStyles.toolbarButton)){selectedLane=legacy?"legacy:"+i:lane.Id;CurrentDocument.Selection="lane:"+lane.Id;selectedClips.Clear();ShowDeck(CurrentAsset);}
                if(!legacy)
                {
                    bool mute=GUI.Toggle(new Rect(8,y+28,32,22),mutedLanes.Contains(lane.Id),"M",EditorStyles.miniButton);
                    bool solo=GUI.Toggle(new Rect(44,y+28,32,22),soloLanes.Contains(lane.Id),"S",EditorStyles.miniButton);
                    if(mute)mutedLanes.Add(lane.Id);else mutedLanes.Remove(lane.Id);if(solo)soloLanes.Add(lane.Id);else soloLanes.Remove(lane.Id);
                    if(arrangementPlayer!=null)arrangementPlayer.SetLaneAudition(lane.Id,mute,solo);
                    GUI.Label(new Rect(80,y+28,95,22),new GUIContent(AudioGain.ToDecibels(lane.Gain).ToString("0.#")+" dB",(lane.Output!=null?lane.Output.name:"Arrangement output")+(lane.Parameter!=null?" · "+lane.Parameter.name:"")),EditorStyles.miniLabel);
                    
                }
                foreach(var clip in lane.Clips)
                {
                    double start=clip.Start,duration=clip.Duration;
                    if(dragKind==1&&selectedClips.Contains(clip.Id))start+=dragDelta;
                    if(clip.Id==dragClip&&dragKind==2){start+=dragDelta;duration-=dragDelta;}
                    if(clip.Id==dragClip&&dragKind==3)duration+=dragDelta;
                    Rect rect=new Rect(header+(float)start*doc.Zoom,y+3,Mathf.Max(4,(float)duration*doc.Zoom),rowHeight-8);
                    if((rect.xMax<doc.Scroll.x||rect.x>doc.Scroll.x+viewport.width)&&dragClip!=clip.Id)continue;
                    EditorGUI.DrawRect(rect,selectedClips.Contains(clip.Id)?new Color(.35f,.43f,.27f):new Color(.22f,.34f,.35f));
                    DrawClipWaveform(clip,rect,duration,doc.Scroll.x,viewport.width);
                    GUI.Label(new Rect(rect.x+6,rect.y,Mathf.Max(0,rect.width-12),22),new GUIContent(clip.Source!=null?clip.Source.name:"Missing",clip.Id),EditorStyles.miniLabel);
                    if(!legacy)HandleClipDrag(score,lane,clip,rect,doc.Zoom);
                    else if(Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition)){selectedLane="legacy:"+i;ShowDeck(CurrentAsset);Event.current.Use();}
                }
            }
            if(!legacy){var addArea=new Rect(0,68+score.Lanes.Count*rowHeight,totalWidth,rowHeight);GUI.Label(new Rect(8,addArea.y+12,350,24),"Drop AudioClips here to create tracks",EditorStyles.miniLabel);}
            if(arrangementPlayer!=null&&arrangementPlayer.IsPlaying){float x=header+(float)arrangementPlayer.Position*doc.Zoom;EditorGUI.DrawRect(new Rect(x,20,2,height-20),new Color(.66f,.81f,.46f));}
            if(!legacy&&Event.current.type==EventType.MouseDown&&Event.current.mousePosition.y<24&&Event.current.mousePosition.x>=header&&arrangementPlayer!=null&&arrangementPlayer.IsPlaying){arrangementPlayer.Seek((Event.current.mousePosition.x-header)/doc.Zoom);Event.current.Use();}
            GUI.EndScrollView();
            if(!legacy&&Event.current.type==EventType.KeyDown&&!EditorGUIUtility.editingTextField)
            {
                if((Event.current.control||Event.current.command)&&Event.current.keyCode==KeyCode.D){ArrangementAuthoring.Duplicate(score,selectedClips);Event.current.Use();}
                else if(Event.current.keyCode==KeyCode.Delete||Event.current.keyCode==KeyCode.Backspace){ArrangementAuthoring.Delete(score,selectedClips);selectedClips.Clear();Event.current.Use();}
                else if(Event.current.keyCode==KeyCode.Escape){CancelDrag();Event.current.Use();}
            }
        }
        private static void DrawClipWaveform(ArrangementClip clip,Rect rect,double duration,float scroll,float width)
        {
            var texture=WaveformCache.Get(clip.Source);if(texture==null)return;
            double length=MusicTrack.GetExactDuration(clip.Source),span=length-clip.Trim;
            if(span<=0||duration<=0)return;
            double px=rect.width/duration;
            double visibleStart=Math.Max(0,(scroll-rect.x)/px),visibleEnd=Math.Min(duration,(scroll+width-rect.x)/px);
            if(visibleEnd<=visibleStart)return;
            long first=clip.Repeat?(long)Math.Floor(visibleStart/span):0;
            for(long piece=first;;piece++)
            {
                double begin=piece*span,a=Math.Max(visibleStart,begin),b=Math.Min(visibleEnd,begin+span);
                if(a>=visibleEnd)break;
                if(b>a)GUI.DrawTextureWithTexCoords(new Rect(rect.x+(float)(a*px),rect.y+20,(float)((b-a)*px),rect.height-22),texture,new Rect((float)((clip.Trim+a-begin)/length),0,(float)((b-a)/length),1));
                if(!clip.Repeat)break;
            }
        }
        private static string Musical(double seconds,MusicArrangement score){double beat=seconds/score.BeatSeconds;return (1+(int)(beat/score.BeatsPerBar))+":"+(1+(int)(beat%score.BeatsPerBar));}
        private static void Cue(string name,double time,float header,float zoom){GUI.Label(new Rect(header+(float)time*zoom,25,100,32),new GUIContent("▼ "+name,name+" · "+time.ToString("0.###")+" s"),EditorStyles.miniLabel);}
        private double Snap(double value,MusicArrangement score)=>snap==0?value:Math.Round(value/(score.BeatSeconds*(snap==2?score.BeatsPerBar:1)))*score.BeatSeconds*(snap==2?score.BeatsPerBar:1);
        private void TimelineDrop(MusicArrangement score,string laneId,Rect area,float header,float zoom)
        {
            var e=Event.current;if(!area.Contains(e.mousePosition))return;var clips=DragAndDrop.objectReferences.OfType<AudioClip>().ToArray();if(clips.Length==0)return;
            if(e.type==EventType.Repaint){float x=header+(float)Math.Max(0,Snap((e.mousePosition.x-header)/zoom,score))*zoom;EditorGUI.DrawRect(new Rect(x,area.y,Mathf.Max(4,(float)MusicTrack.GetExactDuration(clips[0])*zoom),area.height),new Color(.6f,.8f,.4f,.25f));}
            if(e.type==EventType.DragUpdated){DragAndDrop.visualMode=DragAndDropVisualMode.Copy;canvas.MarkDirtyRepaint();e.Use();}
            if(e.type==EventType.DragPerform){Undo.IncrementCurrentGroup();int group=Undo.GetCurrentGroup();foreach(var clip in clips)ArrangementAuthoring.AddClip(score,laneId,clip,Math.Max(0,Snap((e.mousePosition.x-header)/zoom,score)));Undo.CollapseUndoOperations(group);DragAndDrop.AcceptDrag();ShowDeck(score);e.Use();}
        }
        private void HandleClipDrag(MusicArrangement score,ArrangementLane lane,ArrangementClip clip,Rect rect,float zoom)
        {
            var e=Event.current;
            if(e.type==EventType.MouseDown&&e.button==0&&rect.Contains(e.mousePosition))
            {
                if(!e.shift&&!selectedClips.Contains(clip.Id))selectedClips.Clear();selectedClips.Add(clip.Id);selectedLane=lane.Id;CurrentDocument.Selection=clip.Id;ShowDeck(score);
                canvas.Focus();GUIUtility.hotControl=GUIUtility.GetControlID(FocusType.Passive);dragKind=e.mousePosition.x-rect.x<6?2:rect.xMax-e.mousePosition.x<6?3:1;dragClip=clip.Id;dragOrigin=e.mousePosition.x;dragDelta=0;dragStart=clip.Start;dragTrim=clip.Trim;dragDuration=clip.Duration;e.Use();
            }
            if(dragClip!=clip.Id||dragKind==0)return;
            if(e.type==EventType.MouseDrag){dragDelta=(float)Snap((e.mousePosition.x-dragOrigin)/zoom,score);if(dragKind==1)dragDelta=Mathf.Max(-(float)score.Lanes.SelectMany(l=>l.Clips).Where(c=>selectedClips.Contains(c.Id)).Min(c=>c.Start),dragDelta);if(dragKind==2)dragDelta=Mathf.Clamp(dragDelta,-(float)Math.Min(dragStart,dragTrim),(float)dragDuration-.001f);if(dragKind==3)dragDelta=Mathf.Max(-(float)dragDuration+.001f,Mathf.Min(dragDelta,clip.Repeat?float.MaxValue:(float)(MusicTrack.GetExactDuration(clip.Source)-clip.Trim-clip.Duration)));e.Use();}
            if(e.type==EventType.MouseUp)
            {
                if(dragKind==1)ArrangementAuthoring.Move(score,selectedClips,dragDelta);
                else{Undo.RecordObject(score,"Trim timeline clip");if(dragKind==2){clip.Start=dragStart+dragDelta;clip.Trim=dragTrim+dragDelta;clip.Duration=dragDuration-dragDelta;}else clip.Duration=dragDuration+dragDelta;clip.FadeIn=Math.Min(clip.FadeIn,clip.Duration);clip.FadeOut=Math.Min(clip.FadeOut,clip.Duration-clip.FadeIn);EditorUtility.SetDirty(score);}
                CancelDrag();ShowDeck(score);e.Use();
            }
        }
        private void CancelDrag(){if(dragKind!=0)GUIUtility.hotControl=0;dragKind=0;dragClip=null;dragDelta=0;}
        private void DrawParameters()
        {
            var parameters=new HashSet<SoundParameter>();
            if(CurrentAsset is MusicTrack music)foreach(var layer in music.Layers)if(layer!=null&&layer.Parameter!=null)parameters.Add(layer.Parameter);
            if(CurrentAsset is MusicArrangement score)foreach(var lane in score.Lanes)if(lane.Parameter!=null)parameters.Add(lane.Parameter);
            if(CurrentAsset is SoundEvent sound){if(sound.BlendParameter!=null)parameters.Add(sound.BlendParameter);if(sound.SwitchParameter!=null)parameters.Add(sound.SwitchParameter);foreach(var binding in sound.ParameterBindings)if(binding!=null&&binding.Parameter!=null)parameters.Add(binding.Parameter);}
            if(CurrentAsset is SoundParameter direct)parameters.Add(direct);
            foreach(var parameter in parameters){EditorGUILayout.BeginVertical(EditorStyles.helpBox);if(GUILayout.Button(parameter.name,EditorStyles.boldLabel))ShowDeck(parameter);DrawLiveParameter(parameter);EditorGUILayout.EndVertical();}
            if(parameters.Count==0)EditorGUILayout.LabelField("No referenced parameters. Assign a binding in Deck.");
        }
        private void DrawTransitions()
        {
            transitionProfile=(MusicStateProfile)EditorGUILayout.ObjectField("State profile",CurrentAsset is MusicStateProfile p?p:transitionProfile,typeof(MusicStateProfile),false);
            if(transitionProfile==null){EditorGUILayout.LabelField("Choose a MusicStateProfile. These are authored rules, not pending capture.");return;}
            if(GUILayout.Button("Edit rules in Deck"))ShowDeck(transitionProfile);
            foreach(var rule in transitionProfile.Transitions)if(rule!=null)EditorGUILayout.LabelField(rule.From+" → "+rule.To,rule.Quantization+" · "+rule.FadeSeconds+" s · "+rule.Curve);
            var player=mode=="Live"&&AudioRuntime.Exists?AudioSystem.Music:legacyPlayer;
            if(player==null){EditorGUILayout.LabelField("Runtime state unavailable");return;}
            using(new EditorGUI.DisabledScope(!player.IsTransitionPending))if(GUILayout.Button("Cancel pending transition"))player.CancelTransition();
            EditorGUILayout.LabelField("Current",player.CurrentTrack!=null?player.CurrentTrack.name:"None");EditorGUILayout.LabelField("Pending",player.PendingTrack!=null?player.PendingTrack.name:"None");EditorGUILayout.LabelField("Captured fade",player.PendingFadeSeconds+" s");
            foreach(var state in transitionProfile.States)if(state!=null&&GUILayout.Button("Request "+state.Name))transitionProfile.Request(player,state.Name);
        }
        private void DrawRequests()
        {
            if(!AudioRuntime.Exists){EditorGUILayout.LabelField("No local runtime request capture.");return;}
            foreach(var record in AudioRuntime.Instance.Debugger.Records.Reverse().Take(40)){EditorGUILayout.BeginHorizontal();if(GUILayout.Button(record.EventName,GUILayout.Width(160))){OpenDocument(EventDebuggerWindow.SettingsEvent(record));deckSection=record.Rejection.HasInstanceLimit||record.Rejection.HasCooldown?"Limits":"Sources";}EditorGUILayout.LabelField(record.DescribeOutcome());EditorGUILayout.EndHorizontal();}
        }
    }
}
