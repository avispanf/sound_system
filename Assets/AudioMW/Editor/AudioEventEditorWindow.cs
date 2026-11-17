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
        private TwoPaneSplitView mainSplit;
        private VisualElement libraryPanel;
        [SerializeField] private string mode = "Author";
        [SerializeField] private string view = "Container";
        private readonly List<Object> browserAssets = new List<Object>();
        private Object deckTarget;
        private SerializedObject deckSerialized;
        private ListView browser;
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
        private bool bars;
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
        [MenuItem("Window/AudioMW/Event Editor", false, 0)]
        public static void Open() { var window = GetWindow<AudioEventEditorWindow>(); window.titleContent = new GUIContent("AudioMW | Event Editor"); window.minSize = new Vector2(760,520); window.Show(); }
        private void OnEnable() { Undo.undoRedoPerformed += OnUndo; EditorApplication.playModeStateChanged += OnPlayState; EditorApplication.update += UpdateSession; }
        private void OnDisable() { Undo.undoRedoPerformed -= OnUndo; EditorApplication.playModeStateChanged -= OnPlayState; EditorApplication.update -= UpdateSession; StopAudition(); ReleaseLive(); capture?.Dispose(); capture=null; deckSerialized?.Dispose(); if(legacyProjection!=null)DestroyImmediate(legacyProjection); }
        private void OnUndo() { ShowDeck(deckTarget); RefreshDocuments(); canvas?.MarkDirtyRepaint(); }
        private void OnPlayState(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.ExitingEditMode) { StopAudition(); ReleaseLive(); } }
        private void OnProjectChange() { RefreshBrowser(); RefreshDocuments(); }
        public void CreateGUI()
        {
            rootVisualElement.Clear(); AudioWorkspace.Style(rootVisualElement); rootVisualElement.AddToClassList("audiomw-event-editor-v2");
            var top = new Toolbar { name="studio-v2-toolbar" }; top.Add(new Label("AudioMW | Event Editor"));
            top.Add(new ToolbarButton(()=>{drawerOpen=!drawerOpen;UpdateCompactLayout();}){text="Browser",name="studio-browser-toggle"});
            top.Add(new ToolbarSpacer { flex=true }); top.Add(new ToolbarButton(AudioWorkspace.Mixer) { text="Mixer" });
            var tools = new ToolbarMenu { text="Tools" }; tools.menu.AppendAction("Review agent proposals",_=>AudioAgentReviewWindow.Open()); tools.menu.AppendAction("Project setup",_=>AudioProjectSetupWindow.Open()); tools.menu.AppendAction("Reset layout",_=>{ overviewOpen=true;deckHeight=240;drawerOpen=false; CreateGUI(); }); top.Add(tools);
            top.Add(new ToolbarButton(SaveDocument) { text="Save",name="studio-save" }); rootVisualElement.Add(top);
            var main = new TwoPaneSplitView(0,260,TwoPaneSplitViewOrientation.Horizontal) { viewDataKey="AudioMW.V2.BrowserWidth",name="studio-v2-main" }; mainSplit=main;main.style.flexGrow=1; rootVisualElement.Add(main);
            var library = new VisualElement { name="studio-v2-browser" }; library.style.minWidth=220; library.style.maxWidth=360; main.Add(library);libraryPanel=library;
            var tabs = new Toolbar{name="studio-browser-tabs"}; string[] names={"Events","Banks","Assets","Parameters"};
            for(int i=0;i<4;i++) { int index=i; tabs.Add(new ToolbarButton(()=>SwitchBrowser(index)) {text=names[i],name="browser-tab-"+i}); } library.Add(tabs);
            search=new ToolbarSearchField(); search.SetValueWithoutNotify(queries[browserTab]); search.RegisterValueChangedCallback(e=>{queries[browserTab]=e.newValue;RefreshBrowser();}); library.Add(search);
            browser=new ListView { fixedItemHeight=24,selectionType=SelectionType.Multiple,name="studio-v2-library" }; browser.style.flexGrow=1;
            browser.makeItem=()=>{var row=new VisualElement();row.AddToClassList("studio-browser-row");row.Add(new Image());row.Add(new Label());return row;};
            browser.bindItem=(e,i)=>{var asset=browserAssets[i];e.Q<Image>().image=AssetDatabase.GetCachedIcon(AssetDatabase.GetAssetPath(asset));e.Q<Label>().text=asset.name;e.tooltip=AssetDatabase.GetAssetPath(asset);};
            browser.selectionChanged+=items=>{if(browserRefreshing)return;foreach(Object item in items){if(browserTab==0)OpenDocument(item,false);else ShowDeck(item);break;}};
            browser.itemsChosen+=items=>{foreach(Object item in items){OpenDocument(item,true);break;}};
            browser.RegisterCallback<PointerDownEvent>(e=>{browserPointer=e.position;browserDragArmed=e.button==0;},TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerUpEvent>(e=>browserDragArmed=false,TrickleDown.TrickleDown);
            rootVisualElement.RegisterCallback<PointerMoveEvent>(e=>
            {
                if(!browserDragArmed||(e.pressedButtons&1)==0||browser.selectedItem==null||Vector2.Distance(browserPointer,e.position)<8)return;
                browserDragArmed=false;DragAndDrop.PrepareStartDrag();DragAndDrop.objectReferences=browser.selectedItems.Cast<Object>().ToArray();DragAndDrop.StartDrag("AudioMW assets");e.StopPropagation();
            },TrickleDown.TrickleDown); library.Add(browser);
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
            rootVisualElement.RegisterCallback<GeometryChangedEvent>(e=>{ overview.style.display=overviewOpen&&e.newRect.width>=1400?DisplayStyle.Flex:DisplayStyle.None;if(bottomTab=="")deck.style.height=Mathf.Clamp(deckHeight,160,e.newRect.height<850?180:320);UpdateCompactLayout(); });
            rootVisualElement.RegisterCallback<KeyDownEvent>(e=>{if(e.target is TextElement || e.target is TextField)return;if(e.actionKey&&e.keyCode==KeyCode.S){SaveDocument();e.StopPropagation();}if(e.keyCode==KeyCode.Escape)CancelDrag();});
            RefreshBrowser();RefreshDocuments();RestoreDocumentSelection();ShowDeck(CurrentAsset);RefreshDock();RefreshTransport();RefreshOverview();
        }
        private void UpdateCompactLayout()
        {
            if(mainSplit==null)return;
            bool compact=position.width<1000;
            var toggle=rootVisualElement.Q<ToolbarButton>("studio-browser-toggle");if(toggle!=null)toggle.style.display=compact?DisplayStyle.Flex:DisplayStyle.None;
            if(compact&&!drawerOpen)mainSplit.CollapseChild(0);else mainSplit.UnCollapse();
            if(libraryPanel!=null){libraryPanel.style.minWidth=220;mainSplit.fixedPaneInitialDimension=position.width<1400?220:260;}
        }
        public void SwitchBrowser(int index) {browserTab=Mathf.Clamp(index,0,3);search?.SetValueWithoutNotify(queries[browserTab]);EditorAudioPreview.Stop();RefreshBrowser();}
        private void RefreshBrowser()
        {
            if(browser==null)return;for(int i=0;i<4;i++)rootVisualElement.Q<ToolbarButton>("browser-tab-"+i)?.EnableInClassList("studio-v2-active",i==browserTab);browserRefreshing=true;browser.ClearSelection();browserAssets.Clear();string[] types=browserTab==0?new[]{"SoundEvent","MusicTrack","MusicArrangement","MusicStateProfile","VoiceLine"}:new[]{browserTab==1?"SoundBank":browserTab==2?"AudioClip":"SoundParameter"};
            foreach(string type in types)foreach(string guid in AssetDatabase.FindAssets("t:"+type)){var asset=AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid));if(asset!=null&&asset.name.IndexOf(queries[browserTab],StringComparison.OrdinalIgnoreCase)>=0)browserAssets.Add(asset);}
            browserAssets.Sort((a,b)=>string.CompareOrdinal(a.name,b.name));browser.itemsSource=browserAssets;browser.Rebuild();browserRefreshing=false;
        }
        public void OpenDocument(Object asset,bool pin=true)
        {
            if(asset==null)return;string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));if(string.IsNullOrEmpty(guid))return;
            int index=documents.FindIndex(x=>x.Guid==guid);
            if(index<0){if(CurrentDocument!=null&&!CurrentDocument.Pinned&&CurrentAsset!=null&&!EditorUtility.IsDirty(CurrentAsset)){index=documentIndex;documents[index]=new Document{Guid=guid,Pinned=pin};}else{documents.Add(new Document{Guid=guid,Pinned=pin});index=documents.Count-1;}}
            else if(pin)documents[index].Pinned=true;
            if(CurrentDocument==null||CurrentDocument.Guid!=guid)StopAudition();
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
            string stamp=documentIndex+string.Join("|",documents.Select(d=>{var a=AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(d.Guid));return d.Guid+":"+(a!=null?a.name:"")+":"+(a!=null&&EditorUtility.IsDirty(a))+":"+d.Pinned;}));
            if(documentsStamp==stamp)return;documentsStamp=stamp;documentTabs.Clear();
            for(int i=0;i<documents.Count;i++){int index=i;var asset=AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(documents[i].Guid));if(asset==null)continue;if(EditorUtility.IsDirty(asset))documents[i].Pinned=true;
                var tabGroup=new VisualElement();tabGroup.AddToClassList("studio-document-tab");tabGroup.EnableInClassList("studio-v2-active",index==documentIndex);documentTabs.Add(tabGroup);
                var b=new ToolbarButton(()=>OpenDocument(asset,documents[index].Pinned)){text=asset.name+(EditorUtility.IsDirty(asset)?" •":""),tooltip=AssetDatabase.GetAssetPath(asset)};b.EnableInClassList("studio-v2-active",index==documentIndex);tabGroup.Add(b);
                tabGroup.Add(new ToolbarButton(()=>{StopAudition();documents.RemoveAt(index);if(index<documentIndex)documentIndex--;documentIndex=Math.Min(documentIndex,documents.Count-1);selectedClips.Clear();selectedLane="";ShowDeck(CurrentAsset);RefreshDocuments();RefreshOverview();}){text="×"});}
        }
        public void SaveDocument(){if(CurrentAsset is MusicArrangement score && score.Validate()!=""){notice=score.Validate();return;}if(deckTarget!=null)AssetDatabase.SaveAssetIfDirty(deckTarget);if(CurrentAsset!=null)AssetDatabase.SaveAssetIfDirty(CurrentAsset);RefreshDocuments();}
        private void ShowDeck(Object target)
        {
            deckSerialized?.Dispose();deckSerialized=null;deckTarget=target;
            if(target!=null)deckSerialized=new SerializedObject(target);
            if(breadcrumb!=null)breadcrumb.text=target==CurrentAsset?(target!=null?target.name:"Select an object"):(CurrentAsset!=null?CurrentAsset.name:"No event")+"  ›  "+(target!=null?target.name:"Select an object");
        }
        private void CreateAsset(string kind)
        {
            Object asset;
            if(kind=="Music Arrangement")asset=CreateInstance<MusicArrangement>();else if(kind=="Music Track")asset=CreateInstance<MusicTrack>();else if(kind=="Bank")asset=CreateInstance<SoundBank>();else if(kind=="Parameter")asset=CreateInstance<SoundParameter>();else{var item=CreateInstance<SoundEvent>();item.Clips=browser.selectedItems.OfType<AudioClip>().ToArray();if(kind!="Random / Sequence")item.ContainerMode=(ContainerMode)Enum.Parse(typeof(ContainerMode),kind);asset=item;}
            string path=EditorUtility.SaveFilePanelInProject("Create "+kind,"New "+kind.Replace(" / "," "),"asset","Choose the asset location.");
            if(string.IsNullOrEmpty(path)){DestroyImmediate(asset);return;}AssetDatabase.CreateAsset(asset,path);Undo.RegisterCreatedObjectUndo(asset,"Create audio document");RefreshBrowser();OpenDocument(asset);
        }
        private void RefreshOverview()
        {
            if(overview==null)return;overview.Clear();overview.Add(new Label("Overview"));var item=CurrentAsset; if(item==null)return;
            overview.Add(new Label(AssetDatabase.GetAssetPath(item)));if(item is SoundEvent sound){overview.Add(new Label("Output: "+(sound.MixerGroup!=null?sound.MixerGroup.name:"Unity output")));foreach(var bank in AudioSourceCatalog.BanksFor(sound))overview.Add(new Label("Bank: "+bank.name));overview.Add(new Label(SoundEventValidation.Describe(sound)));}
            if(item is MusicTrack track){overview.Add(new Label("Output: "+(track.MixerGroup!=null?track.MixerGroup.name:"Unity output")));overview.Add(new Label("Legacy intro / body · "+track.Layers.Length+" layers"));}
            if(item is MusicArrangement arrangement){overview.Add(new Label("Output: "+(arrangement.Output!=null?arrangement.Output.name:"Unity output")));overview.Add(new Label(arrangement.Lanes.Count+" tracks · "+arrangement.Tempo+" BPM"));overview.Add(new Label("Authored edits apply on restart"));}
            overview.Add(new Button(()=>{overviewOpen=false;overview.style.display=DisplayStyle.None;}){text="Collapse overview"});
        }
    }
}
