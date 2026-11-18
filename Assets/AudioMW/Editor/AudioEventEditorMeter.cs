using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow
    {
        private enum MeterMode { SamplePeak, TruePeak, Momentary, ShortTerm, Integrated }
        private static readonly string[] MeterModes = { "Peak · dBFS", "True peak · dBTP", "M · LUFS (400 ms)", "S · LUFS (3 s)", "I · LUFS (10 min max)" };
        private static readonly string[] MeterInputs = { "Source PCM (dry)", "Game listener" };
        [SerializeField] private bool meterOpen;
        [SerializeField] private MeterMode meterMode;
        [SerializeField] private int meterInput;
        private IMGUIContainer meterPanel;
        private EditorListenerMeterTap meterTap;
        private PcmLevelMeter levels;
        private AudioClip measuredClip;
        private float[] dryBuffer;
        private int nextSample, discontinuities;
        private double nextListenerCheck, lastBufferTime;
        private string meterStatus = "", measuredName = "";
        private bool wasMoving;

        private void BuildMeter(VisualElement row)
        {
            meterPanel = new IMGUIContainer(DrawMeter) { name = "studio-level-meter" };
            meterPanel.style.width = 152;
            meterPanel.style.flexShrink = 0;
            meterPanel.style.display = meterOpen ? DisplayStyle.Flex : DisplayStyle.None;
            row.Add(meterPanel);
            rootVisualElement.Q("studio-meter-toggle")?.EnableInClassList("studio-v2-active", meterOpen);
        }

        private void ToggleMeter()
        {
            meterOpen = !meterOpen;
            if (!meterOpen) DetachMeter();
            meterPanel.style.display = meterOpen ? DisplayStyle.Flex : DisplayStyle.None;
            rootVisualElement.Q("studio-meter-toggle")?.EnableInClassList("studio-v2-active", meterOpen);
        }

        private void ResetLevels()
        {
            levels = null;
            measuredClip = null;
            nextSample = 0;
            wasMoving = false;
        }

        private void DetachMeter()
        {
            if (meterTap != null) DestroyImmediate(meterTap);
            meterTap = null;
            ResetLevels();
            measuredName = "";
            meterStatus = "";
            nextListenerCheck = 0;
        }

        private void TickMeter(double now)
        {
            if (!meterOpen || meterPanel == null) return;
            if (meterInput == 1) TickListener(now);
            else TickDry();
            meterPanel.MarkDirtyRepaint();
        }

        private void TickListener(double now)
        {
            measuredName = "Game listener";
            if (!EditorApplication.isPlaying || EditorApplication.isPaused)
            {
                DetachMeter();
                meterStatus = EditorApplication.isPaused ? "Play Mode paused" : "Enter Play Mode";
                return;
            }
            if (now >= nextListenerCheck)
            {
                nextListenerCheck = now + .5;
                AudioListener active = null;
                int count = 0;
                foreach (var candidate in UnityEngine.Object.FindObjectsOfType<AudioListener>())
                    if (candidate.isActiveAndEnabled) { active = candidate; count++; }
                if (count != 1 || active.GetComponent<AudioSource>() != null)
                {
                    DetachMeter();
                    nextListenerCheck = now + .5;
                    meterStatus = count != 1 ? "One listener required" : "Listener shares an AudioSource";
                    return;
                }
                if (meterTap == null || meterTap.gameObject != active.gameObject)
                {
                    DetachMeter();
                    meterTap = active.gameObject.AddComponent<EditorListenerMeterTap>();
                    meterTap.hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
                    discontinuities = 0;
                    lastBufferTime = now;
                }
            }
            if (meterTap == null) return;
            int drops = meterTap.Discontinuities;
            if (drops != discontinuities)
            {
                discontinuities = drops;
                ResetLevels();
                while (meterTap.TryRead(out _, out _, out _)) meterTap.Advance();
                meterStatus = "Capture interrupted · mono/stereo only";
                return;
            }
            while (meterTap.TryRead(out var data,out int channels,out int frames))
            {
                int rate = AudioSettings.outputSampleRate;
                if (rate >= 1000)
                {
                    if (levels == null || levels.Rate != rate || levels.Channels != channels) levels = new PcmLevelMeter(channels,rate);
                    levels.Push(data,frames);
                }
                meterTap.Advance();
                lastBufferTime = now;
            }
            if (now - lastBufferTime > .55) { ResetLevels(); meterStatus = "No listener PCM"; }
            else meterStatus = levels == null ? "Waiting for PCM" : "Listener mix";
        }

        private void TickDry()
        {
            AudioClip source = EditorAudioPreview.CurrentClip;
            bool moving = EditorAudioPreview.IsPlaying;
            double time = EditorAudioPreview.Position;
            if (source == null || (!moving && !EditorAudioPreview.IsPaused))
            {
                moving = arrangementPlayer != null && arrangementPlayer.IsPlaying || legacyPlayer != null && legacyPlayer.IsPlaying;
                double cursor = AuditionPosition;
                var score = CurrentAsset as MusicArrangement;
                bool legacy = CurrentAsset is MusicTrack && projectionSource == CurrentAsset;
                if (legacy) score = legacyProjection;
                source = null;
                if (score != null && score.Lanes != null)
                {
                    for (int i = 0; i < score.Lanes.Count; i++)
                    {
                        var lane = score.Lanes[i];
                        if (lane == null || (score.Lanes.Count != 1 && selectedLane != (legacy ? "legacy:" + i : lane.Id))) continue;
                        int candidates = 0, selected = 0;
                        foreach (var clip in lane.Clips)
                        {
                            if (clip.Source == null || cursor < clip.Start || cursor >= clip.Start + clip.Duration) continue;
                            candidates++;
                            bool isSelected = selectedClips.Contains(clip.Id);
                            if (isSelected) selected++;
                            if (source != null && !isSelected) continue;
                            source = clip.Source;
                            time = DrySourcePosition(clip, cursor);
                        }
                        if (candidates > 1 && selected != 1)
                        {
                            ResetLevels();
                            measuredName = lane.Name;
                            meterStatus = "Overlapping sources · select one clip";
                            return;
                        }
                        break;
                    }
                }
            }
            if (source == null || source.channels > 2 || source.loadType != AudioClipLoadType.DecompressOnLoad || source.loadState != AudioDataLoadState.Loaded)
            {
                ResetLevels();
                measuredName = source != null ? source.name : "";
                meterStatus = source == null ? "Play a dry source or select a playing timeline track" : "Readable mono/stereo PCM required";
                return;
            }
            measuredName = source.name;
            int frame = (int)Math.Min(source.samples, Math.Max(0, Math.Floor(time * source.frequency)));
            if (source != measuredClip || nextSample > frame || frame - nextSample > source.frequency / 2 || moving && !wasMoving)
            {
                levels = new PcmLevelMeter(source.channels, source.frequency);
                measuredClip = source;
                nextSample = frame;
            }
            wasMoving = moving;
            meterStatus = moving ? "Dry · before gain / FX" : "Transport stopped / paused";
            if (!moving) return;
            const int chunk = 512;
            if (dryBuffer == null || dryBuffer.Length != chunk * source.channels) dryBuffer = new float[chunk * source.channels];
            while (nextSample + chunk <= frame)
            {
                if (!source.GetData(dryBuffer,nextSample)) { ResetLevels(); meterStatus = "PCM unavailable"; return; }
                levels.Push(dryBuffer,chunk);
                nextSample += chunk;
            }
        }

        internal static double DrySourcePosition(ArrangementClip clip, double cursor)
        {
            double elapsed = Math.Max(0, cursor - clip.Start);
            double span = MusicTrack.GetExactDuration(clip.Source) - clip.Trim;
            return clip.Trim + (clip.Repeat && span > 0 ? elapsed % span : elapsed);
        }

        private void DrawMeter()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("Level meter", EditorStyles.boldLabel);
            int input = EditorGUILayout.Popup(meterInput,MeterInputs);
            if (input != meterInput) { DetachMeter(); meterInput = input; }
            meterMode = (MeterMode)EditorGUILayout.Popup((int)meterMode,MeterModes);
            GUILayout.Label(new GUIContent(measuredName,measuredName),EditorStyles.miniLabel);
            var area = GUILayoutUtility.GetRect(92,90,GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint) PaintLevelBars(area);
            GUILayout.Label(meterStatus,EditorStyles.wordWrappedMiniLabel);
            if (GUILayout.Button("Reset measurement",EditorStyles.miniButton)) DetachMeter();
            EditorGUILayout.EndVertical();
        }

        private void PaintLevelBars(Rect area)
        {
            bool loudness = (int)meterMode >= (int)MeterMode.Momentary;
            int count = loudness ? 1 : levels?.Channels ?? 1;
            float top = loudness ? 0 : 6;
            float y0 = area.y + 8, height = Mathf.Max(4,area.height - 32);
            float left = area.x + 30;
            float groupWidth = Mathf.Min(area.width - 36, count == 2 ? 58 : 31);
            float width = groupWidth / count;
            float start = left + (area.xMax - left - groupWidth) * .5f;
            var rail = new Color(.10f, .12f, .13f);
            for (int tick = loudness ? 0 : 6; tick >= -60; tick -= 6)
            {
                float y = y0 + (top - tick) / (top + 60) * height;
                GUI.Label(new Rect(area.x,y-8,28,16),tick.ToString(),EditorStyles.miniLabel);
                EditorGUI.DrawRect(new Rect(left,y,area.xMax-left,1),new Color(.29f,.31f,.32f));
            }
            for (int c = 0; c < count; c++)
            {
                double db = double.NaN;
                if (levels != null && levels.Blocks > 0)
                {
                    switch(meterMode)
                    {
                        case MeterMode.SamplePeak: db = levels.Peak[c]; break;
                        case MeterMode.TruePeak: db = levels.TruePeak[c]; break;
                        case MeterMode.Momentary: db = levels.Momentary; break;
                        case MeterMode.ShortTerm: db = levels.ShortTerm; break;
                        case MeterMode.Integrated: db = levels.Integrated(); break;
                    }
                }
                float x = start + c * width + 2;
                float barWidth = Mathf.Max(3, width - 5);
                EditorGUI.DrawRect(new Rect(x,y0,barWidth,height), rail);
                if (!double.IsNaN(db))
                {
                    float size = Mathf.InverseLerp(-60,top,(float)db)*height;
                    float barTop = y0 + height - size;
                    EditorGUI.DrawRect(new Rect(x,barTop,barWidth,size), new Color(.48f,.76f,.48f));
                    if (!loudness)
                    {
                        float warning = y0 + (top + 6) / (top + 60) * height;
                        float overload = y0 + top / (top + 60) * height;
                        if (barTop < warning) EditorGUI.DrawRect(new Rect(x,barTop,barWidth,warning-barTop),new Color(.88f,.70f,.32f));
                        if (barTop < overload) EditorGUI.DrawRect(new Rect(x,barTop,barWidth,overload-barTop),new Color(.90f,.36f,.27f));
                    }
                    if (meterMode == MeterMode.TruePeak)
                    {
                        float hold = Mathf.InverseLerp(-60,top,(float)levels.MaxTruePeak[c]);
                        EditorGUI.DrawRect(new Rect(x,y0+(1-hold)*height,barWidth,2),new Color(.96f,.9f,.69f));
                    }
                }
                string reading = double.IsNaN(db) ? "—" : double.IsNegativeInfinity(db) ? "−∞" : db.ToString("0.0");
                float labelWidth = (area.width - 7) / count;
                float labelX = area.x + c * labelWidth;
                GUI.Label(new Rect(labelX,y0+height+3,labelWidth,18),(count == 2 ? c == 0 ? "L " : "R " : "") + reading,EditorStyles.centeredGreyMiniLabel);
            }
        }
    }
}
