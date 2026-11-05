using UnityEditor;
using UnityEngine;

namespace AudioMW.Editor
{
    [CustomEditor(typeof(AudioBankScope))]
    [CanEditMultipleObjects]
    public sealed class AudioBankScopeInspector : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() { return Application.isPlaying; }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("banks"));
            serializedObject.ApplyModifiedProperties();
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Banks load while this component is enabled. Shared banks stay loaded until their last owner leaves. Wait for IsReady before starting addressed events.", MessageType.Info);
                return;
            }
            if (serializedObject.isEditingMultipleObjects) return;
            AudioBankScope scope = (AudioBankScope)target;
            if (!scope.isActiveAndEnabled)
            {
                EditorGUILayout.LabelField("Audio scope", "Disabled");
                return;
            }
            if (scope.IsReady)
            {
                EditorGUILayout.LabelField("Audio scope", "Ready");
                return;
            }
            foreach (SoundBank bank in scope.Banks)
            {
                if (bank == null) continue;
                if (bank.PendingStreamCount > 0)
                {
                    EditorGUILayout.LabelField("Audio scope", "Loading audio…");
                    return;
                }
            }
            EditorGUILayout.HelpBox("Audio is not ready. Check the asset loader, bank addresses and clip loading state. Disable and enable this scope to retry after resolving a loading failure.", MessageType.Warning);
        }
    }
}
