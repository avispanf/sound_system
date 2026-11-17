using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    [InitializeOnLoad]
    public static class EditorAudioPreview
    {
        private static MethodInfo playMethod;
        private static MethodInfo stopMethod;
        private static MethodInfo isPlayingMethod;
        private static MethodInfo positionMethod, pauseMethod, resumeMethod;
        private static int startSample;
        private static float pausedPosition;
        public static bool IsPaused { get; private set; }
        public static bool CanPause { get { Resolve(); return pauseMethod != null && resumeMethod != null; } }
        private static bool resolved;
        private static double loadDeadline;
        public static AudioClip CurrentClip { get; private set; }
        public static bool IsLoading { get; private set; }
        public static string LastError { get; private set; } = "";

        static EditorAudioPreview()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Stop;
            EditorApplication.quitting += Stop;
            EditorApplication.update += UpdateLoading;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode) Stop();
            };
        }

        public static bool IsAvailable
        {
            get { Resolve(); return playMethod != null && stopMethod != null; }
        }

        public static bool IsPlaying
        {
            get
            {
                Resolve();
                if (CurrentClip == null || IsLoading || IsPaused || isPlayingMethod == null) return false;
                try { return (bool)isPlayingMethod.Invoke(null, Array.Empty<object>()); }
                catch (Exception) { return false; }
            }
        }

        public static float Position
        {
            get
            {
                Resolve();
                if (IsPaused) return pausedPosition;
                if (!IsPlaying || positionMethod == null) return 0;
                try { return (float)positionMethod.Invoke(null, Array.Empty<object>()); }
                catch (Exception) { return 0; }
            }
        }

        public static void Play(AudioClip clip) { Play(clip,0); }
        public static void Play(AudioClip clip,int sample)
        {
            Stop();
            if (clip == null) { LastError = "Choose an audio source to preview."; return; }
            if (!IsAvailable) { LastError = "Source preview is unavailable in this Unity version."; return; }
            CurrentClip = clip;
            startSample=Mathf.Clamp(sample,0,Math.Max(0,clip.samples-1));
            if (clip.loadState == AudioDataLoadState.Unloaded && !clip.LoadAudioData())
            {
                Fail("Unity could not load this source. Check its import settings.");
                return;
            }
            IsLoading = true;
            loadDeadline = EditorApplication.timeSinceStartup + 30;
            UpdateLoading();
        }

        private static void UpdateLoading()
        {
            if (!IsLoading) return;
            if (CurrentClip == null) { Fail("The source was removed before it could play."); return; }
            if (CurrentClip.loadState == AudioDataLoadState.Failed) { Fail("Source audio data failed to load. Check its import settings."); return; }
            if (CurrentClip.loadState != AudioDataLoadState.Loaded)
            {
                if (EditorApplication.timeSinceStartup > loadDeadline) Fail("Source loading timed out. Check its import settings and retry.");
                return;
            }
            IsLoading = false;
            try
            {
                playMethod.Invoke(null, playMethod.GetParameters().Length == 3
                    ? new object[] { CurrentClip, startSample, false } : new object[] { CurrentClip });
            }
            catch (Exception exception) { Fail("Source preview failed: " + (exception.InnerException ?? exception).Message); }
        }

        public static void TogglePause()
        {
            if(!CanPause||CurrentClip==null||IsLoading||!IsPaused&&!IsPlaying)return;
            try
            {
                if(IsPaused){resumeMethod.Invoke(null,Array.Empty<object>());IsPaused=false;}
                else {pausedPosition=Position;pauseMethod.Invoke(null,Array.Empty<object>());IsPaused=true;}
            }
            catch(Exception exception){Fail("Source pause failed: "+(exception.InnerException??exception).Message);}
        }
        private static void Fail(string message)
        {
            Stop();
            LastError = message;
        }

        public static void Stop()
        {
            CurrentClip = null;
            IsPaused=false;pausedPosition=0;startSample=0;
            IsLoading = false;
            LastError = "";
            Resolve();
            if (stopMethod == null) return;
            try { stopMethod.Invoke(null, Array.Empty<object>()); }
            catch (Exception exception) { LastError = "Source preview could not stop: " + (exception.InnerException ?? exception).Message; }
        }

        private static void Resolve()
        {
            if (resolved) return;
            resolved = true;
            Type type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");
            if (type == null) return;
            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            playMethod = type.GetMethod("PlayPreviewClip", flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? type.GetMethod("PlayClip", flags, null, new[] { typeof(AudioClip), typeof(int), typeof(bool) }, null)
                ?? type.GetMethod("PlayClip", flags, null, new[] { typeof(AudioClip) }, null);
            stopMethod = type.GetMethod("StopAllPreviewClips", flags, null, Type.EmptyTypes, null)
                ?? type.GetMethod("StopAllClips", flags, null, Type.EmptyTypes, null);
            isPlayingMethod = type.GetMethod("IsPreviewClipPlaying", flags, null, Type.EmptyTypes, null)
                ?? type.GetMethod("IsClipPlaying", flags, null, Type.EmptyTypes, null);
            pauseMethod=type.GetMethod("PausePreviewClip",flags,null,Type.EmptyTypes,null);
            resumeMethod=type.GetMethod("ResumePreviewClip",flags,null,Type.EmptyTypes,null);
            positionMethod = type.GetMethod("GetPreviewClipPosition", flags, null, Type.EmptyTypes, null);
        }
    }
}
