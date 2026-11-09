using System.Linq;
using AudioMW;
using UnityEngine;

public sealed class BadShiftCafeLife : MonoBehaviour
{
    public BadShiftDemo Demo;
    public GameObject PlayerStick,EnemyStick;
    public int PlayerHealth { get; private set; }=100;
    public int EnemyHealth { get; private set; }=100;
    public bool PlayerDead=>PlayerHealth<=0;
    public bool EnemyDead=>EnemyHealth<=0;
    public bool Duel { get; private set; }
    public bool Muted { get; private set; }
    public float PlayerSwingUntil { get; private set; }
    public float EnemySwingUntil { get; private set; }
    public float PlayerReactionUntil { get; private set; }
    public float EnemyReactionUntil { get; private set; }
    public float BossReactionUntil { get; private set; }
    public string ReactionQuote=>Time.time<quoteUntil?quote:"";
    public string CombatStatus=>PlayerDead?"You fainted! Returning to work…":EnemyDead?"Rival down! Tea break, then another round.":"Stick sparring · You "+PlayerHealth+" / 100 · Rival "+EnemyHealth+" / 100";
    private BadShiftUseTarget[] targets;
    private Vector3 spawn;
    private Quaternion spawnRotation;
    private float nextSwing,nextEnemy,respawnAt,nextReaction,quoteUntil,volumeBefore;
    private string quote;
    private void Awake(){Demo=GetComponent<BadShiftDemo>();spawn=transform.position;spawnRotation=transform.rotation;volumeBefore=AudioListener.volume;}
    private void OnEnable(){BadShiftImpactProp.Reacted+=OnImpact;}
    private void OnDisable(){BadShiftImpactProp.Reacted-=OnImpact;}
    private void Start(){targets=FindObjectsByType<BadShiftUseTarget>(FindObjectsSortMode.None);PlayerStick.SetActive(false);EnemyStick.SetActive(false);}
    public void ToggleSound(){Muted=!Muted;AudioListener.volume=Muted?0:volumeBefore;}
    public BadShiftUseTarget Nearest()
    {
        if(targets==null)return null;
        BadShiftUseTarget result=null;float best=2.3f;
        foreach(var target in targets)if(target!=null&&target.isActiveAndEnabled)
        {
            float distance=target.Distance(transform.position);
            if(distance<best){best=distance;result=target;}
        }
        return result;
    }
    public string Prompt
    {
        get
        {
            if(PlayerDead)return "Reviving shortly · R resets the scene";
            if(Duel&&InReach())return EnemyDead?"Rival recovering…":"E / Swing stick";
            var target=Nearest();string label=target==null?"":target.Kind==BadShiftUseKind.Sink?"Tap sink / room sound":target.Kind==BadShiftUseKind.Toilet?"Tap cistern / room sound":target.Label;return target!=null?"E / "+label+(target.Kind==BadShiftUseKind.Fridge?" · G / upper door":""):"E / Use nearby objects · Shift / Run · Space / Jump";
        }
    }
    public void Interact()
    {
        if(PlayerDead||AudioListener.pause)return;
        if(Duel&&InReach()){Swing();return;}
        var pickup=Demo.Game.Pickups.Where(p=>p!=null&&!p.Collected).OrderBy(p=>Vector3.Distance(p.transform.position,transform.position)).FirstOrDefault();
        if(pickup!=null&&Vector3.Distance(pickup.transform.position,transform.position)<1){Demo.Game.TryCollect(pickup);return;}
        var target=Nearest();
        if(target==null){if(pickup!=null)Demo.Game.TryCollect(pickup);return;}
        switch(target.Kind)
        {
            case BadShiftUseKind.Microwave:Demo.PlayMicrowave();break;
            case BadShiftUseKind.Boss:Demo.Game.Speak();React("I am the boss. The broccoli boss.",false);break;
            case BadShiftUseKind.Bathroom:target.Door.ToggleFor(GetComponent<CharacterController>());break;
            case BadShiftUseKind.Fridge:Demo.Game.FridgeDoors.Lower.ToggleFor(GetComponent<CharacterController>());break;
            case BadShiftUseKind.Spar:Swing();break;
            case BadShiftUseKind.Sink:Demo.PlayRoom();React("Wash your bones before cooking.",false);break;
            case BadShiftUseKind.Toilet:Demo.PlayRoom();React("That echo is not on the menu.",false);break;
            case BadShiftUseKind.Prop:
                var body=target.GetComponent<Rigidbody>();
                if(body!=null&&!body.isKinematic){Vector3 direction=Vector3.ProjectOnPlane(body.worldCenterOfMass-transform.position,Vector3.up).normalized;body.AddForce((direction+Vector3.up*(body.mass<1?.8f:.15f))*body.mass*(body.mass<1?3:1.5f),ForceMode.Impulse);target.GetComponent<BadShiftImpactProp>()?.ReportPush();}
                break;
        }
    }
    private bool InReach()
    {
        Vector3 difference=Demo.Dancer.position-transform.position;difference.y=0;return difference.sqrMagnitude<2.7f*2.7f;
    }
    private bool ClearStrike()
    {
        Vector3 from=transform.position+Vector3.up*1.2f,to=Demo.Dancer.position+Vector3.up*1.2f;
        Vector3 delta=to-from;
        foreach(var hit in Physics.RaycastAll(from,delta.normalized,Mathf.Max(0,delta.magnitude-.6f),~0,QueryTriggerInteraction.Ignore))
            if(!hit.collider.transform.IsChildOf(transform)&&!hit.collider.transform.IsChildOf(Demo.Dancer))return false;
        return true;
    }
    public void Swing()
    {
        if(PlayerDead||EnemyDead||AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking||Time.time<nextSwing||!InReach()||!ClearStrike())return;
        if(!Duel){Duel=true;PlayerStick.SetActive(true);EnemyStick.SetActive(true);nextEnemy=Time.time+.8f;}
        nextSwing=Time.time+.55f;PlayerSwingUntil=Time.time+.35f;
        Vector3 direction=Demo.Dancer.position-transform.position;direction.y=0;if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);
        EnemyHealth=Mathf.Max(0,EnemyHealth-25);EnemyReactionUntil=Time.time+.45f;
        AudioSystem.PlayTrackedAtPosition(Demo.Game.Impact,Demo.Dancer.position);
        if(EnemyDead){respawnAt=Time.time+3;React("Mandatory tea break!",true);}
    }
    public void React(string message,bool broken)
    {
        quote=message;quoteUntil=Time.time+3;BossReactionUntil=Time.time+(broken?1.4f:.7f);
        if(broken){PlayerReactionUntil=Time.time+.65f;EnemyReactionUntil=Time.time+.65f;}
    }
    private void OnImpact(Vector3 position,bool broken)
    {
        if(!broken&&Time.time<nextReaction)return;
        nextReaction=Time.time+.7f;
        if(Vector3.Distance(position,transform.position)<4)PlayerReactionUntil=Time.time+.5f;
        if(Vector3.Distance(position,Demo.Dancer.position)<5)EnemyReactionUntil=Time.time+.5f;
        React(broken?"That plate comes out of your celery!":"Easy! This is a cafe, not a bowling alley.",broken);
    }
    private void Update()
    {
        if(AudioListener.pause)return;
        if((PlayerDead||EnemyDead)&&Time.time>=respawnAt)
        {
            if(PlayerDead)Demo.Game.RespawnAt(spawn,spawnRotation);
            PlayerHealth=EnemyHealth=100;Duel=false;PlayerStick.SetActive(false);EnemyStick.SetActive(false);
        }
        if(!Duel||PlayerDead||EnemyDead||AudioSystem.VoiceOver.IsSpeaking)return;
        if(!InReach()){Duel=false;PlayerStick.SetActive(false);EnemyStick.SetActive(false);return;}
        if(Time.time>=nextEnemy&&ClearStrike())
        {
            nextEnemy=Time.time+1.1f;EnemySwingUntil=Time.time+.35f;PlayerReactionUntil=Time.time+.45f;PlayerHealth=Mathf.Max(0,PlayerHealth-30);
            AudioSystem.PlayTrackedAtPosition(Demo.Game.Impact,transform.position);
            if(PlayerDead){respawnAt=Time.time+3;React("You are on a three-second break.",true);}
        }
    }
    private void OnDestroy(){if(Muted)AudioListener.volume=volumeBefore;}
}
