using AudioMW;
using System.Collections.Generic;
using UnityEngine;

public sealed class BadShiftAudioGuide : MonoBehaviour
{
    private BadShiftDemo demo;
    private sealed class Pulse
    {
        public string Name;
        public Vector3 Position;
        public float Created;
        public int Count = 1;
    }
    private readonly List<Pulse> pulses = new List<Pulse>();
    private readonly List<Rect> drawn = new List<Rect>();
    private EventDebugger observedDebugger;
    private long lastSequence;
    public const float LabelLifetime = .85f;
    public int LabelCount => pulses.Count;
    public string LatestLabel => pulses.Count > 0 ? pulses[pulses.Count - 1].Name : "";
    public static float ConnectionStrength(float meters,float pixels) => (1-Mathf.InverseLerp(3,5,meters))*(1-Mathf.InverseLerp(140,260,pixels));
    public static float LabelDistanceAlpha(float meters) => 1-Mathf.InverseLerp(4,7,meters);
    private void CaptureLabels()
    {
        var debugger=AudioSystem.Debugger;
        if(observedDebugger!=debugger){observedDebugger=debugger;lastSequence=0;pulses.Clear();}
        float now=Time.unscaledTime;
        pulses.RemoveAll(p=>now-p.Created>=LabelLifetime);
        foreach(var record in debugger.Records)
        {
            if(record.Sequence<=lastSequence)continue;
            lastSequence=record.Sequence;
            if(!demo.AudioLabelsVisible||record.Outcome!=PlaybackOutcome.Played||record.Event==null||record.Event.Loop||record.Event.SpatialBlend<.01f||!record.HasGainCapture||record.FinalVolume<=0||Time.realtimeSinceStartup-record.Time>.2f)continue;
            float distance=Vector3.Distance(record.Position,demo.Game.Listener.position);
            if(LabelDistanceAlpha(distance)<=0||distance>record.Event.MaxDistance)continue;
            Pulse recent=pulses.FindLast(p=>p.Name==record.EventName&&now-p.Created<.1f&&Vector3.Distance(p.Position,record.Position)<.7f);
            if(recent!=null){recent.Count++;continue;}
            if(pulses.Count==12)pulses.RemoveAt(0);
            pulses.Add(new Pulse{Name=record.EventName,Position=record.Position,Created=now});
        }
        if(!demo.AudioLabelsVisible)pulses.Clear();
    }
    public void DrawLabels(float w,float h,float top,float bottom)
    {
        if(!demo.AudioLabelsVisible)return;
        drawn.Clear();var style=Style(12,new Color(.92f,.98f,.96f),true);style.wordWrap=false;style.alignment=TextAnchor.MiddleCenter;style.clipping=TextClipping.Clip;
        for(int i=pulses.Count-1;i>=0;i--)
        {
            var pulse=pulses[i];float age=Time.unscaledTime-pulse.Created;
            float alpha=Mathf.Clamp01(age/.06f)*Mathf.Clamp01((LabelLifetime-age)/.3f)*LabelDistanceAlpha(Vector3.Distance(pulse.Position,demo.Game.Listener.position));
            if(alpha<=0)continue;
            Vector2 anchor=Project(pulse.Position+Vector3.up*.65f,h,out bool front);
            if(!front||anchor.x<24||anchor.x>w-24||anchor.y<top||anchor.y>bottom)continue;
            string label=pulse.Name+(pulse.Count>1?" ×"+pulse.Count:"");float width=Mathf.Clamp(style.CalcSize(new GUIContent(label)).x+22,66,220);
            Rect rect=new Rect(anchor.x-width*.5f,anchor.y-25-age*18,width,25);bool placed=false;
            for(int lane=0;lane<4;lane++)
            {
                if(rect.x>=24&&rect.xMax<=w-24&&rect.y>=top&&rect.yMax<=bottom&&(!demo.AudioMapVisible||(!rect.Overlaps(new Rect(w-414,116,390,360))&&!rect.Overlaps(new Rect(24,116,370,102))))&&!drawn.Exists(other=>other.Overlaps(rect))){placed=true;break;}
                rect.y-=28;
            }
            if(!placed)continue;drawn.Add(rect);
            GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.035f,.09f,.105f,.78f*alpha),0,8);
            GUI.DrawTexture(new Rect(rect.x+5,rect.y+8,3,9),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.48f,.91f,.76f,alpha),0,1);
            style.normal.textColor=new Color(.92f,.98f,.96f,alpha);GUI.Label(new Rect(rect.x+9,rect.y,rect.width-13,rect.height),new GUIContent(label,pulse.Name),style);
        }
    }
    public bool HasMixerDuck { get; private set; }
    public float MixerDuckDb { get; private set; }
    public float DistanceGain { get; private set; }
    public bool HasDistanceModel { get; private set; }
    public string DistanceModel { get; private set; }
    private void Awake(){demo=GetComponent<BadShiftDemo>();}
    private void Update()
    {
        if(!AudioRuntime.Exists){pulses.Clear();return;}
        CaptureLabels();
        var mix=AudioRuntime.Instance.ActiveMixProfile;float db=0;
        HasMixerDuck=mix!=null&&mix.Routing!=null&&mix.Routing.Mixer!=null&&mix.Routing.Mixer.GetFloat("AudioMW_MusicDuckDb",out db);MixerDuckDb=db;
        var voice=demo.FridgeHandle.Voice;var source=voice!=null?voice.Source:null;
        DistanceGain=0;DistanceModel="No physical voice";
        HasDistanceModel=source!=null;if(source==null)return;
        float distance=demo.ListenerDistance,min=source.minDistance,max=source.maxDistance;
        if(source.rolloffMode==AudioRolloffMode.Linear){DistanceGain=1-Mathf.InverseLerp(min,max,distance);DistanceModel="Linear distance model";}
        else if(source.rolloffMode==AudioRolloffMode.Custom){DistanceGain=source.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(Mathf.Clamp01(distance/Mathf.Max(.001f,max)));DistanceModel="Custom distance curve";}
        else {DistanceGain=min/Mathf.Max(min,Mathf.Min(max,distance));DistanceModel="Approx. logarithmic distance";}
        DistanceGain=Mathf.Lerp(1,Mathf.Clamp01(DistanceGain),source.spatialBlend);
    }
    private static GUIStyle Style(int size,Color color,bool bold=false)
    {
        var s=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,fontStyle=bold?FontStyle.Bold:FontStyle.Normal};s.normal.textColor=color;return s;
    }
    private static void Panel(Rect r){GUI.DrawTexture(r,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.04f,.065f,.09f,.84f),0,10);}
    private static void Line(Vector2 a,Vector2 b,Color color)
    {
        Matrix4x4 old=GUI.matrix;Vector2 delta=b-a;GUI.matrix=old*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
        GUI.DrawTexture(new Rect(0,-1,delta.magnitude,2),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,0);GUI.matrix=old;
    }
    private Vector2 Project(Vector3 world,float h,out bool front)
    {
        float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);Vector3 p=demo.Game.View.WorldToScreenPoint(world);front=p.z>0;return new Vector2(p.x/scale,h-p.y/scale);
    }
    public void Draw(float w,float h,float bottom)
    {
        if(!AudioRuntime.Exists)return;
        Color white=new Color(.94f,.96f,.98f),muted=new Color(.66f,.75f,.81f),mint=new Color(.48f,.91f,.76f);
        var small=Style(12,muted);var text=Style(13,white);var title=Style(14,mint,true);
        float x=w-414,y=116,height=Mathf.Min(360,bottom-132);Panel(new Rect(x,y,390,height));
        GUI.Label(new Rect(x+16,y+12,358,24),"LIVE AUDIO / Tab changes view",title);
        var music=AudioSystem.Music;
        string transition=music.IsTransitionPending?"Queued: "+music.PendingTrack.name+" / "+music.PendingQuantization+" / "+Mathf.Max(0,(float)(music.PendingStartDspTime-AudioSettings.dspTime)).ToString("0.0")+" s":music.IsCrossfading?"Crossfade in progress":"Music: "+(music.CurrentTrack!=null?music.CurrentTrack.name:"Stopped");
        GUI.Label(new Rect(x+16,y+40,358,38),transition,text);
        GUI.Label(new Rect(x+16,y+78,358,36),"VO → music: "+(HasMixerDuck?MixerDuckDb.ToString("0.0")+" dB (mixer)":"duck parameter "+AudioSystem.GetParameter(demo.Game.VoiceDuck).ToString("0.00"))+"\nPhysical voices: "+AudioSystem.ActiveVoiceCount,small);
        GUI.Label(new Rect(x+16,y+121,358,22),"EVENT JOURNAL / newest first",title);
        var records=AudioSystem.Debugger.Records;float row=y+149;int shown=0;string previous=null;
        for(int i=records.Count-1;i>=0&&shown<3;i--)
        {
            var r=records[i];if(r.EventName==previous)continue;previous=r.EventName;
            string result=r.Outcome==PlaybackOutcome.Played?(r.ClipName??"selected child")+" / gain "+(r.HasGainCapture?(float.IsNegativeInfinity(r.FinalVolumeDb)?"−∞":r.FinalVolumeDb.ToString("0.0"))+" dB":"not captured"):r.DescribeOutcome();
            GUI.Label(new Rect(x+16,row,358,18),r.Time.ToString("0.0")+"s · "+r.EventName,text);
            GUI.Label(new Rect(x+16,row+19,358,36),result,small);row+=56;shown++;
        }
        if(shown==0)GUI.Label(new Rect(x+16,row,358,40),"Interact with a prop to inspect its actual event.",small);
        Transform source=demo.Fridge;string label="FRIDGE → PLAYER LISTENER";
        string detail=demo.ListenerDistance.ToString("0.0")+" m · distance gain ~"+(HasDistanceModel?(DistanceGain*100).ToString("0")+"%":"unavailable")+"\n"+DistanceModel+"; camera zoom does not move ears.";
        Color link=mint;
        if(AudioSystem.VoiceOver.IsSpeaking)
        {
            source=demo.Boss;label="VOICE → MUSIC DUCKING";detail=HasMixerDuck?"Mixer reduction "+MixerDuckDb.ToString("0.0")+" dB\nMusic recovers when the line ends.":"Duck parameter "+AudioSystem.GetParameter(demo.Game.VoiceDuck).ToString("0.00");link=new Color(1,.78f,.4f);
        }
        else if(Vector3.Distance(demo.Game.transform.position,demo.Room.position)<3)
        {
            source=demo.Room;label="ROOM SOURCE → PROCESSING";detail=new[]{"Dry / original clip","Echo / repeated reflections","Reverb / room tail"}[demo.EffectMode]+"\nQ changes processing; E taps a fixture.";
        }
        else if(Vector3.Distance(demo.Game.transform.position,demo.Microwave.position)<3)
        {
            source=demo.Microwave;label="CONTAINER → SELECTED CLIP";detail=demo.LastAnswer+"\nE plays / Q changes Random or ShuffleBag.";
        }
        Vector2 a=Project(source.position+Vector3.up*1.6f,h,out bool sourceFront),b=Project(demo.Game.Listener.position,h,out bool listenerFront);
        float proximity=ConnectionStrength(Vector3.Distance(source.position,demo.Game.Listener.position),Vector2.Distance(a,b));
        if(proximity>0&&sourceFront&&listenerFront&&a.x>24&&a.x<w-24&&b.x>24&&b.x<w-24&&a.y>110&&a.y<bottom&&b.y>110&&b.y<bottom)
        {
            link.a=proximity;Line(a,b,new Color(link.r,link.g,link.b,.6f*proximity));Vector2 direction=(b-a).normalized,side=new Vector2(-direction.y,direction.x);
            Line(b,b-direction*13+side*6,link);Line(b,b-direction*13-side*6,link);
        }
        GUI.Label(new Rect(x+16,y+height-20,358,18),"Event gain excludes distance, mixer and master. Not LUFS.",Style(10,muted));
        if(Vector3.Distance(source.position,demo.Game.Listener.position)>5)return;
        Rect info=new Rect(24,116,370,102);Panel(info);
        GUI.Label(new Rect(info.x+14,info.y+12,342,22),label,Style(14,link,true));GUI.Label(new Rect(info.x+14,info.y+40,342,52),detail,text);

    }
}
