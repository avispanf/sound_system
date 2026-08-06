using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class LiveTweakWindow : EditorWindow
    {
        private ListView listView;
        private Label summaryLabel;
        private List<string> guids = new List<string>();

        [MenuItem("Window/AudioMW/Live Tweaks")]
        public static void Open()
        {
            LiveTweakWindow window = GetWindow<LiveTweakWindow>();
            window.titleContent = new GUIContent("Live Tweaks");
            window.minSize = new Vector2(320f, 200f);
            window.Show();
        }

        public void CreateGUI()
        {
            Label header = new Label("Assets changed during the last play session");
            header.style.paddingLeft = 6f;
            header.style.paddingTop = 6f;
            header.style.whiteSpace = WhiteSpace.Normal;
            rootVisualElement.Add(header);

            listView = new ListView();
            listView.fixedItemHeight = 22f;
            listView.style.flexGrow = 1f;
            listView.makeItem = MakeItem;
            listView.bindItem = BindItem;
            listView.itemsSource = guids;
            rootVisualElement.Add(listView);

            VisualElement footer = new VisualElement();
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.paddingLeft = 6f;
            footer.style.paddingBottom = 6f;

            Button revertAll = new Button(() =>
            {
                LiveTweakTracker.RevertAll();
                Refresh();
            });
            revertAll.text = "Revert all";
            footer.Add(revertAll);

            Button keepAll = new Button(() =>
            {
                LiveTweakTracker.ClearChanges();
                Refresh();
            });
            keepAll.text = "Keep all";
            footer.Add(keepAll);

            rootVisualElement.Add(footer);

            summaryLabel = new Label();
            summaryLabel.style.paddingLeft = 6f;
            summaryLabel.style.paddingBottom = 6f;
            rootVisualElement.Add(summaryLabel);

            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
        }

        private VisualElement MakeItem()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4f;

            Label label = new Label();
            label.name = "name";
            label.style.flexGrow = 1f;
            row.Add(label);

            Button ping = new Button();
            ping.name = "ping";
            ping.text = "Select";
            row.Add(ping);

            Button revert = new Button();
            revert.name = "revert";
            revert.text = "Revert";
            row.Add(revert);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= guids.Count)
            {
                return;
            }

            string guid = guids[index];
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);

            element.Q<Label>("name").text = asset != null ? asset.name : path;

            Button ping = element.Q<Button>("ping");
            ping.clickable = new Clickable(() =>
            {
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
            });

            Button revert = element.Q<Button>("revert");
            revert.clickable = new Clickable(() =>
            {
                LiveTweakTracker.Revert(guid);
                Refresh();
            });
        }

        private void Refresh()
        {
            guids = LiveTweakTracker.ChangedGuids;

            if (listView != null)
            {
                listView.itemsSource = guids;
                listView.Rebuild();
            }

            if (summaryLabel != null)
            {
                summaryLabel.text = guids.Count == 0
                    ? "no changes recorded"
                    : guids.Count + " asset(s) changed in play mode";
            }
        }
    }
}
