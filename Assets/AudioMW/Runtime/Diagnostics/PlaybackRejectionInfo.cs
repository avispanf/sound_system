namespace AudioMW
{
    public enum PlaybackContentIssue { None, NoUsableSource, MissingSwitchParameter, NoSwitchMatch, CircularReference, NestingLimit }

    public struct PlaybackRejectionInfo
    {
        public SoundEvent ProblemEvent;
        public PlaybackContentIssue ContentIssue;
        public float SwitchValue;
        public int SwitchState;
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
