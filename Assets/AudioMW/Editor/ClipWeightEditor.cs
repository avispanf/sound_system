using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class ClipWeightEditor : VisualElement
    {
        private readonly SerializedObject serialized;
        private readonly List<FloatField> fields = new List<FloatField>();
        private readonly Label summary = new Label { name = "weight-summary" };
        private string signature;

        public ClipWeightEditor(SerializedObject serialized)
        {
            this.serialized = serialized;
            name = "clip-weight-editor";
            if (serialized.isEditingMultipleObjects)
            {
                Add(new HelpBox("Multiple events selected. Weights use source slots: Audio clips first, then Clip references. Missing weights use 1.", HelpBoxMessageType.Info));
                Add(new PropertyField(serialized.FindProperty("clipWeights"), "Clip weights"));
                return;
            }
            this.TrackSerializedObjectValue(serialized, _ => Refresh());
            Refresh();
        }

        public static float EffectiveWeight(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) || value < 0 ? 0 : value;
        }

        public static void SetWeight(SerializedObject serialized, int index, float value)
        {
            if (index < 0 || serialized.isEditingMultipleObjects) return;
            serialized.Update();
            SoundEvent item = serialized.targetObject as SoundEvent;
            if (item == null || index >= item.TotalClipCount) return;
            SerializedProperty weights = serialized.FindProperty("clipWeights");
            int oldSize = weights.arraySize;
            if (oldSize <= index)
            {
                weights.arraySize = index + 1;
                for (int i = oldSize; i <= index; i++) weights.GetArrayElementAtIndex(i).floatValue = 1;
            }
            weights.GetArrayElementAtIndex(index).floatValue = EffectiveWeight(value);
            serialized.ApplyModifiedProperties();
        }

        private void Refresh()
        {
            SoundEvent item = serialized.targetObject as SoundEvent;
            if (item == null) return;
            List<string> names = new List<string>();
            for (int i = 0; i < item.Clips.Length; i++) names.Add("Clip " + (i + 1) + " · " + (item.Clips[i] != null ? item.Clips[i].name : "Missing clip"));
            for (int i = 0; i < item.ClipReferences.Length; i++)
            {
                ClipReference reference = item.ClipReferences[i];
                names.Add("Reference " + (i + 1) + " · " + (reference == null ? "Missing reference" : reference.DirectClip != null ? reference.DirectClip.name : string.IsNullOrWhiteSpace(reference.Address) ? "Missing source" : reference.Address));
            }
            string current = string.Join("\n", names);
            if (signature != current)
            {
                signature = current; Clear(); fields.Clear();
                Add(new Label("Source weights") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
                Add(new HelpBox("Weights belong to source slots. After reordering clips, check their weights. Missing weights use 1; zero disables a slot.", HelpBoxMessageType.Info));
                for (int i = 0; i < names.Count; i++)
                {
                    int index = i;
                    FloatField field = new FloatField(names[i]) { name = "weight-" + i, isDelayed = true, tooltip = names[i] + "\nRelative selection weight. Zero disables this source slot." };
                    field.AddToClassList(BaseField<float>.alignedFieldUssClassName);
                    field.RegisterValueChangedCallback(evt => { SetWeight(serialized, index, evt.newValue); Refresh(); });
                    fields.Add(field); Add(field);
                }
                summary.style.whiteSpace = WhiteSpace.Normal; Add(summary);
            }
            double total = 0;
            for (int i = 0; i < fields.Count; i++)
            {
                float weight = i < item.ClipWeights.Length ? item.ClipWeights[i] : 1;
                fields[i].SetValueWithoutNotify(weight);
                total += EffectiveWeight(weight);
            }
            summary.text = fields.Count == 0 ? "Add source clips to set their weights." : total <= 0 ? "All source slots are disabled. This event cannot select a clip." : "Total relative weight: " + total.ToString("0.###");
        }
    }
}
