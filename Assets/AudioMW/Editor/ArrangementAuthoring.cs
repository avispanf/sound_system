using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public static class ArrangementAuthoring
    {
        public static MusicArrangement FromLegacy(MusicTrack track)
        {
            var result = ScriptableObject.CreateInstance<MusicArrangement>();
            result.name = track.name + " Arrangement"; result.Tempo = track.Tempo; result.BeatsPerBar = track.BeatsPerBar;
            result.Output = track.MixerGroup; result.Gain = track.Volume;
            result.Exit = track.IntroDuration + track.BodyDuration; result.Loop = track.Loop && track.BodyDuration > 0;
            result.LoopStart = track.IntroDuration; result.LoopEnd = result.Exit;
            AddLegacy(result, "Intro", track.IntroClip, 0, null);
            AddLegacy(result, "Body", track.LoopClip, track.IntroDuration, null);
            for (int i = 0; i < track.Layers.Length; i++)
            {
                var layer = track.Layers[i]; if (layer == null || layer.Clip == null) continue;
                var lane = AddLegacy(result, layer.Name, layer.Clip, track.IntroDuration, layer.Parameter);
                using (var serialized = new SerializedObject(track))
                {
                    var curve = serialized.FindProperty("layers").GetArrayElementAtIndex(i).FindPropertyRelative("weightCurve").animationCurveValue;
                    if(curve!=null&&curve.length>0)lane.Weight = new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
                    if(layer.Parameter==null||curve==null||curve.length==0){lane.Parameter=null;lane.Gain=layer.EvaluateWeight(0);}
                }
            }
            foreach (var marker in track.Markers) if (marker != null) result.Markers.Add(new ArrangementMarker { Name = marker.Name, Seconds = marker.PositionInBeats(track.BeatsPerBar) * 60 / track.Tempo });
            return result;
        }
        private static ArrangementLane AddLegacy(MusicArrangement result, string name, AudioClip source, double start, SoundParameter parameter)
        {
            if (source == null) return null;
            var lane = new ArrangementLane { Name = name, Parameter = parameter };
            lane.Clips.Add(new ArrangementClip { Source = source, Start = start, Duration = MusicTrack.GetExactDuration(source) }); result.Lanes.Add(lane); return lane;
        }
        public static ArrangementClip AddClip(MusicArrangement score, string laneId, AudioClip source, double start)
        {
            Undo.RecordObject(score, "Add timeline clip");
            var lane = score.Lanes.Find(x => x.Id == laneId);
            if (lane == null) { lane = new ArrangementLane { Name = source.name }; score.Lanes.Add(lane); }
            var clip = new ArrangementClip { Source = source, Start = Math.Max(0,start), Duration = MusicTrack.GetExactDuration(source) };
            lane.Clips.Add(clip); EditorUtility.SetDirty(score); return clip;
        }
        public static void Move(MusicArrangement score, ICollection<string> ids, double delta)
        {
            double minimum = double.PositiveInfinity;
            foreach (var lane in score.Lanes) foreach (var clip in lane.Clips) if (ids.Contains(clip.Id)) minimum = Math.Min(minimum,clip.Start);
            delta = Math.Max(-minimum,delta); Undo.RecordObject(score,"Move timeline clips");
            foreach (var lane in score.Lanes) foreach (var clip in lane.Clips) if (ids.Contains(clip.Id)) clip.Start += delta;
            EditorUtility.SetDirty(score);
        }
        public static void Duplicate(MusicArrangement score, ICollection<string> ids)
        {
            Undo.RecordObject(score,"Duplicate timeline clips");
            foreach (var lane in score.Lanes)
            {
                var copies = new List<ArrangementClip>();
                foreach (var clip in lane.Clips) if (ids.Contains(clip.Id))
                {
                    var copy = JsonUtility.FromJson<ArrangementClip>(JsonUtility.ToJson(clip)); copy.Id = Guid.NewGuid().ToString("N"); copy.Start += clip.Duration; copies.Add(copy);
                }
                lane.Clips.AddRange(copies);
            }
            EditorUtility.SetDirty(score);
        }
        public static void SetFades(MusicArrangement score,string id,double fadeIn,double fadeOut)
        {
            if(double.IsNaN(fadeIn)||double.IsInfinity(fadeIn)||double.IsNaN(fadeOut)||double.IsInfinity(fadeOut))return;
            foreach(var lane in score.Lanes)foreach(var clip in lane.Clips)if(clip.Id==id)
            {
                Undo.RecordObject(score,"Change clip fades");clip.FadeIn=Math.Max(0,Math.Min(clip.Duration,fadeIn));clip.FadeOut=Math.Max(0,Math.Min(clip.Duration-clip.FadeIn,fadeOut));EditorUtility.SetDirty(score);return;
            }
        }
        public static void Delete(MusicArrangement score, ICollection<string> ids)
        {
            Undo.RecordObject(score,"Delete timeline clips"); foreach(var lane in score.Lanes) lane.Clips.RemoveAll(c=>ids.Contains(c.Id)); EditorUtility.SetDirty(score);
        }
    }
}
