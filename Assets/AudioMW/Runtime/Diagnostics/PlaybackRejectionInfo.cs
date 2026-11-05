namespace AudioMW
{
    public struct PlaybackRejectionInfo
    {
        public bool HasInstanceLimit;
        public string Scope;
        public int CurrentCount;
        public int Limit;
        public bool HasCooldown;
        public float RemainingSeconds;
        public bool HasVoiceCapacity;
        public int RequestedVoices;
        public int AvailableVoices;
        public int VoiceCapacity;
    }
}
