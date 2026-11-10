using System.Linq;
using AudioMW;
using UnityEngine;

public sealed class BadShiftCafeLife : MonoBehaviour
{
    public BadShiftDemo Demo;
    public GameObject PlayerStick,EnemyStick;
    public BadShiftBattle Battle;
    public int PlayerHealth=>Battle!=null?Battle.PlayerHealth:100;
    public int EnemyHealth=>Battle!=null?Battle.EnemyHealth:100;
    public bool PlayerDead=>PlayerHealth<=0;
    public bool EnemyDead=>EnemyHealth<=0;
    public bool Duel=>Battle!=null&&Battle.Active;
    public bool Muted { get; private set; }
    public float PlayerSwingUntil=>Battle!=null?Battle.PlayerSwingUntil:0;
    public float EnemySwingUntil=>Battle!=null?Battle.EnemySwingUntil:0;
    public float PlayerReactionUntil=>Mathf.Max(playerReaction,Battle!=null?Battle.PlayerHurtUntil:0);
    public float EnemyReactionUntil=>Mathf.Max(enemyReaction,Battle!=null?Battle.EnemyHurtUntil:0);
    private float playerReaction,enemyReaction;
    public float BossReactionUntil { get; private set; }
    public string ReactionQuote=>Time.time<quoteUntil?quote:"";
    public string CombatStatus=>Battle!=null?Battle.Status:"";
    private BadShiftUseTarget[] targets;
    private float nextReaction,quoteUntil;
    private string quote;
    private void Awake(){Demo=GetComponent<BadShiftDemo>();Muted=BadShiftSilentLaunch.Requested;AudioListener.volume=Muted?0:1;}
    private void OnEnable(){BadShiftImpactProp.Reacted+=OnImpact;}
    private void OnDisable(){BadShiftImpactProp.Reacted-=OnImpact;}
    private void Start(){targets=FindObjectsByType<BadShiftUseTarget>(FindObjectsSortMode.None);PlayerStick.SetActive(false);EnemyStick.SetActive(false);}
    public void ToggleSound(){Muted=!Muted;AudioListener.volume=Muted?0:1;}
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
            if(Duel)return Battle.Status;
            var target=Nearest();string label=target==null?"":target.Kind==BadShiftUseKind.Sink?"Tap sink / room sound":target.Kind==BadShiftUseKind.Toilet?"Tap cistern / room sound":target.Label;return target!=null?"E / "+label+(target.Kind==BadShiftUseKind.Fridge?" · G / upper door":""):"E / Use nearby objects · Shift / Run · Space / Jump";
        }
    }
    public void Interact()
    {
        if(PlayerDead||AudioListener.pause)return;
        if(Duel)return;
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
            case BadShiftUseKind.Spar:Battle.Begin();break;
            case BadShiftUseKind.Sink:Demo.PlayRoom();React("Wash your bones before cooking.",false);break;
            case BadShiftUseKind.Toilet:Demo.PlayRoom();React("That echo is not on the menu.",false);break;
            case BadShiftUseKind.Prop:
                var body=target.GetComponent<Rigidbody>();
                if(body!=null&&!body.isKinematic){Vector3 direction=Vector3.ProjectOnPlane(body.worldCenterOfMass-transform.position,Vector3.up).normalized;body.AddForce((direction+Vector3.up*(body.mass<1?.8f:.15f))*body.mass*(body.mass<1?3:1.5f),ForceMode.Impulse);target.GetComponent<BadShiftImpactProp>()?.ReportPush();}
                break;
        }
    }
    public void Swing(){if(Battle!=null)Battle.Attack();}
    public void React(string message,bool broken)
    {
        quote=message;quoteUntil=Time.time+3;BossReactionUntil=Time.time+(broken?1.4f:.7f);
        if(broken){playerReaction=Time.time+.65f;enemyReaction=Time.time+.65f;}
    }
    private void OnImpact(Vector3 position,bool broken)
    {
        if(!broken&&Time.time<nextReaction)return;
        nextReaction=Time.time+.7f;
        if(Vector3.Distance(position,transform.position)<4)playerReaction=Time.time+.5f;
        if(Vector3.Distance(position,Demo.Dancer.position)<5)enemyReaction=Time.time+.5f;
        React(broken?"That plate comes out of your celery!":"Easy! This is a cafe, not a bowling alley.",broken);
    }
    private void OnDestroy(){AudioListener.volume=BadShiftSilentLaunch.Requested?0:1;}
}
