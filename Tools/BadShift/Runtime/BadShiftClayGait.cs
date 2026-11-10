using System.Linq;
using AudioMW;
using UnityEngine;

[DefaultExecutionOrder(120)]
public sealed class BadShiftClayGait : MonoBehaviour
{
    public BadShiftDemo Demo;
    public bool Employee;
    public float Cycle { get; private set; }
    public bool IsWalking { get; private set; }
    public bool[] Planted { get; } = new bool[2];
    public Vector3[] FootPositions { get; } = new Vector3[2];
    private Transform hips,chest;
    private Transform[] upper,lower,thigh,shin,knee,shoe;
    private Transform[] visuals;
    private Vector3[] positions,scales;
    private Quaternion[] rotations;
    private Vector3 hipPose,lastPosition;
    private Quaternion chestPose;
    private Vector3[] anchors=new Vector3[2],takeoff=new Vector3[2];
    private bool[] swinging=new bool[2];
    private bool applied,initialized,wasGrounded;
    private float lean;
    private readonly float[] recovery=new float[2];
    private readonly Vector3[] recoveryFrom=new Vector3[2];
    private readonly bool[] recovering=new bool[2];
    private readonly RaycastHit[] hits=new RaycastHit[32];
    private void Awake()
    {
        var bones=GetComponentsInChildren<Transform>();
        Transform Find(string name)=>bones.First(t=>t.name==name);
        hips=Find("hips");chest=Find("chest");
        upper=new[]{Find("upperleg.l"),Find("upperleg.r")};lower=new[]{Find("lowerleg.l"),Find("lowerleg.r")};
        thigh=upper.Select(t=>t.Find("Clay limb")).ToArray();shin=lower.Select(t=>t.Find("Clay limb")).ToArray();knee=lower.Select(t=>t.Find("Clay joint")).ToArray();shoe=new[]{Find("foot.l").Find("Clay shoe"),Find("foot.r").Find("Clay shoe")};
        visuals=thigh.Concat(shin).Concat(knee).Concat(shoe).ToArray();positions=visuals.Select(t=>t.localPosition).ToArray();scales=visuals.Select(t=>t.localScale).ToArray();rotations=visuals.Select(t=>t.localRotation).ToArray();
        lastPosition=transform.position;
    }
    private void Restore()
    {
        if(!applied)return;
        hips.localPosition=hipPose;chest.localRotation=chestPose;
        for(int i=0;i<visuals.Length;i++){visuals[i].localPosition=positions[i];visuals[i].localRotation=rotations[i];visuals[i].localScale=scales[i];}
        applied=false;
    }
    private void Update(){Restore();}
    private Vector3 Ground(Vector3 point)
    {
        int count=Physics.RaycastNonAlloc(point+Vector3.up*1.4f,Vector3.down,hits,3,~0,QueryTriggerInteraction.Ignore);float nearest=float.PositiveInfinity;
        for(int i=0;i<count;i++)if(!hits[i].collider.transform.IsChildOf(Demo.Game.transform)&&hits[i].normal.y>.6f&&hits[i].distance<nearest){nearest=hits[i].distance;point.y=hits[i].point.y+.075f;}
        return point;
    }
    private static void Segment(Transform visual,Vector3 a,Vector3 b)
    {
        visual.SetPositionAndRotation((a+b)*.5f,Quaternion.FromToRotation(Vector3.up,b-a));
        visual.localScale=new Vector3(.09f,Vector3.Distance(a,b)/visual.parent.lossyScale.y,.09f);
    }
    private void LateUpdate()
    {
        bool hold=AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking;
        float distance=Vector3.ProjectOnPlane(transform.position-lastPosition,Vector3.up).magnitude;lastPosition=transform.position;
        if(distance>1.5f){initialized=false;Cycle=0;swinging[0]=false;swinging[1]=false;recovering[0]=recovering[1]=false;}
        bool grounded=Employee||Demo.Game.IsGrounded;
        if(grounded&&!wasGrounded)initialized=false;
        wasGrounded=grounded;
        bool walking=!Employee&&grounded&&Demo.Game.MovementSpeed>.15f;
        if(!hold)
        {
            IsWalking=walking;
            if(walking)Cycle+=distance/2.2f;
            lean=Mathf.Lerp(lean,walking?Mathf.Min(1,Demo.Game.MovementSpeed/6):0,1-Mathf.Exp(-10*Time.deltaTime));
        }
        hipPose=hips.localPosition;chestPose=chest.localRotation;applied=true;
        Vector3[] originalFeet={shoe[0].position,shoe[1].position};
        float beat=Mathf.Floor(Cycle*24)/24*Mathf.PI*2;
        hips.position+=Vector3.up*(.43f+(IsWalking?Mathf.Sin(beat*2)*.045f:0));
        chest.localRotation*=Quaternion.Euler(lean*9,Mathf.Sin(beat-.6f)*lean*7,Mathf.Sin(beat)*lean*7);
        for(int i=0;i<2;i++)
        {
            Vector3 foot=originalFeet[i];float phase=Mathf.Repeat(Cycle+i*.5f,1);
            if(!Employee&&grounded)
            {
                Vector3 neutral=Demo.Game.transform.position+Demo.Game.transform.right*(i==0?-.21f:.21f);
                if(!initialized){anchors[i]=Ground(neutral);FootPositions[i]=anchors[i];}
                bool swing=IsWalking&&phase>=.55f;
                if(!hold)
                {
                    if(swing&&!swinging[i])takeoff[i]=anchors[i];
                    if(swing)
                    {
                        float u=(phase-.55f)/.45f;
                        Vector3 end=Ground(neutral+Demo.Game.transform.forward*.52f);
                        foot=Vector3.Lerp(takeoff[i],end,Mathf.SmoothStep(0,1,u))+Vector3.up*(Mathf.Sin(u*Mathf.PI)*(.20f+lean*.10f));
                        FootPositions[i]=foot;
                    }
                    else
                    {
                        if(swinging[i]){anchors[i]=Ground(FootPositions[i]);Demo.Game.PlayFootstep();}
                        foot=anchors[i];FootPositions[i]=foot;
                    }
                    if(!IsWalking)
                    {
                        if(!recovering[i]&&Vector3.ProjectOnPlane(neutral-anchors[i],Vector3.up).magnitude>.34f)
                        {recovering[i]=true;recovery[i]=0;recoveryFrom[i]=FootPositions[i];}
                        if(recovering[i])
                        {
                            recovery[i]=Mathf.Min(1,recovery[i]+Time.deltaTime/.18f);
                            Vector3 end=Ground(neutral);
                            foot=Vector3.Lerp(recoveryFrom[i],end,Mathf.SmoothStep(0,1,recovery[i]))+Vector3.up*(Mathf.Sin(recovery[i]*Mathf.PI)*.10f);
                            FootPositions[i]=foot;
                            if(recovery[i]>=1){anchors[i]=end;recovering[i]=false;}
                        }
                    }
                    else recovering[i]=false;
                    swinging[i]=swing;Planted[i]=!swing&&!recovering[i];
                }
                else foot=FootPositions[i];
            }
            else {Planted[i]=false;FootPositions[i]=foot;swinging[i]=false;}
            Vector3 hip=upper[i].position,ankle=foot+Vector3.up*.10f;
            Vector3 delta=ankle-hip;float length=delta.magnitude;
            if(length>1.12f)
            {
                ankle=hip+delta/length*1.12f;foot=ankle-Vector3.up*.10f;delta=ankle-hip;length=1.12f;
                FootPositions[i]=foot;Planted[i]=false;
                if(!Employee&&grounded){anchors[i]=foot;takeoff[i]=foot;swinging[i]=false;recovering[i]=false;}
            }
            Vector3 bend=Vector3.ProjectOnPlane(transform.forward,delta.normalized).normalized;
            Vector3 joint=(hip+ankle)*.5f+bend*Mathf.Sqrt(Mathf.Max(.015f,.56f*.56f-length*length*.25f));
            Segment(thigh[i],hip,joint);Segment(shin[i],joint,ankle);knee[i].position=joint;
            shoe[i].SetPositionAndRotation(foot,Quaternion.Euler(0,transform.eulerAngles.y,0));
        }
        initialized=true;
    }
    private void OnDisable(){Restore();}
}
