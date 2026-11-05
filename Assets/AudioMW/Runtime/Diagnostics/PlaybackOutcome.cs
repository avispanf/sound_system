namespace AudioMW
{
    public enum PlaybackOutcome
    {
        Played = 0,
        RejectedNullEvent = 1,
        RejectedNoClips = 2,
        RejectedNoVoice = 3,
        RejectedNoValidLayers = 4,
        RejectedCooldown = 5,
        RejectedInstanceLimit = 6,
        RejectedContainerGraph = 7
    }
}
