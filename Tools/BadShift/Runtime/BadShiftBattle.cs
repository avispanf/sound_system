using AudioMW;
using UnityEngine;

public enum BadShiftBattlePhase { Idle, Intro, Fighting, FallingVictory, FallingDefeat, Victory, Defeat }

[DefaultExecutionOrder(10)]
public sealed class BadShiftBattle : MonoBehaviour
{
    public BadShiftCafeLife Life;
    public CharacterController EnemyBody;
    public Vector3 ArenaCenter;
    public SoundEvent AttackSound,HitSound,BlockSound,VictorySound,DefeatSound,ChallengeSound;
    public BadShiftBattlePhase Phase { get; private set; }
    public bool Active=>Phase!=BadShiftBattlePhase.Idle;
    public bool Finished=>Phase==BadShiftBattlePhase.Victory||Phase==BadShiftBattlePhase.Defeat;
    public bool Falling=>Phase==BadShiftBattlePhase.FallingVictory||Phase==BadShiftBattlePhase.FallingDefeat;
    public bool LocksMovement=>Phase==BadShiftBattlePhase.Intro||Falling||Finished;
    public float FallProgress=>Falling||Finished?Mathf.Clamp01((Time.time-fallStarted)/.85f):0;
    public bool IsBlocking { get; private set; }
    public int PlayerHealth { get; private set; }=100;
    public int EnemyHealth { get; private set; }=100;
    public float PlayerSwingUntil { get; private set; }
    public float EnemySwingUntil { get; private set; }
    public float PlayerHurtUntil { get; private set; }
    public float EnemyHurtUntil { get; private set; }
    public float PlayerFlashUntil { get; private set; }
    public float EnemyFlashUntil { get; private set; }
    public float PlayerBlockUntil { get; private set; }
    public bool EnemyWindingUp=>enemyHitAt>0;
    public float EnemySpeed { get; private set; }
    public bool CanInvite=>!Active&&!Life.Demo.Game.Tour.Running&&Life.Nearest()!=null&&Life.Nearest().Kind==BadShiftUseKind.Spar;
    public string EnemyTaunt=>!Finished&&Time.time<tauntUntil?taunt:"";
    public string Status=>Phase==BadShiftBattlePhase.Intro?"THE BREADSTICK WARDEN · Get ready":Falling?"THE FINAL BONK":Phase==BadShiftBattlePhase.Victory?"VICTORY · The lunch break is yours":Phase==BadShiftBattlePhase.Defeat?"DEFEAT · The shift got you":IsBlocking?"BLOCKING · Release RMB to attack":EnemyWindingUp?"Incoming! Hold RMB to block · LMB attack":"LMB attack · Hold RMB block · WASD move · MMB orbit";
    private CharacterController player;
    private Vector3 playerSpawn,enemySpawn,enemyPush,enemyAim,enemyMotion;
    private Quaternion playerRotation,enemyRotation;
    private float phaseUntil,nextAttack,nextEnemy,enemyHitAt,fallStarted,nextTaunt,tauntUntil;
    private bool playerConnected,enemyConnected;
    private string taunt;
    private int tauntIndex;
    private SoundHandle resultTheme;
    private static readonly string[] Taunts={"Mr Broccoli said: defend the lunch break!","Prepare to get bonked!","Your bones look under-seasoned.","I trained in the cutlery drawer.","This is a five-star breadstick!","No refunds. Only ribs."};
    private void Awake()
    {
        EnemyBody.minMoveDistance=0;player=GetComponent<CharacterController>();playerSpawn=transform.position;playerRotation=transform.rotation;
        enemySpawn=EnemyBody.transform.position;enemyRotation=EnemyBody.transform.rotation;
    }
    private static float FlatDistance(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    public void Begin()
    {
        if(!CanInvite||AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking)return;
        resultTheme.Stop();Life.Demo.Game.Tour.Stop();Phase=BadShiftBattlePhase.Intro;phaseUntil=Time.time+1.2f;
        PlayerHealth=EnemyHealth=100;enemyHitAt=0;enemyPush=Vector3.zero;IsBlocking=false;
        Life.PlayerStick.SetActive(true);Life.EnemyStick.SetActive(true);Say(Taunts[tauntIndex++%Taunts.Length]);nextTaunt=Time.time+4;
        Life.Demo.Game.RequestMusic(true);Play(ChallengeSound,transform.position);
    }
    public void SetBlocking(bool value){IsBlocking=value&&Phase==BadShiftBattlePhase.Fighting&&Time.time>=PlayerSwingUntil;}
    public void Attack()
    {
        if(Phase!=BadShiftBattlePhase.Fighting||IsBlocking||AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking||Time.time<nextAttack)return;
        nextAttack=Time.time+.65f;PlayerSwingUntil=Time.time+.35f;playerConnected=false;
        Vector3 direction=Vector3.ProjectOnPlane(EnemyBody.transform.position-transform.position,Vector3.up);
        if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.LookRotation(direction);
        Play(AttackSound,transform.position);
    }
    public bool ContactWindow(bool enemy)
    {
        float remaining=(enemy?EnemySwingUntil:PlayerSwingUntil)-Time.time;
        return Phase==BadShiftBattlePhase.Fighting&&!AudioListener.pause&&!AudioSystem.VoiceOver.IsSpeaking&&remaining>0&&remaining<.29f&&!(enemy?enemyConnected:playerConnected);
    }
    private bool StrikeClear(CharacterController source,CharacterController target,Vector3 aim)
    {
        Vector3 delta=target.transform.position-source.transform.position;
        Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up);
        if(flat.magnitude>2.8f||Mathf.Abs(delta.y)>.9f||Vector3.Angle(aim,flat)>75)return false;
        foreach(var hit in Physics.RaycastAll(source.transform.position+Vector3.up,delta.normalized,Mathf.Max(0,delta.magnitude-.35f),~0,QueryTriggerInteraction.Ignore))
            if(!hit.collider.transform.IsChildOf(source.transform)&&!hit.collider.transform.IsChildOf(target.transform))return false;
        return true;
    }
    public void WeaponContact(bool enemyAttack)
    {
        if(!ContactWindow(enemyAttack)||!StrikeClear(enemyAttack?EnemyBody:player,enemyAttack?player:EnemyBody,enemyAttack?enemyAim:transform.forward))return;
        if(enemyAttack)enemyConnected=true;else playerConnected=true;
        if(enemyAttack&&IsBlocking&&Vector3.Dot(transform.forward,(EnemyBody.transform.position-transform.position).normalized)>.1f)
        {
            PlayerBlockUntil=Time.time+.22f;Life.Demo.Game.AddImpulse((transform.position-EnemyBody.transform.position).normalized*.8f);
            Play(BlockSound,transform.position);Say("Hey! No cutlery shields!");return;
        }
        Hurt(!enemyAttack);
    }
    private void Hurt(bool enemy)
    {
        Vector3 away=Vector3.ProjectOnPlane((enemy?EnemyBody.transform.position:transform.position)-(enemy?transform.position:EnemyBody.transform.position),Vector3.up).normalized;
        if(enemy){EnemyHealth=Mathf.Max(0,EnemyHealth-25);EnemyHurtUntil=Time.time+.5f;EnemyFlashUntil=Time.time+.22f;enemyPush=away*3.5f;Say("Ouch! My favourite rib!");}
        else{PlayerHealth=Mathf.Max(0,PlayerHealth-20);PlayerHurtUntil=Time.time+.5f;PlayerFlashUntil=Time.time+.22f;Life.Demo.Game.AddImpulse(away*3.5f);}
        Play(HitSound,enemy?EnemyBody.transform.position:transform.position);
        if(PlayerHealth==0||EnemyHealth==0)
        {
            Phase=EnemyHealth==0?BadShiftBattlePhase.FallingVictory:BadShiftBattlePhase.FallingDefeat;fallStarted=Time.time;phaseUntil=Time.time+1.15f;
            enemyHitAt=0;PlayerSwingUntil=EnemySwingUntil=0;IsBlocking=false;
            Say(EnemyHealth==0?"Fine. I needed a lie-down.":"Mr Broccoli will hear about this.");
        }
    }
    private void Say(string line){taunt=line;tauntUntil=Time.time+2.6f;}
    public void ReadPointer(bool attackPressed,bool blockHeld,Vector2 position)
    {
        SetBlocking(blockHeld);
        float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
        if(attackPressed&&position.y>105*scale&&position.y<Screen.height-200*scale)Attack();
    }
    private void Update()
    {
        enemyMotion=Vector3.zero;
        if(!Active||AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking){EnemySpeed=0;return;}
        float dt=Time.deltaTime;
        if(Falling&&Time.time>=phaseUntil)
        {
            Phase=EnemyHealth==0?BadShiftBattlePhase.Victory:BadShiftBattlePhase.Defeat;Life.Demo.Game.SilenceBattleBackground();
            resultTheme=AudioSystem.PlayTracked(EnemyHealth==0?VictorySound:DefeatSound);
        }
        if(Phase==BadShiftBattlePhase.Intro&&Time.time>=phaseUntil){Phase=BadShiftBattlePhase.Fighting;nextEnemy=Time.time+.6f;nextAttack=Time.time;}
        if(Phase!=BadShiftBattlePhase.Fighting){EnemySpeed=0;return;}
        if(Time.time>=nextTaunt){Say(Taunts[tauntIndex++%Taunts.Length]);nextTaunt=Time.time+4.2f;}
        Vector3 toward=Vector3.ProjectOnPlane(transform.position-EnemyBody.transform.position,Vector3.up);
        if(enemyHitAt>0)
        {
            EnemySpeed=0;
            if(Time.time>=enemyHitAt){enemyHitAt=0;EnemySwingUntil=Time.time+.35f;enemyConnected=false;Play(AttackSound,EnemyBody.transform.position);}
            return;
        }
        if(Time.time<EnemySwingUntil){EnemySpeed=0;return;}
        if(toward.sqrMagnitude>.01f)EnemyBody.transform.rotation=Quaternion.Slerp(EnemyBody.transform.rotation,Quaternion.LookRotation(toward),1-Mathf.Exp(-7*dt));
        EnemySpeed=0;
        if(toward.magnitude>1.5f&&Time.time>=EnemyHurtUntil&&FlatDistance(transform.position,ArenaCenter)<4){enemyMotion=toward.normalized*1.9f;EnemySpeed=1.9f;}
        if(toward.magnitude<1.85f&&Time.time>=nextEnemy){enemyAim=toward.normalized;enemyHitAt=Time.time+.7f;nextEnemy=Time.time+1.65f;}
    }
    private void LateUpdate()
    {
        if(!Active||AudioListener.pause||AudioSystem.VoiceOver.IsSpeaking)return;
        EnemyBody.Move((enemyMotion+enemyPush+Vector3.down*2)*Time.deltaTime);enemyPush=Vector3.MoveTowards(enemyPush,Vector3.zero,12*Time.deltaTime);
    }
    public void ReturnToCafe()
    {
        resultTheme.Stop();Phase=BadShiftBattlePhase.Idle;PlayerHealth=EnemyHealth=100;enemyHitAt=0;IsBlocking=false;tauntUntil=0;
        PlayerSwingUntil=EnemySwingUntil=PlayerHurtUntil=EnemyHurtUntil=PlayerFlashUntil=EnemyFlashUntil=PlayerBlockUntil=0;
        enemyPush=Vector3.zero;EnemySpeed=0;
        EnemyBody.enabled=false;EnemyBody.transform.SetPositionAndRotation(enemySpawn,enemyRotation);EnemyBody.enabled=true;
        Life.Demo.Game.RespawnAt(playerSpawn,playerRotation);Life.Demo.Game.FreezeForVoice=false;
        Life.PlayerStick.SetActive(false);Life.EnemyStick.SetActive(false);Life.Demo.Game.RequestMusic(false);
    }
    public void Rematch(){ReturnToCafe();Life.Demo.Game.RespawnAt(ArenaCenter+new Vector3(-1.5f,.15f,-.7f),Quaternion.LookRotation(Vector3.right));Begin();}
    private void OnDestroy(){resultTheme.Stop();}
    private static void Play(SoundEvent sound,Vector3 position){if(sound!=null)AudioSystem.PlayTrackedAtPosition(sound,position);}
}
