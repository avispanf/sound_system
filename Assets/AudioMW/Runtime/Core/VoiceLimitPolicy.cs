namespace AudioMW
{
    public enum VoiceLimitPolicy
    {
        StealOldest = 0,
        RejectNewest = 1,
        StealQuietest = 2,
        StealLowestPriority = 3
    }
}
