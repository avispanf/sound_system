using System.Collections.Generic;

namespace AudioMW.Editor
{
    public static class SoundEventValidation
    {
        public static string Describe(SoundEvent soundEvent)
        {
            return Visit(soundEvent, new HashSet<SoundEvent>());
        }

        private static string Visit(SoundEvent item, HashSet<SoundEvent> path)
        {
            if (item == null) return "Assign a sound event to every child row.";
            if (path.Count >= 32) return "Container nesting exceeds 32 levels. Simplify the event hierarchy.";
            if (!path.Add(item)) return "Circular child reference: " + item.name + ". Remove the reference to allow playback.";
            string error = "";
            if (item.VolumeVariationMode == VolumeVariationMode.Decibels && item.VolumeRandomDbMin > item.VolumeRandomDbMax)
            {
                path.Remove(item);
                return "Minimum gain variation exceeds maximum. Swap the dB offsets to make the range clear.";
            }
            if (item.ContainerMode == ContainerMode.Simple)
            {
                if (!item.HasClips) error = "Add audio clips to hear this event.";
                else
                {
                    bool usable = false;
                    for (int i = 0; i < item.TotalClipCount; i++)
                    {
                        float weight = i < item.ClipWeights.Length ? item.ClipWeights[i] : 1f;
                        if ((item.GetClip(i) != null || (i >= item.Clips.Length && item.ClipReferences[i - item.Clips.Length] != null && item.ClipReferences[i - item.Clips.Length].IsAddressed)) && (item.SelectionMode != ClipSelectionMode.WeightedRandom ||
                            (weight > 0 && !float.IsNaN(weight) && !float.IsInfinity(weight)))) usable = true;
                    }
                    if (!usable) error = "Assign a clip with a positive weight to allow playback.";
                }
            }
            else if (item.ContainerMode == ContainerMode.Blend)
            {
                bool usable = false;
                foreach (BlendLayer layer in item.BlendLayers) if (layer != null && layer.IsValid) usable = true;
                if (!usable) error = "Add at least one layer with an audio clip.";
                else if (item.BlendParameter == null) error = "Assign a blend parameter to control the layer curves.";
            }
            else
            {
                if (item.ContainerMode == ContainerMode.Switch && item.SwitchParameter == null) error = "Assign a state parameter to select child events.";
                else if (item.Children.Length == 0) error = "Add child events to build this container.";
                else foreach (SoundEventChild child in item.Children)
                {
                    error = Visit(child != null ? child.Event : null, path);
                    if (!string.IsNullOrEmpty(error)) break;
                }
            }
            path.Remove(item);
            return error;
        }
    }
}
