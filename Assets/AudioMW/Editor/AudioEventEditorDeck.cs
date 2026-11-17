using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow
    {
        private string deckSection="Properties";
        private string musicDeckSection="Properties";
        private SoundBank membership;
        private int deckSourceIndex=-1,deckChildIndex=-1;
        private SoundEvent deckChildParent;
        private void DrawDeck()
        {
            if(deckTarget==null||deckSerialized==null){EditorGUILayout.LabelField("Select an event, lane, clip or binding.");return;}
            deckSerialized.Update();
            if(deckTarget is AudioClip source)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                GUILayout.Label(AudioSourcePreviewPane.SourceFormat(source)+" · "+source.frequency+" Hz",EditorStyles.miniLabel);GUILayout.FlexibleSpace();DrawWaveformZoom();
                if(GUILayout.Button("Play source (dry)",EditorStyles.toolbarButton,GUILayout.Width(126))){StopAudition();notice="";EditorAudioPreview.Play(source);}
                if(GUILayout.Button("Stop",EditorStyles.toolbarButton,GUILayout.Width(46)))EditorAudioPreview.Stop();
                if(GUILayout.Button("Import settings",EditorStyles.toolbarButton,GUILayout.Width(108)))Selection.activeObject=source;
                EditorGUILayout.EndHorizontal();GUILayout.Space(8);
                Rect r=GUILayoutUtility.GetRect(100,84,GUILayout.ExpandWidth(true));DrawSourceWaveform(source,r);
                EditorGUILayout.LabelField("Click waveform to audition from here · source channels are shown separately",EditorStyles.miniLabel);return;
            }
            if(deckTarget is MusicArrangement score){DrawArrangementDeck(score);if(deckSerialized.ApplyModifiedProperties())TrackEdit(deckTarget);return;}
            if(deckTarget is SoundParameter parameter){DrawLiveParameter(parameter);}
            if(deckTarget is SoundEvent sound)
            {
                DrawSoundDeck(sound);
            }
            else if(deckTarget is MusicTrack music)DrawLegacyMusicDeck(music);
            else
            {
                var iterator=deckSerialized.GetIterator();bool children=true;while(iterator.NextVisible(children)){children=false;if(iterator.name=="m_Script")continue;EditorGUILayout.PropertyField(iterator,true);}
            }
            if(deckSerialized!=null&&deckSerialized.targetObject==deckTarget&&deckSerialized.ApplyModifiedProperties())TrackEdit(deckTarget);
        }
        private void DeckField(string name,string title=null)
        {
            var property=deckSerialized.FindProperty(name);if(property==null)return;
            var label=new GUIContent(title??ObjectNames.NicifyVariableName(name));
            if(property.isArray||property.propertyType==SerializedPropertyType.Generic)EditorGUILayout.PropertyField(property,label,true);
            else EditorGUI.PropertyField(EditorGUILayout.GetControlRect(false,24),property,label,true);
        }
        private void DrawSoundDeck(SoundEvent sound)
        {
            if(deckChildParent!=null&&deckChildIndex>=0&&deckChildIndex<deckChildParent.Children.Length)
            {
                using(var parent=new SerializedObject(deckChildParent))
                {
                    var edge=parent.FindProperty("children").GetArrayElementAtIndex(deckChildIndex);
                    EditorGUILayout.BeginHorizontal();GUILayout.Label("Child of "+deckChildParent.name,EditorStyles.miniLabel,GUILayout.Width(180));
                    float previous=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=70;
                    EditorGUILayout.PropertyField(edge.FindPropertyRelative("gain"),new GUIContent("Edge gain"));
                    if(deckChildParent.ContainerMode==ContainerMode.Switch)EditorGUILayout.PropertyField(edge.FindPropertyRelative("state"),new GUIContent("State"));
                    EditorGUIUtility.labelWidth=previous;EditorGUILayout.EndHorizontal();if(parent.ApplyModifiedProperties())TrackEdit(deckChildParent);
                }
            }
            if(deckSourceIndex>=0&&deckSourceIndex<sound.TotalClipCount&&sound.ContainerMode==ContainerMode.Simple)
            {
                EditorGUILayout.BeginHorizontal();GUILayout.Label("Source "+(deckSourceIndex+1),EditorStyles.boldLabel);if(GUILayout.Button("Container properties",GUILayout.Width(160)))deckSourceIndex=-1;EditorGUILayout.EndHorizontal();
                if(deckSourceIndex<0)return;
                var source=deckSourceIndex<sound.Clips.Length?deckSerialized.FindProperty("clips").GetArrayElementAtIndex(deckSourceIndex):deckSerialized.FindProperty("clipReferences").GetArrayElementAtIndex(deckSourceIndex-sound.Clips.Length);
                EditorGUILayout.PropertyField(source,new GUIContent("Source reference"),true);
                EditorGUILayout.LabelField("Selection weight",SourceWeight(sound,deckSourceIndex).ToString("0.###")+" · edit in source table");
                EditorGUILayout.LabelField(sound.SelectionMode==ClipSelectionMode.WeightedRandom?"Relative probability. Missing weights use 1; an all-zero set falls back to uniform selection.":"Weight is inactive for "+AudioEventContextPane.SelectionLabel(sound.SelectionMode)+".",EditorStyles.wordWrappedMiniLabel);
                return;
            }
            float oldLabel=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=100;
            EditorGUILayout.BeginHorizontal();EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(deckScroll.contentRect.width*.29f,220,300)));
            if(sound.ContainerMode==ContainerMode.Simple)DeckField("selectionMode","Selection");else DeckField("containerMode","Container");
            EditorGUILayout.LabelField("History","Shared per event",EditorStyles.miniLabel);
            DeckField("volume","Gain");DeckField("pitch","Pitch ratio");DeckField("maxInstances","Max voices");DeckField("limitPolicy","On limit");
            EditorGUILayout.EndVertical();GUILayout.Space(18);EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach(string section in new[]{"Properties","Sources","Variation","Spatial","Bindings","Limits"})if(GUILayout.Toggle(deckSection==section,section,EditorStyles.toolbarButton))deckSection=section;
            EditorGUILayout.EndHorizontal();GUILayout.Space(6);
            if(deckSection=="Properties")
            {
                DeckField("containerMode","Container");DeckField("mixerGroup","Output");
                EditorGUILayout.BeginHorizontal();DeckField("loop","Loop");DeckField("ignorePause","Ignore pause");EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();membership=(SoundBank)EditorGUILayout.ObjectField("Add to bank",membership,typeof(SoundBank),false);
                using(new EditorGUI.DisabledScope(membership==null))if(GUILayout.Button("Assign",GUILayout.Width(65))){Undo.RecordObject(membership,"Assign event to bank");if(!membership.Events.Contains(sound))membership.Events=membership.Events.Concat(new[]{sound}).ToArray();EditorUtility.SetDirty(membership);TrackEdit(membership);RefreshOverview();}EditorGUILayout.EndHorizontal();
            }
            else if(deckSection=="Sources")
            {
                if(sound.ContainerMode==ContainerMode.Simple){DeckField("clips","Direct sources");DeckField("clipReferences","Addressed sources");EditorGUILayout.LabelField("Edit weights in the source table. Order: direct, then addressed.",EditorStyles.wordWrappedMiniLabel);}
                else if(sound.ContainerMode==ContainerMode.Blend)DeckField("blendLayers","Blend layers");
                else DeckField("children","Child events");
            }
            else if(deckSection=="Spatial")
            {
                DeckField("attenuationPreset","Preset");
                if(sound.AttenuationPreset!=null)
                {
                    EditorGUILayout.LabelField("Preset owns spatial settings",EditorStyles.miniLabel);
                    if(GUILayout.Button("Edit shared preset in Deck"))EditorApplication.delayCall+=()=>{if(this!=null&&sound!=null)ShowDeck(sound.AttenuationPreset);};
                }
                else {DeckField("spatialBlend","2D / 3D");EditorGUILayout.BeginHorizontal();DeckField("minDistance","Min (m)");DeckField("maxDistance","Max (m)");EditorGUILayout.EndHorizontal();DeckField("rolloffMode","Rolloff");}
            }
            else if(deckSection=="Variation")
            {
                DeckField("volumeVariationMode","Gain mode");
                if(sound.VolumeVariationMode==VolumeVariationMode.Decibels){DeckField("volumeRandomDbMin","Min (dB)");DeckField("volumeRandomDbMax","Max (dB)");}
                else {DeckField("volumeRandomMin","Min offset");DeckField("volumeRandomMax","Max offset");}
                EditorGUILayout.BeginHorizontal();DeckField("pitchRandomMin","Pitch min");DeckField("pitchRandomMax","Pitch max");EditorGUILayout.EndHorizontal();
            }
            else if(deckSection=="Bindings")
            {
                if(sound.ContainerMode==ContainerMode.Blend)DeckField("blendParameter","Blend");
                if(sound.ContainerMode==ContainerMode.Switch)DeckField("switchParameter","Switch");
                DeckField("parameterBindings","Bindings");
            }
            else {DeckField("maxInstancesPerEmitter","Per emitter");DeckField("cooldownSeconds","Cooldown (s)");DeckField("concurrencyGroup","Group");DeckField("priority","Priority");DeckField("allowVirtualization","Virtualization");}
            EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();EditorGUIUtility.labelWidth=oldLabel;
        }
        private void DrawFields(string[] names)
        {
            EditorGUILayout.BeginHorizontal();int middle=(names.Length+1)/2;float previousLabel=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=126;
            for(int col=0;col<2;col++){EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp((deckScroll.contentRect.width-36)/2,220,390)));for(int i=col*middle;i<Math.Min(names.Length,(col+1)*middle);i++){var property=deckSerialized.FindProperty(names[i]);if(property!=null)EditorGUILayout.PropertyField(property,true);}EditorGUILayout.EndVertical();GUILayout.Space(16);}GUILayout.FlexibleSpace();EditorGUILayout.EndHorizontal();EditorGUIUtility.labelWidth=previousLabel;
        }
        private void MusicDeckTabs()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            foreach(string section in new[]{"Properties","Modulation","Routing"})
                if(GUILayout.Toggle(musicDeckSection==section,section,EditorStyles.toolbarButton,GUILayout.Width(100)))musicDeckSection=section;
            GUILayout.FlexibleSpace();EditorGUILayout.EndHorizontal();GUILayout.Space(6);
        }
        private void PropertyRow(SerializedProperty property,string label,float height=22)
        {
            if(property==null)return;
            var rect=EditorGUILayout.GetControlRect(false,height);
            if((property.propertyType==SerializedPropertyType.Float||property.propertyType==SerializedPropertyType.Integer)&&property.name!="volume"&&property.name!="Gain")rect.width=Mathf.Min(rect.width,260);
            EditorGUI.PropertyField(rect,property,new GUIContent(label),true);
        }
        private void DrawWeightPanel(SerializedProperty parameter,SerializedProperty curve)
        {
            PropertyRow(parameter,"Parameter");
            if(parameter.objectReferenceValue==null)
            {
                EditorGUILayout.HelpBox("No parameter binding. The authored constant weight is used.",MessageType.None);return;
            }
            EditorGUILayout.LabelField("Layer weight · normalized 0–1",EditorStyles.miniLabel);
            EditorGUI.BeginChangeCheck();
            var edited=EditorGUILayout.CurveField(GUIContent.none,curve.animationCurveValue,new Color(.66f,.81f,.46f),new Rect(0,0,1,1),GUILayout.Height(position.height<850?66:104));
            if(EditorGUI.EndChangeCheck())curve.animationCurveValue=edited;
            EditorGUILayout.BeginHorizontal();GUILayout.Label("0 · Parameter min",EditorStyles.miniLabel);GUILayout.FlexibleSpace();GUILayout.Label("Parameter max · 1",EditorStyles.miniLabel);EditorGUILayout.EndHorizontal();
        }
        public static int LegacyLayerIndex(MusicTrack track,int laneIndex)
        {
            int slot=(track.IntroClip!=null?1:0)+(track.LoopClip!=null?1:0);
            for(int i=0;i<track.Layers.Length;i++)
            {
                if(track.Layers[i]==null||!track.Layers[i].IsValid)continue;
                if(slot++==laneIndex)return i;
            }
            return -1;
        }
        private void DrawLegacyMusicDeck(MusicTrack music)
        {
            int laneIndex=-1;if(selectedLane.StartsWith("legacy:"))int.TryParse(selectedLane.Substring(7),out laneIndex);
            int layerIndex=LegacyLayerIndex(music,laneIndex);
            SerializedProperty layer=layerIndex>=0?deckSerialized.FindProperty("layers").GetArrayElementAtIndex(layerIndex):null;
            MusicDeckTabs();float previous=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=100;
            EditorGUILayout.BeginHorizontal();EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(deckScroll.contentRect.width*.34f,270,370)));
            if(layer!=null)
            {
                PropertyRow(layer.FindPropertyRelative("layerName"),"Track name");PropertyRow(layer.FindPropertyRelative("clip"),"Source");
                using(new EditorGUI.DisabledScope(layer.FindPropertyRelative("parameter").objectReferenceValue!=null))PropertyRow(layer.FindPropertyRelative("defaultWeight"),"Fallback weight");PropertyRow(layer.FindPropertyRelative("fadeSeconds"),"Response (s)");
            }
            else
            {
                PropertyRow(deckSerialized.FindProperty("introClip"),"Intro source");PropertyRow(deckSerialized.FindProperty("loopClip"),"Body source");
                PropertyRow(deckSerialized.FindProperty("volume"),"Event gain");PropertyRow(deckSerialized.FindProperty("loop"),"Loop body");
            }
            EditorGUILayout.EndVertical();GUILayout.Space(20);EditorGUILayout.BeginVertical();
            if(musicDeckSection=="Routing")
            {
                PropertyRow(deckSerialized.FindProperty("mixerGroup"),"Event output");
                EditorGUILayout.LabelField("All legacy layers inherit this event route.",EditorStyles.wordWrappedMiniLabel);
                if(GUILayout.Button("Open native Mixer",GUILayout.Width(160)))AudioWorkspace.Mixer();
            }
            else if(layer!=null)DrawWeightPanel(layer.FindPropertyRelative("parameter"),layer.FindPropertyRelative("weightCurve"));
            else if(musicDeckSection=="Modulation")EditorGUILayout.HelpBox("Select a music layer above to edit its parameter and weight curve.",MessageType.None);
            else
            {
                PropertyRow(deckSerialized.FindProperty("tempo"),"Tempo (BPM)");PropertyRow(deckSerialized.FindProperty("beatsPerBar"),"Beats / bar");
                EditorGUILayout.PropertyField(deckSerialized.FindProperty("markers"),new GUIContent("Music markers"),true);
                EditorGUILayout.LabelField("Legacy timing is fixed. Use an editable arrangement for trim, placement and pause.",EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();EditorGUIUtility.labelWidth=previous;
        }
        private void DrawArrangementDeck(MusicArrangement score)
        {
            MusicDeckTabs();
            var lanes=deckSerialized.FindProperty("Lanes");int laneIndex=score.Lanes.FindIndex(l=>l.Id==selectedLane);
            if(laneIndex>=0)
            {
                var lane=score.Lanes[laneIndex];var laneProperty=lanes.GetArrayElementAtIndex(laneIndex);int clipIndex=lane.Clips.FindIndex(c=>selectedClips.Contains(c.Id));
                var property=clipIndex>=0?laneProperty.FindPropertyRelative("Clips").GetArrayElementAtIndex(clipIndex):laneProperty;
                EditorGUILayout.BeginHorizontal();float previousLabel=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=100;
                EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(deckScroll.contentRect.width*.40f,270,380)));
                foreach(string field in clipIndex>=0?new[]{"Source","Start","Duration","Gain"}:new[]{"Name","Gain"})
                    PropertyRow(property.FindPropertyRelative(field),field+(field=="Start"||field=="Duration"?" (s)":""));
                if(clipIndex<0)EditorGUILayout.LabelField(lane.Clips.Count+" clips · session M/S in track header",EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();GUILayout.Space(24);EditorGUILayout.BeginVertical();
                if(musicDeckSection=="Routing")
                {
                    PropertyRow(laneProperty.FindPropertyRelative("Output"),"Track output");
                    EditorGUILayout.LabelField("Fallback: "+(score.Output!=null?score.Output.name:"Unity default output"),EditorStyles.miniLabel);
                    if(GUILayout.Button("Open native Mixer",GUILayout.Width(160)))AudioWorkspace.Mixer();
                }
                else if(clipIndex>=0&&musicDeckSection=="Properties")
                {
                    PropertyRow(property.FindPropertyRelative("Trim"),"Source trim (s)");
                    PropertyRow(property.FindPropertyRelative("Repeat"),"Repeat source");
                    PropertyRow(property.FindPropertyRelative("FadeIn"),"Fade in (s)");
                    PropertyRow(property.FindPropertyRelative("FadeOut"),"Fade out (s)");
                }
                else DrawWeightPanel(laneProperty.FindPropertyRelative("Parameter"),laneProperty.FindPropertyRelative("Weight"));
                EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();EditorGUIUtility.labelWidth=previousLabel;
                EditorGUILayout.BeginHorizontal();
                if(GUILayout.Button("Arrangement",GUILayout.Width(100))){selectedLane="";selectedClips.Clear();CurrentDocument.Selection="";}
                if(clipIndex>=0){if(GUILayout.Button("Duplicate",GUILayout.Width(100))){deckSerialized.ApplyModifiedProperties();ArrangementAuthoring.Duplicate(score,selectedClips);deckSerialized.Update();}if(GUILayout.Button("Delete clip",GUILayout.Width(100))){deckSerialized.ApplyModifiedProperties();ArrangementAuthoring.Delete(score,selectedClips);selectedClips.Clear();deckSerialized.Update();}}
                else
                {
                    using(new EditorGUI.DisabledScope(laneIndex==0))if(GUILayout.Button("Move track up")){deckSerialized.ApplyModifiedProperties();Undo.RecordObject(score,"Reorder lane");score.Lanes.RemoveAt(laneIndex);score.Lanes.Insert(laneIndex-1,lane);EditorUtility.SetDirty(score);deckSerialized.Update();}
                    using(new EditorGUI.DisabledScope(laneIndex==score.Lanes.Count-1))if(GUILayout.Button("Move track down")){deckSerialized.ApplyModifiedProperties();Undo.RecordObject(score,"Reorder lane");score.Lanes.RemoveAt(laneIndex);score.Lanes.Insert(laneIndex+1,lane);EditorUtility.SetDirty(score);deckSerialized.Update();}
                    if(GUILayout.Button("Delete track")){deckSerialized.ApplyModifiedProperties();Undo.RecordObject(score,"Delete lane");score.Lanes.Remove(lane);selectedLane="";EditorUtility.SetDirty(score);deckSerialized.Update();}
                }
                EditorGUILayout.EndHorizontal();
            }
            else if(musicDeckSection=="Routing")DrawFields(new[]{"Output","Gain"});
            else if(musicDeckSection=="Modulation")EditorGUILayout.HelpBox("Select a track to edit its parameter response.",MessageType.None);
            else DrawFields(new[]{"Tempo","BeatsPerBar","Entry","Exit","Loop","LoopStart","LoopEnd","Gain","Markers"});
            string issue=score.Validate();if(issue!="")EditorGUILayout.HelpBox(issue,MessageType.Error);
            if(breadcrumb!=null)breadcrumb.tooltip=breadcrumb.text+"\nTiming is in seconds. Tempo changes the grid, not speed. Authored arrangement edits apply on restart; pause/resume retains the playing snapshot.";
        }
    }
}
