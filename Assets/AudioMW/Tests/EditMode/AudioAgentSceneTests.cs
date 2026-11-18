using System;
using System.IO;
using System.Linq;
using AudioMW.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AudioMW.Tests
{
    public class AudioAgentSceneTests
    {
        private Scene previous, scene;
        private bool restoreUntitled;
        private NewSceneSetup previousSetup;
        private string folder, path;
        private GameObject target;
        private SoundEvent sound;
        [SetUp] public void SetUp()
        {
            folder = null;
            previous = SceneManager.GetActiveScene();
            restoreUntitled = string.IsNullOrEmpty(previous.path);
            if (restoreUntitled && (!Application.isBatchMode || previous.isDirty || SceneManager.sceneCount != 1))
                Assert.Ignore("Open a saved active scene or run these tests in an isolated batch project.");
            previousSetup = previous.rootCount==0 ? NewSceneSetup.EmptyScene : NewSceneSetup.DefaultGameObjects;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, restoreUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            folder = "Assets/AgentBindingTest-"+Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets",Path.GetFileName(folder));
            sound = ScriptableObject.CreateInstance<SoundEvent>();
            AssetDatabase.CreateAsset(sound,folder+"/loop.asset");
            target = new GameObject("Kitchen cooler");
            path = folder+"/Fixture.unity";
            EditorSceneManager.SaveScene(scene,path);
        }
        [TearDown] public void Cleanup()
        {
            if(string.IsNullOrEmpty(folder)) return;
            if(restoreUntitled) EditorSceneManager.NewScene(previousSetup,NewSceneMode.Single);
            else if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            if(scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
            AssetDatabase.DeleteAsset(folder);
        }
        private AudioSceneBindingDraft Draft()
        {
            var entry = AudioAgentScenePlanner.Find("cooler").Single();
            return new AudioSceneBindingDraft { ObjectId=entry.Id, ExpectedHash=entry.Hash, EventGuid=AssetDatabase.AssetPathToGUID(folder+"/loop.asset"), Intent="Attach kitchen compressor" };
        }
        [Test] public void SyntheticBindingCreatesOneUndoableEmitterAndDoesNotSaveScene()
        {
            var before=File.ReadAllBytes(path);
            var draft=Draft();var plan=AudioAgentScenePlanner.Prepare(draft,"test");
            Assert.IsNull(target.GetComponent<SoundEmitter>());Assert.IsFalse(scene.isDirty);
            Assert.AreEqual("",AudioAgentPlanner.Check(plan,"test"));
            AudioAgentPlanner.Apply(plan,"test");
            Assert.AreSame(sound,target.GetComponent<SoundEmitter>().Event);Assert.IsTrue(scene.isDirty);
            CollectionAssert.AreEqual(before,File.ReadAllBytes(path));
            AudioAgentPlanner.Apply(plan,"test");Assert.AreEqual(1,target.GetComponents<SoundEmitter>().Length);
            Undo.PerformUndo();Assert.IsNull(target.GetComponent<SoundEmitter>());
            Undo.PerformRedo();Assert.AreSame(sound,target.GetComponent<SoundEmitter>().Event);
        }
        [Test] public void ImportedLoopBindsByGuidAndSurvivesSceneReload()
        {
            string wave = Path.GetFullPath("Library/agent-loop-" + Guid.NewGuid().ToString("N") + ".wav");
            try
            {
                AudioAgentTests.WriteWave(wave);
                var mixer = AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>(AudioMixPlanner.TemplatePath);
                var route = mixer.FindMatchingGroups("SFX").Single(group => group.name == "SFX");
                var import = AudioAgentPlanner.Prepare(new AudioAgentDraft
                {
                    Destination = folder, Intent = "Test compressor import",
                    Events = new[] { new AudioAgentEventDraft
                    {
                        Name = "Compressor", Purpose = "Synthetic cooler", Files = new[] { wave },
                        MixerGroupId = GlobalObjectId.GetGlobalObjectIdSlow(route).ToString(), CreateLoop = true
                    } }
                }, "test");
                string eventPath = import.Read().Changes.Single().EventPath;
                Assert.IsNull(AssetDatabase.LoadAssetAtPath<SoundEvent>(eventPath));
                AudioAgentPlanner.Apply(import, "test");

                var draft = Draft();
                draft.EventGuid = AssetDatabase.AssetPathToGUID(eventPath);
                var binding = AudioAgentScenePlanner.Prepare(draft, "test");
                Assert.IsNull(target.GetComponent<SoundEmitter>());
                AudioAgentPlanner.Apply(binding, "test");
                Assert.IsTrue(EditorSceneManager.SaveScene(scene));
                string targetName = target.name;
                var holding = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try
                {
                    Assert.IsTrue(EditorSceneManager.CloseScene(scene, true));
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    Assert.IsTrue(SceneManager.SetActiveScene(scene));
                    var emitter = scene.GetRootGameObjects().Single(go => go.name == targetName).GetComponent<SoundEmitter>();
                    Assert.IsNotNull(emitter);
                    Assert.AreEqual(draft.EventGuid, AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(emitter.Event)));
                    Assert.IsTrue(emitter.PlayOnEnable);
                    Assert.IsTrue(emitter.Event.Loop);
                    Assert.AreSame(route, emitter.Event.MixerGroup);
                    Assert.AreEqual(800, emitter.Event.Clips.Single().samples);
                }
                finally { EditorSceneManager.CloseScene(holding, true); }
            }
            finally { if (File.Exists(wave)) File.Delete(wave); }
        }

        [Test] public void RejectsChangedSceneAndWrongSession()
        {
            var plan=AudioAgentScenePlanner.Prepare(Draft(),"test");
            Assert.IsNotEmpty(AudioAgentPlanner.Check(plan,"other"));
            target.name="Moved target";EditorSceneManager.MarkSceneDirty(scene);
            Assert.IsNotEmpty(AudioAgentPlanner.Check(plan,"test"));
            EditorSceneManager.SaveScene(scene);
            Assert.IsNotEmpty(AudioAgentPlanner.Check(plan,"test"));
        }
        [Test] public void ReplacesOnlyEventOnExistingEmitter()
        {
            var emitter=target.AddComponent<SoundEmitter>();emitter.enabled=false;
            EditorSceneManager.SaveScene(scene);
            var plan=AudioAgentScenePlanner.Prepare(Draft(),"test");AudioAgentPlanner.Apply(plan,"test");
            Assert.AreSame(emitter,target.GetComponent<SoundEmitter>());Assert.IsFalse(emitter.enabled);Assert.AreSame(sound,emitter.Event);
            Undo.PerformUndo();Assert.IsNull(emitter.Event);
        }
    }
}
