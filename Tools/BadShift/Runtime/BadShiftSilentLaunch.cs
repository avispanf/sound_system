using System;
using UnityEngine;

public static class BadShiftSilentLaunch
{
    public static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-audiomw-silent")>=0;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize(){if(Requested)AudioListener.volume=0;}
}
