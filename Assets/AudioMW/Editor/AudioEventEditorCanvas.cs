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
        private double dragStart,dragTrim,dragDuration,dragFade;
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
        [SerializeField] private int waveformZoom=2;
        private void DrawWaveformZoom(){waveformZoom=EditorGUILayout.Popup(waveformZoom,new[]{"Wave ×1","Wave ×2","Wave ×4","Wave ×8"},GUILayout.Width(85));}
        private int containerSource=-1;
        private string containerDocument="";
        private SoundEvent containerFocus;
        private readonly HashSet<string> collapsedContainers=new HashSet<string>();
        private Vector2 sourceScroll,structureScroll;
        private void DrawContainer()
        {
            string guid=CurrentDocument.Guid;if(containerDocument!=guid){containerDocument=guid;containerFocus=CurrentAsset as SoundEvent;containerSource=0;sourceScroll=Vector2.zero;structureScroll=Vector2.zero;collapsedContainers.Clear();}
            if(containerFocus==null&&CurrentAsset is SoundEvent currentSound)containerFocus=currentSound;
            var sources=AudioSourceCatalog.Collect(containerFocus!=null?(Object)containerFocus:CurrentAsset);
            containerSource=Mathf.Clamp(containerSource,-1,sources.Count-1);
            Rect area=new Rect(0,0,canvas.contentRect.width,Mathf.Max(120,canvas.contentRect.height-4));
            HandleContainerDrop(area);
            float structureWidth=Mathf.Clamp(area.width*.30f,160,250);
            Rect structure=new Rect(area.x+6,area.y+6,structureWidth-10,area.height-12);
            Rect sheet=new Rect(area.x+structureWidth+4,area.y+6,area.width-structureWidth-10,area.height-12);
            GUI.Box(structure,GUIContent.none,EditorStyles.helpBox);GUI.Box(sheet,GUIContent.none,EditorStyles.helpBox);
            GUILayout.BeginArea(structure);structureScroll=GUILayout.BeginScrollView(structureScroll);
            if(CurrentAsset is SoundEvent root)DrawContainerBranch(root,null,-1,root.name,0,new HashSet<SoundEvent>());
            else GUILayout.Label(CurrentAsset.name,EditorStyles.boldLabel);
            GUILayout.EndScrollView();GUILayout.EndArea();
            float listHeight=Mathf.Clamp(sheet.height*.53f,90,260);
            GUILayout.BeginArea(new Rect(sheet.x+8,sheet.y+6,sheet.width-16,listHeight));
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);GUILayout.Label("Source",EditorStyles.boldLabel);GUILayout.Label("Length",GUILayout.Width(65));GUILayout.Label(sources.Any(s=>s.Owner!=null&&s.SourceIndex>=0&&s.Owner.SelectionMode==ClipSelectionMode.WeightedRandom)?"Weight":"Weight (inactive)",GUILayout.Width(105));EditorGUILayout.EndHorizontal();
            sourceScroll=GUILayout.BeginScrollView(sourceScroll);
            for(int i=0;i<sources.Count;i++)
            {
                var source=sources[i];Rect row=GUILayoutUtility.GetRect(100,28,GUILayout.ExpandWidth(true));
                EditorGUI.DrawRect(row,i==containerSource?new Color(.22f,.29f,.21f):i%2==0?new Color(.16f,.17f,.18f):new Color(.14f,.15f,.16f));
                GUI.Label(new Rect(row.x+6,row.y,Mathf.Max(0,row.width-190),28),new GUIContent(source.Name,source.Location),EditorStyles.label);
                GUI.Label(new Rect(row.xMax-180,row.y,65,28),source.Clip!=null?source.Clip.length.ToString("0.###")+" s":"—",EditorStyles.miniLabel);
                if(source.SourceIndex<0)GUI.Label(new Rect(row.xMax-105,row.y+3,96,22),"—",EditorStyles.miniLabel);
                else using(new EditorGUI.DisabledScope(source.Owner==null||source.Owner.SelectionMode!=ClipSelectionMode.WeightedRandom))
                {
                    float weight=SourceWeight(source.Owner,source.SourceIndex);
                    EditorGUI.BeginChangeCheck();float next=EditorGUI.DelayedFloatField(new Rect(row.xMax-105,row.y+3,96,22),weight);
                    if(EditorGUI.EndChangeCheck()){SetSourceWeight(source.Owner,source.SourceIndex,next);TrackEdit(source.Owner);}
                }
                if(Event.current.type==EventType.MouseDown&&new Rect(row.x,row.y,Mathf.Max(0,row.width-110),row.height).Contains(Event.current.mousePosition)){SelectContainerSource(source,i);Event.current.Use();}
            }
            if(sources.Count==0)GUILayout.Label("No sources. Drop AudioClips here.",EditorStyles.wordWrappedMiniLabel);
            GUILayout.EndScrollView();GUILayout.EndArea();
            Rect preview=new Rect(sheet.x+8,sheet.y+listHeight+14,sheet.width-16,Mathf.Max(60,sheet.height-listHeight-22));
            GUILayout.BeginArea(preview);
            if(containerSource>=0&&containerSource<sources.Count)
            {
                var source=sources[containerSource];EditorGUILayout.BeginHorizontal();GUILayout.Label(source.Name,EditorStyles.boldLabel);GUILayout.FlexibleSpace();DrawWaveformZoom();
                using(new EditorGUI.DisabledScope(source.Clip==null||source.PreviewIssue!=""))if(GUILayout.Button("Play source (dry)",GUILayout.Width(125))){StopAudition();EditorAudioPreview.Play(source.Clip);}
                if(GUILayout.Button("Stop",GUILayout.Width(45)))EditorAudioPreview.Stop();EditorGUILayout.EndHorizontal();
                if(source.PreviewIssue!="")EditorGUILayout.HelpBox(source.PreviewIssue,MessageType.Info);
                if(source.Clip!=null)
                {
                    Rect wave=GUILayoutUtility.GetRect(100,Mathf.Max(40,preview.height-58),GUILayout.ExpandWidth(true));EditorGUI.DrawRect(wave,new Color(.10f,.12f,.13f));
                    if(!WaveformCache.Draw(source.Clip,wave,0,-1,Mathf.Pow(2,waveformZoom)))GUI.Label(wave,"Waveform unavailable: PCM not readable.");
                    DrawSourceRuler(source.Clip, GUILayoutUtility.GetRect(100,18,GUILayout.ExpandWidth(true)));
                }
            }
            else GUILayout.Label("Select a source to inspect its waveform.",EditorStyles.miniLabel);
            GUILayout.EndArea();
        }
        private void DrawContainerBranch(SoundEvent item,SoundEvent parent,int childIndex,string path,int depth,HashSet<SoundEvent> visited)
        {
            if(item==null){GUILayout.Label("Missing child event",EditorStyles.miniLabel);return;}
            if(depth>=32||!visited.Add(item)){GUILayout.Label("Cycle / depth limit: "+item.name,EditorStyles.miniLabel);return;}
            Rect row=GUILayoutUtility.GetRect(100,26,GUILayout.ExpandWidth(true));
            if(containerFocus==item&&containerSource<0)EditorGUI.DrawRect(row,new Color(.22f,.29f,.21f));
            float indent=Mathf.Min(depth*14,70);
            bool open=!collapsedContainers.Contains(path);
            bool next=EditorGUI.Foldout(new Rect(row.x+indent,row.y,16,row.height),open,GUIContent.none);
            if(next!=open){if(next)collapsedContainers.Remove(path);else collapsedContainers.Add(path);}
            if(GUI.Button(new Rect(row.x+indent+18,row.y,row.width-indent-18,row.height),new GUIContent(parent!=null&&parent.Children[childIndex]!=null&&!string.IsNullOrEmpty(parent.Children[childIndex].Label)?parent.Children[childIndex].Label:item.name,item.ContainerMode+" · "+AssetDatabase.GetAssetPath(item)),EditorStyles.label))
            {containerFocus=item;containerSource=-1;ShowDeck(item);deckChildParent=parent;deckChildIndex=childIndex;}
            if(next)
            {
                if(item.ContainerMode==ContainerMode.Simple||item.ContainerMode==ContainerMode.Blend)
                {
                    var entries=AudioSourceCatalog.Collect(item);
                    for(int index=0;index<entries.Count;index++)
                    {
                        var entry=entries[index];Rect leaf=GUILayoutUtility.GetRect(100,24,GUILayout.ExpandWidth(true));
                        if(containerFocus==item&&containerSource==index)EditorGUI.DrawRect(leaf,new Color(.22f,.29f,.21f));
                        if(GUI.Button(new Rect(leaf.x+indent+24,leaf.y,Mathf.Max(1,leaf.width-indent-24),leaf.height),new GUIContent(entry.Name,entry.Location),EditorStyles.label))
                        {containerFocus=item;SelectContainerSource(entry,index);}
                    }
                }
                else for(int index=0;index<item.Children.Length;index++)DrawContainerBranch(item.Children[index]?.Event,item,index,path+"/"+index,depth+1,visited);
            }
            visited.Remove(item);
        }
        private void SelectContainerSource(AudioSourceEntry source,int index)
        {
            if(source.Owner!=null){containerFocus=source.Owner;if(source.SourceIndex>=0)index=source.SourceIndex;}
            containerSource=index;ShowDeck(source.Owner!=null?(Object)source.Owner:source.Clip);deckSourceIndex=source.SourceIndex;
            if(breadcrumb!=null)breadcrumb.text=CurrentAsset.name+" › "+(source.Owner!=null&&source.Owner!=CurrentAsset?source.Owner.name+" › ":"")+source.Name;
        }
        public static float SourceWeight(SoundEvent item,int index)
        {
            if(item==null||index<0)return 1;
            float value=index<item.ClipWeights.Length?item.ClipWeights[index]:1;
            return float.IsNaN(value)||float.IsInfinity(value)?0:Mathf.Max(0,value);
        }
        public static void SetSourceWeight(SoundEvent item,int index,float value)
        {
            if(item==null||item.ContainerMode!=ContainerMode.Simple||item.SelectionMode!=ClipSelectionMode.WeightedRandom||index<0||index>=item.TotalClipCount||float.IsNaN(value)||float.IsInfinity(value))return;
            using(var serialized=new SerializedObject(item))
            {
                var weights=serialized.FindProperty("clipWeights");int old=weights.arraySize;
                if(index>=old){weights.arraySize=index+1;for(int i=old;i<=index;i++)weights.GetArrayElementAtIndex(i).floatValue=1;}
                weights.GetArrayElementAtIndex(index).floatValue=Mathf.Max(0,value);serialized.ApplyModifiedProperties();
            }
        }
        private static void DrawSourceRuler(AudioClip clip,Rect rect)
        {
            int ticks=Mathf.Max(2,(int)(rect.width/90));
            EditorGUI.DrawRect(new Rect(rect.x,rect.y,rect.width,1),new Color(.3f,.34f,.36f));
            for(int i=0;i<=ticks;i++)
            {
                float x=rect.x+rect.width*i/ticks;
                EditorGUI.DrawRect(new Rect(x,rect.y,1,4),new Color(.5f,.55f,.57f));
                GUI.Label(new Rect(i==ticks?x-55:x+2,rect.y+2,55,16),(clip.length*i/ticks).ToString("0.###")+" s",EditorStyles.miniLabel);
            }
        }
        private void HandleContainerDrop(Rect area)
        {
            var e=Event.current;if(!area.Contains(e.mousePosition))return;
            var clips=DragAndDrop.objectReferences.OfType<AudioClip>().ToArray();if(clips.Length==0)return;
            if(e.type==EventType.DragUpdated){DragAndDrop.visualMode=containerFocus!=null&&containerFocus.ContainerMode==ContainerMode.Simple?DragAndDropVisualMode.Copy:DragAndDropVisualMode.Rejected;e.Use();}
            if(e.type==EventType.DragPerform&&containerFocus!=null)
            {
                var item=containerFocus;
                if(item.ContainerMode!=ContainerMode.Simple){notice="Drop sources into a Simple/Random container. Child event fields are editable in Deck.";return;}
                Undo.RecordObject(item,"Add audio sources");item.Clips=item.Clips.Concat(clips).ToArray();EditorUtility.SetDirty(item);TrackEdit(item);DragAndDrop.AcceptDrag();ShowDeck(item);e.Use();
            }
        }
        private void DrawTimeline(MusicArrangement score,bool legacy)
        {
            var doc=CurrentDocument;if(doc==null)return;
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bars=GUILayout.Toggle(bars,"Bars:Beats",EditorStyles.toolbarButton,GUILayout.Width(90));if(!bars)GUILayout.Label("Seconds",GUILayout.Width(60));
            GUILayout.Label(score.Tempo+" BPM · "+score.BeatsPerBar+"/4",GUILayout.Width(120));snap=EditorGUILayout.Popup(snap,new[]{"Snap off","Beat","Bar"},GUILayout.Width(85));
            if(doc.Zoom>0){EditorGUI.BeginChangeCheck();doc.Zoom=GUILayout.HorizontalSlider(doc.Zoom,1,500,GUILayout.Width(100));if(EditorGUI.EndChangeCheck())doc.FitTimeline=false;}
            if(GUILayout.Button("Fit all",EditorStyles.toolbarButton,GUILayout.Width(60)))doc.FitTimeline=true;
            using(new EditorGUI.DisabledScope(legacy))if(GUILayout.Button("+ Track",EditorStyles.toolbarButton,GUILayout.Width(70))){Undo.RecordObject(score,"Add lane");score.Lanes.Add(new ArrangementLane{Name="Track "+(score.Lanes.Count+1)});EditorUtility.SetDirty(score);}
            DrawWaveformZoom();GUILayout.Label("Height",GUILayout.Width(42));doc.LaneHeight=GUILayout.HorizontalSlider(doc.LaneHeight,48,100,GUILayout.Width(65));
            GUILayout.FlexibleSpace();EditorGUILayout.EndHorizontal();
            Rect viewport=GUILayoutUtility.GetRect(200,Mathf.Max(120,canvas.layout.height-(legacy?48:24)),GUILayout.ExpandWidth(true));
            float header=Mathf.Min(250,viewport.width*.35f),ruler=68,rowHeight=doc.LaneHeight;
            Rect body=new Rect(viewport.x+header,viewport.y+ruler,Mathf.Max(1,viewport.width-header-16),Mathf.Max(1,viewport.height-ruler-16));
            double end=Math.Max(score.Exit,score.Lanes.SelectMany(l=>l.Clips).Select(c=>c.Start+c.Duration).DefaultIfEmpty(score.Exit).Max());
            if(doc.FitTimeline||doc.Zoom<=0){doc.Zoom=Mathf.Clamp((body.width-16)/(float)Math.Max(1,end),1,500);doc.Scroll.x=0;}
            float contentWidth=Mathf.Max(body.width,(float)end*doc.Zoom+16),contentHeight=Mathf.Max(body.height,(score.Lanes.Count+1)*rowHeight);
            var input=Event.current;
            if(input.type==EventType.ScrollWheel&&viewport.Contains(input.mousePosition))
            {
                if(input.control||input.command){doc.FitTimeline=false;float point=Mathf.Clamp(input.mousePosition.x-body.x,0,body.width);float time=(point+doc.Scroll.x)/doc.Zoom;doc.Zoom=Mathf.Clamp(doc.Zoom*Mathf.Pow(1.1f,-input.delta.y),1,500);doc.Scroll.x=Mathf.Max(0,time*doc.Zoom-point);}
                else if(input.shift)doc.Scroll.x+=input.delta.y*20;
                else doc.Scroll.y+=input.delta.y*20;
                input.Use();
            }
            doc.Scroll.x=Mathf.Clamp(doc.Scroll.x,0,Mathf.Max(0,contentWidth-body.width));
            doc.Scroll.y=Mathf.Clamp(doc.Scroll.y,0,Mathf.Max(0,contentHeight-body.height));
            doc.Scroll.x=GUI.HorizontalScrollbar(new Rect(body.x,body.yMax,body.width,16),doc.Scroll.x,body.width,0,contentWidth);
            doc.Scroll.y=GUI.VerticalScrollbar(new Rect(body.xMax,body.y,16,body.height),doc.Scroll.y,body.height,0,contentHeight);
            double grid=score.BeatSeconds;while(grid*doc.Zoom<24)grid*=2;
            double first=Math.Floor(doc.Scroll.x/doc.Zoom/grid)*grid,last=(doc.Scroll.x+body.width)/doc.Zoom;
            GUI.BeginGroup(new Rect(body.x,viewport.y,body.width,ruler));
            EditorGUI.DrawRect(new Rect(0,0,body.width,ruler),new Color(.12f,.13f,.14f));
            if(score.Loop)
            {
                float x=(float)score.LoopStart*doc.Zoom-doc.Scroll.x,w=(float)(score.LoopEnd-score.LoopStart)*doc.Zoom;
                EditorGUI.DrawRect(new Rect(x,26,w,22),new Color(.25f,.34f,.20f));
                GUI.Label(new Rect(Mathf.Max(0,x)+5,27,Mathf.Min(w,180),20),"Loop region",EditorStyles.miniLabel);
            }
            for(double t=first;t<=last;t+=grid)
            {
                float x=(float)t*doc.Zoom-doc.Scroll.x;
                bool major=Math.Abs(t/(score.BeatSeconds*score.BeatsPerBar)-Math.Round(t/(score.BeatSeconds*score.BeatsPerBar)))<.001;
                EditorGUI.DrawRect(new Rect(x,major?14:19,1,major?11:6),new Color(.45f,.48f,.50f));
                GUI.Label(new Rect(x+4,0,72,18),bars?Musical(t,score):t.ToString("0.##"),EditorStyles.miniLabel);
            }
            float entryX=(float)score.Entry*doc.Zoom-doc.Scroll.x,exitX=(float)score.Exit*doc.Zoom-doc.Scroll.x;
            GUI.Label(new Rect(entryX,48,100,20),"▼ Entry",EditorStyles.miniLabel);
            GUI.Label(new Rect(exitX-72,48,72,20),"Exit ▼",EditorStyles.miniLabel);
            foreach(var cue in score.Markers)GUI.Label(new Rect((float)cue.Seconds*doc.Zoom-doc.Scroll.x,48,120,20),new GUIContent("◆ "+cue.Name,cue.Seconds+" s"),EditorStyles.miniLabel);
            if(input.type==EventType.MouseDown&&input.button==0&&input.mousePosition.y>=0&&input.mousePosition.y<26&&input.mousePosition.x>=0&&input.mousePosition.x<body.width)
            {
                doc.Cursor=Math.Max(0,Math.Min(end,(input.mousePosition.x+doc.Scroll.x)/doc.Zoom));
                if(!legacy&&arrangementPlayer!=null&&arrangementPlayer.IsPlaying)arrangementPlayer.Seek(doc.Cursor);
                canvas.Focus();input.Use();
            }
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(viewport.x,viewport.y,header,ruler));
            EditorGUI.DrawRect(new Rect(0,0,header,ruler),new Color(.16f,.17f,.18f));
            GUI.Label(new Rect(12,4,header-20,20),bars?"Bars / beats":"Time · seconds",EditorStyles.boldLabel);
            GUI.Label(new Rect(12,30,header-20,20),"Logic / markers",EditorStyles.miniLabel);
            GUI.EndGroup();
            GUI.BeginGroup(new Rect(viewport.x,body.y,header,body.height));
            EditorGUI.DrawRect(new Rect(0,0,header,body.height),new Color(.16f,.17f,.18f));
            for(int i=0;i<score.Lanes.Count;i++)
            {
                var lane=score.Lanes[i];float y=i*rowHeight-doc.Scroll.y;
                bool selected=selectedLane==(legacy?"legacy:"+i:lane.Id);
                EditorGUI.DrawRect(new Rect(0,y,header-1,rowHeight-1),selected?new Color(.22f,.29f,.21f):new Color(.17f,.18f,.19f));
                if(selected)EditorGUI.DrawRect(new Rect(0,y,3,rowHeight-1),new Color(.66f,.81f,.46f));
                if(GUI.Button(new Rect(10,y+3,header-108,22),new GUIContent(lane.Name,lane.Name),EditorStyles.boldLabel)){selectedLane=legacy?"legacy:"+i:lane.Id;doc.Selection="lane:"+lane.Id;selectedClips.Clear();ShowDeck(CurrentAsset);}
                using(new EditorGUI.DisabledScope(legacy))
                {
                    bool mute=GUI.Toggle(new Rect(header-100,y+6,22,20),mutedLanes.Contains(lane.Id),"M",EditorStyles.miniButton);
                    bool solo=GUI.Toggle(new Rect(header-75,y+6,22,20),soloLanes.Contains(lane.Id),"S",EditorStyles.miniButton);
                    if(mute)mutedLanes.Add(lane.Id);else mutedLanes.Remove(lane.Id);if(solo)soloLanes.Add(lane.Id);else soloLanes.Remove(lane.Id);
                    if(arrangementPlayer!=null)arrangementPlayer.SetLaneAudition(lane.Id,mute,solo);
                }
                GUI.Label(new Rect(header-49,y+6,48,20),AudioGain.ToDecibels(lane.Gain).ToString("0.#")+" dB",EditorStyles.miniLabel);
                GUI.Label(new Rect(10,y+26,header-20,18),new GUIContent(lane.Output!=null?lane.Output.name:"Event output",lane.Parameter!=null?lane.Parameter.name:"No parameter binding"),EditorStyles.miniLabel);
            }
            GUI.EndGroup();
            GUI.BeginGroup(body);
            EditorGUI.DrawRect(new Rect(0,0,body.width,body.height),new Color(.11f,.12f,.13f));
            for(int i=0;i<score.Lanes.Count;i++)
            {
                var lane=score.Lanes[i];float y=i*rowHeight-doc.Scroll.y;
                if((y+rowHeight<0||y>body.height)&&!lane.Clips.Any(c=>c.Id==dragClip))continue;
                EditorGUI.DrawRect(new Rect(0,y,body.width,rowHeight-1),i%2==0?new Color(.14f,.15f,.16f):new Color(.125f,.135f,.145f));
                for(double t=first;t<=last;t+=grid){bool bar=Math.Abs(t/(score.BeatSeconds*score.BeatsPerBar)-Math.Round(t/(score.BeatSeconds*score.BeatsPerBar)))<.001;EditorGUI.DrawRect(new Rect((float)t*doc.Zoom-doc.Scroll.x,y,1,rowHeight),bar?new Color(.32f,.35f,.37f):new Color(.21f,.23f,.25f));}
                foreach(var clip in lane.Clips)
                {
                    double start=clip.Start,duration=clip.Duration;
                    if(dragKind==1&&selectedClips.Contains(clip.Id))start+=dragDelta;
                    if(clip.Id==dragClip&&dragKind==2){start+=dragDelta;duration-=dragDelta;}
                    if(clip.Id==dragClip&&dragKind==3)duration+=dragDelta;
                    Rect rect=new Rect((float)start*doc.Zoom-doc.Scroll.x,y+3,Mathf.Max(4,(float)duration*doc.Zoom),rowHeight-8);
                    if((rect.xMax<0||rect.x>body.width)&&dragClip!=clip.Id)continue;
                    EditorGUI.DrawRect(rect,selectedClips.Contains(clip.Id)?new Color(.27f,.34f,.21f):new Color(.13f,.21f,.23f));
                    EditorGUI.DrawRect(new Rect(rect.x,rect.y,rect.width,19),selectedClips.Contains(clip.Id)?new Color(.37f,.46f,.28f):new Color(.23f,.35f,.37f));
                    DrawClipWaveform(clip,rect,duration,0,body.width);
                    Color edge=selectedClips.Contains(clip.Id)?new Color(.66f,.81f,.46f):new Color(.32f,.45f,.46f);
                    EditorGUI.DrawRect(new Rect(rect.x,rect.y,rect.width,1),edge);
                    EditorGUI.DrawRect(new Rect(rect.x,rect.yMax-1,rect.width,1),edge);
                    EditorGUI.DrawRect(new Rect(rect.x,rect.y,1,rect.height),edge);
                    EditorGUI.DrawRect(new Rect(rect.xMax-1,rect.y,1,rect.height),edge);
                    GUI.Label(new Rect(rect.x+6,rect.y,Mathf.Max(0,rect.width-12),22),new GUIContent(clip.Source!=null?clip.Source.name:"Missing",clip.Id),EditorStyles.miniLabel);
                    if(!legacy)
                    {
                        EditorGUIUtility.AddCursorRect(new Rect(rect.x,rect.y,6,rect.height),MouseCursor.ResizeHorizontal);
                        EditorGUIUtility.AddCursorRect(new Rect(rect.xMax-6,rect.y,6,rect.height),MouseCursor.ResizeHorizontal);
                        double fadeIn=clip.FadeIn,fadeOut=clip.FadeOut;
                        if(dragClip==clip.Id&&dragKind==4)fadeIn=dragFade+dragDelta;
                        if(dragClip==clip.Id&&dragKind==5)fadeOut=dragFade-dragDelta;
                        Handles.BeginGUI();Handles.color=new Color(.85f,.9f,.78f,.9f);
                        if(fadeIn>0)Handles.DrawLine(new Vector3(rect.x,rect.yMax),new Vector3(rect.x+(float)fadeIn*doc.Zoom,rect.y+20));
                        if(fadeOut>0)Handles.DrawLine(new Vector3(rect.xMax-(float)fadeOut*doc.Zoom,rect.y+20),new Vector3(rect.xMax,rect.yMax));
                        Handles.EndGUI();
                        if(selectedClips.Contains(clip.Id))
                        {
                            Rect leftFade=new Rect(rect.x+(float)fadeIn*doc.Zoom-4,rect.y+17,8,8),rightFade=new Rect(rect.xMax-(float)fadeOut*doc.Zoom-4,rect.y+17,8,8);
                            EditorGUI.DrawRect(leftFade,new Color(.8f,.87f,.71f));EditorGUI.DrawRect(rightFade,new Color(.8f,.87f,.71f));
                            EditorGUIUtility.AddCursorRect(leftFade,MouseCursor.SlideArrow);EditorGUIUtility.AddCursorRect(rightFade,MouseCursor.SlideArrow);
                        }
                    }
                    if(!legacy)HandleClipDrag(score,lane,clip,rect,doc.Zoom);
                    else if(Event.current.type==EventType.MouseDown&&rect.Contains(Event.current.mousePosition)){selectedLane="legacy:"+i;ShowDeck(CurrentAsset);Event.current.Use();}
                }
            }
            if(!legacy)
            {
                GUI.Label(new Rect(12,score.Lanes.Count*rowHeight-doc.Scroll.y+12,body.width-24,24),"+ Drop audio to create tracks",EditorStyles.miniLabel);
                for(int i=0;i<=score.Lanes.Count;i++)TimelineDrop(score,i<score.Lanes.Count?score.Lanes[i].Id:"",new Rect(0,i*rowHeight-doc.Scroll.y,body.width,rowHeight),-doc.Scroll.x,doc.Zoom);
            }
            GUI.EndGroup();
            double cursor=arrangementPlayer!=null&&arrangementPlayer.IsPlaying?arrangementPlayer.Position:doc.Cursor;
            float cursorX=body.x+(float)cursor*doc.Zoom-doc.Scroll.x;
            if(cursorX>=body.x&&cursorX<=body.xMax)
            {
                EditorGUI.DrawRect(new Rect(cursorX,viewport.y+18,1,body.yMax-viewport.y-18),new Color(.66f,.81f,.46f));
                GUI.Label(new Rect(cursorX-6,viewport.y,18,22),"▼",EditorStyles.miniLabel);
            }
            if(!legacy&&Event.current.type==EventType.KeyDown&&!EditorGUIUtility.editingTextField)
            {
                if((Event.current.control||Event.current.command)&&Event.current.keyCode==KeyCode.D){ArrangementAuthoring.Duplicate(score,selectedClips);Event.current.Use();}
                else if(Event.current.keyCode==KeyCode.Delete||Event.current.keyCode==KeyCode.Backspace){ArrangementAuthoring.Delete(score,selectedClips);selectedClips.Clear();Event.current.Use();}
                else if(Event.current.keyCode==KeyCode.Escape){CancelDrag();Event.current.Use();}
            }
        }
        private void DrawClipWaveform(ArrangementClip clip,Rect rect,double duration,float scroll,float width)
        {
            if(clip.Source==null)return;
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
                if(b>a)WaveformCache.Draw(clip.Source,new Rect(rect.x+(float)(a*px),rect.y+20,(float)((b-a)*px),rect.height-22),clip.Trim+a-begin,b-a,Mathf.Pow(2,waveformZoom));
                if(!clip.Repeat)break;
            }
        }
        private static string Musical(double seconds,MusicArrangement score){double beat=seconds/score.BeatSeconds;return (1+(int)(beat/score.BeatsPerBar))+":"+(1+(int)(beat%score.BeatsPerBar));}
        private static void Cue(string name,double time,float header,float zoom,bool end=false){GUI.Label(new Rect(header+(float)time*zoom-(end?100:0),25,100,32),new GUIContent("▼ "+name,name+" · "+time.ToString("0.###")+" s"),EditorStyles.miniLabel);}
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
                canvas.Focus();GUIUtility.hotControl=GUIUtility.GetControlID(FocusType.Passive);Rect fadeInHandle=new Rect(rect.x+(float)clip.FadeIn*zoom-6,rect.y+14,12,14),fadeOutHandle=new Rect(rect.xMax-(float)clip.FadeOut*zoom-6,rect.y+14,12,14);
                dragKind=fadeInHandle.Contains(e.mousePosition)?4:fadeOutHandle.Contains(e.mousePosition)?5:e.mousePosition.x-rect.x<6?2:rect.xMax-e.mousePosition.x<6?3:1;dragFade=dragKind==4?clip.FadeIn:clip.FadeOut;dragClip=clip.Id;dragOrigin=e.mousePosition.x;dragDelta=0;dragStart=clip.Start;dragTrim=clip.Trim;dragDuration=clip.Duration;e.Use();
            }
            if(dragClip!=clip.Id||dragKind==0)return;
            if(e.type==EventType.MouseDrag){dragDelta=(float)Snap((e.mousePosition.x-dragOrigin)/zoom,score);if(dragKind==1)dragDelta=Mathf.Max(-(float)score.Lanes.SelectMany(l=>l.Clips).Where(c=>selectedClips.Contains(c.Id)).Min(c=>c.Start),dragDelta);if(dragKind==2)dragDelta=Mathf.Clamp(dragDelta,-(float)Math.Min(dragStart,dragTrim),(float)dragDuration-.001f);if(dragKind==3)dragDelta=Mathf.Max(-(float)dragDuration+.001f,Mathf.Min(dragDelta,clip.Repeat?float.MaxValue:(float)(MusicTrack.GetExactDuration(clip.Source)-clip.Trim-clip.Duration)));if(dragKind==4)dragDelta=Mathf.Clamp((e.mousePosition.x-dragOrigin)/zoom,-(float)dragFade,(float)(clip.Duration-clip.FadeOut-dragFade));if(dragKind==5)dragDelta=Mathf.Clamp((e.mousePosition.x-dragOrigin)/zoom,(float)(dragFade-clip.Duration+clip.FadeIn),(float)dragFade);e.Use();}
            if(e.type==EventType.MouseUp)
            {
                if(Mathf.Abs(dragDelta)<.00001f){CancelDrag();e.Use();return;}
                if(dragKind==1)ArrangementAuthoring.Move(score,selectedClips,dragDelta);
                else if(dragKind==4||dragKind==5)ArrangementAuthoring.SetFades(score,clip.Id,dragKind==4?dragFade+dragDelta:clip.FadeIn,dragKind==5?dragFade-dragDelta:clip.FadeOut);
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
