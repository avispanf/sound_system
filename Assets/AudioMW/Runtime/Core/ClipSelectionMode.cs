namespace AudioMW
{
    public enum ClipSelectionMode
    {
        Random = 0,
        [UnityEngine.InspectorName("No immediate repeat")]
        RandomNoRepeat = 1,
        Sequential = 2,
        WeightedRandom = 3,
        ShuffleBag = 4
    }
}
