using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(125)]
public sealed class BadShiftCafeReactions : MonoBehaviour
{
    public BadShiftCafeLife Life;
    private Transform playerRig,enemyRig,arm;
    private Quaternion playerRest,enemyRest,armRest;
    private Vector3 playerPosition,enemyPosition,playerScale,enemyScale,armPosition,facePosition;
    private Quaternion bossSource;
    private bool bossApplied;
    private Transform[] heads;
    private Quaternion[] headSources;
    private bool headsApplied;
    private Transform[] crown;
    private Vector3[] crownScales;
    private void Start()
    {
        var d=Life.Demo;playerRig=d.PlayerAnimator.transform.Find("Rig");enemyRig=d.DanceAnimator.transform.Find("Rig");
        playerRest=playerRig.localRotation;enemyRest=enemyRig.localRotation;
        playerPosition=playerRig.localPosition;enemyPosition=enemyRig.localPosition;playerScale=playerRig.localScale;enemyScale=enemyRig.localScale;
        heads=new[]{playerRig,enemyRig}.Select(r=>r.GetComponentsInChildren<Transform>().First(t=>t.name=="head")).ToArray();headSources=new Quaternion[2];
        arm=d.Boss.GetComponentsInChildren<Transform>().First(t=>t.name=="Broccoli pointing arm");armRest=arm.localRotation;armPosition=arm.localPosition;
        var face=d.Boss.GetComponentsInChildren<Transform>().Where(t=>t.name=="Sphere"&&t.GetComponent<Renderer>()!=null&&t.GetComponent<Renderer>().enabled).ToArray();
        facePosition=face.Aggregate(Vector3.zero,(sum,t)=>sum+d.Boss.InverseTransformPoint(t.position))/Mathf.Max(1,face.Length);
        crown=d.Boss.GetComponentsInChildren<Transform>().Where(t=>t.name=="Broccoli crown").ToArray();crownScales=crown.Select(t=>t.localScale).ToArray();
    }
    private void Restore()
    {
        if(playerRig!=null){playerRig.localRotation=playerRest;playerRig.localPosition=playerPosition;playerRig.localScale=playerScale;}
        if(enemyRig!=null){enemyRig.localRotation=enemyRest;enemyRig.localPosition=enemyPosition;enemyRig.localScale=enemyScale;}
        if(headsApplied){for(int i=0;i<2;i++)if(heads[i]!=null)heads[i].localRotation=headSources[i];headsApplied=false;}
    }
    private void Update(){Restore();}
    private void LateUpdate()
    {
        float player=Mathf.Clamp01((Life.PlayerReactionUntil-Time.time)/.5f),enemy=Mathf.Clamp01((Life.EnemyReactionUntil-Time.time)/.5f);
        PoseActor(false,player);PoseActor(true,enemy);
        PoseBoss();
    }
    private void PoseBoss()
    {
        var battle=Life.Battle;
        float reaction=Mathf.Clamp01((Life.BossReactionUntil-Time.time)/1.4f);
        float pose=Mathf.Floor(Time.time*12)/12;
        arm.localPosition=armPosition;
        arm.localRotation=armRest*Quaternion.Euler(Mathf.Sin(pose*2)*8,Mathf.Sin(pose*1.3f)*12,Mathf.Sin(pose*(reaction>0?14:3))*(reaction>0?30:8));
        float outcome=0,t=0;
        bool win=battle!=null&&battle.Phase==BadShiftBattlePhase.Victory;
        if(battle!=null&&battle.Finished)
        {
            t=battle.OutcomeTime;outcome=Mathf.SmoothStep(0,1,t/.3f)*(1-Mathf.SmoothStep(0,1,(t-4.7f)/.7f));
            if(!AudioListener.pause)bossSource=Life.Demo.Boss.localRotation;bossApplied=true;
            float nod=win?Mathf.Sin(t*9)*8:10;
            float shake=win?0:Mathf.Sin(t*7)*12;
            Life.Demo.Boss.localRotation=bossSource*Quaternion.Euler(nod*outcome,shake*outcome,Mathf.Sin(t*5)*3*outcome);
            arm.localRotation=Quaternion.Slerp(arm.localRotation,armRest*Quaternion.Euler(win?Mathf.Sin(t*11)*18:0,0,win?65+Mathf.Sin(t*11)*15:105),outcome);
            if(!win)arm.localPosition=Vector3.Lerp(armPosition,facePosition+Life.Demo.Boss.InverseTransformVector(-Life.Demo.Boss.forward*.15f+Life.Demo.Boss.right*.08f),outcome);
        }
        for(int i=0;i<crown.Length;i++)
        {
            float stretch=1+Mathf.Sin(pose*5-i*.5f)*(.018f+reaction*.07f)+Mathf.Sin(t*9-i*.65f)*outcome*(win?.075f:.035f);
            crown[i].localScale=Vector3.Scale(crownScales[i],new Vector3(1/Mathf.Sqrt(stretch),stretch,1/Mathf.Sqrt(stretch)));
        }
    }
    private void PoseActor(bool enemy,float reaction)
    {
        var battle=Life.Battle;int side=enemy?1:0;
        Transform rig=enemy?enemyRig:playerRig;
        Quaternion rest=enemy?enemyRest:playerRest;
        Vector3 position=enemy?enemyPosition:playerPosition,scale=enemy?enemyScale:playerScale;
        bool dead=enemy?Life.EnemyDead:Life.PlayerDead;
        float tilt=0,twist=0,pitch=0,stretch=1,lift=0,headLag=0;
        if(dead)
        {
            float p=battle.FallProgress;
            float flop=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.18f,.68f,p));
            float settle=Mathf.InverseLerp(.68f,1,p);
            float bounce=Mathf.Sin(settle*Mathf.PI*2.5f)*(1-settle);
            tilt=85*flop-Mathf.Sin(Mathf.Clamp01(p/.25f)*Mathf.PI)*10+bounce*12;
            stretch=1-Mathf.Sin(Mathf.Clamp01(p/.22f)*Mathf.PI)*.16f-Mathf.Sin(settle*Mathf.PI)*.13f;
            lift=Mathf.Sin(flop*Mathf.PI)*.06f;
            headLag=Mathf.Sin(p*19)*(1-p)*24;
        }
        else if(battle!=null&&battle.Celebrating(enemy))
        {
            float t=battle.OutcomeTime,w=battle.CelebrationWeight;
            float jig=t<1.4f?Mathf.Sin(t*Mathf.PI*4):Mathf.Sin((t-1.4f)*13);
            float bow=Mathf.Sin(Mathf.Clamp01((t-3.1f)/1)*Mathf.PI);
            lift=t<1.4f?Mathf.Abs(jig)*.17f*w:0;
            stretch=1+jig*.065f*w;tilt=jig*(t<1.4f?7:11)*w;
            Vector3 towardCamera=Vector3.ProjectOnPlane(Life.Demo.Game.View.transform.position-rig.position,Vector3.up);
            float facing=towardCamera.sqrMagnitude>.01f?Mathf.DeltaAngle(rig.parent.eulerAngles.y,Quaternion.LookRotation(towardCamera).eulerAngles.y):0;
            twist=(facing+Mathf.Sin(t*7)*14)*w;pitch=bow*28*w;headLag=-jig*9*w;
        }
        else if(enemy&&battle!=null&&battle.PhaseTwoIntro)
        {
            float t=battle.PhaseTwoIntroProgress,w=Mathf.Sin(t*Mathf.PI),shake=Mathf.Sin(t*38)*w;
            tilt=shake*9;twist=shake*18;pitch=-w*10;stretch=1-w*.10f+shake*.035f;
            headLag=-shake*24;lift=Mathf.Abs(Mathf.Sin(t*18))*.035f*w;
        }
        else
        {
            float swing=Mathf.Clamp01(((enemy?Life.EnemySwingUntil:Life.PlayerSwingUntil)-Time.time)/.35f);
            float wind=enemy&&battle!=null&&battle.EnemyWindingUp?1:swing>0?1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.76f,1-swing)):0;
            float until=enemy?Life.EnemySwingUntil:Life.PlayerSwingUntil;
            float recovery=swing<=0&&until>0?Mathf.SmoothStep(0,1,Mathf.Clamp01((until+.18f-Time.time)/.18f)):0;
            float strike=swing>0?Mathf.Sin(Mathf.Min(.72f,1-swing)*Mathf.PI):recovery*.77f;
            bool guard=!enemy&&battle!=null&&battle.IsBlocking;
            float blocked=!enemy&&battle!=null?Mathf.Clamp01((battle.PlayerBlockUntil-Time.time)/.22f):0;
            float recoil=Mathf.Sin((1-reaction)*Mathf.PI*3)*reaction;
            tilt=recoil*20+blocked*9;twist=wind*-18+strike*24;
            pitch=guard?12+blocked*8:wind*-7+strike*9;
            stretch=1-(guard?.07f:0)-wind*.045f-reaction*.09f;
            headLag=-twist*.7f+recoil*16;
            if(!enemy&&battle!=null&&battle.PlayerStrikeStep==2){twist=-twist;tilt-=strike*7;}
            if(!enemy&&battle!=null&&battle.PlayerStrikeStep==3){pitch=-wind*16+strike*22;stretch-=wind*.06f;headLag+=wind*14;}
            if(enemy&&battle!=null)
            {
                if(battle.EnemyAttack==BadShiftEnemyAttack.Backhand){twist=-twist;tilt-=strike*7;}
                if(battle.EnemyAttack==BadShiftEnemyAttack.Overhead)
                {
                    pitch=-wind*18+strike*24;stretch-=wind*.08f;headLag+=wind*16;
                    if(battle.EnemyWindingUp)tilt+=Mathf.Sin(battle.EnemyWindupProgress*18)*battle.EnemyWindupProgress*4;
                }
                if(battle.EnemyAttack==BadShiftEnemyAttack.Thrust){pitch=-wind*12+strike*20;twist=wind*12-strike*9;headLag+=wind*10;}
            }
            if(battle!=null&&battle.BlockFeedback>0)
            {
                float block=battle.BlockFeedback,wobble=Mathf.Sin((1-block)*Mathf.PI*4)*block;
                if(enemy){pitch=-block*18;tilt+=wobble*17;headLag+=wobble*24;stretch-=block*.09f;}
                else{stretch-=block*.07f;headLag+=wobble*9;}
            }
        }
        rig.localRotation=rest*Quaternion.Euler(pitch,twist,tilt);
        rig.localPosition=position+Vector3.up*lift;
        rig.localScale=Vector3.Scale(scale,new Vector3(1/Mathf.Sqrt(stretch),stretch,1/Mathf.Sqrt(stretch)));
        headSources[side]=heads[side].localRotation;heads[side].localRotation*=Quaternion.Euler(headLag*.45f,battle!=null&&battle.Celebrating(enemy)?headLag:-twist*.5f,headLag);headsApplied=true;
    }
    private void OnDisable()
    {
        Restore();
        if(arm!=null){arm.localRotation=armRest;arm.localPosition=armPosition;}
        if(bossApplied&&Life!=null&&Life.Demo!=null&&Life.Demo.Boss!=null)Life.Demo.Boss.localRotation=bossSource;
        if(crown!=null)for(int i=0;i<crown.Length;i++)if(crown[i]!=null)crown[i].localScale=crownScales[i];
    }
}
