using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public static class AudioInspectionData
    {
        public sealed class Use { public Object Asset; public string Label; public string Unit; public Func<float,float> Evaluate; }
        public sealed class Route { public Object Owner; public string Path; public AudioMixerGroup Group; }
        public static bool IsParent(SoundEvent item) { return item.ContainerMode == ContainerMode.Layered || item.ContainerMode == ContainerMode.Switch; }
        public static float DistanceModel(float distance,float min,float max,float blend,AudioRolloffMode mode,AnimationCurve curve)
        {
            min = Mathf.Max(.01f,min); max = Mathf.Max(min,max); distance = Mathf.Max(0,distance);
            float gain = mode == AudioRolloffMode.Linear ? (distance <= min ? 1 : max <= min ? 0 : 1-Mathf.InverseLerp(min,max,distance)) : mode == AudioRolloffMode.Custom && curve != null ? Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(distance/max))) : min/Mathf.Max(min,distance);
            return Mathf.Lerp(1,Mathf.Clamp01(gain),Mathf.Clamp01(blend));
        }
        private static IEnumerable<T> Assets<T>() where T : Object
        {
            foreach (string guid in AssetDatabase.FindAssets("t:"+typeof(T).Name)) { var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)); if (asset != null) yield return asset; }
        }
        public static List<Use> ParameterUses(SoundParameter parameter)
        {
            var result = new List<Use>(); if (parameter == null) return result;
            foreach (var item in Assets<SoundEvent>())
            {
                foreach (var binding in item.ParameterBindings) if (binding != null && binding.Parameter == parameter) result.Add(new Use { Asset=item,Label=item.name+" / "+binding.Target,Unit="Multiplier",Evaluate=binding.IsValid ? binding.Evaluate : null });
                if (item.ContainerMode == ContainerMode.Switch && item.SwitchParameter == parameter) result.Add(new Use { Asset=item,Label=item.name+" / Switch · rounded state at trigger" });
                if (item.ContainerMode == ContainerMode.Blend && item.BlendParameter == parameter) foreach (var layer in item.BlendLayers) if (layer != null) result.Add(new Use { Asset=item,Label=item.name+" / "+(layer.Clip != null ? layer.Clip.name : "Missing clip"),Unit="Layer weight",Evaluate=x=>layer.EvaluateWeight(parameter.Normalize(x)) });
            }
            foreach (var track in Assets<MusicTrack>()) foreach (var layer in track.Layers) if (layer != null && layer.Parameter == parameter) result.Add(new Use { Asset=track,Label=track.name+" / "+layer.Name,Unit="Layer weight",Evaluate=layer.EvaluateWeight });
            foreach (var profile in Assets<MixerRoutingProfile>()) foreach (var binding in profile.Bindings) if (binding != null && binding.Parameter == parameter) result.Add(new Use { Asset=profile,Label=profile.name+" / "+binding.ExposedName,Unit="Native mixer control units",Evaluate=binding.IsValid ? binding.Evaluate : null });
            foreach (var profile in Assets<AudioMixProfile>()) if (profile.VoiceDuck == parameter) result.Add(new Use { Asset=profile,Label=profile.name+" / VO duck ownership" });
            return result;
        }
        public static List<Route> Routes(Object asset)
        {
            var result = new List<Route>();
            if (asset is SoundEvent item) Visit(item,item.name,new HashSet<SoundEvent>(),result,0);
            else if (asset is MusicTrack track) Add(result,track,track.name,track.MixerGroup);
            else if (asset is VoiceLine voice) Add(result,voice,voice.name,voice.MixerGroup);
            return result;
        }
        private static void Visit(SoundEvent item,string path,HashSet<SoundEvent> ancestors,List<Route> result,int depth)
        {
            if (item == null) { result.Add(new Route { Path=path+" → Missing child" }); return; }
            if (depth >= 32 || !ancestors.Add(item)) { result.Add(new Route { Owner=item,Path=path+" → Invalid: cycle or depth limit" }); return; }
            if (!IsParent(item)) Add(result,item,path,item.MixerGroup);
            else if (item.Children.Length == 0) result.Add(new Route { Owner=item,Path=path+" → No child events" });
            else foreach (var child in item.Children) Visit(child != null ? child.Event : null,path+" → "+(item.ContainerMode == ContainerMode.Switch && child != null ? "[state "+child.State+"] " : "")+(child != null && child.Event != null ? child.Event.name : "missing"),ancestors,result,depth+1);
            ancestors.Remove(item);
        }
        private static void Add(List<Route> result,Object owner,string path,AudioMixerGroup group)
        {
            result.Add(new Route { Owner=owner,Group=group,Path=path+" → "+(group != null ? group.audioMixer.name+" / "+group.name : "Unity default output (no mixer group)") });
        }
    }

    public static class AudioResponseGraph
    {
        public static void Draw(float max,float marker,Func<float,float> evaluate,string xLabel,string yLabel,float min=0)
        {
            Rect area = GUILayoutUtility.GetRect(100,220,GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(area,EditorGUIUtility.isProSkin ? new Color(.10f,.12f,.13f) : new Color(.91f,.94f,.93f));
            Rect plot = new Rect(area.x+46,area.y+28,Mathf.Max(1,area.width-58),area.height-66);
            float range = Mathf.Max(.0001f,max-min), low=0,high=1; var values = new float[97];
            for(int i=0;i<values.Length;i++) { values[i]=evaluate(min+range*i/(values.Length-1)); if(float.IsNaN(values[i])||float.IsInfinity(values[i])) { GUI.Label(area,"Response unavailable: non-finite authored value",EditorStyles.wordWrappedLabel);return; } low=Mathf.Min(low,values[i]);high=Mathf.Max(high,values[i]); }
            Handles.BeginGUI(); var previous = Handles.color;
            for(int i=0;i<=4;i++) { float y=plot.y+plot.height*i/4;Handles.color=new Color(.5f,.5f,.5f,.3f);Handles.DrawLine(new Vector3(plot.x,y),new Vector3(plot.xMax,y));GUI.Label(new Rect(area.x+2,y-9,43,18),Mathf.Lerp(high,low,i/4f).ToString("0.##"),EditorStyles.miniLabel); }
            var points = new Vector3[values.Length];for(int i=0;i<values.Length;i++)points[i]=new Vector3(plot.x+plot.width*i/(values.Length-1),plot.yMax-(values[i]-low)/(high-low)*plot.height);
            Handles.color=EditorGUIUtility.isProSkin ? new Color(.44f,.79f,.72f) : new Color(.10f,.40f,.34f);Handles.DrawAAPolyLine(2,points);
            float mx=plot.x+Mathf.Clamp01((marker-min)/range)*plot.width;Handles.DrawLine(new Vector3(mx,plot.y),new Vector3(mx,plot.yMax));Handles.color=previous;Handles.EndGUI();
            GUI.Label(new Rect(area.x+8,area.y+4,area.width-16,20),yLabel,EditorStyles.miniLabel);
            GUI.Label(new Rect(plot.x,plot.yMax+3,90,18),min.ToString("0.##"),EditorStyles.miniLabel);GUI.Label(new Rect(plot.xMax-45,plot.yMax+3,45,18),max.ToString("0.##"),EditorStyles.miniLabel);
            GUI.Label(new Rect(plot.x,area.yMax-22,plot.width,20),xLabel+" · marker "+marker.ToString("0.##"),EditorStyles.miniLabel);
        }
    }
}
