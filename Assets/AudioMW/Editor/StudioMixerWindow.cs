using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

namespace AudioMW.Editor
{
    public sealed class StudioMixerWindow : EditorWindow
    {
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup selected;
        private AudioMixerGroup pendingMasterOutput;
        [SerializeField] private AudioMixerSnapshot snapshot;
        [SerializeField] private Object routeSource;
        [SerializeField] private string laneId = "", search = "";
        [SerializeField] private int deckTab;
        [SerializeField] private List<Object> edits = new List<Object>();
        private List<StudioMixerChannel> channels = new List<StudioMixerChannel>();
        private AudioMixerSnapshot[] snapshots = Array.Empty<AudioMixerSnapshot>();
        private readonly Dictionary<AudioMixerGroup, float> levels = new Dictionary<AudioMixerGroup, float>();
        private sealed class Input
        {
            internal Object Asset;
            internal string LaneId, Name;
        }
        private readonly Dictionary<AudioMixerGroup, List<Input>> inputs = new Dictionary<AudioMixerGroup, List<Input>>();
        [SerializeField] private List<AudioMixerGroup> collapsed = new List<AudioMixerGroup>();
        private MixerRoutingProfile[] profiles = Array.Empty<MixerRoutingProfile>();
        private Vector2 treeScroll, stripScroll, deckScroll, routeScroll;
        private string message = "";
        private int bindingIndex;
        private bool refreshQueued;
        private GUIStyle heading, small, label, bright, centered;
        private static readonly Color Back = new Color32(27,29,32,255), Panel = new Color32(36,39,43,255), Raised = new Color32(46,50,54,255), Line = new Color32(65,71,75,255), Accent = new Color32(170,207,117,255), Selected = new Color32(58,75,48,255), TextColor = new Color32(224,229,229,255), Subtle = new Color32(172,182,189,255);
        private static readonly float[] Marks = { 20, 12, 6, 0, -6, -12, -24, -48, -80 };

        public static void Open(Object context = null, string trackId = null)
        {
            var window = GetWindow<StudioMixerWindow>();
            window.SetContext(context, trackId);
            window.Show();
            window.Focus();
        }

        private void OnEnable()
        {
            titleContent = new GUIContent("AudioMW | Mixer");
            minSize = new Vector2(700, 478);
            Undo.undoRedoPerformed += QueueRefresh;
            QueueRefresh();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= QueueRefresh;
            EditorApplication.delayCall -= Refresh;
            refreshQueued = false;
        }

        private void OnProjectChange() => QueueRefresh();
        private void OnFocus() => QueueRefresh();
        private void OnInspectorUpdate() { if (EditorApplication.isPlaying) Repaint(); }

        private void QueueRefresh()
        {
            if (refreshQueued) return;
            refreshQueued = true;
            EditorApplication.delayCall += Refresh;
        }

        private void SetContext(Object context, string trackId)
        {
            var bus = context as AudioMixerGroup;
            if (context != null)
            {
                routeSource = context is AudioMixer || context is AudioMixerGroup ? null : context;
                laneId = context is MusicArrangement ? trackId ?? "" : "";
                if (StudioMixerRoutes.Supports(context))
                    bus = StudioMixerRoutes.Output(context, laneId) ?? StudioMixerRoutes.Output(context);
            }
            if (context is AudioMixer chosen) mixer = chosen;
            if (bus != null) { mixer = bus.audioMixer; selected = bus; }
            if (mixer == null)
            {
                var guids = AssetDatabase.FindAssets("t:AudioMixer", new[] { "Assets" });
                if (guids.Length == 1) mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            Refresh();
        }

        private void Refresh()
        {
            EditorApplication.delayCall -= Refresh;
            refreshQueued = false;
            channels = mixer != null ? StudioMixerChannel.Read(mixer) : new List<StudioMixerChannel>();
            snapshots = mixer != null ? AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(mixer)).OfType<AudioMixerSnapshot>().ToArray() : Array.Empty<AudioMixerSnapshot>();
            if (!snapshots.Contains(snapshot) && mixer != null)
                using (var data = new SerializedObject(mixer))
                    snapshot = data.FindProperty("m_StartSnapshot")?.objectReferenceValue as AudioMixerSnapshot;
            if (!snapshots.Contains(snapshot)) snapshot = snapshots.FirstOrDefault();
            if (!channels.Any(c => c.Group == selected)) selected = channels.FirstOrDefault()?.Group;
            pendingMasterOutput = mixer != null ? mixer.outputAudioMixerGroup : null;
            levels.Clear();
            inputs.Clear();
            foreach (var channel in channels)
            {
                if (channel.Level(snapshot, out var value)) levels[channel.Group] = value;
                inputs[channel.Group] = new List<Input>();
            }
            if (mixer != null)
            {
                foreach (var type in new[] { "SoundEvent", "MusicTrack", "VoiceLine", "MusicArrangement" })
                    foreach (string guid in AssetDatabase.FindAssets("t:" + type))
                    {
                        var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                        if (!StudioMixerRoutes.Supports(asset)) continue;
                        AddInput(StudioMixerRoutes.Output(asset), asset);
                        if (asset is MusicArrangement score && score.Lanes != null)
                            foreach (var lane in score.Lanes) if (lane != null) AddInput(lane.Output, score, lane);
                    }
                profiles = AssetDatabase.FindAssets("t:MixerRoutingProfile").Select(g => AssetDatabase.LoadAssetAtPath<MixerRoutingProfile>(AssetDatabase.GUIDToAssetPath(g))).Where(p => p != null && p.Mixer == mixer).ToArray();
            }
            Repaint();
        }

        private void AddInput(AudioMixerGroup group, Object asset, ArrangementLane lane = null)
        {
            if (group != null && inputs.TryGetValue(group, out var list))
                list.Add(new Input { Asset = asset, LaneId = lane?.Id ?? "", Name = lane == null ? asset.name : asset.name + " / " + lane.Name });
        }

        private static GUIStyle Style(FontStyle weight, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var style = new GUIStyle(EditorStyles.label) { fontStyle = weight, fontSize = size, alignment = alignment, clipping = TextClipping.Clip, padding = new RectOffset(0,0,0,0) };
            style.normal.textColor = color;
            return style;
        }

        private static void Fill(Rect rect, Color color) => EditorGUI.DrawRect(rect, color);
        private static void Caption(Rect rect, string text, GUIStyle style) => GUI.Label(rect, new GUIContent(text, text), style);
        private Rect Row(Rect area, float y, float h = 22) => new Rect(area.x + 12, y, Mathf.Max(1,area.width - 24), h);

        private void OnGUI()
        {
            if (heading == null)
            {
                heading = Style(FontStyle.Bold, 13, TextColor);
                label = Style(FontStyle.Normal, 12, TextColor);
                bright = Style(FontStyle.Bold, 12, Accent);
                small = Style(FontStyle.Normal, 11, Subtle);
                centered = Style(FontStyle.Bold, 11, TextColor, TextAnchor.MiddleCenter);
            }
            float width = position.width, height = position.height;
            Fill(new Rect(0,0,width,height), Back);
            Fill(new Rect(0,0,width,32), Panel);
            Caption(new Rect(12,0,200,32), "AudioMW  |  Mixer", heading);
            using (new EditorGUI.DisabledScope(!edits.Any(o => o != null && EditorUtility.IsDirty(o))))
                if (GUI.Button(new Rect(width-70,4,62,24),"Save")) { foreach (var asset in edits) if (asset != null) AssetDatabase.SaveAssetIfDirty(asset); edits.Clear(); }
            if (GUI.Button(new Rect(width-201,4,126,24),"Open Unity Mixer")) NativeMixer();
            if (GUI.Button(new Rect(8,39,112,23),"‹   Event Editor")) AudioEventEditorWindow.Open();
            var next = (AudioMixer)EditorGUI.ObjectField(new Rect(132,41,Mathf.Min(width*.23f,260),20),mixer,typeof(AudioMixer),false);
            if (next != mixer) { mixer = next; selected = null; Refresh(); }
            float sx = 147 + Mathf.Min(width*.23f,260);
            Caption(new Rect(sx,38,62,24),"Snapshot",small);
            if (snapshot != null)
            {
                if (EditorGUI.DropdownButton(new Rect(sx+62,40,Mathf.Min(146, width - 216 - (sx + 62)),21), new GUIContent(snapshot.name), FocusType.Keyboard))
                {
                    var menu = new GenericMenu();
                    foreach (var item in snapshots) menu.AddItem(new GUIContent(item.name), snapshot == item, () => { snapshot = item; Refresh(); });
                    menu.ShowAsContext();
                }
            }
            Caption(new Rect(width-204,38,199,24), EditorApplication.isPlaying ? "●   Local Play Mode  ·  read-only" : "○   Edit Mode  ·  mixer asset", EditorApplication.isPlaying ? bright : small);
            if (mixer == null) { EditorGUI.HelpBox(new Rect(16,84,width-32,48),"Choose a Unity AudioMixer in the toolbar to inspect its channels and assign sources.",MessageType.Info); return; }
            float treeW = width < 1000 ? 150 : 204;
            float routeW = width < 1000 ? 188 : 228;
            float deckH = Mathf.Clamp(height*.31f,158,244);
            float bodyH = height - deckH - 99;
            var left = new Rect(4,70,treeW,bodyH);
            var right = new Rect(width-routeW-4,70,routeW,bodyH);
            var strips = Rect.MinMaxRect(left.xMax+4,70,right.x-4,left.yMax);
            DrawTree(left);
            DrawStrips(strips);
            DrawRouting(right);
            DrawDeck(new Rect(4,left.yMax+4,width-8,deckH));
            Caption(new Rect(12,height-23,width-24,22),string.IsNullOrEmpty(message) ? AssetDatabase.GetAssetPath(mixer) + "     ·     Snapshot gain (dB)     ·     Bus meters unavailable" : message,small);
        }

        private void NativeMixer()
        {
            if (selected != null) Selection.activeObject = selected;
            else if (mixer != null) Selection.activeObject = mixer;
            EditorApplication.ExecuteMenuItem("Window/Audio/Audio Mixer");
        }

        private void Choose(AudioMixerGroup group)
        {
            if (group == selected) return;
            selected = group; bindingIndex = 0; deckScroll = routeScroll = Vector2.zero; message = "";
        }

        private void DrawTree(Rect area)
        {
            Fill(area, Panel);
            Caption(Row(area,area.y+6), "Routing", heading);
            search = EditorGUI.TextField(Row(area,area.y+33), search, EditorStyles.toolbarSearchField);
            var items = channels.Where(c => search.Length > 0 ? c.Group.name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0 : VisibleInTree(c)).ToList();
            var viewport = Rect.MinMaxRect(area.x+3,area.y+64,area.xMax-3,area.yMax-4);
            treeScroll = GUI.BeginScrollView(viewport,treeScroll,new Rect(0,0,viewport.width-16,items.Count*26));
            for (int i=0;i<items.Count;i++)
            {
                var channel = items[i];
                var row = new Rect(0,i*26,viewport.width-16,25);
                if (channel.Group == selected) Fill(row, Selected);
                float indent = Mathf.Min(60, Depth(channel)*12);
                if (channels.Any(c => c.Parent == channel.Group))
                {
                    var toggle = new Rect(3+indent,row.y,17,25);
                    Caption(toggle,collapsed.Contains(channel.Group) ? "▸" : "▾",small);
                    if (GUI.Button(toggle,GUIContent.none,GUIStyle.none))
                    {
                        if (!collapsed.Remove(channel.Group)) collapsed.Add(channel.Group);
                    }
                }
                if (channel.Icon != null) GUI.DrawTexture(new Rect(20+indent,row.y+6,13,13),channel.Icon,ScaleMode.ScaleToFit);
                Caption(new Rect(38+indent,row.y,row.width-indent-38,25),channel.Group.name,channel.Group == selected ? bright : label);
                if (GUI.Button(row,GUIContent.none,GUIStyle.none)) Choose(channel.Group);
            }
            GUI.EndScrollView();
        }

        private bool VisibleInTree(StudioMixerChannel node)
        {
            for (int depth = 0; node?.Parent != null && depth < channels.Count; depth++)
            {
                if (collapsed.Contains(node.Parent)) return false;
                node = channels.Find(c => c.Group == node.Parent);
            }
            return true;
        }

        private int Depth(StudioMixerChannel node)
        {
            int depth = 0;
            while (node?.Parent != null && depth < channels.Count) { depth++; node = channels.Find(c => c.Group == node.Parent); }
            return depth;
        }

        private void DrawStrips(Rect area)
        {
            Fill(area,Back);
            var order = channels.OrderBy(c => c.Master).ToArray();
            float width = Mathf.Clamp((area.width-2)/Mathf.Max(1,order.Length),102,146);
            float total = order.Length*width;
            float h = area.height - (total > area.width ? 17 : 0);
            stripScroll = GUI.BeginScrollView(area,stripScroll,new Rect(0,0,Mathf.Max(1,total),h));
            for(int i=0;i<order.Length;i++) DrawStrip(new Rect(i*width,0,width-4,h),order[i]);
            GUI.EndScrollView();
        }

        private void DrawStrip(Rect rect, StudioMixerChannel channel)
        {
            bool active = channel.Group == selected;
            Fill(rect,active ? Accent : Line);
            var inner = new Rect(rect.x+1,1,rect.width-2,rect.height-2);
            Fill(inner,Panel);
            Fill(new Rect(inner.x,1,inner.width,37),active ? Selected : Raised);
            if(channel.Icon!=null) GUI.DrawTexture(new Rect(inner.x+7,11,14,14),channel.Icon,ScaleMode.ScaleToFit);
            Caption(new Rect(inner.x+26,2,inner.width-27,34),channel.Group.name,active ? bright : label);
            if (GUI.Button(new Rect(inner.x,1,inner.width,36),GUIContent.none,GUIStyle.none)) Choose(channel.Group);
            bool compact = rect.height < 290;
            var field = new Rect(inner.x+8,compact ? 42 : 62,inner.width-16,21);
            if (!compact) Caption(new Rect(field.x,40,field.width,20),"Gain · dB",small);
            float switchY = rect.yMax - (compact ? 68 : 112);
            float graphTop = field.yMax + 12;
            var graph = new Rect(field.x,graphTop,field.width,Mathf.Max(1,switchY - 13 - graphTop));
            if (levels.TryGetValue(channel.Group,out float db))
            {
                using (new EditorGUI.DisabledScope(!StudioMixerChannel.Editable(snapshot)))
                {
                    EditorGUI.BeginChangeCheck();
                    Rect valueField = field;
                    if (compact)
                    {
                        valueField.width -= 22;
                        Caption(new Rect(valueField.xMax+4,field.y,18,field.height),"dB",small);
                    }
                    float next = EditorGUI.DelayedFloatField(valueField,db);
                    if (EditorGUI.EndChangeCheck()) SetLevel(channel,next);
                    Fader(graph,channel,db);
                }
            }
            else Caption(field,"Native only",small);

            DrawChannelSwitch(new Rect(field.x,switchY,(field.width-5)*.5f,23),channel,false);
            DrawChannelSwitch(new Rect(field.x+(field.width+5)*.5f,switchY,(field.width-5)*.5f,23),channel,true);
            float y = switchY + 27;
            if (compact)
            {
                Caption(new Rect(field.x,y,field.width,17),inputs[channel.Group].Count + " inputs",small);
                Caption(new Rect(field.x,y+18,field.width,18),"→ " + Destination(channel),label);
                return;
            }
            Caption(new Rect(field.x,y,field.width,20),inputs[channel.Group].Count+" inputs",small);
            Fill(new Rect(field.x,y+23,field.width,1),Line);
            Caption(new Rect(field.x,y+27,field.width,18),"Output",small);
            string output = Destination(channel);
            Caption(new Rect(field.x,y+46,field.width,22),output,label);
        }

        private void DrawChannelSwitch(Rect area, StudioMixerChannel channel, bool solo)
        {
            bool? value = solo ? channel.Solo : channel.Mute;
            string title = solo ? "Solo" : "Mute";
            Fill(area,value == true ? Selected : Raised);
            Caption(area,solo ? "S" : "M",value == true ? bright : centered);
            using (new EditorGUI.DisabledScope(!value.HasValue || !StudioMixerChannel.Editable(channel.Group)))
                if (GUI.Button(area,new GUIContent("",title + " · native group setting stored in the mixer asset"),GUIStyle.none)
                    && channel.SetSwitch(solo,value != true))
                {
                    Choose(channel.Group);
                    Remember(channel.Group);
                }
        }

        private string Destination(StudioMixerChannel c) => c.Parent != null ? c.Parent.name : c.Master ? (mixer.outputAudioMixerGroup != null ? mixer.outputAudioMixerGroup.name : "Device") : "Unavailable";

        private static float Normalized(float db)
        {
            for(int i=1;i<Marks.Length;i++)
                if(db >= Marks[i]) return (i-1+Mathf.InverseLerp(Marks[i-1],Marks[i],db))/(Marks.Length-1);
            return 1;
        }

        private void Fader(Rect area, StudioMixerChannel channel, float db)
        {
            float x=area.x+area.width*.53f;
            var meter = new Rect(area.xMax-12,area.y,10,area.height);
            Fill(meter,Back);
            GUI.Label(meter,new GUIContent("","Bus peak feed unavailable. Use Unity’s Audio Mixer meters."));
            Fill(new Rect(x-4,area.y,8,area.height),Back);
            float markSpacing = area.height / (Marks.Length - 1);
            int labelStep = Mathf.Max(1, Mathf.CeilToInt(16 / markSpacing));
            for(int i=0;i<Marks.Length;i++)
            {
                float y=area.y+markSpacing*i;
                Fill(new Rect(x-13,y,8,1),Line);
                if (i == Marks.Length-1 || (i % labelStep == 0 && (Marks.Length-1-i) * markSpacing >= 16))
                    Caption(new Rect(area.x,y-8,37,16),Marks[i].ToString("+0;-0;0"),small);
            }
            float at=Normalized(db);
            EditorGUI.BeginChangeCheck();
            float moved=GUI.VerticalSlider(new Rect(x-13,area.y-4,26,area.height+8),at,0,1,GUIStyle.none,GUIStyle.none);
            if(EditorGUI.EndChangeCheck())
            {
                float step=moved*(Marks.Length-1); int i=Mathf.Min(Marks.Length-2,(int)step);
                SetLevel(channel,Mathf.Round(Mathf.Lerp(Marks[i],Marks[i+1],step-i)*10)/10);
            }
            var thumb=new Rect(x-12,area.y+at*area.height-5,24,10);
            Fill(thumb,channel.Group==selected ? Accent : Subtle);
            Fill(new Rect(thumb.x,thumb.y,24,1),TextColor);
            Fill(new Rect(thumb.x+2,thumb.center.y,20,1),Panel);
        }

        private void SetLevel(StudioMixerChannel channel,float value)
        {
            if(channel.Level(snapshot,out var saved,Mathf.Clamp(value,-80,20)))
            {
                levels[channel.Group]=saved;
                Remember(snapshot);
                Choose(channel.Group);
            }
            else message="Gain unavailable for this snapshot. Open the native mixer.";
        }

        private void Remember(Object item)
        {
            if (!edits.Contains(item)) edits.Add(item);
        }

        private void DrawRouting(Rect area)
        {
            Fill(area,Panel);
            Caption(Row(area,area.y+6),"Routing",heading);
            var node = channels.Find(c => c.Group == selected);
            if (node == null) return;
            var view = Rect.MinMaxRect(area.x+8,area.y+35,area.xMax-5,area.yMax-3);
            int depth = Depth(node);
            bool hasLanes = routeSource is MusicArrangement;
            var content = new Rect(0,0,view.width-16,266 + (depth+2)*26 + (hasLanes ? 29 : 0) + (node.Master ? 106 : 0));
            routeScroll = GUI.BeginScrollView(view,routeScroll,content);
            float w=content.width;
            Caption(new Rect(4,0,w-4,20),"Source asset",small);
            var next = EditorGUI.ObjectField(new Rect(4,23,w-5,21),routeSource,typeof(Object),false);
            if (next != routeSource) { routeSource=next; laneId=""; message=""; }
            float y=52;
            if (routeSource is MusicArrangement score)
            {
                var lane = score.Lanes?.Find(l => l != null && l.Id == laneId);
                if (EditorGUI.DropdownButton(new Rect(4,y,w-5,22),new GUIContent(string.IsNullOrEmpty(laneId) ? "Arrangement output" : lane?.Name ?? "Missing track"),FocusType.Keyboard))
                {
                    var menu=new GenericMenu();
                    menu.AddItem(new GUIContent("Arrangement output"),string.IsNullOrEmpty(laneId),()=>laneId="");
                    if(score.Lanes!=null)foreach(var item in score.Lanes.Where(l=>l!=null))
                    {
                        string id=item.Id;
                        menu.AddItem(new GUIContent("Track / "+item.Name),id==laneId,()=>laneId=id);
                    }
                    menu.ShowAsContext();
                }
                y+=29;
            }
            bool missingLane = !string.IsNullOrEmpty(laneId) && routeSource is MusicArrangement arrangement
                && (arrangement.Lanes == null || arrangement.Lanes.Count(lane => lane != null && lane.Id == laneId) != 1);
            bool supported=StudioMixerRoutes.Supports(routeSource) && !missingLane;
            var output=StudioMixerRoutes.Output(routeSource,laneId);
            string current=output!=null ? output.audioMixer.name+" / "+output.name : string.IsNullOrEmpty(laneId) ? "Unity default output" : "Inherit arrangement output";
            Caption(new Rect(4,y,w-4,20),"Current output",small);
            Caption(new Rect(4,y+20,w-4,22),supported ? current : missingLane ? "Reselect a unique track" : routeSource==null ? "Choose a source" : "Choose a routable leaf",label);
            y+=53;
            Caption(new Rect(4,y,w-4,20),"Destination",small);
            if(EditorGUI.DropdownButton(new Rect(4,y+22,w-5,22),new GUIContent(node.Group.name),FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                foreach(var c in channels) menu.AddItem(new GUIContent(string.Join("/",Chain(c))),c.Group==selected,()=>Choose(c.Group));
                menu.ShowAsContext();
            }
            using(new EditorGUI.DisabledScope(!supported || !StudioMixerChannel.Editable(routeSource)))
            {
                using(new EditorGUI.DisabledScope(output==selected))
                    if(GUI.Button(new Rect(4,y+50,w-5,25),"Assign output")) Assign(selected);
                using(new EditorGUI.DisabledScope(output==null))
                    if(GUI.Button(new Rect(4,y+80,w-5,21),string.IsNullOrEmpty(laneId) ? "Use Unity default" : "Inherit arrangement")) Assign(null);
            }
            y+=119;
            Fill(new Rect(4,y,w-5,1),Line);
            Caption(new Rect(4,y+8,w-4,23),"Channel path",heading);
            y+=38;
            foreach(string step in Chain(node).Reverse())
            {
                Fill(new Rect(4,y,w-5,23),step==node.Group.name ? Selected : Raised);
                Caption(new Rect(12,y,w-19,23),step,label);
                y+=27;
            }
            if (node.Master)
            {
                y += 8;
                Caption(new Rect(4,y,w-4,20),"Mixer output  ·  device or external group",small);
                pendingMasterOutput = (AudioMixerGroup)EditorGUI.ObjectField(new Rect(4,y+23,w-5,21),pendingMasterOutput,typeof(AudioMixerGroup),false);
                bool valid = StudioMixerRoutes.CanConnect(mixer,pendingMasterOutput);
                using (new EditorGUI.DisabledScope(!valid || !StudioMixerChannel.Editable(mixer) || mixer.outputAudioMixerGroup==pendingMasterOutput))
                    if (GUI.Button(new Rect(4,y+50,w-5,23), "Assign mixer output") && StudioMixerRoutes.Connect(mixer,pendingMasterOutput))
                    {
                        Remember(mixer);
                        message = mixer.name + "   →   " + Destination(node);
                    }
                if (!valid) Caption(new Rect(4,y+75,w-4,20),"This route would create a loop.",small);
            }
            GUI.EndScrollView();
        }

        private IEnumerable<string> Chain(StudioMixerChannel channel)
        {
            var path=new Stack<string>();
            var seen=new HashSet<AudioMixerGroup>();
            while(channel!=null && seen.Add(channel.Group))
            {
                path.Push(channel.Group.name);
                channel=channels.Find(c=>c.Group==channel.Parent);
            }
            return path;
        }

        private void Assign(AudioMixerGroup group)
        {
            if (!StudioMixerRoutes.Assign(routeSource,laneId,group)) { message="This output cannot be edited."; return; }
            Remember(routeSource);
            message = routeSource.name+"   →   "+(group!=null ? group.name : string.IsNullOrEmpty(laneId) ? "Unity default output" : "Arrangement output");
            QueueRefresh();
        }

        private void DrawDeck(Rect area)
        {
            Fill(area,Panel);
            var node=channels.Find(c=>c.Group==selected);
            if(node==null)return;
            Caption(Row(area,area.y+4),node.Group.name+"  /  AudioMixer group",heading);
            if(GUI.Button(new Rect(area.xMax-151,area.y+4,142,23),"Open group in Unity"))NativeMixer();
            Fill(new Rect(area.x,area.y+31,area.width,1),Line);
            string[] tabs={"Inputs","Bindings","Effects"};
            for(int i=0;i<tabs.Length;i++)
            {
                var tab=new Rect(area.x+8+i*92,area.y+37,88,23);
                Fill(tab,i==deckTab ? Selected : Raised);
                if(i==deckTab)Fill(new Rect(tab.x,tab.yMax-2,tab.width,2),Accent);
                Caption(tab,tabs[i],centered);
                if(GUI.Button(tab,GUIContent.none,GUIStyle.none) && deckTab!=i){deckTab=i;deckScroll=Vector2.zero;}
            }
            var body=Rect.MinMaxRect(area.x+12,area.y+70,area.xMax-12,area.yMax-8);
            if(deckTab==1){DrawBindings(body,node);return;}
            string[] names = deckTab==2 ? node.Effects : inputs[selected].Select(a=>a.Name).ToArray();
            if(names==null || names.Length==0)
            {
                Caption(body,deckTab==2 ? names==null ? "Effect list unavailable. Inspect this group in Unity." : "No effects on this group." : "No AudioMW sources assigned directly to this group.",small);
                return;
            }
            deckScroll=GUI.BeginScrollView(body,deckScroll,new Rect(0,0,body.width-16,names.Length*27));
            for(int i=0;i<names.Length;i++)
            {
                var row=new Rect(0,i*27,body.width-17,25);
                Fill(row,i%2==0 ? Raised : Panel);
                Caption(new Rect(9,row.y,row.width*.42f,25),names[i]??"Unavailable",label);
                if(deckTab==0)
                {
                    var owner=inputs[selected][i];
                    Caption(new Rect(row.width*.44f,row.y,row.width*.55f,25),AssetDatabase.GetAssetPath(owner.Asset),small);
                    if(GUI.Button(row,GUIContent.none,GUIStyle.none)){routeSource=owner.Asset;laneId=owner.LaneId;message="";}
                }
            }
            GUI.EndScrollView();
        }

        private void DrawBindings(Rect area,StudioMixerChannel node)
        {
            var bindings=profiles.SelectMany(p=>p.Bindings.Where(b=>b!=null && !string.IsNullOrEmpty(node.ExposedGain) && b.ExposedName==node.ExposedGain).Select(b=>(profile:p,binding:b))).ToArray();
            if(bindings.Length==0)
            {
                Caption(new Rect(area.x,area.y,area.width,26),string.IsNullOrEmpty(node.ExposedGain) ? "Channel gain has no exposed parameter." : "No project profile binds "+node.ExposedGain+".",small);
                return;
            }
            bindingIndex=Mathf.Clamp(bindingIndex,0,bindings.Length-1);
            var item=bindings[bindingIndex];
            float sidebar=Mathf.Min(340,area.width*.36f);
            var selector=new Rect(area.x,area.y,sidebar-15,22);
            if(EditorGUI.DropdownButton(selector,new GUIContent(item.profile.name+"  /  "+(item.binding.Parameter!=null ? item.binding.Parameter.name : "Missing parameter")),FocusType.Keyboard))
            {
                var menu=new GenericMenu();
                for(int i=0;i<bindings.Length;i++){int index=i;var b=bindings[i];menu.AddItem(new GUIContent(b.profile.name+" / "+(b.binding.Parameter!=null?b.binding.Parameter.name:"Missing parameter")+" ("+(i+1)+")"),i==bindingIndex,()=>bindingIndex=index);}
                menu.ShowAsContext();
            }
            Caption(new Rect(area.x,area.y+30,sidebar-16,22),item.binding.ExposedName,label);
            Caption(new Rect(area.x,area.y+55,sidebar-16,20),"Output  "+item.binding.OutputMin.ToString("0.##")+" → "+item.binding.OutputMax.ToString("0.##")+" dB",small);
            if(GUI.Button(new Rect(area.x,area.y+82,142,23),"Edit routing profile"))AudioWorkspace.Pin(item.profile);
            var plot=Rect.MinMaxRect(area.x+sidebar+36,area.y+22,area.xMax-12,area.yMax-20);
            if(plot.height<22 || !item.binding.IsValid) return;
            Caption(new Rect(plot.x,area.y,plot.width,20),"Parameter response  ·  dB",small);
            Fill(plot,Back);
            for(int i=0;i<=8;i++)Fill(new Rect(plot.x+i*plot.width/8,plot.y,1,plot.height),Line);
            for(int i=0;i<=4;i++)Fill(new Rect(plot.x,plot.y+i*plot.height/4,plot.width,1),Line);
            var binding=item.binding;
            float low=Mathf.Min(binding.OutputMin,binding.OutputMax),high=Mathf.Max(binding.OutputMin,binding.OutputMax);
            Caption(new Rect(plot.x-33,plot.y-4,32,16),high.ToString("0.#"),small);
            Caption(new Rect(plot.x-33,plot.yMax-12,32,16),low.ToString("0.#"),small);
            Caption(new Rect(plot.x,plot.yMax+1,plot.width,17),binding.Parameter.MinValue.ToString("0.##")+"   ·   "+binding.Parameter.name,small);
            Caption(new Rect(plot.xMax-32,plot.yMax+1,32,17),binding.Parameter.MaxValue.ToString("0.##"),small);
            if(Event.current.type!=EventType.Repaint)return;
            var points=new Vector3[97];
            for(int i=0;i<points.Length;i++)
            {
                float t=i/(float)(points.Length-1);
                float value=binding.Evaluate(Mathf.Lerp(binding.Parameter.MinValue,binding.Parameter.MaxValue,t));
                points[i]=new Vector3(plot.x+t*plot.width,plot.yMax-(high==low?.5f:Mathf.InverseLerp(low,high,value))*plot.height);
            }
            Handles.BeginGUI();var old=Handles.color;Handles.color=Accent;Handles.DrawAAPolyLine(2,points);Handles.color=old;Handles.EndGUI();
        }
    }
}
