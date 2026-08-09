using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public static class EditorAudioPreview
    {
        private static MethodInfo playMethod;
        private static MethodInfo stopMethod;
        private static MethodInfo isPlayingMethod;
        private static bool resolved;

        public static bool IsAvailable
        {
            get
            {
                Resolve();
                return playMethod != null && stopMethod != null;
            }
        }

        public static bool IsPlaying
        {
            get
            {
                Resolve();

                if (isPlayingMethod == null)
                {
                    return false;
                }

                try
                {
                    return (bool)isPlayingMethod.Invoke(null, new object[0]);
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        public static void Play(AudioClip clip)
        {
            if (clip == null)
            {
                return;
            }

            Resolve();

            if (playMethod == null)
            {
                Debug.LogWarning("AudioMW preview is unavailable in this Unity version; enter play mode to audition clips.");
                return;
            }

            try
            {
                Stop();

                ParameterInfo[] parameters = playMethod.GetParameters();

                if (parameters.Length == 3)
                {
                    playMethod.Invoke(null, new object[] { clip, 0, false });
                }
                else
                {
                    playMethod.Invoke(null, new object[] { clip });
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("AudioMW preview failed: " + exception.Message);
            }
        }

        public static void Stop()
        {
            Resolve();

            if (stopMethod == null)
            {
                return;
            }

            try
            {
                stopMethod.Invoke(null, new object[0]);
            }
            catch (Exception)
            {
            }
        }

        private static void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;

            Type type = typeof(AudioImporter).Assembly.GetType("UnityEditor.AudioUtil");

            if (type == null)
            {
                return;
            }

            BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            playMethod = type.GetMethod("PlayPreviewClip", flags)
                         ?? type.GetMethod("PlayClip", flags);

            stopMethod = type.GetMethod("StopAllPreviewClips", flags)
                         ?? type.GetMethod("StopAllClips", flags);

            isPlayingMethod = type.GetMethod("IsPreviewClipPlaying", flags)
                              ?? type.GetMethod("IsClipPlaying", flags);
        }
    }
}
