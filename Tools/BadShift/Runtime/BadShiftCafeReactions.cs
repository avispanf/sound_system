using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(125)]
public sealed class BadShiftCafeReactions : MonoBehaviour
{
    public BadShiftCafeLife Life;
    private Transform playerRig,enemyRig,arm;
    private Quaternion playerRest,enemyRest,armRest;
    private Vector3 playerPosition,enemyPosition,playerScale,enemyScale;
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
        arm=d.Boss.GetComponentsInChildren<Transform>().First(t=>t.name=="Broccoli pointing arm");armRest=arm.localRotation;
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
        float reaction=Mathf.Clamp01((Life.BossReactionUntil-Time.time)/1.4f);
        float pose=Mathf.Floor(Time.time*12)/12;
        arm.localRotation=armRest*Quaternion.Euler(Mathf.Sin(pose*2)*8,Mathf.Sin(pose*1.3f)*12,Mathf.Sin(pose*(reaction>0?14:3))* (reaction>0?30:8));
        for(int i=0;i<crown.Length;i++){float stretch=1+Mathf.Sin(pose*5-i*.5f)*(.018f+reaction*.07f);crown[i].localScale=Vector3.Scale(crownScales[i],new Vector3(1/Mathf.Sqrt(stretch),stretch,1/Mathf.Sqrt(stretch)));}
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
        }
        rig.localRotation=rest*Quaternion.Euler(pitch,twist,tilt);
        rig.localPosition=position+Vector3.up*lift;
        rig.localScale=Vector3.Scale(scale,new Vector3(1/Mathf.Sqrt(stretch),stretch,1/Mathf.Sqrt(stretch)));
        headSources[side]=heads[side].localRotation;heads[side].localRotation*=Quaternion.Euler(headLag*.45f,-twist*.5f,headLag);headsApplied=true;
    }
    private void OnDisable()
    {
        Restore();
        if(arm!=null)arm.localRotation=armRest;
        if(crown!=null)for(int i=0;i<crown.Length;i++)if(crown[i]!=null)crown[i].localScale=crownScales[i];
    }
}
