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
        private void DrawDeck()
        {
            if(deckTarget==null||deckSerialized==null){EditorGUILayout.LabelField("Select an event, lane, clip or binding.");return;}
            deckSerialized.Update();
            if(deckTarget is AudioClip source)
            {
                EditorGUILayout.LabelField(source.name,EditorStyles.boldLabel);EditorGUILayout.LabelField(AudioSourcePreviewPane.SourceFormat(source)+" · "+source.frequency+" Hz");
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Play source (dry)")){StopAudition();EditorAudioPreview.Play(source);}if(GUILayout.Button("Stop source"))EditorAudioPreview.Stop();if(GUILayout.Button("Select import settings"))Selection.activeObject=source;EditorGUILayout.EndHorizontal();
                var texture=WaveformCache.Get(source);Rect r=GUILayoutUtility.GetRect(100,70,GUILayout.ExpandWidth(true));if(texture!=null)GUI.DrawTexture(r,texture,ScaleMode.StretchToFill);else GUI.Label(r,"Waveform unavailable: PCM data not readable.");return;
            }
            if(deckTarget is MusicArrangement score){DrawArrangementDeck(score);deckSerialized.ApplyModifiedProperties();return;}
            if(deckTarget is SoundParameter parameter){DrawLiveParameter(parameter);}
            if(deckTarget is SoundEvent sound)
            {
                EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                foreach(string section in new[]{"Properties","Sources","Variation","Spatial","Bindings","Limits"})if(GUILayout.Toggle(deckSection==section,section,EditorStyles.toolbarButton))deckSection=section;
                EditorGUILayout.EndHorizontal();
                string[] fields=deckSection=="Properties"?new[]{"containerMode","selectionMode","volume","pitch","loop","mixerGroup","ignorePause"}:deckSection=="Variation"?new[]{"volumeVariationMode","volumeRandomMin","volumeRandomMax","volumeRandomDbMin","volumeRandomDbMax","pitchRandomMin","pitchRandomMax"}:deckSection=="Sources"?new[]{"clips","clipReferences","clipWeights","children","blendLayers"}:deckSection=="Spatial"?new[]{"attenuationPreset","spatialBlend","minDistance","maxDistance","rolloffMode","priority","allowVirtualization"}:deckSection=="Bindings"?new[]{"blendParameter","switchParameter","parameterBindings"}:new[]{"maxInstances","cooldownSeconds","maxInstancesPerEmitter","limitPolicy","concurrencyGroup"};
                DrawFields(fields);
                EditorGUILayout.BeginHorizontal();membership=(SoundBank)EditorGUILayout.ObjectField("Bank",membership,typeof(SoundBank),false);
                using(new EditorGUI.DisabledScope(membership==null))if(GUILayout.Button("Assign event",GUILayout.Width(100))){Undo.RecordObject(membership,"Assign event to bank");if(!membership.Events.Contains(sound))membership.Events=membership.Events.Concat(new[]{sound}).ToArray();EditorUtility.SetDirty(membership);RefreshOverview();}EditorGUILayout.EndHorizontal();
                if(deckSection=="Spatial"&&sound.AttenuationPreset!=null&&GUILayout.Button("Edit attenuation preset here"))EditorApplication.delayCall+=()=>{if(this!=null&&sound!=null)ShowDeck(sound.AttenuationPreset);};
                if(deckSection=="Sources")EditorGUILayout.LabelField("Selection history is shared per event. Sequential advances once per trigger.",EditorStyles.wordWrappedMiniLabel);
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
            if(deckSerialized!=null&&deckSerialized.targetObject==deckTarget)deckSerialized.ApplyModifiedProperties();
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
