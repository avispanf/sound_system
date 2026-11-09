using System.Linq;
using AudioMW;
using UnityEngine;

[DefaultExecutionOrder(130)]
public sealed class BadShiftClayArms : MonoBehaviour
{
    public BadShiftDemo Demo;
    public bool Employee;
    private Transform[] joints;
    private Quaternion[] source,offset;
    private BadShiftClayGait gait;
    private float clock,danceWeight,runWeight,airWeight;
    private bool applied;
    private void Awake()
    {
        var bones=GetComponentsInChildren<Transform>();
        joints=new[]{"upperarm.l","lowerarm.l","hand.l","upperarm.r","lowerarm.r","hand.r"}.Select(n=>bones.First(t=>t.name==n)).ToArray();
        source=new Quaternion[6];offset=Enumerable.Repeat(Quaternion.identity,6).ToArray();gait=GetComponent<BadShiftClayGait>();
    }
    private void Restore()
    {
        if(!applied)return;
        for(int i=0;i<6;i++)joints[i].localRotation=source[i];applied=false;
    }
    private void Update(){Restore();}
    private void LateUpdate()
    {
        bool hold=AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking;
        if(!hold)
        {
            clock+=Time.deltaTime;
            bool dance=Employee&&AudioSystem.Music.CurrentTrack!=null&&AudioSystem.Music.CurrentTrack.name=="Combat";
            float response=1-Mathf.Exp(-9*Time.deltaTime);
            danceWeight=Mathf.Lerp(danceWeight,dance?1:0,response);
            runWeight=Mathf.Lerp(runWeight,!Employee?Mathf.Clamp01(Demo.Game.MovementSpeed/6):0,response);
            airWeight=Mathf.Lerp(airWeight,!Employee&&!Demo.Game.IsGrounded?1:0,response);
            float stride=gait.Cycle*Mathf.PI*2;
            float music=(float)AudioSystem.Music.Clock.GetBeat(AudioSettings.dspTime);
            float beat=Mathf.Floor(music*6)/6*Mathf.PI;
            float idle=Mathf.Floor(clock*12)/12;
            for(int side=0;side<2;side++)
            {
                float sign=side==0?1:-1;
                float swing=Mathf.Sin(stride+side*Mathf.PI);
                float flourish=Mathf.Sin(beat+side*Mathf.PI*.5f);
                float flick=Mathf.Sin(beat*2-.8f+side*Mathf.PI);
                offset[side*3]=Quaternion.Euler(swing*runWeight*28+airWeight*-24,flourish*danceWeight*18,sign*(Mathf.Sin(idle*2+side)*3+flourish*danceWeight*32+airWeight*20));
                offset[side*3+1]=Quaternion.Euler(Mathf.Sin(stride-.65f+side*Mathf.PI)*runWeight*22,0,sign*(danceWeight*(18+flick*26)+airWeight*22));
                offset[side*3+2]=Quaternion.Euler(Mathf.Sin(stride-1.1f+side*Mathf.PI)*runWeight*20,flourish*danceWeight*20,sign*(Mathf.Sin(beat*2-1.4f+side)*danceWeight*32+airWeight*12));
            }
        }
        for(int i=0;i<6;i++){source[i]=joints[i].localRotation;joints[i].localRotation=source[i]*offset[i];}
        applied=true;
    }
    private void OnDisable(){Restore();}
}
