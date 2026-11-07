using System;
using System.Collections.Generic;
using System.IO;
using AudioMW;
using UnityEngine;

public sealed class AudioMWPlayerSmoke : MonoBehaviour
{
    public SoundEvent Simple;
    public SoundEvent Layered;
    public SoundEvent Switch;
    public SoundParameter State;
    public MusicTrack MusicA;
    public MusicTrack MusicB;
    private readonly List<string> checks = new List<string>();
    private double initialDsp;
    private double deadline;
    private int step;
    private SoundHandle active;

    [Serializable] private sealed class Report
    {
        public bool success;
        public string unity;
        public string platform;
        public string error;
        public string[] checks;
        public bool dspAdvanced;
    }

    private void Start()
    {
        deadline = Time.realtimeSinceStartupAsDouble + 15;
        initialDsp = AudioSettings.dspTime;
        try
        {
            Check(Simple != null && Simple.Clips.Length > 0 && Simple.Clips[0] != null, "serialized clip reference");
            active = AudioSystem.PlayTracked(Simple);
            Check(active.IsAssigned && active.IsPlaying, "simple playback admitted");
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private void Update()
    {
        if (step < 0) return;
        try
        {
            if (Time.realtimeSinceStartupAsDouble > deadline) throw new Exception("Player smoke timeout: DSP/audio did not advance.");
            if (step == 0)
            {
                if (AudioSettings.dspTime < initialDsp + 0.1) return;
                Check(active.IsPlaying && active.Voice != null && active.Voice.Source.isPlaying, "native AudioSource advances");
                active.Stop(); Check(!active.IsPlaying && AudioSystem.ActiveVoiceCount == 0, "handle stop releases voice");
                active = AudioSystem.PlayTracked(Layered);
                Check(active.IsPlaying && AudioSystem.ActiveVoiceCount == 2, "serialized layered graph plays two voices");
                active.Stop();
                ParameterStore local = new ParameterStore(); local.Set(State, 3.6f);
                Check(!AudioSystem.PlayTracked(Switch, local).IsAssigned, "unmatched switch rejected");
                EventDebugRecord record; Check(AudioSystem.Debugger.TryGetLast(out record), "diagnostic record available");
                Check(record.Rejection.ContentIssue == PlaybackContentIssue.NoSwitchMatch && record.Rejection.SwitchState == 4, "actual local switch state captured");
                local.Set(State, 1); active = AudioSystem.PlayTracked(Switch, local);
                Check(active.IsPlaying, "matched switch plays"); active.Stop();
                AudioSystem.Music.Play(MusicA); step = 1;
            }
            else if (step == 1)
            {
                Check(AudioSystem.Music.CurrentTrack == MusicA && AudioSystem.Music.ChannelCount > 0, "serialized music starts");
                AudioSystem.Music.TransitionTo(MusicB, MusicQuantization.Bar, 0.2f, MusicFadeCurve.EqualPower);
                Check(AudioSystem.Music.IsTransitionPending, "quantized music scheduled");
                double boundary = AudioSystem.Music.PendingStartDspTime;
                AudioSystem.Music.TransitionTo(MusicB, MusicQuantization.Bar, 0.5f);
                Check(AudioSystem.Music.PendingStartDspTime == boundary, "repeat destination keeps boundary");
                Check(AudioSystem.Music.CancelTransition() && !AudioSystem.Music.IsTransitionPending, "pending music cancelled");
                AudioSystem.Shutdown();
                Check(AudioSystem.ActiveVoiceCount == 0 && !AudioSystem.Music.IsPlaying, "shutdown releases playback");
                Finish(null);
            }
        }
        catch (Exception error) { Finish(error.ToString()); }
    }

    private void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks.Add(label);
    }

    private void Finish(string error)
    {
        step = -1;
        string[] args = Environment.GetCommandLineArgs();
        string path = Path.Combine(Application.persistentDataPath, "audiomw-player-smoke.json");
        for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "--audiomw-report") path = args[i + 1];
        try
        {
            string report = JsonUtility.ToJson(new Report { success = error == null, unity = Application.unityVersion, platform = Application.platform.ToString(), error = error, checks = checks.ToArray(), dspAdvanced = AudioSettings.dspTime > initialDsp }, true);
            File.WriteAllText(path, report); Debug.Log(report);
        }
        catch (Exception failure) { Debug.LogException(failure); Application.Quit(2); return; }
        Application.Quit(error == null ? 0 : 1);
    }
}
