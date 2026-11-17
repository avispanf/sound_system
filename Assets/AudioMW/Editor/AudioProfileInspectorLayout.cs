using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;

namespace AudioMW.Editor
{
    internal static class AudioProfileInspectorLayout
    {
        public static void Configure(VisualElement root, UnityEngine.Object asset, SerializedObject serialized)
        {
            root.userData = asset;
            Undo.UndoRedoCallback synchronizeUndo = () =>
            {
                if (root.panel == null || serialized.targetObject == null) return;
                serialized.Update();
                root.Unbind();
                root.Bind(serialized);
            };
            root.RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += synchronizeUndo);
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= synchronizeUndo);
            AudioWorkspace.Style(root);
            root.AddToClassList("audiomw-inspection");
            root.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                bool narrow = evt.newRect.width < 380;
                root.EnableInClassList("audiomw-inspector-narrow", narrow);
                root.Query<VisualElement>(className: narrow ? "unity-base-field__aligned" : "audiomw-stacked-native-field").ForEach(field =>
                {
                    field.EnableInClassList("audiomw-stacked-native-field", narrow);
                    field.EnableInClassList("unity-base-field__aligned", !narrow);
                    Label label = field.Q<Label>(className: "unity-base-field__label");
                    if (label == null) return;
                    label.style.width = narrow ? StyleKeyword.Auto : StyleKeyword.Null;
                    label.style.minWidth = narrow ? new StyleLength(0f) : new StyleLength(StyleKeyword.Null);
                });
            });
            root.AddToClassList("audiomw-profile-inspector");
        }
    }
}
