using UnityEngine;

[DefaultExecutionOrder(195)]
public sealed class BadShiftWeaponContact : MonoBehaviour
{
    public BadShiftBattle Battle;
    private readonly Vector3[] previousA=new Vector3[2],previousB=new Vector3[2];
    private readonly bool[] tracked=new bool[2];
    private readonly Collider[] contacts=new Collider[32];
    private void LateUpdate()
    {
        if(!Battle.Active){tracked[0]=tracked[1]=false;return;}
        Physics.SyncTransforms();
        for(int side=0;side<2;side++)
        {
            Transform stick=(side==0?Battle.Life.PlayerStick:Battle.Life.EnemyStick).transform;
            Vector3 a=stick.TransformPoint(Vector3.down*.45f),b=stick.TransformPoint(Vector3.up*.45f);
            if(!tracked[side]){previousA[side]=a;previousB[side]=b;tracked[side]=true;}
            if(Battle.ContactWindow(side==1))
            {
                float travel=Mathf.Max(Vector3.Distance(previousA[side],a),Vector3.Distance(previousB[side],b));
                int samples=Mathf.Clamp(Mathf.CeilToInt(travel/.08f),1,64);
                Collider opponent=side==0?Battle.EnemyBody:Battle.GetComponent<CharacterController>();
                for(int step=1;step<=samples;step++)
                {
                    float t=step/(float)samples;
                    int count=Physics.OverlapCapsuleNonAlloc(Vector3.Lerp(previousA[side],a,t),Vector3.Lerp(previousB[side],b,t),.10f,contacts,~0,QueryTriggerInteraction.Ignore);
                    for(int i=0;i<count;i++)if(contacts[i]==opponent){Battle.WeaponContact(side==1);break;}
                    if(!Battle.ContactWindow(side==1))break;
                }
            }
            previousA[side]=a;previousB[side]=b;
        }
    }
}
