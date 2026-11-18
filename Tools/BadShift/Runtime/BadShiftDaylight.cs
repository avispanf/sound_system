using UnityEngine;

public enum BadShiftSkyMode { Auto, Night, Day }

[DefaultExecutionOrder(300)]
public sealed class BadShiftDaylight : MonoBehaviour
{
    public Light Sun, Fill;
    public Light[] OfficeLights;
    public Renderer[] OfficeDiffusers, Shafts;
    public Camera View;
    public BadShiftBattlePresentation Battle;
    [Min(12)] public float CycleSeconds = 96;
    [SerializeField, Range(0,1)] private float phase = .73f;
    public float Phase => phase;
    public BadShiftSkyMode Mode { get; private set; }
    public string ModeLabel => Mode==BadShiftSkyMode.Night ? "Light: Night" : Mode==BadShiftSkyMode.Day ? "Light: Day" : "Auto · " + Period;
    public string Period => phase<.18f || phase>=.86f ? "Night" : phase<.235f ? "Twilight" : phase<.29f ? "Sunrise" :
        phase<.65f ? "Day" : phase<.71f ? "Evening" : phase<.79f ? "Sunset" : "Dusk";
    private MaterialPropertyBlock block;
    private float lastPhase = -1, lastBattle = -1;
    private void Awake() { block = new MaterialPropertyBlock(); }
    private static readonly int Tint = Shader.PropertyToID("_Tint"), Emission = Shader.PropertyToID("_EmissionColor");
    private static readonly Color Moon = new Color(.53f,.64f,.98f), Day = new Color(1,.95f,.83f), RedSun = new Color(1,.23f,.075f);
    private static readonly Color NightSky = new Color(.08f,.095f,.16f), DaySky = new Color(.50f,.66f,.78f), SunsetSky = new Color(.65f,.31f,.32f);

    public void CycleMode() => SetMode((BadShiftSkyMode)(((int)Mode+1)%3));
    public void SetMode(BadShiftSkyMode mode) => Mode=mode;

    private void LateUpdate()
    {
        if (!AudioListener.pause)
        {
            if (Mode == BadShiftSkyMode.Auto) phase=Mathf.Repeat(phase+Time.deltaTime/Mathf.Max(12,CycleSeconds),1);
            else
            {
                float target=Mode==BadShiftSkyMode.Day ? .50f : 0;
                phase=Mathf.Repeat(Mathf.MoveTowardsAngle(phase*360,target*360,Time.deltaTime*180)/360,1);
            }
        }
        Apply();
    }

    private void Apply()
    {
        float battle=Battle==null ? 0 : Battle.Weight;
        if (phase == lastPhase && battle == lastBattle) return;
        lastPhase=phase; lastBattle=battle;
        float elevation=-Mathf.Cos(phase*2*Mathf.PI);
        float daylight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.20f,.42f,elevation));
        float horizon=1-Mathf.SmoothStep(0,1,Mathf.Abs(elevation-.065f)/.42f);
        float office=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.36f,.10f,elevation));
        float evening=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.43f,.59f,phase));
        Color warmth=Color.Lerp(new Color(1,.52f,.23f),RedSun,evening);
        Color sunColor=Color.Lerp(Color.Lerp(Moon,Day,daylight),warmth,horizon);
        Sun.color=Color.Lerp(sunColor,new Color(.58f,.64f,1),battle);
        Sun.intensity=(Mathf.Lerp(.35f,1.18f,daylight)+horizon*.38f)*(1-.58f*battle);
        Vector3 sunlight=new Vector3(Mathf.Lerp(.45f,1,horizon),-Mathf.Lerp(.32f,1.1f,Mathf.Clamp01(elevation)),Mathf.Lerp(.31f,-.28f,evening));
        Sun.transform.rotation=Quaternion.LookRotation(sunlight,Vector3.up);
        Fill.color=Color.Lerp(Moon,new Color(.81f,.89f,1),daylight);
        Fill.intensity=Mathf.Lerp(.16f,.38f,daylight)*(1-.6f*battle);
        var sky=Color.Lerp(Color.Lerp(NightSky,DaySky,daylight),SunsetSky,horizon*.86f);
        View.backgroundColor=Color.Lerp(sky,new Color(.055f,.065f,.12f),battle);
        RenderSettings.ambientSkyColor=Color.Lerp(Color.Lerp(new Color(.22f,.27f,.42f),new Color(.68f,.74f,.82f),daylight),new Color(.17f,.19f,.33f),battle);
        RenderSettings.ambientEquatorColor=Color.Lerp(new Color(.16f,.18f,.27f),new Color(.51f,.49f,.47f),daylight*(1-battle));
        RenderSettings.ambientGroundColor=Color.Lerp(new Color(.09f,.09f,.16f),new Color(.32f,.30f,.27f),daylight*(1-battle));
        foreach(var light in OfficeLights)
        {
            light.enabled=office>.003f;
            light.intensity=office*13*(1-.83f*battle);
        }
        block.Clear();
        block.SetColor(Emission,Color.white*(.012f+office*2.8f*(1-.7f*battle)));
        foreach(var fixture in OfficeDiffusers) fixture.SetPropertyBlock(block);
        block.Clear();
        block.SetColor(Tint,new Color(warmth.r*1.8f,warmth.g*1.8f,warmth.b*1.8f,horizon*.28f*(1-battle)));
        foreach(var shaft in Shafts)
        {
            shaft.enabled=horizon>.015f && battle<.995f;
            shaft.SetPropertyBlock(block);
            var scale=shaft.transform.localScale;
            scale.z=Mathf.Lerp(-1,1,evening);
            shaft.transform.localScale=scale;
        }
    }
}
