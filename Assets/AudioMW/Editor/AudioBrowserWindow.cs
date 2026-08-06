using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AudioMW.Editor
{
    public sealed class AudioBrowserWindow : EditorWindow
    {
        private ListView listView;
        private ToolbarSearchField searchField;
        private Label summaryLabel;

        private readonly List<Object> allAssets = new List<Object>();
        private readonly List<Object> filtered = new List<Object>();

        [MenuItem("Window/AudioMW/Audio Browser")]
        public static void Open()
        {
            AudioBrowserWindow window = GetWindow<AudioBrowserWindow>();
            window.titleContent = new GUIContent("Audio Browser");
            window.minSize = new Vector2(280f, 240f);
            window.Show();
        }

        private void OnEnable()
        {
            Refresh();
        }

        private void OnFocus()
        {
            Refresh();
            RebuildList();
        }

        public void CreateGUI()
        {
            Toolbar toolbar = new Toolbar();

            searchField = new ToolbarSearchField();
            searchField.style.flexGrow = 1f;
            searchField.RegisterValueChangedCallback(evt => ApplyFilter(evt.newValue));
            toolbar.Add(searchField);

            ToolbarButton refreshButton = new ToolbarButton(() =>
            {
                Refresh();
                RebuildList();
            });
            refreshButton.text = "Refresh";
            toolbar.Add(refreshButton);

            rootVisualElement.Add(toolbar);

            listView = new ListView();
            listView.fixedItemHeight = 20f;
            listView.selectionType = SelectionType.Single;
            listView.style.flexGrow = 1f;
            listView.makeItem = MakeItem;
            listView.bindItem = BindItem;
            listView.itemsSource = filtered;
            listView.selectionChanged += OnSelectionChanged;
            rootVisualElement.Add(listView);

            summaryLabel = new Label();
            summaryLabel.style.paddingLeft = 6f;
            summaryLabel.style.paddingTop = 2f;
            summaryLabel.style.paddingBottom = 4f;
            rootVisualElement.Add(summaryLabel);

            Refresh();
            RebuildList();
        }

        private static VisualElement MakeItem()
        {
            VisualElement row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = 4f;

            Label kind = new Label();
            kind.name = "kind";
            kind.style.width = 58f;
            kind.style.opacity = 0.6f;
            row.Add(kind);

            Label label = new Label();
            label.name = "name";
            label.style.flexGrow = 1f;
            row.Add(label);

            return row;
        }

        private void BindItem(VisualElement element, int index)
        {
            if (index < 0 || index >= filtered.Count)
            {
                return;
            }

            Object asset = filtered[index];
            element.Q<Label>("kind").text = KindOf(asset);
            element.Q<Label>("name").text = asset != null ? asset.name : "(missing)";
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            foreach (object item in selection)
            {
                Object asset = item as Object;
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }

                break;
            }
        }

        private static string KindOf(Object asset)
        {
            if (asset is SoundEvent)
            {
                return "event";
            }

            if (asset is SoundBank)
            {
                return "bank";
            }

            if (asset is SoundParameter)
            {
                return "param";
            }

            return "asset";
        }

        private void Refresh()
        {
            allAssets.Clear();
            Collect<SoundEvent>();
            Collect<SoundBank>();
            Collect<SoundParameter>();
        }

        private void Collect<T>() where T : Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).Name);

            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                T asset = AssetDatabase.LoadAssetAtPath<T>(path);

                if (asset != null)
                {
                    allAssets.Add(asset);
                }
            }
        }

        private void ApplyFilter(string query)
        {
            filtered.Clear();

            bool empty = string.IsNullOrEmpty(query);

            for (int i = 0; i < allAssets.Count; i++)
            {
                Object asset = allAssets[i];
                if (asset == null)
                {
                    continue;
                }

                if (empty || asset.name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    filtered.Add(asset);
                }
            }

            if (listView != null)
            {
                listView.itemsSource = filtered;
                listView.Rebuild();
            }

            if (summaryLabel != null)
            {
                summaryLabel.text = filtered.Count + " of " + allAssets.Count + " assets";
            }
        }

        private void RebuildList()
        {
            ApplyFilter(searchField != null ? searchField.value : string.Empty);
        }
    }
}
