using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioSceneBindingDraft
    {
        public string ObjectId, ExpectedHash, EventGuid, Intent;
    }

    [Serializable] public sealed class AudioSceneBindingChange
    {
        public string ObjectId, ObjectPath, ScenePath, BeforeHash, Before, EventGuid, EventPath, EventHash, After;
    }

    internal static class AudioAgentScenePlanner
    {
        internal static AudioAgentAsset[] Find(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return Array.Empty<AudioAgentAsset>();
            if (query.Length > 128) throw new ArgumentException("Object search is limited to 128 characters.");
            var scene = SceneManager.GetActiveScene();
            RequireScene(scene);
            var result = new List<AudioAgentAsset>();
            string snapshot = AudioAgentPlanner.Fingerprint(scene.path);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var item in root.GetComponentsInChildren<Transform>(true))
                {
                    if (item.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var go = item.gameObject;
                    result.Add(new AudioAgentAsset { Name = go.name, Path = scene.path + " / " + Hierarchy(go),
                        Id = GlobalObjectId.GetGlobalObjectIdSlow(go).ToString(), Detail = Before(go), Hash = Hash(go,snapshot) });
                    if (result.Count == 32) return result.ToArray();
                }
            return result.ToArray();
        }

        private static void RequireScene(Scene scene)
        {
            if (PrefabStageUtility.GetCurrentPrefabStage()!=null || !scene.IsValid() || !scene.isLoaded || !scene.path.StartsWith("Assets/",StringComparison.Ordinal) || scene.isDirty)
                throw new InvalidOperationException("Open and save the target scene under Assets, outside Prefab Mode, before inspecting bindings.");
        }
        private static GameObject Resolve(string id)
        {
            var scene = SceneManager.GetActiveScene();
            RequireScene(scene);
            if (!GlobalObjectId.TryParse(id,out var key) || !(GlobalObjectId.GlobalObjectIdentifierToObjectSlow(key) is GameObject go) || go.scene != scene)
                throw new InvalidOperationException("The reviewed object is unavailable in the active scene.");
            return go;
        }
        private static string Hierarchy(GameObject go)
        {
            var names=new Stack<string>();
            for (var t=go.transform;t!=null;t=t.parent) names.Push(t.name);
            return string.Join("/",names);
        }
        private static string Before(GameObject go)
        {
            var emitters=go.GetComponents<SoundEmitter>();
            if (emitters.Length==0) return "No SoundEmitter. A new emitter will play on enable, follow this object and stop on disable.";
            if (emitters.Length!=1) return "Multiple SoundEmitters. Choose the binding manually.";
            var e=emitters[0];
            var so=new SerializedObject(e);
            return (e.Event!=null?AssetDatabase.GetAssetPath(e.Event):"Unassigned event") + " · " + (e.enabled?"enabled":"disabled") +
                " · play on enable " + so.FindProperty("playOnEnable").boolValue + " · follow " + so.FindProperty("attachToTransform").boolValue +
                " · stop on disable " + so.FindProperty("stopOnDisable").boolValue;
        }
        private static string Hash(GameObject go,string sceneSnapshot) => AudioProjectPlanner.Hash(sceneSnapshot+"|"+GlobalObjectId.GetGlobalObjectIdSlow(go)+"|"+Hierarchy(go)+"|"+Before(go));

        internal static AudioAgentPlan Prepare(AudioSceneBindingDraft draft,string session)
        {
            AudioAgentPlanner.Ready();
            if(draft==null || string.IsNullOrWhiteSpace(draft.Intent) || draft.Intent.Length>2000) throw new ArgumentException("Describe the intended scene binding.");
            var go=Resolve(draft.ObjectId);
            string hash=Hash(go,AudioAgentPlanner.Fingerprint(go.scene.path));
            if(hash!=draft.ExpectedHash)throw new InvalidOperationException("The scene or emitter changed. Inspect again.");
            var emitters=go.GetComponents<SoundEmitter>();
            if(emitters.Length>1)throw new InvalidOperationException("Ambiguous emitter on target.");
            string path=AssetDatabase.GUIDToAssetPath(draft.EventGuid);
            var sound=AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
            if(!path.StartsWith("Assets/",StringComparison.Ordinal) || sound==null)throw new ArgumentException("Choose a saved project SoundEvent.");
            AudioAgentPlanner.Clean(sound);
            if(sound.MixerGroup!=null)AudioAgentPlanner.Clean(sound.MixerGroup.audioMixer);
            if(sound.AttenuationPreset!=null)AudioAgentPlanner.Clean(sound.AttenuationPreset);
            if(emitters.Length==1 && emitters[0].Event==sound)throw new InvalidOperationException("This emitter already uses the selected event.");
            var route=sound.MixerGroup!=null?sound.MixerGroup.audioMixer.name+" / "+sound.MixerGroup.name:"Default output";
            var preset=sound.AttenuationPreset;
            string spatial=preset!=null?preset.name:("Event spatial blend "+sound.SpatialBlend.ToString("0.##")+" · "+sound.MinDistance+"–"+sound.MaxDistance+" m");
            var change=new AudioSceneBindingChange{ObjectId=draft.ObjectId,ObjectPath=Hierarchy(go),ScenePath=go.scene.path,BeforeHash=hash,Before=Before(go),
                EventGuid=draft.EventGuid,EventPath=path,EventHash=AudioAgentPlanner.Fingerprint(path),
                After=(emitters.Length==0?"Add SoundEmitter":"Replace event on existing SoundEmitter")+" · "+path+" · "+route+" · "+spatial+" · loop "+sound.Loop};
            return new AudioAgentPlan(new AudioAgentReview{Id=Guid.NewGuid().ToString("N"),Project=AudioProjectPlanner.ProjectIdentity(),Session=session,Intent=draft.Intent,
                Unity=Application.unityVersion,BuildTarget=EditorUserBuildSettings.activeBuildTarget.ToString(),HasSceneBinding=true,SceneBinding=change,
                Limitations=new[]{"Changes one scene instance. Save the scene in Unity after application. Other gameplay scripts can also start audio on this object."}});
        }

        internal static void Check(AudioSceneBindingChange change)
        {
            var go=Resolve(change.ObjectId);
            if(go.scene.path!=change.ScenePath || Hash(go,AudioAgentPlanner.Fingerprint(go.scene.path))!=change.BeforeHash ||
                AssetDatabase.GUIDToAssetPath(change.EventGuid)!=change.EventPath || AudioAgentPlanner.Fingerprint(change.EventPath)!=change.EventHash)
                throw new InvalidOperationException("The reviewed scene, emitter, sound or dependencies changed. Inspect again.");
            var sound=AssetDatabase.LoadAssetAtPath<SoundEvent>(change.EventPath);
            AudioAgentPlanner.Clean(sound);
            if(sound.AttenuationPreset!=null)AudioAgentPlanner.Clean(sound.AttenuationPreset);
            if(sound.MixerGroup!=null)AudioAgentPlanner.Clean(sound.MixerGroup.audioMixer);
        }
        internal static void Apply(AudioAgentPlan plan)
        {
            var c=plan.Read().SceneBinding;
            var go=Resolve(c.ObjectId);
            Undo.IncrementCurrentGroup();
            int group=Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bind AudioMW scene emitter");
            try
            {
                var emitter=go.GetComponent<SoundEmitter>() ?? Undo.AddComponent<SoundEmitter>(go);
                Undo.RecordObject(emitter,"Bind AudioMW scene emitter");
                emitter.Event=AssetDatabase.LoadAssetAtPath<SoundEvent>(c.EventPath);
                PrefabUtility.RecordPrefabInstancePropertyModifications(emitter);
                EditorSceneManager.MarkSceneDirty(go.scene);
                Undo.FlushUndoRecordObjects(); Undo.CollapseUndoOperations(group);
                plan.State="applied"; plan.Result="Scene emitter updated. Save the scene in Unity to persist the binding.";
            }
            catch { Undo.RevertAllDownToGroup(group); throw; }
        }
    }
}
