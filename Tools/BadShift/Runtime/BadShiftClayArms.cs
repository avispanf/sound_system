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
    private Renderer[] surfaces;
    private Transform rig;
    private float clock,danceWeight,runWeight,airWeight,windupWeight;
    private bool applied;
    private void Awake()
    {
        var bones=GetComponentsInChildren<Transform>();rig=transform.Find("Rig");surfaces=GetComponentsInChildren<Renderer>(true);
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
        if(life!=null&&life.Duel)
        {
            bool dead=Employee?life.EnemyDead:life.PlayerDead;
            if(dead)PoseFalling();else if(life.Battle.Celebrating(Employee))PoseCelebration();else if(Employee&&life.Battle.PhaseTwoIntro)PoseOvertime();else PoseWeapon(remaining);
        }
        applied=true;
    }
    private void PoseWeapon(float remaining)
    {
        Transform body=Employee?life.Battle.EnemyBody.transform:Demo.Game.transform;
        Transform upper=joints[3],lower=joints[4],hand=joints[5];
        bool block=!Employee&&life.Battle.IsBlocking;
        float swing=remaining>0?1-remaining:0;
        float until=Employee?life.EnemySwingUntil:life.PlayerSwingUntil;
        float recovery=remaining<=0&&until>0?Mathf.SmoothStep(0,1,Mathf.Clamp01((until+.18f-Time.time)/.18f)):0;
        float cut=remaining>0?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.76f,swing)):recovery;
        float follow=Mathf.Sin(Mathf.InverseLerp(.76f,1,swing)*Mathf.PI)*.12f;
        float wind=Employee&&life.Battle.EnemyWindingUp?windupWeight:remaining>0?1-cut:0;
        float recoil=!Employee?Mathf.Clamp01((life.Battle.PlayerBlockUntil-Time.time)/.22f):0;
        bool preparing=Employee&&life.Battle.EnemyWindingUp;
        var attack=life.Battle.EnemyAttack;
        int step=Employee?(attack==BadShiftEnemyAttack.Backhand?2:attack==BadShiftEnemyAttack.Overhead?3:1):life.Battle.PlayerStrikeStep;
        bool variant=remaining>0||recovery>0||preparing;
        bool reverse=step==2&&variant,smash=step==3&&variant,thrust=Employee&&attack==BadShiftEnemyAttack.Thrust&&variant;
        float rebound=Employee?life.Battle.BlockFeedback:0;
        Vector3 target=upper.position+body.forward*(block?.32f-recoil*.10f:.4f-wind*.23f)+body.right*(block?-.24f:Mathf.Lerp(.36f,-.12f,cut))+Vector3.up*(block?.13f:-.3f+wind*.32f-follow);
        if(!block&&reverse)target=remaining>0||preparing?upper.position+body.forward*(.4f-wind*.15f)+body.right*Mathf.Lerp(-.28f,.3f,cut)+Vector3.up*(-.25f+wind*.1f):Vector3.Lerp(target,upper.position+body.forward*.4f+body.right*.3f-Vector3.up*.25f,recovery);
        if(!block&&smash)target=remaining>0||preparing?upper.position+body.forward*(.32f+cut*.1f)+Vector3.up*Mathf.Lerp(.27f,-.3f,cut):Vector3.Lerp(target,upper.position+body.forward*.42f-Vector3.up*.3f,recovery);
        if(thrust)
        {
            Vector3 drawn=upper.position-body.forward*.14f+body.right*.08f-Vector3.up*.12f;
            Vector3 extended=upper.position+body.forward*.62f+body.right*.04f-Vector3.up*.1f;
            target=remaining>0||preparing?Vector3.Lerp(drawn,extended,cut):Vector3.Lerp(target,extended,recovery);
        }
        if(rebound>0)target+=Vector3.up*(Mathf.Sin((1-rebound)*Mathf.PI)*.18f)-body.forward*rebound*.18f;
        SolveArm(3,target,body.right-Vector3.up*.5f);
        Vector3 blade=block?(Vector3.up-body.right*.48f-body.forward*recoil*.3f).normalized:remaining>0?Quaternion.AngleAxis(Mathf.Lerp(100,-78,cut),Vector3.up)*body.forward:Employee&&life.Battle.EnemyWindingUp?(-body.forward*.65f+Vector3.up).normalized:(body.forward*.35f+Vector3.up).normalized;
        Vector3 finish=Quaternion.AngleAxis(reverse?78:-78,Vector3.up)*body.forward;
        if(!block&&reverse&&(remaining>0||preparing))blade=Quaternion.AngleAxis(Mathf.Lerp(-100,78,cut),Vector3.up)*body.forward;
        if(smash)
        {
            finish=(body.forward-Vector3.up*.35f).normalized;
            if(remaining>0||preparing)blade=Vector3.Slerp((Vector3.up-body.forward*.35f).normalized,finish,cut);
        }
        if(thrust){finish=body.forward;if(remaining>0||preparing)blade=body.forward;}
        if(!block&&recovery>0)blade=Vector3.Slerp(blade,finish,recovery);
        if(rebound>0)blade=Vector3.Slerp(blade,(Vector3.up-body.forward*.7f+body.right*Mathf.Sin(rebound*18)*.35f).normalized,rebound);
        Transform stick=(Employee?life.EnemyStick:life.PlayerStick).transform;
        float length=stick.TransformVector(Vector3.up).magnitude;
        stick.SetPositionAndRotation(hand.position+blade*length*.44f,Quaternion.FromToRotation(Vector3.up,blade));
        if(block||smash&&(remaining>0||recovery>0||preparing))SolveArm(0,hand.position+blade*(block?.28f:.18f),-body.right-Vector3.up*.4f);
        else
        {
            joints[0].localRotation*=Quaternion.Euler(-wind*35,0,-wind*35-cut*18);
            joints[1].localRotation*=Quaternion.Euler(-wind*25,0,cut*32);
            joints[2].localRotation*=Quaternion.Euler(0,0,Mathf.Sin(swing*Mathf.PI*2)*24);
        }
    }
    private void PoseOvertime()
    {
        Transform body=life.Battle.EnemyBody.transform;
        float t=life.Battle.PhaseTwoIntroProgress,w=Mathf.Sin(t*Mathf.PI),shake=Mathf.Sin(t*38)*w;
        Vector3 rest=joints[3].position+body.forward*.4f+body.right*.36f-Vector3.up*.3f;
        Vector3 complaint=joints[3].position+body.forward*.25f+body.right*.12f+Vector3.up*(.32f+shake*.06f);
        SolveArm(3,Vector3.Lerp(rest,complaint,w),body.right-Vector3.up*.3f);
        Vector3 blade=(Vector3.up+body.right*shake*.3f+body.forward*.2f).normalized;
        Transform stick=life.EnemyStick.transform;float length=stick.TransformVector(Vector3.up).magnitude;
        stick.SetPositionAndRotation(joints[5].position+blade*length*.44f,Quaternion.FromToRotation(Vector3.up,blade));
        Vector3 fist=joints[0].position-body.right*.26f+body.forward*.2f+Vector3.up*(.3f-shake*.08f);
        SolveArm(0,Vector3.Lerp(joints[2].position,fist,w),-body.right-Vector3.up*.3f);
    }
    private void PoseCelebration()
    {
        var battle=life.Battle;Transform body=rig;
        float t=battle.OutcomeTime,w=battle.CelebrationWeight;
        float guitar=Mathf.SmoothStep(0,1,(t-1.2f)/.3f)*(1-Mathf.SmoothStep(0,1,(t-3)/.3f));
        float wave=Mathf.Sin(t*12),bow=Mathf.Sin(Mathf.Clamp01((t-3.1f)/1)*Mathf.PI);
        Vector3 rest=joints[3].position+body.forward*.4f+body.right*.36f-Vector3.up*.3f;
        Vector3 raised=joints[3].position+Vector3.up*(.36f+wave*.06f)+body.right*.2f+body.forward*.15f;
        Vector3 strum=joints[3].position+body.forward*.42f-body.right*.12f-Vector3.up*(.28f+wave*.045f);
        Vector3 target=Vector3.Lerp(raised,strum,guitar)-Vector3.up*(bow*.28f);
        SolveArm(3,Vector3.Lerp(rest,target,w),body.right-Vector3.up*.4f);
        Vector3 blade=Vector3.Slerp((Vector3.up+body.forward*.35f).normalized,(-body.right+Vector3.up*.12f).normalized,guitar);
        Transform stick=(Employee?life.EnemyStick:life.PlayerStick).transform;float length=stick.TransformVector(Vector3.up).magnitude;
        stick.SetPositionAndRotation(joints[5].position+blade*length*.44f,Quaternion.FromToRotation(Vector3.up,blade));
        Vector3 free=joints[0].position-body.right*.3f+Vector3.up*(.25f+wave*.07f)+body.forward*.15f;
        Vector3 fret=joints[5].position+blade*.38f;
        Vector3 left=Vector3.Lerp(free,fret,guitar);
        left=Vector3.Lerp(left,joints[0].position-body.right*.45f+body.forward*.3f,bow);
        SolveArm(0,Vector3.Lerp(joints[2].position,left,w),-body.right-Vector3.up*.3f);
    }
    private void SolveArm(int index,Vector3 target,Vector3 pole)
    {
        Transform upper=joints[index],lower=joints[index+1],hand=joints[index+2];
        float lengthA=Vector3.Distance(upper.position,lower.position),lengthB=Vector3.Distance(lower.position,hand.position);
        Vector3 direction=(target-upper.position).normalized;float distance=Mathf.Clamp(Vector3.Distance(target,upper.position),Mathf.Abs(lengthA-lengthB)+.001f,(lengthA+lengthB)*.98f);
        target=upper.position+direction*distance;
        float along=(lengthA*lengthA-lengthB*lengthB+distance*distance)/(2*distance);
        Vector3 bend=Vector3.ProjectOnPlane(pole,direction).normalized;
        Vector3 elbow=upper.position+direction*along+bend*Mathf.Sqrt(Mathf.Max(0,lengthA*lengthA-along*along));
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
    }
    private void PoseFalling()
    {
        float p=life.Battle.FallProgress;
        float flop=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.65f,p));
        float wobble=Mathf.Sin(p*23)*Mathf.Sin(Mathf.PI*p)*18;
        for(int side=0;side<2;side++)
        {
            float sign=side==0?1:-1;
            joints[side*3].localRotation*=Quaternion.Euler(-flop*28,wobble,sign*(flop*48+wobble));
            joints[side*3+1].localRotation*=Quaternion.Euler(wobble*1.6f,0,sign*flop*22);
            joints[side*3+2].localRotation*=Quaternion.Euler(wobble*2,0,sign*flop*28);
        }
        Transform body=Employee?life.Battle.EnemyBody.transform:Demo.Game.transform;
        float bottom=float.PositiveInfinity;
        foreach(var surface in surfaces)if(surface.enabled&&surface.gameObject.activeInHierarchy)bottom=Mathf.Min(bottom,surface.bounds.min.y);
        if(!float.IsInfinity(bottom))rig.position+=Vector3.up*Mathf.Max(0,body.position.y+.025f-bottom);
    }
    private void OnDisable(){Restore();}
}
