using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow
    {
        private string deckSection="Properties";
        private SoundBank membership;
        private int deckSourceIndex=-1,deckChildIndex=-1;
        private SoundEvent deckChildParent;
        private void DrawDeck()
        {
            if(deckTarget==null||deckSerialized==null){EditorGUILayout.LabelField("Select an event, lane, clip or binding.");return;}
            deckSerialized.Update();
            if(deckTarget is AudioClip source)
            {
                EditorGUILayout.LabelField(source.name,EditorStyles.boldLabel);EditorGUILayout.LabelField(AudioSourcePreviewPane.SourceFormat(source)+" · "+source.frequency+" Hz");
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Play source (dry)")){StopAudition();notice="";EditorAudioPreview.Play(source);}if(GUILayout.Button("Stop source"))EditorAudioPreview.Stop();if(GUILayout.Button("Select import settings"))Selection.activeObject=source;EditorGUILayout.EndHorizontal();
                Rect r=GUILayoutUtility.GetRect(100,70,GUILayout.ExpandWidth(true));if(!WaveformCache.Draw(source,r))GUI.Label(r,"Waveform unavailable: PCM data not readable.");return;
            }
            if(deckTarget is MusicArrangement score){DrawArrangementDeck(score);if(deckSerialized.ApplyModifiedProperties())TrackEdit(deckTarget);return;}
            if(deckTarget is SoundParameter parameter){DrawLiveParameter(parameter);}
            if(deckTarget is SoundEvent sound)
            {
                DrawSoundDeck(sound);
            }
            else if(deckTarget is MusicTrack music)
            {
                int laneIndex=-1;
                if(selectedLane.StartsWith("legacy:"))int.TryParse(selectedLane.Substring(7),out laneIndex);
                int layerIndex=laneIndex-1-(music.IntroClip!=null?1:0);
                if(layerIndex>=0&&layerIndex<music.Layers.Length)
                {
                    var layer=deckSerialized.FindProperty("layers").GetArrayElementAtIndex(layerIndex);
                    EditorGUILayout.LabelField(music.Layers[layerIndex].Name,EditorStyles.boldLabel);
                    EditorGUILayout.PropertyField(layer,true);
                }
                else DrawFields(new[]{"introClip","loopClip","loop","volume","mixerGroup","tempo","beatsPerBar","markers"});
            }
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
        private void DrawArrangementDeck(MusicArrangement score)
        {
            var lanes=deckSerialized.FindProperty("Lanes");int laneIndex=score.Lanes.FindIndex(l=>l.Id==selectedLane);
            if(laneIndex>=0)
            {
                var lane=score.Lanes[laneIndex];var laneProperty=lanes.GetArrayElementAtIndex(laneIndex);int clipIndex=lane.Clips.FindIndex(c=>selectedClips.Contains(c.Id));
                EditorGUILayout.BeginHorizontal();bool selectArrangement=GUILayout.Button("Arrangement",EditorStyles.miniButton,GUILayout.Width(100));EditorGUILayout.LabelField(lane.Name+(clipIndex>=0?" › "+lane.Clips[clipIndex].Source?.name:""));EditorGUILayout.EndHorizontal();
                if(selectArrangement){selectedLane="";selectedClips.Clear();CurrentDocument.Selection="";return;}
                var property=clipIndex>=0?laneProperty.FindPropertyRelative("Clips").GetArrayElementAtIndex(clipIndex):laneProperty;
                string[] fields=clipIndex>=0?new[]{"Source","Start","Trim","Duration","Repeat","FadeIn","FadeOut","Gain"}:new[]{"Name","Gain","Output","Parameter","Weight"};
                EditorGUILayout.BeginHorizontal();
                float previousLabel=EditorGUIUtility.labelWidth;EditorGUIUtility.labelWidth=100;
                EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Clamp(deckScroll.contentRect.width*.40f,270,380)));
                foreach(string field in fields)
                {
                    if(field=="Weight"||field=="Parameter")continue;
                    if(clipIndex>=0&&(field=="Trim"||field=="FadeOut"))continue;
                    bool paired=clipIndex>=0&&(field=="Start"||field=="FadeIn");
                    if(paired)EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(property.FindPropertyRelative(field),new GUIContent(field+(field=="Start"||field=="Duration"||field.StartsWith("Fade")?" (s)":"")),true);
                    if(paired){EditorGUIUtility.labelWidth=65;string other=field=="Start"?"Trim":"FadeOut";EditorGUILayout.PropertyField(property.FindPropertyRelative(other),new GUIContent(other+" (s)"));EditorGUIUtility.labelWidth=100;EditorGUILayout.EndHorizontal();}
                }
                EditorGUILayout.EndVertical();GUILayout.Space(24);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Parameter response",EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(laneProperty.FindPropertyRelative("Parameter"),new GUIContent("Binding"));
                if(lane.Parameter!=null)
                {
                    EditorGUILayout.PropertyField(laneProperty.FindPropertyRelative("Weight"),GUIContent.none,GUILayout.Height(position.height<850?64:104));
                    EditorGUILayout.LabelField("Normalized parameter 0–1  /  Layer weight 0–1",EditorStyles.miniLabel);
                }
                else EditorGUILayout.HelpBox("No parameter binding. This track uses its authored gain.",MessageType.None);
                EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();EditorGUIUtility.labelWidth=previousLabel;
                EditorGUILayout.BeginHorizontal();
                if(clipIndex>=0){if(GUILayout.Button("Duplicate",GUILayout.Width(100))){deckSerialized.ApplyModifiedProperties();ArrangementAuthoring.Duplicate(score,selectedClips);deckSerialized.Update();}if(GUILayout.Button("Delete clip",GUILayout.Width(100))){deckSerialized.ApplyModifiedProperties();ArrangementAuthoring.Delete(score,selectedClips);selectedClips.Clear();deckSerialized.Update();}}
                else
                {
                    using(new EditorGUI.DisabledScope(laneIndex==0))if(GUILayout.Button("Move track up")){deckSerialized.ApplyModifiedProperties();Undo.RecordObject(score,"Reorder lane");score.Lanes.RemoveAt(laneIndex);score.Lanes.Insert(laneIndex-1,lane);EditorUtility.SetDirty(score);deckSerialized.Update();}
                    using(new EditorGUI.DisabledScope(laneIndex==score.Lanes.Count-1))if(GUILayout.Button("Move track down")){deckSerialized.ApplyModifiedProperties();Undo.RecordObject(score,"Reorder lane");score.Lanes.RemoveAt(laneIndex);score.Lanes.Insert(laneIndex+1,lane);EditorUtility.SetDirty(score);deckSerialized.Update();}
                    if(GUILayout.Button("Delete track")){deckSerialized.ApplyModifiedProperties();Undo.RecordObject(score,"Delete lane");score.Lanes.Remove(lane);selectedLane="";EditorUtility.SetDirty(score);deckSerialized.Update();}
                }
                EditorGUILayout.EndHorizontal();
            }
            else DrawFields(new[]{"Tempo","BeatsPerBar","Entry","Exit","Loop","LoopStart","LoopEnd","Gain","Output","Markers"});
            string issue=score.Validate();if(issue!="")EditorGUILayout.HelpBox(issue,MessageType.Error);
            EditorGUILayout.LabelField("Seconds are stored. Tempo changes the grid, not clip speed. Playback takes a snapshot; authored changes apply on restart.",EditorStyles.wordWrappedMiniLabel);
        }
    }
}
