using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.ShortcutManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public sealed partial class AudioEventEditorWindow : EditorWindow
    {
        [Serializable] public sealed class Document
        {
            public string Guid;
            public bool Pinned;
            public float Zoom;
            public bool FitTimeline=true;
            public float LaneHeight=56;
            public bool FitLanes=true;
            public bool FollowPlayhead=true;
            public double Cursor;
            public Vector2 Scroll;
            public string Selection = "";
        }
        [SerializeField] private List<Document> documents = new List<Document>();
        [SerializeField] private int documentIndex = -1;
        [SerializeField] private int browserTab;
        [SerializeField] private string[] queries = { "", "", "", "" };
        [SerializeField] private bool overviewOpen = true;
        [SerializeField] private float deckHeight=240;
        private bool browserRefreshing,drawerOpen;
        private Vector2 responsiveSize=new Vector2(-1,-1);
        private TwoPaneSplitView mainSplit;
        private VisualElement libraryPanel;
        [SerializeField] private string mode = "Author";
        [SerializeField] private string view = "Container";
        private readonly List<Object> browserAssets = new List<Object>();
        private Object deckTarget;
        [Serializable] private sealed class DependentEdit { public string Document; public Object Target; }
        [SerializeField] private List<DependentEdit> documentEdits=new List<DependentEdit>();
        private SerializedObject deckSerialized;
        private TreeView browser;
        private readonly Dictionary<int,List<int>> browserExpansion=new Dictionary<int,List<int>>();
        private readonly List<int> browserFolders=new List<int>();
        private int builtBrowserTab=-1;
        private VisualElement center, overview, deck, canvasHost, bottom;
        private Toolbar documentTabs;
        private Label sessionLabel, breadcrumb;
        private IMGUIContainer canvas;
        private ScrollView deckScroll;
        private ToolbarSearchField search;
        private string bottomTab = "";
        private string notice = "";
        private readonly HashSet<string> selectedClips = new HashSet<string>();
        private string selectedLane = "";
        private int snap = 1;
        private bool bars = true;
        private bool compactOverviewOpen;
        private Vector2 browserPointer;
        private bool browserDragArmed;
        private string documentsStamp;
        private MusicArrangement legacyProjection;
        private MusicTrack projectionSource;
        private int projectionVersion;
        public Object CurrentAsset => documentIndex >= 0 && documentIndex < documents.Count ? AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(documents[documentIndex].Guid)) : null;
        private Document CurrentDocument => documentIndex >= 0 && documentIndex < documents.Count ? documents[documentIndex] : null;
        public int BrowserTab => browserTab;
        public Object DeckTarget => deckTarget;
        [Shortcut("AudioMW/Save document",typeof(AudioEventEditorWindow),KeyCode.S,ShortcutModifiers.Action)]
        private static void SaveShortcut(ShortcutArguments args){((AudioEventEditorWindow)args.context).SaveDocument();}
        [Shortcut("AudioMW/Play or stop audition",typeof(AudioEventEditorWindow),KeyCode.Space)]
        private static void TransportShortcut(ShortcutArguments args)
        {
            var window=(AudioEventEditorWindow)args.context;
            if(!window.IsTransportKeyBlocked(window.rootVisualElement.focusController.focusedElement))window.ToggleTransport();
        }
        [Shortcut("AudioMW/Return to entry",typeof(AudioEventEditorWindow),KeyCode.Home)]
        private static void EntryShortcut(ShortcutArguments args)
        {
            var window=(AudioEventEditorWindow)args.context;
            if(!window.IsTransportKeyBlocked(window.rootVisualElement.focusController.focusedElement))window.ReturnToEntry();
        }
        [MenuItem("Window/AudioMW/Event Editor", false, 0)]
        public static void Open() { var window = GetWindow<AudioEventEditorWindow>(); window.titleContent = new GUIContent("AudioMW | Event Editor"); window.minSize = new Vector2(760,520); window.Show(); }
        private void OnEnable() { titleContent = new GUIContent("AudioMW | Event Editor"); Undo.undoRedoPerformed += OnUndo; EditorApplication.playModeStateChanged += OnPlayState; EditorApplication.update += UpdateSession; }
        private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; EditorApplication.playModeStateChanged -= OnPlayState; EditorApplication.update -= UpdateSession; string pending=pendingAuditionGuid; StopAudition(); pendingAuditionGuid=pending; ReleaseLive(); capture?.Dispose(); capture=null; deckSerialized?.Dispose(); if(legacyProjection!=null)DestroyImmediate(legacyProjection); }
        private void OnUndo() { ShowDeck(deckTarget); RefreshDocuments(); canvas?.MarkDirtyRepaint(); }
        private void OnPlayState(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.ExitingPlayMode||state==PlayModeStateChange.ExitingEditMode)
            {
                string pending=state==PlayModeStateChange.ExitingEditMode?pendingAuditionGuid:"";
                StopAudition();ReleaseLive();pendingAuditionGuid=pending;
            }
            if(state==PlayModeStateChange.EnteredPlayMode&&pendingAuditionGuid!="")
            {
                string guid=pendingAuditionGuid;pendingAuditionGuid="";
                EditorApplication.delayCall+=()=>{if(this!=null&&EditorApplication.isPlaying&&CurrentAsset!=null&&AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(CurrentAsset))==guid)PlayAudition();};
            }
        }
        private void OnProjectChange() { RefreshBrowser(); RefreshDocuments(); }
        public void CreateGUI()
        {
            if(browser!=null&&browser.viewController!=null&&builtBrowserTab>=0)browserExpansion[builtBrowserTab]=browserFolders.Where(id=>browser.IsExpanded(id)).ToList();
            builtBrowserTab=-1;browserFolders.Clear();
            responsiveSize=new Vector2(-1,-1);rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement); rootVisualElement.AddToClassList("audiomw-event-editor-v2");
            var top = new Toolbar { name="studio-v2-toolbar" }; top.Add(new Label("AudioMW | Event Editor"));
            top.Add(new ToolbarButton(()=>{drawerOpen=!drawerOpen;UpdateCompactLayout();}){text="Browser",name="studio-browser-toggle"});
            top.Add(new ToolbarSpacer { flex=true }); top.Add(new ToolbarButton(() => StudioMixerWindow.Open(CurrentAsset, selectedLane)) { text="Mixer" });
            var tools = new ToolbarMenu { text="Tools" }; tools.menu.AppendAction("Review agent proposals",_=>AudioAgentReviewWindow.Open()); tools.menu.AppendAction("Project setup",_=>AudioProjectSetupWindow.Open()); tools.menu.AppendAction("Reset layout",_=>{ overviewOpen=true;compactOverviewOpen=false;deckHeight=240;drawerOpen=false; CreateGUI(); }); top.Add(tools);
            top.Add(new ToolbarButton(SaveDocument) { text="Save",name="studio-save" }); rootVisualElement.Add(top);
            var main = new TwoPaneSplitView(0,260,TwoPaneSplitViewOrientation.Horizontal) { viewDataKey="AudioMW.V2.BrowserWidth",name="studio-v2-main" }; mainSplit=main;main.style.flexGrow=1; rootVisualElement.Add(main);
            var library = new VisualElement { name="studio-v2-browser" }; library.style.minWidth=220; library.style.maxWidth=360; main.Add(library);libraryPanel=library;
            var tabs = new Toolbar{name="studio-browser-tabs"}; string[] names={"Events","Banks","Assets","Parameters"};
            for(int i=0;i<4;i++) { int index=i; tabs.Add(new ToolbarButton(()=>SwitchBrowser(index)) {text=names[i],name="browser-tab-"+i}); } library.Add(tabs);
            search=new ToolbarSearchField(); search.SetValueWithoutNotify(queries[browserTab]); search.RegisterValueChangedCallback(e=>{queries[browserTab]=e.newValue;RefreshBrowser();}); library.Add(search);
            browser=new TreeView { fixedItemHeight=24,selectionType=SelectionType.Multiple,name="studio-v2-library",viewDataKey="AudioMW.Studio.Library" }; browser.style.flexGrow=1;
            browser.makeItem=()=>{var row=new VisualElement();row.AddToClassList("studio-browser-row");row.Add(new Image());row.Add(new Label());return row;};
            browser.bindItem=(e,i)=>{var asset=browser.GetItemDataForIndex<Object>(i);e.Q<Image>().image=AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(asset));e.Q<Label>().text=asset.name;e.tooltip=AssetDatabase.GetAssetPath(asset);};
            browser.selectionChanged+=items=>{if(browserRefreshing)return;foreach(Object item in items){if(item is DefaultAsset)continue;if(browserTab==0)OpenDocument(item,false);else ShowDeck(item);break;}};
            browser.itemsChosen+=items=>{foreach(Object item in items){if(item is DefaultAsset)continue;OpenDocument(item,true);break;}};
            browser.RegisterCallback<PointerDownEvent>(e=>{browserPointer=e.position;browserDragArmed=e.button==0;},TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(e=>browserDragArmed=false,TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerMoveEvent>(e=>
            {
                if(!browserDragArmed||(e.pressedButtons&1)==0||browser.selectedItem==null||Vector2.Distance(browserPointer,e.position)<8)return;
                browserDragArmed=false;if(!browser.selectedItems.OfType<Object>().Any(a=>!(a is DefaultAsset)))return;DragAndDrop.PrepareStartDrag();DragAndDrop.objectReferences=browser.selectedItems.OfType<Object>().Where(a=>!(a is DefaultAsset)).ToArray();DragAndDrop.StartDrag("AudioMW assets");e.StopPropagation();
            },TrickleDown.TrickleDown); library.Add(browser);
            var summary = new VisualElement { name = "studio-browser-summary" };
            browserAssetTitle = new Label { name = "studio-summary-name" };
            browserAssetDetail = new Label { name = "studio-summary-detail" };
            summary.Add(browserAssetTitle);
            summary.Add(browserAssetDetail);
            library.Add(summary);
            var newMenu=new ToolbarMenu{text="+ New"}; foreach(string type in new[]{"Random / Sequence","Layered","Blend","Switch","Music Track","Music Arrangement","Bank","Parameter"}) {string kind=type;newMenu.menu.AppendAction(kind,_=>CreateAsset(kind));} library.Add(newMenu);
            center=new VisualElement{name="studio-v2-center"};center.style.minWidth=480;center.style.flexGrow=1;main.Add(center);
            documentsStamp=null;documentTabs=new Toolbar{name="studio-v2-documents"};center.Add(documentTabs);
            BuildTransport(center);
            var canvasRow=new VisualElement{name="studio-v2-canvas-row"};canvasRow.style.flexDirection=FlexDirection.Row;canvasRow.style.flexGrow=1;canvasRow.style.minHeight=160;center.Add(canvasRow);
            canvasHost=new VisualElement{name="studio-v2-canvas"};canvasHost.style.flexGrow=1;canvasHost.style.minWidth=0;canvasRow.Add(canvasHost);
            canvas=new IMGUIContainer(DrawCanvas){focusable=true};canvas.style.flexGrow=1;canvasHost.Add(canvas);
            overview=new VisualElement{name="studio-v2-overview"};canvasRow.Add(overview);
            var divider=new VisualElement{name="studio-deck-resize"};divider.style.height=5;divider.style.flexShrink=0;center.Add(divider);
            float resizeY=0,resizeHeight=0;
            divider.RegisterCallback<PointerDownEvent>(e=>{if(bottomTab!="")return;resizeY=e.position.y;resizeHeight=deck.resolvedStyle.height;divider.CapturePointer(e.pointerId);e.StopPropagation();});
            divider.RegisterCallback<PointerMoveEvent>(e=>{if(!divider.HasPointerCapture(e.pointerId))return;deckHeight=Mathf.Clamp(resizeHeight+resizeY-e.position.y,160,Mathf.Max(160,position.height-430));deck.style.height=deckHeight;});
            divider.RegisterCallback<PointerUpEvent>(e=>divider.ReleasePointer(e.pointerId));
            deck=new VisualElement{name="studio-v2-deck"};deck.style.height=deckHeight;deck.style.minHeight=160;center.Add(deck);
            breadcrumb=new Label("Select an event, track or clip");deck.Add(breadcrumb);
            deckScroll=new ScrollView();deckScroll.style.flexGrow=1;deck.Add(deckScroll);var deckGui=new IMGUIContainer(DrawDeck);deckGui.style.width=Length.Percent(100);deckScroll.contentContainer.style.width=Length.Percent(100);deckScroll.Add(deckGui);
            var dockTabs=new Toolbar{name="studio-v2-dock-tabs"};foreach(string label in new[]{"Profiler","Issues","Live changes"}){string tab=label;dockTabs.Add(new ToolbarButton(()=>{bottomTab=bottomTab==tab?"":tab;RefreshDock();}){text=label});}center.Add(dockTabs);
            bottom=new VisualElement{name="studio-v2-dock"};center.Add(bottom);
            rootVisualElement.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);rootVisualElement.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            rootVisualElement.RegisterCallback<KeyDownEvent>(e=>
            {
                if(IsTransportKeyBlocked(e.target))return;
                if(e.actionKey&&e.keyCode==KeyCode.S){SaveDocument();e.StopPropagation();}
                else if(e.keyCode==KeyCode.Escape)CancelDrag();
            },TrickleDown.TrickleDown);
            RefreshBrowser();RefreshDocuments();RestoreDocumentSelection();ShowDeck(CurrentAsset);RefreshDock();RefreshTransport();RefreshOverview();
        }
        private void OnGeometryChanged(GeometryChangedEvent e){UpdateCompactLayout();}
        private void UpdateCompactLayout()
        {
            if(mainSplit==null)return;
            if(responsiveSize!=position.size)
            {
                responsiveSize=position.size;
                if(overview!=null)overview.style.display=(position.width >= 1400 ? overviewOpen : compactOverviewOpen) ?DisplayStyle.Flex:DisplayStyle.None;
                if(deck!=null&&bottomTab=="")deck.style.height=Mathf.Clamp(deckHeight,160,position.height<850?200:320);
                mainSplit.fixedPaneInitialDimension=position.width<1400?220:260;
            }
            bool compact=position.width<1000;
            var toggle=rootVisualElement.Q<ToolbarButton>("studio-browser-toggle");if(toggle!=null)toggle.style.display=compact?DisplayStyle.Flex:DisplayStyle.None;
            if(compact&&!drawerOpen)mainSplit.CollapseChild(0);else mainSplit.UnCollapse();
            if(libraryPanel!=null)libraryPanel.style.minWidth=220;
        }
        public void SwitchBrowser(int index) {browserTab=Mathf.Clamp(index,0,3);search?.SetValueWithoutNotify(queries[browserTab]);EditorAudioPreview.Stop();RefreshBrowser();}
        private void RefreshBrowser()
        {
            if(browser==null)return;for(int i=0;i<4;i++)rootVisualElement.Q<ToolbarButton>("browser-tab-"+i)?.EnableInClassList("studio-v2-active",i==browserTab);browserRefreshing=true;if(builtBrowserTab>=0)browserExpansion[builtBrowserTab]=browserFolders.Where(id=>browser.IsExpanded(id)).ToList();browserFolders.Clear();var selected=browser.selectedItems.OfType<Object>().Where(a=>a!=null).Select(a=>a.GetInstanceID()).ToList();browserAssets.Clear();string[] types=browserTab==0?new[]{"SoundEvent","MusicTrack","MusicArrangement","MusicStateProfile","VoiceLine"}:new[]{browserTab==1?"SoundBank":browserTab==2?"AudioClip":"SoundParameter"};
            foreach(string type in types)foreach(string guid in AssetDatabase.FindAssets("t:"+type)){var asset=AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));if(asset!=null&&asset.name.IndexOf(queries[browserTab],StringComparison.OrdinalIgnoreCase)>=0)browserAssets.Add(asset);}
            browserAssets.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));
            browser.SetRootItems(BuildLibraryTree("",browserAssets));browser.Rebuild();
            if(browserExpansion.TryGetValue(browserTab,out var expanded)&&string.IsNullOrEmpty(queries[browserTab])){foreach(int id in expanded)browser.ExpandItem(id);}else browser.ExpandAll();builtBrowserTab=browserTab;
            if(browserTab==0&&CurrentAsset!=null&&browserAssets.Contains(CurrentAsset)){browser.SetSelectionById(CurrentAsset.GetInstanceID());}else browser.SetSelectionById(selected);browserRefreshing=false;
        }
        private List<TreeViewItemData<Object>> BuildLibraryTree(string parent,List<Object> assets)
        {
            var result=new List<TreeViewItemData<Object>>();
            string prefix=parent.Length==0?"":parent+"/";
            var groups=assets.GroupBy(a=>AssetDatabase.GetAssetPath(a).Substring(prefix.Length).Split('/')[0]).OrderBy(g=>g.Key,StringComparer.OrdinalIgnoreCase);
            foreach(var group in groups)
            {
                string path=prefix+group.Key;
                if(AssetDatabase.IsValidFolder(path))
                {
                    var folder=AssetDatabase.LoadAssetAtPath<Object>(path);browserFolders.Add(folder.GetInstanceID());
                    result.Add(new TreeViewItemData<Object>(folder.GetInstanceID(),folder,BuildLibraryTree(path,group.ToList())));
                }
                else foreach(var asset in group)result.Add(new TreeViewItemData<Object>(asset.GetInstanceID(),asset));
            }
            return result;
        }
        public void OpenDocument(Object asset,bool pin=true)
        {
            if(asset==null)return;string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));if(string.IsNullOrEmpty(guid))return;
            if(CurrentDocument==null||CurrentDocument.Guid!=guid)StopAudition();
            int index=documents.FindIndex(x=>x.Guid==guid);
            if(index<0){if(CurrentDocument!=null&&!CurrentDocument.Pinned&&CurrentAsset!=null&&!EditorUtility.IsDirty(CurrentAsset)){index=documentIndex;documents[index]=new Document{Guid=guid,Pinned=pin};}else{documents.Add(new Document{Guid=guid,Pinned=pin});index=documents.Count-1;}}
            else if(pin)documents[index].Pinned=true;
            if (browserTab == 0 && browser != null && browserAssets.Contains(asset) && !browser.selectedItems.OfType<Object>().Contains(asset))
            {
                browserRefreshing = true;
                try { browser.SetSelectionById(asset.GetInstanceID()); }
                finally { browserRefreshing = false; }
            }
            documentIndex=index;selectedClips.Clear();selectedLane="";Selection.activeObject=asset;view=asset is MusicTrack||asset is MusicArrangement?"Timeline":asset is SoundParameter?"Parameters":asset is MusicStateProfile?"Transitions":"Container";EditorAudioPreview.Stop();RestoreDocumentSelection();ShowDeck(asset);RefreshDocuments();RefreshOverview();RefreshTransport();
        }
        private void RestoreDocumentSelection()
        {
            selectedClips.Clear();selectedLane="";
            if(!(CurrentAsset is MusicArrangement score)||CurrentDocument==null)return;
            string id=CurrentDocument.Selection;
            foreach(var lane in score.Lanes)
            {
                if(id=="lane:"+lane.Id){selectedLane=lane.Id;return;}
                if(lane.Clips.Any(c=>c.Id==id)){selectedLane=lane.Id;selectedClips.Add(id);return;}
            }
        }
        private void RefreshDocuments()
        {
            if(documentTabs==null)return;
            string stamp=documentIndex+string.Join("|",documents.Select(d=>{var a=AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(d.Guid));return d.Guid+":"+(a!=null?a.name:"")+":"+(a!=null&&(EditorUtility.IsDirty(a)||documentEdits.Any(e=>e.Document==d.Guid&&e.Target!=null&&EditorUtility.IsDirty(e.Target))))+":"+d.Pinned;}));
            if(documentsStamp==stamp)return;documentsStamp=stamp;documentTabs.Clear();
            for(int i=0;i<documents.Count;i++){int index=i;var asset=AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(documents[i].Guid));if(asset==null)continue;if(EditorUtility.IsDirty(asset))documents[i].Pinned=true;
                var tabGroup=new VisualElement();tabGroup.AddToClassList("studio-document-tab");tabGroup.EnableInClassList("studio-v2-active",index==documentIndex);documentTabs.Add(tabGroup);
                var b=new ToolbarButton(()=>OpenDocument(asset,documents[index].Pinned)){text=asset.name+(EditorUtility.IsDirty(asset)||documentEdits.Any(e=>e.Document==documents[index].Guid&&e.Target!=null&&EditorUtility.IsDirty(e.Target))?" •":""),tooltip=AssetDatabase.GetAssetPath(asset)};b.EnableInClassList("studio-v2-active",index==documentIndex);tabGroup.Add(b);
                tabGroup.Add(new ToolbarButton(()=>{StopAudition();documents.RemoveAt(index);if(index<documentIndex)documentIndex--;documentIndex=Math.Min(documentIndex,documents.Count-1);selectedClips.Clear();selectedLane="";ShowDeck(CurrentAsset);RefreshDocuments();RefreshOverview();}){text="×"});}
        }
        private void TrackEdit(Object target)
        {
            if(target==null||CurrentDocument==null)return;
            if(!documentEdits.Any(e=>e.Document==CurrentDocument.Guid&&e.Target==target))documentEdits.Add(new DependentEdit{Document=CurrentDocument.Guid,Target=target});
            CurrentDocument.Pinned=true;
            if (browserAssetDetail != null) browserAssetDetail.text = AssetSummary(deckTarget);
            RefreshOverview();
        }
        public void SaveDocument()
        {
            if(CurrentAsset is MusicArrangement score&&score.Validate()!=""){notice=score.Validate();return;}
            if(CurrentDocument!=null){foreach(var edit in documentEdits.Where(e=>e.Document==CurrentDocument.Guid))if(edit.Target!=null)AssetDatabase.SaveAssetIfDirty(edit.Target);documentEdits.RemoveAll(e=>e.Document==CurrentDocument.Guid);}
            if(deckTarget!=null)AssetDatabase.SaveAssetIfDirty(deckTarget);
            if(CurrentAsset!=null)AssetDatabase.SaveAssetIfDirty(CurrentAsset);RefreshDocuments();
        }
        private Label browserAssetTitle;
        private Label browserAssetDetail;
        private static string AssetSummary(Object target)
        {
            if (target is MusicArrangement score)
                return score.Lanes.Count + " tracks  ·  " + score.Tempo + " BPM  ·  " + (score.Exit - score.Entry).ToString("0.##") + " s";
            if (target is AudioClip clip) return AudioSourcePreviewPane.SourceFormat(clip);
            if (target is SoundEvent sound && sound.ContainerMode == ContainerMode.Simple)
                return AudioEventContextPane.SelectionLabel(sound.SelectionMode) + "  ·  " + sound.TotalClipCount + " sources";
            if (target is SoundEvent container) return ObjectNames.NicifyVariableName(container.ContainerMode.ToString());
            return target != null ? ObjectNames.NicifyVariableName(target.GetType().Name) : "";
        }

        private void ShowDeck(Object target)
        {
            deckSerialized?.Dispose();deckSerialized=null;deckTarget=target;deckSourceIndex=-1;deckChildParent=null;deckChildIndex=-1;
            if(target!=null)deckSerialized=new SerializedObject(target);
            if (browserAssetTitle != null)
            {
                var path = target != null ? AssetDatabase.GetAssetPath(target) : "";
                browserAssetTitle.parent.style.display = target == null ? DisplayStyle.None : DisplayStyle.Flex;
                browserAssetTitle.text = target != null ? target.name : "";
                browserAssetTitle.tooltip = path;
                browserAssetDetail.text = AssetSummary(target);
                browserAssetDetail.tooltip = path;
            }
            if(breadcrumb!=null)breadcrumb.text=target==CurrentAsset?(target!=null?target.name:"Select an object"):(CurrentAsset!=null?CurrentAsset.name:"No event")+"  ›  "+(target!=null?target.name:"Select an object");
            if(breadcrumb!=null&&target==CurrentAsset)
            {
                if(target is MusicTrack music&&selectedLane.StartsWith("legacy:")&&int.TryParse(selectedLane.Substring(7),out int index))
                {
                    int layer=LegacyLayerIndex(music,index);
                    if(layer>=0)breadcrumb.text+="  ›  "+music.Layers[layer].Name+"  ›  "+music.Layers[layer].Clip.name;
                }
                else if(target is MusicArrangement score)
                {
                    var lane=score.Lanes.FirstOrDefault(x=>x.Id==selectedLane);
                    if(lane!=null){breadcrumb.text+="  ›  "+lane.Name;var clip=lane.Clips.FirstOrDefault(x=>selectedClips.Contains(x.Id));if(clip?.Source!=null)breadcrumb.text+="  ›  "+clip.Source.name;}
                }
                breadcrumb.tooltip=breadcrumb.text;
            }
        }
        private void CreateAsset(string kind)
        {
            Object asset;
            if(kind=="Music Arrangement")asset=CreateInstance<MusicArrangement>();else if(kind=="Music Track")asset=CreateInstance<MusicTrack>();else if(kind=="Bank")asset=CreateInstance<SoundBank>();else if(kind=="Parameter")asset=CreateInstance<SoundParameter>();else{var item=CreateInstance<SoundEvent>();item.Clips=browser.selectedItems.OfType<AudioClip>().ToArray();if(kind!="Random / Sequence")item.ContainerMode=(ContainerMode)Enum.Parse(typeof(ContainerMode),kind);asset=item;}
            string path=EditorUtility.SaveFilePanelInProject("Create "+kind,"New "+kind.Replace(" / "," "),"asset","Choose the asset location.");
            if(string.IsNullOrEmpty(path)){DestroyImmediate(asset);return;}AssetDatabase.CreateAsset(asset,path);Undo.RegisterCreatedObjectUndo(asset,"Create audio document");RefreshBrowser();OpenDocument(asset);
        }
        private void OverviewRow(string title,string value,Action action=null)
        {
            var row=new VisualElement();row.AddToClassList("studio-overview-row");var key = new Label(title); key.AddToClassList("studio-overview-key"); row.Add(key);
            if(action==null)row.Add(new Label(value));else row.Add(new Button(action){text=value});
            overview.Add(row);
        }
        private void RefreshOverview()
        {
            if(overview==null)return;overview.Clear();var heading=new Label("Overview");heading.AddToClassList("studio-section-heading");overview.Add(heading);
            var item=CurrentAsset;if(item==null)return;
            OverviewRow("Document",item.name,()=>{ShowDeck(item);Selection.activeObject=item;});
            if(item is SoundEvent sound)
            {
                OverviewRow("Output",sound.MixerGroup!=null?sound.MixerGroup.name:"Unity output",()=>StudioMixerWindow.Open(item, selectedLane));
                var banks=AudioSourceCatalog.BanksFor(sound);if(banks.Count==0)OverviewRow("Bank","Not assigned");
                foreach(var bank in banks)OverviewRow("Bank",bank.name,()=>{ShowDeck(bank);Selection.activeObject=bank;});
                float blend=sound.AttenuationPreset!=null?sound.AttenuationPreset.SpatialBlend:sound.SpatialBlend;
                OverviewRow("Spatial",blend<=0?"2D":blend>=1?"3D":"2D / 3D blend",()=>{ShowDeck(sound);deckSection="Spatial";});
                OverviewRow("Limits",sound.MaxInstancesPerEmitter>0?sound.MaxInstancesPerEmitter+" / emitter":"No emitter limit",()=>{ShowDeck(sound);deckSection="Limits";});
                OverviewRow("Selection",AudioEventContextPane.SelectionLabel(sound.SelectionMode));
            }
            if(item is MusicTrack track){OverviewRow("Output",track.MixerGroup!=null?track.MixerGroup.name:"Unity output",()=>StudioMixerWindow.Open(item, selectedLane));OverviewRow("Model","Intro / body + layers");OverviewRow("Layers",track.Layers.Length.ToString());}
            if(item is MusicArrangement arrangement){OverviewRow("Output",arrangement.Output!=null?arrangement.Output.name:"Unity output",()=>StudioMixerWindow.Open(item, selectedLane));OverviewRow("Tracks",arrangement.Lanes.Count.ToString());OverviewRow("Tempo",arrangement.Tempo+" BPM");OverviewRow("Edits","Apply on restart");}
            var sources=AudioSourceCatalog.Collect(item);OverviewRow("Sources",(item is MusicArrangement score?score.Lanes.Sum(l=>l.Clips.Count):sources.Count).ToString());
            if (item is MusicArrangement music)
            {
                var preview = new IMGUIContainer(() =>
                {
                    var lane = music.Lanes.FirstOrDefault(candidate => candidate.Id == selectedLane);
                    var clip = lane?.Clips.FirstOrDefault(candidate => selectedClips.Contains(candidate.Id));
                    if (selectedClips.Count != 1 || clip?.Source == null)
                    {
                        EditorGUILayout.LabelField("Select a clip to see its source waveform.", EditorStyles.wordWrappedMiniLabel);
                        return;
                    }
                    GUILayout.Label(new GUIContent(clip.Source.name, AssetDatabase.GetAssetPath(clip.Source)), EditorStyles.boldLabel);
                    Rect plot = GUILayoutUtility.GetRect(24, 48, GUILayout.ExpandWidth(true));
                    EditorGUI.DrawRect(plot, new Color(.11f,.14f,.16f));
                    if (!WaveformCache.Draw(clip.Source, plot, verticalScale: 0, combineChannels: true))
                        GUI.Label(plot, "Source PCM unavailable", EditorStyles.centeredGreyMiniLabel);
                    EditorGUILayout.LabelField("Source  ·  " + clip.Source.length.ToString("0.###") + " s", EditorStyles.miniLabel);
                });
                overview.Add(preview);
            }
            var path=new Label(AssetDatabase.GetAssetPath(item));path.AddToClassList("studio-asset-path");overview.Add(path);
        }
    }
}
