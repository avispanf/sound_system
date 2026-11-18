using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class BadShiftDeviceBuild : IProcessSceneWithReport
{
    private const string ScenePath="Assets/Playground/RelayField.unity";
    private static bool instrument;
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(!instrument || scene.path!=ScenePath) return;
        var game=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<AudioMWPlayground>(true)).Single();
        game.gameObject.AddComponent<BadShiftDeviceRun>();
    }

    public static void Run()
    {
        if(!Application.isBatchMode || !File.Exists(Path.Combine(Application.dataPath,"../.bad-shift-demo")))throw new InvalidOperationException("Use the isolated BAD SHIFT project.");
        var args=Environment.GetCommandLineArgs();
        int i=Array.IndexOf(args,"-deviceRunOutput");
        if(i<0 || i+1>=args.Length || !Path.IsPathRooted(args[i+1]))throw new ArgumentException("Provide an absolute -deviceRunOutput path.");
        var target=EditorUserBuildSettings.activeBuildTarget;
        if(target!=BuildTarget.Android && target!=BuildTarget.iOS && target!=BuildTarget.StandaloneOSX)throw new ArgumentException("Choose Android, iOS, or StandaloneOSX for a host rehearsal.");
        if(!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target),target))throw new InvalidOperationException("Install Unity build support for "+target);
        var path=args[i+1];
        if(Directory.Exists(path)||File.Exists(path))throw new IOException("Choose an unused build output path.");
        var group=BuildPipeline.GetBuildTargetGroup(target);
        string id=PlayerSettings.GetApplicationIdentifier(group);
        var backend=PlayerSettings.GetScriptingBackend(group);
        var architectures=PlayerSettings.Android.targetArchitectures;
        instrument=true;
        try
        {
            if(target==BuildTarget.Android || target==BuildTarget.iOS)
            {
                PlayerSettings.SetApplicationIdentifier(group,"com.avispanf.audiomw.devicecheck");
                PlayerSettings.SetScriptingBackend(group,ScriptingImplementation.IL2CPP);
                if(target==BuildTarget.Android) PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
            }
            var build=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},target=target,locationPathName=path,options=BuildOptions.Development|BuildOptions.ConnectWithProfiler});
            if(build.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Device build failed: "+build.summary.result);
            Debug.Log("AUDIOMW_DEVICE_BUILD "+target+" "+path);
        }
        finally
        {
            instrument=false;
            if(target==BuildTarget.Android || target==BuildTarget.iOS)
            {
                PlayerSettings.SetApplicationIdentifier(group,id);
                PlayerSettings.SetScriptingBackend(group,backend);
                if(target==BuildTarget.Android)PlayerSettings.Android.targetArchitectures=architectures;
            }
        }
    }
}
