using AudioMW;
using UnityEngine;

public sealed class BadShiftNightTraffic : MonoBehaviour
{
    public Transform Car, Suspension, Neon;
    public SoundEvent SirenEvent;
    public Light[] Beams;
    public Renderer[] HeadlampFaces;
    public GameObject EmergencyBar;
    public Renderer Blue, Red;
    public SoundHandle Siren { get; private set; }
    public bool Emergency => Pass > 0 && Pass % 4 == 0;
    private MaterialPropertyBlock lights;
    private bool signLit;
    public void SetSignLit(bool lit)
    {
        signLit=lit;
        if(lit && !Hum.IsPlaying) Hum=AudioSystem.PlayTrackedAttached(NeonEvent,Neon);
        if(!lit){Hum.Stop(); Hum=SoundHandle.None;}
    }
    public void SetStreetLights(bool night)
    {
        foreach(var beam in Beams)beam.enabled=night;
        foreach(var lamp in HeadlampFaces)lamp.enabled=night;
    }
    public Renderer[] Paint;
    public Material[] Finishes;
    public Material PatrolPaint;
    public GameObject RoofRack, TaxiLamp;
    public SoundEvent EngineEvent, NeonEvent;
    public float[] Speeds = { 6.4f, 9.2f, 7.6f, 10.5f };
    public int Pass { get; private set; }
    public float Speed => Speeds[Pass % Speeds.Length];
    public SoundHandle Engine { get; private set; }
    public SoundHandle Hum { get; private set; }
    private float distance, wait;
    private const float End = 9.4f;

    private void OnEnable()
    {
        if (Car == null || Neon == null || EngineEvent == null || NeonEvent == null) return;
        lights=new MaterialPropertyBlock();
        if(signLit)SetSignLit(true);
        BeginPass(4.6f);
    }

    private void BeginPass(float offset)
    {
        distance = offset;
        Car.gameObject.SetActive(true);
        bool reverse = (Pass & 1) != 0;
        Car.localRotation = Quaternion.Euler(0, reverse ? 180 : 0, 0);
        foreach (var panel in Paint) panel.sharedMaterial = Emergency && PatrolPaint!=null ? PatrolPaint : Finishes[Pass % Finishes.Length];
        if(EmergencyBar!=null)EmergencyBar.SetActive(Emergency);
        RoofRack.SetActive(Pass % 4 == 1);
        TaxiLamp.SetActive(Pass % 4 == 3);
        PositionCar();
        Engine = AudioSystem.PlayTrackedAttached(EngineEvent, Car);
        if(Emergency && SirenEvent!=null) Siren=AudioSystem.PlayTrackedAttached(SirenEvent,Car);
    }

    private void PositionCar()
    {
        int direction = (Pass & 1) == 0 ? -1 : 1;
        Car.localPosition = new Vector3(direction * -.22f, 0, direction * (distance - End));
        float phase = distance * 2.5f;
        Suspension.localPosition = Vector3.up * (.055f * Mathf.Sin(phase) + .018f * Mathf.Sin(phase * 2));
        Suspension.localRotation = Quaternion.Euler(1.5f * Mathf.Cos(phase), 0, 1.2f * Mathf.Sin(phase * .7f));
    }

    private void Update()
    {
        if (AudioListener.pause) return;
        if (!Car.gameObject.activeSelf)
        {
            wait -= Time.deltaTime;
            if (wait <= 0) BeginPass(0);
            return;
        }
        distance += Speed * Time.deltaTime;
        PositionCar();
        if(Emergency && Blue!=null)
        {
            float wave=Mathf.Sin(Time.time*9);
            Flash(Blue,new Color(.035f,.24f,1),wave);
            Flash(Red,new Color(1,.04f,.018f),-wave);
        }
        if (distance < End * 2) return;
        Siren.Stop();
        Engine.Stop();
        Car.gameObject.SetActive(false);
        Pass++;
        wait = 1.5f + (Pass % 3) * .65f;
    }

    private void Flash(Renderer lens,Color color,float phase)
    {
        lights.SetColor("_EmissionColor",color*(phase>0?4:.15f));
        lens.SetPropertyBlock(lights);
    }

    private void OnDisable()
    {
        Engine.Stop();
        Hum.Stop();
        Siren.Stop();
    }
}
