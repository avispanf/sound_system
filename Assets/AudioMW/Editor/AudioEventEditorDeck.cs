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
            else if(deckTarget is MusicTrack music&&selectedLane.StartsWith("legacy:")&&int.TryParse(selectedLane.Substring(7),out int laneIndex))
            {
                if(laneIndex==0&&music.IntroClip!=null)DrawFields(new[]{"introClip","volume","mixerGroup"});
                else{EditorGUILayout.HelpBox("Legacy lane properties remain on MusicTrack. Placement uses the existing intro/body scheduling contract.",MessageType.Info);DrawFields(new[]{"loopClip","layers","markers","tempo","beatsPerBar"});}
            }
            else
            {
                var iterator=deckSerialized.GetIterator();bool children=true;while(iterator.NextVisible(children)){children=false;if(iterator.name=="m_Script")continue;EditorGUILayout.PropertyField(iterator,true);}
            }
            if(deckSerialized!=null&&deckSerialized.targetObject==deckTarget)deckSerialized.ApplyModifiedProperties();
        }
        private void DrawFields(string[] names)
        {
            EditorGUILayout.BeginHorizontal();int middle=(names.Length+1)/2;
            for(int col=0;col<2;col++){EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(220,(deckScroll.contentRect.width-12)/2)));for(int i=col*middle;i<Math.Min(names.Length,(col+1)*middle);i++){var property=deckSerialized.FindProperty(names[i]);if(property!=null)EditorGUILayout.PropertyField(property,true);}EditorGUILayout.EndVertical();}EditorGUILayout.EndHorizontal();
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
                EditorGUILayout.BeginHorizontal();for(int col=0;col<2;col++){EditorGUILayout.BeginVertical();for(int i=col;i<fields.Length;i+=2)EditorGUILayout.PropertyField(property.FindPropertyRelative(fields[i]),new GUIContent(fields[i]+(fields[i]=="Start"||fields[i]=="Trim"||fields[i]=="Duration"||fields[i].StartsWith("Fade")?" (s)":"")),true);EditorGUILayout.EndVertical();}EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                if(clipIndex>=0){if(GUILayout.Button("Duplicate")){deckSerialized.ApplyModifiedProperties();ArrangementAuthoring.Duplicate(score,selectedClips);deckSerialized.Update();}if(GUILayout.Button("Delete clip")){deckSerialized.ApplyModifiedProperties();ArrangementAuthoring.Delete(score,selectedClips);selectedClips.Clear();deckSerialized.Update();}}
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
