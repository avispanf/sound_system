using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AudioMW;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

public sealed class BadShiftDeviceRun : MonoBehaviour
{
    [Serializable] private sealed class Sample
    {
        public string phase;
        public float seconds, obstruction;
        public int voices, virtualVoices, steals, blockedSources;
        public bool music, voiceOver, traffic;
        public long allocatedBytes;
        public double dspCpuPercent = -1;
    }

    [Serializable] private sealed class Report
    {
        public string run, build, platform, device, os, unity, tier, speakers, dspCounter;
        public int sampleRate, bufferFrames, buffers, voiceLimit;
        public bool workloadComplete, mixObserved, pauseObserved, resumeVerified, outputMuted, interruptedDuringLoad;
        public string stage, error;
        public List<Sample> samples = new List<Sample>(160);
    }

    private readonly Report report = new Report();
    private readonly List<SoundOccluder> sources = new List<SoundOccluder>();
    private AudioMWPlayground game;
    private BadShiftSoundscape cafe;
    private ProfilerRecorder dsp;
    private string reportPath;
    private float start, next;
    private double resumeClock;
    private int resumeCursor;
    private bool awaitingResume;

    private void Start()
    {
        game=GetComponent<AudioMWPlayground>(); cafe=game.Soundscape;
        report.run=Guid.NewGuid().ToString("N"); report.build=Application.buildGUID;
        report.platform=Application.platform.ToString(); report.device=SystemInfo.deviceModel;
        report.os=SystemInfo.operatingSystem; report.unity=Application.unityVersion;
        reportPath=Path.Combine(Application.persistentDataPath,"AudioMW-device-"+report.run+".json");
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(.8f);
        start=Time.realtimeSinceStartup;
        report.sampleRate=AudioSettings.outputSampleRate;report.speakers=AudioSettings.speakerMode.ToString();
        AudioSettings.GetDSPBufferSize(out report.bufferFrames,out report.buffers);
        report.voiceLimit=AudioRuntime.Instance.Pool.MaxVoices;
        report.tier=AudioRuntimeSettings.ActiveTier!=null ? AudioRuntimeSettings.ActiveTier.name : "Runtime defaults";
        report.outputMuted=AudioListener.volume==0;
        StartDsp();
        cafe.Sky.SetMode(BadShiftSkyMode.Night);
        report.stage="baseline";
        yield return new WaitForSecondsRealtime(4);
        game.Door.SetOpen(true);
        game.RespawnAt(cafe.Room.TransformPoint(new Vector3(-1,.09f,.54f)), Quaternion.identity);
        yield return new WaitForSecondsRealtime(.8f);
        cafe.ToggleTap();
        if (!cafe.Water.IsPlaying) { Fail("Tap source did not start."); yield break; }
        for(int i=0;i<3;i++)
        {
            var source=Instantiate(cafe.TapOccluder,cafe.Tap.position+Vector3.right*(i-1)*.16f,Quaternion.identity);
            source.name="Device test tap "+(i+1);
            foreach(var visual in source.GetComponentsInChildren<Renderer>()) visual.enabled=false;
            foreach(var shape in source.GetComponentsInChildren<Collider>()) shape.enabled=false;
            source.Event=cafe.TapLoop;source.Play();sources.Add(source);
        }
        game.RespawnAt(cafe.Room.TransformPoint(new Vector3(1.22f,.08f,-3.48f)),Quaternion.identity);
        report.stage="open door";
        yield return new WaitForSecondsRealtime(3);
        game.Door.SetOpen(false);
        report.stage="closed door";
        for(int i=0;i<5;i++)
        {
            game.Speak();
            yield return new WaitForSecondsRealtime(3);
        }
        game.Door.SetOpen(true);
        report.stage="reopened";
        yield return new WaitForSecondsRealtime(3);
        report.workloadComplete=true;
        report.stage="background and resume";
        if(!report.mixObserved)report.error="No sample contained music, VO, a running traffic engine and multiple occluded sources together.";
        Save();
    }

    private void StartDsp()
    {
        report.dspCounter="Unavailable on this player";
        var handles=new List<ProfilerRecorderHandle>();
        ProfilerRecorderHandle.GetAvailable(handles);
        foreach(var h in handles)
        {
            var d=ProfilerRecorderHandle.GetDescription(h);
            if(d.UnitType!=ProfilerMarkerDataUnit.Percent || d.Name.IndexOf("DSP",StringComparison.OrdinalIgnoreCase)<0 ||
                d.Name.IndexOf("CPU",StringComparison.OrdinalIgnoreCase)<0)continue;
            dsp=new ProfilerRecorder(h,1,ProfilerRecorderOptions.StartImmediately);
            report.dspCounter=d.Name;
            break;
        }
    }

    private void Update()
    {
        if(report.stage==null || !AudioRuntime.Exists) return;
        if(awaitingResume && AudioSettings.dspTime>resumeClock+.1 && cafe.Water.Voice!=null && cafe.Water.Voice.Source.isPlaying && cafe.Water.Voice.Source.timeSamples!=resumeCursor && AudioSystem.Music.IsPlaying)
        {
            report.resumeVerified=true; awaitingResume=false;report.stage="resume verified";Save();
        }
        if(Time.realtimeSinceStartup<next || report.samples.Count>=160 || report.workloadComplete) return;
        next=Time.realtimeSinceStartup+.25f;
        var rt=AudioRuntime.Instance;
        var s=new Sample {phase=report.stage,seconds=Time.realtimeSinceStartup-start, voices=rt.Pool.ActiveCount,
            virtualVoices=rt.VirtualVoiceCount, steals=rt.Pool.StealCount,allocatedBytes=Profiler.GetTotalAllocatedMemoryLong(),
            obstruction=cafe.TapOccluder.Occlusion,music=AudioSystem.Music.IsPlaying,voiceOver=AudioSystem.VoiceOver.IsSpeaking,traffic=cafe.Street.Engine.IsPlaying};
        if(cafe.Water.Voice!=null && cafe.Water.Voice.OcclusionVolume<.65f && cafe.TapOccluder.Occlusion>.65f)s.blockedSources++;
        foreach(var src in sources)if(src.ActiveVoice!=null && src.ActiveVoice.OcclusionVolume<.65f && src.Occlusion>.65f)s.blockedSources++;
        if(dsp.Valid && dsp.Count>0)s.dspCpuPercent=dsp.LastValue;
        report.samples.Add(s);
        report.mixObserved |= s.blockedSources>=2 && s.music && s.voiceOver && s.traffic;
    }

    private void OnApplicationPause(bool paused)
    {
        if(reportPath==null)return;
        if(!report.workloadComplete)
        {
            if(paused && report.stage!=null){report.interruptedDuringLoad=true;Save();}
            return;
        }
        if(paused){report.pauseObserved=true; report.resumeVerified=false;Save();}
        else if(report.pauseObserved){resumeClock=AudioSettings.dspTime;resumeCursor=cafe.Water.Voice!=null?cafe.Water.Voice.Source.timeSamples:-1;awaitingResume=true;}
    }
    private void Fail(string why){report.error=why;report.stage="failed";Save();}
    private void Save()
    {
        try
        {
            string tmp=reportPath+".tmp";
            File.WriteAllText(tmp,JsonUtility.ToJson(report,true));
            if(File.Exists(reportPath)) File.Replace(tmp,reportPath,null); else File.Move(tmp,reportPath);
            Debug.Log("AUDIOMW_DEVICE_REPORT "+reportPath+" · "+report.stage);
        }
        catch(Exception e){report.error="Could not save report: "+e.Message;Debug.LogError(report.error);}
    }
    private void OnGUI()
    {
        var rect=new Rect(12,Screen.height*.32f,Mathf.Min(540,Screen.width-24),110);
        GUI.Box(rect,"AudioMW device test");
        GUI.Label(new Rect(rect.x+10,rect.y+22,rect.width-20,42), string.IsNullOrEmpty(report.error)?report.stage=="background and resume"?"Send the app to the background, then reopen it.":report.stage:report.error);
        if(report.workloadComplete && GUI.Button(new Rect(rect.x+10,rect.y+69,185,32),"Copy JSON report"))GUIUtility.systemCopyBuffer=JsonUtility.ToJson(report,true);
    }
    private void OnDestroy()
    {
        dsp.Dispose();
        foreach(var source in sources)if(source!=null)Destroy(source.gameObject);
    }
}
