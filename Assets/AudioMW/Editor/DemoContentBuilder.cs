using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    public static class DemoContentBuilder
    {
        private const string Root = "Assets/AudioMW Demo";
        private const string AudioFolder = Root + "/Audio";
        private const string AssetFolder = Root + "/Assets";

        [MenuItem("Window/AudioMW/Build Demo Content", false, 100)]
        public static void Build()
        {
            EnsureFolders();

            WriteClip("MUS_Bass", DemoSynth.BuildBass());
            WriteClip("MUS_Pad", DemoSynth.BuildPad());
            WriteClip("MUS_Arp", DemoSynth.BuildArp());
            WriteClip("MUS_Pulse", DemoSynth.BuildPulse());
            WriteClip("MUS_Stinger", DemoSynth.BuildStinger());
            WriteClip("SFX_Blip_A", DemoSynth.BuildBlip(880.0, 0.18));
            WriteClip("SFX_Blip_B", DemoSynth.BuildBlip(988.0, 0.18));
            WriteClip("SFX_Blip_C", DemoSynth.BuildBlip(1174.0, 0.18));
            WriteClip("SFX_Impact", DemoSynth.BuildImpact());
            WriteClip("VO_Line_A", DemoSynth.BuildVoiceLine(190.0, 1.6));
            WriteClip("VO_Line_B", DemoSynth.BuildVoiceLine(240.0, 1.9));

            AssetDatabase.Refresh();

            SoundParameter intensity = CreateAsset<SoundParameter>("PARAM_Intensity");
            SoundParameter duck = CreateAsset<SoundParameter>("PARAM_VoiceDuck");
            ConfigureParameter(intensity, 0f, 1f, 0f);
            ConfigureParameter(duck, 0f, 1f, 0f);

            MusicTrack track = CreateAsset<MusicTrack>("MUS_DemoTrack");
            track.LoopClip = LoadClip("MUS_Bass");
            track.Tempo = DemoSynth.Bpm;
            track.BeatsPerBar = DemoSynth.BeatsPerBar;
            track.Volume = 0.8f;
            track.Loop = true;
            track.Layers = new[]
            {
                MusicLayer.CreateRuntime("pad", LoadClip("MUS_Pad"), intensity, Curve(0f, 0.25f, 1f, 1f), 1.2f),
                MusicLayer.CreateRuntime("arp", LoadClip("MUS_Arp"), intensity, Curve(0f, 0f, 1f, 1f), 0.9f),
                MusicLayer.CreateRuntime("pulse", LoadClip("MUS_Pulse"), intensity, Curve(0.5f, 0f, 1f, 1f), 0.6f)
            };
            EditorUtility.SetDirty(track);

            SoundEvent blips = CreateAsset<SoundEvent>("SFX_Blips");
            blips.Clips = new[] { LoadClip("SFX_Blip_A"), LoadClip("SFX_Blip_B"), LoadClip("SFX_Blip_C") };
            blips.SelectionMode = ClipSelectionMode.RandomNoRepeat;
            blips.SpatialBlend = 0f;
            blips.Volume = 0.7f;
            EditorUtility.SetDirty(blips);

            SoundEvent impact = CreateAsset<SoundEvent>("SFX_Impact");
            impact.Clips = new[] { LoadClip("SFX_Impact") };
            impact.SpatialBlend = 1f;
            impact.Volume = 0.9f;
            EditorUtility.SetDirty(impact);

            AttenuationPreset room = CreateAsset<AttenuationPreset>("ATT_SmallRoom");
            EditorUtility.SetDirty(room);
            impact.AttenuationPreset = room;

            VoiceLine lineA = CreateAsset<VoiceLine>("VO_Line_A");
            lineA.Clip = LoadClip("VO_Line_A");
            lineA.Speaker = "Operator";
            lineA.Subtitle = "Signal detected on the lower deck.";
            lineA.Volume = 0.9f;
            EditorUtility.SetDirty(lineA);

            VoiceLine lineB = CreateAsset<VoiceLine>("VO_Line_B");
            lineB.Clip = LoadClip("VO_Line_B");
            lineB.Speaker = "Operator";
            lineB.Subtitle = "Hold position until the sweep completes.";
            lineB.Volume = 0.9f;
            EditorUtility.SetDirty(lineB);

            SoundBank bank = CreateAsset<SoundBank>("BANK_Demo");
            bank.Events = new[] { blips, impact };
            EditorUtility.SetDirty(bank);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("AudioMW demo content built under " + Root);
        }

        private static void ConfigureParameter(SoundParameter parameter, float min, float max, float defaultValue)
        {
            SerializedObject serialized = new SerializedObject(parameter);
            serialized.FindProperty("minValue").floatValue = min;
            serialized.FindProperty("maxValue").floatValue = max;
            serialized.FindProperty("defaultValue").floatValue = defaultValue;
            serialized.FindProperty("isGlobal").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AnimationCurve Curve(float inStart, float outStart, float inEnd, float outEnd)
        {
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(new Keyframe(0f, outStart));
            curve.AddKey(new Keyframe(Mathf.Clamp01(inStart), outStart));
            curve.AddKey(new Keyframe(Mathf.Clamp01(inEnd), outEnd));
            return curve;
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(Root))
            {
                AssetDatabase.CreateFolder("Assets", "AudioMW Demo");
            }

            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                AssetDatabase.CreateFolder(Root, "Audio");
            }

            if (!AssetDatabase.IsValidFolder(AssetFolder))
            {
                AssetDatabase.CreateFolder(Root, "Assets");
            }
        }

        private static void WriteClip(string name, float[] mono)
        {
            string path = AudioFolder + "/" + name + ".wav";
            WavWriter.Write(path, DemoSynth.ToStereo(mono), 2, DemoSynth.SampleRate);
        }

        private static AudioClip LoadClip(string name)
        {
            return AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".wav");
        }

        private static T CreateAsset<T>(string name) where T : ScriptableObject
        {
            string path = AssetFolder + "/" + name + ".asset";
            T existing = AssetDatabase.LoadAssetAtPath<T>(path);

            if (existing != null)
            {
                return existing;
            }

            T created = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(created, path);
            return created;
        }
    }
}
