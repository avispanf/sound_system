using AudioMW;
using UnityEngine;

public sealed class BadShiftNightTraffic : MonoBehaviour
{
    public Transform Car, Suspension, Neon;
    public Renderer[] Paint;
    public Material[] Finishes;
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
        Hum = AudioSystem.PlayTrackedAttached(NeonEvent, Neon);
        BeginPass(4.6f);
    }

    private void BeginPass(float offset)
    {
        distance = offset;
        Car.gameObject.SetActive(true);
        bool reverse = (Pass & 1) != 0;
        Car.localRotation = Quaternion.Euler(0, reverse ? 180 : 0, 0);
        foreach (var panel in Paint) panel.sharedMaterial = Finishes[Pass % Finishes.Length];
        RoofRack.SetActive(Pass % 4 == 1);
        TaxiLamp.SetActive(Pass % 4 == 3);
        PositionCar();
        Engine = AudioSystem.PlayTrackedAttached(EngineEvent, Car);
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
        if (distance < End * 2) return;
        Engine.Stop();
        Car.gameObject.SetActive(false);
        Pass++;
        wait = 1.5f + (Pass % 3) * .65f;
    }

    private void OnDisable()
    {
        Engine.Stop();
        Hum.Stop();
    }
}
