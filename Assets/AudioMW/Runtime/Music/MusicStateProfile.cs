using System;
using System.Collections.Generic;
using UnityEngine;

namespace AudioMW
{
    [Serializable]
    public sealed class MusicState
    {
        public string Name;
        public MusicTrack Track;
    }

    [Serializable]
    public sealed class MusicTransitionRule
    {
        public string From;
        public string To;
        public MusicQuantization Quantization = MusicQuantization.Bar;
    }

    [CreateAssetMenu(fileName = "MusicStates", menuName = "AudioMW/Music State Profile", order = 5)]
    public sealed class MusicStateProfile : ScriptableObject
    {
        [SerializeField] private MusicState[] states = new MusicState[0];
        [SerializeField] private MusicTransitionRule[] transitions = new MusicTransitionRule[0];
        [SerializeField] private MusicQuantization defaultQuantization = MusicQuantization.Bar;

        public MusicState[] States { get { return states; } set { states = value ?? new MusicState[0]; } }
        public MusicTransitionRule[] Transitions { get { return transitions; } set { transitions = value ?? new MusicTransitionRule[0]; } }
        public MusicQuantization DefaultQuantization { get { return defaultQuantization; } set { defaultQuantization = value; } }

        public string Validate()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<MusicTrack> tracks = new HashSet<MusicTrack>();
            foreach (MusicState state in states)
            {
                if (state == null || string.IsNullOrWhiteSpace(state.Name)) return "Give every state a name.";
                if (!names.Add(state.Name)) return "State names must be unique: " + state.Name;
                if (state.Track == null || !state.Track.HasContent) return "Assign a playable track to " + state.Name + ".";
                if (!tracks.Add(state.Track)) return "Use a distinct track asset for each state.";
            }
            if (names.Count == 0) return "Add a music state to get started.";
            HashSet<Tuple<string, string>> routes = new HashSet<Tuple<string, string>>();
            foreach (MusicTransitionRule rule in transitions)
            {
                if (rule == null || !names.Contains(rule.From) || !names.Contains(rule.To)) return "Each transition must name an existing source and destination state.";
                if (!routes.Add(Tuple.Create(rule.From, rule.To))) return "Remove the duplicate transition from " + rule.From + " to " + rule.To + ".";
            }
            return "";
        }

        public string StateFor(MusicTrack track)
        {
            foreach (MusicState state in states) if (state != null && state.Track == track) return state.Name;
            return "";
        }

        public bool Request(MusicPlayer player, string destination)
        {
            if (player == null || !string.IsNullOrEmpty(Validate())) return false;
            MusicState target = Array.Find(states, state => state.Name == destination);
            if (target == null) return false;
            if (player.PendingTrack == target.Track || (player.CurrentTrack == target.Track && !player.IsTransitionPending)) return true;
            string from = StateFor(player.CurrentTrack);
            MusicQuantization quantization = defaultQuantization;
            foreach (MusicTransitionRule rule in transitions)
                if (rule.From == from && rule.To == destination) { quantization = rule.Quantization; break; }
            player.TransitionTo(target.Track, quantization);
            return true;
        }
    }
}
