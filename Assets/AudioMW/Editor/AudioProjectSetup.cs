using System;
using UnityEngine;

namespace AudioMW.Editor
{
    [Serializable] public sealed class AudioProjectBrief
    {
        public string Game = "";
        public string Genre = "";
        public string Audience = "";
        public string Platforms = "";
        public string Perspective = "";
        public string ListeningDevices = "";
        public string MixIntent = "";
        public string DeliveryTarget = "";
        public string ContentNotes = "";
        public int VoiceBudget = 32;
        public int MemoryBudgetMb = 128;
        public string SourceFolder = "Assets";
        public string ProfilePath = "Assets/AudioMWProjectSetup.asset";
    }

    public sealed class AudioProjectSetup : ScriptableObject
    {
        [SerializeField] private string briefJson;
        [SerializeField] private string reviewedPlanJson;
        [SerializeField] private string sourceFingerprint;
        [SerializeField] private int revision;
        public AudioProjectBrief Brief => JsonUtility.FromJson<AudioProjectBrief>(briefJson);
        public string ReviewedPlanJson => reviewedPlanJson;
        public string SourceFingerprint => sourceFingerprint;
        public int Revision => revision;
        internal string BriefJson => briefJson;
        internal void Store(string brief, string plan, string fingerprint)
        {
            briefJson = brief; reviewedPlanJson = plan; sourceFingerprint = fingerprint; revision++;
        }
    }
}
