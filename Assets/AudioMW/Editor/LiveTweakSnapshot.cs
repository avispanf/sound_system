using System;
using System.Collections.Generic;

namespace AudioMW.Editor
{
    [Serializable]
    public struct LiveTweakEntry
    {
        public string Guid;
        public string Json;
    }

    [Serializable]
    public sealed class LiveTweakSnapshot
    {
        public List<LiveTweakEntry> Entries = new List<LiveTweakEntry>();

        public int Count
        {
            get { return Entries.Count; }
        }

        public void Add(string guid, string json)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return;
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Guid == guid)
                {
                    LiveTweakEntry existing = Entries[i];
                    existing.Json = json;
                    Entries[i] = existing;
                    return;
                }
            }

            Entries.Add(new LiveTweakEntry { Guid = guid, Json = json });
        }

        public bool TryGet(string guid, out string json)
        {
            for (int i = 0; i < Entries.Count; i++)
            {
                if (Entries[i].Guid == guid)
                {
                    json = Entries[i].Json;
                    return true;
                }
            }

            json = null;
            return false;
        }

        public void Clear()
        {
            Entries.Clear();
        }

        public static List<string> FindChanged(LiveTweakSnapshot before, LiveTweakSnapshot after)
        {
            List<string> changed = new List<string>();

            if (before == null || after == null)
            {
                return changed;
            }

            for (int i = 0; i < after.Entries.Count; i++)
            {
                LiveTweakEntry current = after.Entries[i];
                string previous;

                if (!before.TryGet(current.Guid, out previous))
                {
                    continue;
                }

                if (!string.Equals(previous, current.Json, StringComparison.Ordinal))
                {
                    changed.Add(current.Guid);
                }
            }

            return changed;
        }
    }
}
