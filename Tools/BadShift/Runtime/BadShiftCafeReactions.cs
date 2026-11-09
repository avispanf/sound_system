using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(180)]
public sealed class BadShiftCafeReactions : MonoBehaviour
{
    public BadShiftCafeLife Life;
    private Transform playerRig,enemyRig,arm;
    private Quaternion playerRest,enemyRest,armRest;
    private Transform[] crown;
    private Vector3[] crownScales;
    private void Start()
    {
        var d=Life.Demo;playerRig=d.PlayerAnimator.transform.Find("Rig");enemyRig=d.DanceAnimator.transform.Find("Rig");
        playerRest=playerRig.localRotation;enemyRest=enemyRig.localRotation;
        arm=d.Boss.GetComponentsInChildren<Transform>().First(t=>t.name=="Broccoli pointing arm");armRest=arm.localRotation;
        crown=d.Boss.GetComponentsInChildren<Transform>().Where(t=>t.name=="Broccoli crown").ToArray();crownScales=crown.Select(t=>t.localScale).ToArray();
    }
    private void Update(){if(playerRig!=null){playerRig.localRotation=playerRest;enemyRig.localRotation=enemyRest;}}
    private void LateUpdate()
    {
        float player=Mathf.Clamp01((Life.PlayerReactionUntil-Time.time)/.5f),enemy=Mathf.Clamp01((Life.EnemyReactionUntil-Time.time)/.5f);
        playerRig.localRotation=playerRest*Quaternion.Euler(0,0,Life.PlayerDead?85:Mathf.Sin(player*12)*player*12);
        enemyRig.localRotation=enemyRest*Quaternion.Euler(0,0,Life.EnemyDead?85:Mathf.Sin(enemy*12)*enemy*16);
        float reaction=Mathf.Clamp01((Life.BossReactionUntil-Time.time)/1.4f);
        float pose=Mathf.Floor(Time.time*12)/12;
        arm.localRotation=armRest*Quaternion.Euler(Mathf.Sin(pose*2)*8,Mathf.Sin(pose*1.3f)*12,Mathf.Sin(pose*(reaction>0?14:3))* (reaction>0?30:8));
        for(int i=0;i<crown.Length;i++){float stretch=1+Mathf.Sin(pose*5-i*.5f)*(.018f+reaction*.07f);crown[i].localScale=Vector3.Scale(crownScales[i],new Vector3(1/Mathf.Sqrt(stretch),stretch,1/Mathf.Sqrt(stretch)));}
    }
    private void OnDisable()
    {
        if(playerRig!=null)playerRig.localRotation=playerRest;
        if(enemyRig!=null)enemyRig.localRotation=enemyRest;
        if(arm!=null)arm.localRotation=armRest;
        if(crown!=null)for(int i=0;i<crown.Length;i++)if(crown[i]!=null)crown[i].localScale=crownScales[i];
    }
}
