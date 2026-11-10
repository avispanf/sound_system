using AudioMW;
using UnityEngine;

[DefaultExecutionOrder(110)]
public sealed class BadShiftClayFace : MonoBehaviour
{
    public BadShiftDemo Demo;
    public Transform Head;
    public Transform[] Eyes;
    public bool Employee;
    private Quaternion rest;
    private Vector3 scale;
    private Vector3[] eyeScales;
    private float clock;
    private BadShiftClayGait gait;
    private void Awake()
    {
        gait=GetComponent<BadShiftClayGait>();
        rest=Head.localRotation;scale=Head.localScale;eyeScales=new Vector3[Eyes.Length];
        for(int i=0;i<Eyes.Length;i++)eyeScales[i]=Eyes[i].localScale;
    }
    private void LateUpdate()
    {
        if(AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking)return;
        var life=Demo.Game.Life;
        if(life!=null&&(Employee?life.EnemyDead:life.PlayerDead))
        {
            Head.localRotation=rest;Head.localScale=scale;
            for(int i=0;i<Eyes.Length;i++)Eyes[i].localScale=Vector3.Scale(eyeScales[i],new Vector3(1,.12f,1));
            return;
        }
        clock+=Time.deltaTime;
        float pose=Mathf.Floor(clock*12)/12+(Employee?.7f:0);
        bool dance=Employee&&AudioSystem.Music.CurrentTrack!=null&&AudioSystem.Music.CurrentTrack.name=="Combat";
        float energy=dance?1:Demo.Game.MovementSpeed>.15f&&!Employee?.55f:.2f;
        float sway=gait!=null&&gait.IsWalking?gait.Cycle*Mathf.PI*2-.8f:pose*3.2f;
        Head.localRotation=rest*Quaternion.Euler(Mathf.Sin(sway*2-.5f)*energy*9,Mathf.Sin(pose*1.7f)*12,Mathf.Sin(sway)*energy*12);
        float stretch=1+Mathf.Sin(pose*7)*energy*.065f;
        Head.localScale=Vector3.Scale(scale,new Vector3(1/Mathf.Sqrt(stretch),stretch,1/Mathf.Sqrt(stretch)));
        float blink=Mathf.Repeat(clock+(Employee?1.1f:0),3.7f);
        float open=blink<.16f?Mathf.Max(.08f,Mathf.Abs(blink-.08f)/.08f):1;
        for(int i=0;i<Eyes.Length;i++)Eyes[i].localScale=Vector3.Scale(eyeScales[i],new Vector3(1,open,1));
    }
    private void OnDisable()
    {
        if(eyeScales==null)return;
        Head.localRotation=rest;Head.localScale=scale;
        for(int i=0;i<Eyes.Length;i++)Eyes[i].localScale=eyeScales[i];
    }
}
