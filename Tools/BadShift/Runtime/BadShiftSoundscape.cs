using AudioMW;
using UnityEngine;

[DefaultExecutionOrder(320)]
public sealed class BadShiftSoundscape : MonoBehaviour
{
    public SoundOccluder TapOccluder;
    public BadShiftCoffeeMill Mill;
    public BadShiftDaylight Sky;
    public BadShiftNightTraffic Street;
    public AudioMWPlayground Game;
    public Transform Room, Tap, Cistern, Pump, Impeller, BowlWater, FlushButton, Gauge;
    private Vector3 waterScale, buttonPosition;
    private bool flushing;
    public GameObject Stream;
    public AudioReverbFilter ListenerReverb;
    public SoundEvent TapLoop, Flush, PumpLoop, SwitchOn, SwitchOff;
    public Renderer[] NeonParts;
    public Light[] SignLights;
    public bool Inside => Room != null && ContainsRoom(Game.Listener.position);
    public bool AfterHours { get; private set; }
    public SoundHandle Water { get; private set; }
    public SoundHandle PumpMotor { get; private set; }
    public float RoomMix { get; private set; }
    public string LastChange { get; private set; }
    private float messageUntil, pumpUntil, flushUntil;
    private bool initialized;
    private MaterialPropertyBlock paint;
    private Color[] litColors;
    private float[] lightIntensities;
    private static readonly int Emission=Shader.PropertyToID("_EmissionColor");

    private void Start()
    {
        waterScale=BowlWater.localScale;buttonPosition=FlushButton.localPosition;
        paint=new MaterialPropertyBlock();
        litColors=new Color[NeonParts.Length];
        for(int i=0;i<litColors.Length;i++) litColors[i]=NeonParts[i].sharedMaterial.GetColor(Emission);
        lightIntensities=new float[SignLights.Length];
        for(int i=0;i<SignLights.Length;i++) lightIntensities[i]=SignLights[i].intensity;
        SetNight(Sky.Darkness>=.5f,false);
        initialized=true;
    }

    public bool ContainsRoom(Vector3 position)
    {
        var p=Room.InverseTransformPoint(position);
        return Mathf.Abs(p.x)<1.9f && p.z>=-1.13f && p.z<1.9f && p.y>-.1f && p.y<3.1f;
    }

    public bool InServiceArea(Vector3 p) => ContainsRoom(p) || (p.x>Pump.position.x-1.8f && p.x<Pump.position.x+3.3f && p.z>=3.7f && p.z<9.3f);

    private void SetNight(bool night,bool transition)
    {
        AfterHours=night;
        for(int i=0;i<NeonParts.Length;i++)
        {
            NeonParts[i].GetPropertyBlock(paint);
            paint.SetColor(Emission,night?litColors[i]:Color.black);
            NeonParts[i].SetPropertyBlock(paint);
        }
        for(int i=0;i<SignLights.Length;i++)
        {
            SignLights[i].enabled=night;
            SignLights[i].intensity=night?lightIntensities[i]:0;
        }
        Street.SetStreetLights(night);
        if(transition) AudioSystem.PlayTrackedAttached(night?SwitchOn:SwitchOff,Street.Neon);
        Street.SetSignLit(night);
        Game.SetAfterHours(night);
        Notify(night?"After sunset · neon relay on / closing shift requested":"After sunrise · neon relay off / opening shift requested");
    }

    public void ToggleTap()
    {
        if(!Inside) return;
        if(Water.IsPlaying) StopTap();
        else if(TapOccluder!=null)
        {
            TapOccluder.Event=TapLoop;
            TapOccluder.Play();
            var voice=TapOccluder.ActiveVoice;
            Water=voice==null ? SoundHandle.None : new SoundHandle(voice.HandleId);
        }
        else Water=AudioSystem.PlayTrackedAttached(TapLoop,Tap);
        Stream.SetActive(Water.IsPlaying);
        Notify(Water.IsPlaying?"Tap open · flowing water starts the yard pump":"Tap closed · water stops");
    }

    public void StopTap()
    {
        Water.Stop();
        Water=SoundHandle.None;
        if (Stream!=null) Stream.SetActive(false);
    }

    public void FlushToilet()
    {
        if(!Inside || Time.time<flushUntil) return;
        AudioSystem.PlayTrackedAttached(Flush,Cistern);
        flushUntil=Time.time+Flush.GetClip(0).length;flushing=true;
        pumpUntil=Mathf.Max(pumpUntil,flushUntil);
        Notify("Cistern flushed · drain and refill start the yard pump");
    }

    public void PrimePump()
    {
        pumpUntil=Time.time+3;
        Notify("Pump primed · walk around the tank to hear its position");
    }

    private void Notify(string value){LastChange=value;messageUntil=Time.time+4.2f;}
    public string Caption
    {
        get
        {
            if(Inside) return "Tiled reflections " + Mathf.RoundToInt(RoomMix*100) + "% · " + (Time.time<flushUntil?"cistern refilling / pump running":Water.IsPlaying?"tap running · step outside and close the door":"tap closed");
            if(TapOccluder!=null && Water.IsPlaying && Vector3.Distance(Game.Listener.position,Tap.position)<8)
                return "Running tap · obstruction " + Mathf.RoundToInt(TapOccluder.Occlusion*100) + "% · move the door to compare";
            if(Mill!=null && Vector3.Distance(Game.Listener.position,Mill.transform.position)<2.8f)
                return Mill.Momentum>0 ? "Coffee grinder · speed " + Mathf.RoundToInt(Mill.Momentum*100) + "% raises pitch and volume" : "Coffee grinder · tap E to spin faster, then listen as it slows";
            if(Time.time<messageUntil) return LastChange;
            if(InServiceArea(Game.Listener.position))return PumpMotor.IsPlaying?"Service yard · pump motor running":"Service yard · E primes the pump; the sink and cistern also start it";
            if(Street.Siren.IsPlaying && Vector3.Distance(Game.Listener.position,Street.Car.position)<20)
                return "Patrol siren · attached to moving car / Doppler ×" + Street.SirenEvent.AttenuationPreset.DopplerLevel.ToString("0.00");
            var score=AudioSystem.Music.CurrentTrack;
            return (AfterHours?"Night · neon on":"Day · neon off") + "  /  " + (Game.CurrentBed!=null?Game.CurrentBed.name:"No ambience") + "  /  " + (score!=null?score.name:"Music stopped");
        }
    }

    private void LateUpdate()
    {
        if(!initialized || AudioListener.pause) return;
        if(!AfterHours && Sky.Darkness>.60f) SetNight(true,true);
        else if(AfterHours && Sky.Darkness<.38f) SetNight(false,true);
        float mix=Mathf.MoveTowards(RoomMix,Inside?1:0,Time.deltaTime*3.6f);
        if(mix!=RoomMix)
        {
            RoomMix=mix;
            ListenerReverb.enabled=mix>0;
            ListenerReverb.room=mix<=0?-10000:Mathf.Max(-10000,-450+2000*Mathf.Log10(mix));
        }
        if(flushing)
        {
            float progress=Mathf.Clamp01(1-(flushUntil-Time.time)/Flush.GetClip(0).length);
            float drain=progress<.24f?progress/.24f:1-(progress-.24f)/.76f;
            BowlWater.localScale=new Vector3(waterScale.x*(1-.67f*drain),waterScale.y,waterScale.z*(1-.67f*drain));
            FlushButton.localPosition=buttonPosition+Vector3.down*(progress<.12f?.026f:0);
            flushing=progress<1;
        }
        bool pumping=Water.IsPlaying || Time.time<pumpUntil;
        if(pumping && !PumpMotor.IsPlaying) PumpMotor=AudioSystem.PlayTrackedAttached(PumpLoop,Pump);
        if(!pumping && PumpMotor.IsPlaying) PumpMotor.Stop();
        Gauge.localRotation=Quaternion.Euler(0,0,pumping?-57:34);
        if(pumping) Impeller.Rotate(Vector3.forward,Time.deltaTime*360,Space.Self);
    }

    private void OnDisable()
    {
        Water.Stop();PumpMotor.Stop();
        if(ListenerReverb!=null)ListenerReverb.enabled=false;
        if(Stream!=null)Stream.SetActive(false);
    }
}
