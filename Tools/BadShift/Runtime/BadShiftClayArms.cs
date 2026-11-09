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
    private BadShiftCafeLife life;
    private float clock,danceWeight,runWeight,airWeight,windupWeight;
    private bool applied;
    private void Awake()
    {
        var bones=GetComponentsInChildren<Transform>();
        joints=new[]{"upperarm.l","lowerarm.l","hand.l","upperarm.r","lowerarm.r","hand.r"}.Select(n=>bones.First(t=>t.name==n)).ToArray();
        source=new Quaternion[6];offset=Enumerable.Repeat(Quaternion.identity,6).ToArray();gait=GetComponent<BadShiftClayGait>();life=Demo.Game.Life;
    }
    private void Restore()
    {
        if(!applied)return;
        for(int i=0;i<6;i++)joints[i].localRotation=source[i];applied=false;
    }
    private void Update(){Restore();}
    private void LateUpdate()
    {
        bool hold=AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking||(life!=null&&(Employee?life.EnemyDead:life.PlayerDead));
        if(!hold)
        {
            clock+=Time.deltaTime;
            windupWeight=Mathf.MoveTowards(windupWeight,Employee&&life!=null&&life.Battle!=null&&life.Battle.EnemyWindingUp?1:0,Time.deltaTime*7);
            bool dance=Employee&&Demo.EmployeeDancing;
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
        float remaining=life==null?0:Mathf.Clamp01(((Employee?life.EnemySwingUntil:life.PlayerSwingUntil)-Time.time)/.35f);
        for(int i=0;i<6;i++){source[i]=joints[i].localRotation;joints[i].localRotation=source[i]*offset[i];if(i>=3)joints[i].localRotation*=Quaternion.Euler(35*windupWeight,0,-30*windupWeight);if(i>=3&&remaining>0)joints[i].localRotation*=Quaternion.Euler(Mathf.Sin((1-remaining)*Mathf.PI)*-65,0,Mathf.Sin((1-remaining)*Mathf.PI)*35);}
        if(life!=null&&life.Duel&&!hold)PoseWeapon(remaining);
        applied=true;
    }
    private void PoseWeapon(float remaining)
    {
        Transform body=Employee?life.Battle.EnemyBody.transform:Demo.Game.transform;
        Transform upper=joints[3],lower=joints[4],hand=joints[5];
        bool block=!Employee&&life.Battle.IsBlocking;
        float swing=remaining>0?1-remaining:0;
        Vector3 target=upper.position+body.forward*(block?.35f:.4f)+body.right*(block?-.2f:.15f)+Vector3.up*(block?.1f:-.3f);
        float lengthA=Vector3.Distance(upper.position,lower.position),lengthB=Vector3.Distance(lower.position,hand.position);
        Vector3 direction=(target-upper.position).normalized;float distance=Mathf.Clamp(Vector3.Distance(target,upper.position),Mathf.Abs(lengthA-lengthB)+.001f,(lengthA+lengthB)*.98f);
        target=upper.position+direction*distance;
        float along=(lengthA*lengthA-lengthB*lengthB+distance*distance)/(2*distance);
        Vector3 bend=Vector3.ProjectOnPlane(body.right-Vector3.up*.5f,direction).normalized;
        Vector3 elbow=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,lengthA*lengthA-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
        Vector3 blade=block?(Vector3.up-body.right*.45f).normalized:remaining>0?Quaternion.AngleAxis(Mathf.Lerp(75,-65,Mathf.SmoothStep(0,1,swing)),Vector3.up)*body.forward:Employee&&life.Battle.EnemyWindingUp?(-body.forward*.4f+Vector3.up).normalized:(body.forward*.35f+Vector3.up).normalized;
        Transform stick=(Employee?life.EnemyStick:life.PlayerStick).transform;
        float length=stick.TransformVector(Vector3.up).magnitude;
        stick.SetPositionAndRotation(hand.position+blade*length*.44f,Quaternion.FromToRotation(Vector3.up,blade));
    }
    private void OnDisable(){Restore();}
}
