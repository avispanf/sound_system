using AudioMW;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BadShiftDemo : MonoBehaviour
{
    public AudioMWPlayground Game;
    public SoundEvent MicrowaveRandom;
    public SoundEvent MicrowaveBag;
    public SoundEvent FridgeSound;
    public Transform Microwave;
    public Transform MicrowaveVisual;
    public float MicrowaveResponseUntil => responseUntil;
    public Transform Fridge;
    public Transform Boss;
    public Transform Room;
    public Transform Dancer;
    public Light RaveLight;
    public Animator PlayerAnimator;
    public Animator DanceAnimator;
    private float responseUntil;
    private Material microwaveMaterial;
    private Color microwaveColor;
    private string playerPose;
    private string staffPose;
    private Quaternion bossRest;
    private float bossYaw;
    private bool wasNearBoss;
    private float nextGreeting;
    public string BossQuote => Game.Life!=null&&Game.Life.Battle!=null&&Game.Life.Battle.Finished?Game.Life.Battle.BossOutcomeQuote:AudioRuntime.Exists && AudioSystem.VoiceOver.IsSpeaking ? AudioSystem.VoiceOver.CurrentLine.Subtitle : Game.Life!=null?Game.Life.ReactionQuote:"";
    private Quaternion dancerRest;
    private float reactionTime;
    private Transform[] eyes;
    private Vector3[] eyeScales;
    public AudioSource RoomSource;
    public AudioEchoFilter Echo;
    public AudioReverbFilter Reverb;
    private SoundHandle fridgeHandle;
    private bool bag = true;
    private int effect;
    public bool AudioMapVisible { get; private set; } = true;
    public SoundHandle FridgeHandle=>fridgeHandle;
    private BadShiftAudioGuide guide;
    private string answer = "Ready for questionable cuisine";
    private Vector3 danceBase;
    private float dancePhase;
    public bool EmployeeDancing => (Game.Life==null||!Game.Life.Duel) && ((AudioSystem.Music.CurrentTrack!=null&&AudioSystem.Music.CurrentTrack.name=="Combat") || Vector3.Distance(Game.transform.position,Dancer.position)<3.5f);
    public string LastAnswer => answer;
    public int EffectMode => effect;
    public float ListenerDistance => Vector3.Distance(Game.Listener.position, Fridge.position);
    private void Start()
    {
        guide=GetComponent<BadShiftAudioGuide>();
        BadShiftImpactProp.ResetCounters();
        MicrowaveBag.ResetSelection(); MicrowaveRandom.ResetSelection();
        danceBase = Dancer.localPosition; dancerRest = Dancer.localRotation; bossRest = Boss.localRotation;
        fridgeHandle = AudioSystem.PlayTrackedAtPosition(FridgeSound, Fridge.position);
        microwaveMaterial = (MicrowaveVisual != null ? MicrowaveVisual : Microwave).GetComponent<Renderer>().material; microwaveColor = microwaveMaterial.color;
        var found = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in Boss.GetComponentsInChildren<Transform>()) if (child.name == "Sphere") found.Add(child);
        eyes = found.ToArray(); eyeScales = new Vector3[eyes.Length]; for (int i=0;i<eyes.Length;i++) eyeScales[i]=eyes[i].localScale;
        SetEffect(0);
    }
    public void SetEffect(int mode)
    {
        effect = Mathf.Clamp(mode, 0, 2);
        RoomSource.Stop(); Echo.enabled = effect == 1; Reverb.enabled = effect == 2;
    }
    public void PlayRoom() { RoomSource.Play(); }
    public void PlayMicrowave()
    {
        responseUntil = Time.time + .65f;
        AudioSystem.PlayTrackedAtPosition(bag ? MicrowaveBag : MicrowaveRandom, Microwave.position);
        if (AudioSystem.Debugger.TryGetLast(out EventDebugRecord record)) answer = record.Outcome == PlaybackOutcome.Played ? record.ClipName : record.DescribeOutcome();
    }
    public void ToggleSelection() { bag = !bag; }
    private int Station()
    {
        Vector3 p = Game.transform.position;
        if (Vector3.Distance(p, Microwave.position) < 3) return 1;
        if (Vector3.Distance(p, Boss.position) < 4) return 2;
        if (Vector3.Distance(p, Room.position) < 3) return 3;
        if (p.x > 5) return 4;
        return 0;
    }
    private void Update()
    {
        Keyboard k = Keyboard.current;
        if (AudioListener.pause) return;
        if (k != null)
        {
            if (k.tabKey.wasPressedThisFrame) AudioMapVisible = !AudioMapVisible;
            if (k.qKey.wasPressedThisFrame) { if (Station() == 1) ToggleSelection(); if (Station() == 3) SetEffect((effect + 1) % 3); }
            if (k.xKey.wasPressedThisFrame) { if (Station() == 1) PlayMicrowave(); if (Station() == 2) Game.Speak(); if (Station() == 3) PlayRoom(); }
        }
        bool speaking = AudioSystem.VoiceOver.IsSpeaking;
        Game.FreezeForVoice = speaking || (Game.Life!=null&&(Game.Life.PlayerDead||(Game.Life.Battle!=null&&Game.Life.Battle.LocksMovement)));
        reactionTime += Time.deltaTime;
        for (int i=0;i<eyes.Length;i++) eyes[i].localScale = Vector3.Scale(eyeScales[i], new Vector3(1,Mathf.Repeat(reactionTime,4)<.12f?.15f:1,1));
        Vector3 towardPlayer = Game.transform.position - Boss.position;
        bool nearBoss = towardPlayer.sqrMagnitude < 16;
        if (nearBoss && !wasNearBoss && Time.time >= nextGreeting && !speaking)
        {
            Game.Speak(); nextGreeting = Time.time + 15;
        }
        wasNearBoss = nearBoss;
        float look = (Game.Life!=null&&Game.Life.Battle!=null&&Game.Life.Battle.Finished)||towardPlayer.sqrMagnitude < 81 ? Mathf.Clamp(Mathf.Atan2(-towardPlayer.x, -towardPlayer.z) * Mathf.Rad2Deg, -30, 30) : Mathf.Sin(reactionTime * .65f) * 12;
        bossYaw = Mathf.LerpAngle(bossYaw, look, 1 - Mathf.Exp(-3 * Time.deltaTime));
        Boss.localRotation = Quaternion.Euler(speaking ? Mathf.Sin(reactionTime*9)*5 : Mathf.Sin(reactionTime*1.2f)*1.2f, bossYaw, Mathf.Sin(reactionTime*.8f)*1.3f) * bossRest;
        MusicPlayer music = AudioSystem.Music;
        bool combat = EmployeeDancing;
        if (!speaking && !AudioListener.pause && combat && (Game.Life==null||!Game.Life.Duel))
        {
            dancePhase += Time.deltaTime * 7;
            Dancer.localRotation = dancerRest * Quaternion.Euler(0, Mathf.Sin(dancePhase) * 25, Mathf.Sin(dancePhase * 2) * 12);
            Dancer.localPosition = danceBase + Vector3.up * Mathf.Abs(Mathf.Sin(dancePhase)) * 0.15f;
        }
        if (!combat && !speaking) { Dancer.localPosition = danceBase; Dancer.localRotation = Game.Life!=null&&Game.Life.Duel?Quaternion.identity:dancerRest; }
        string desiredPlayer = !Game.IsGrounded ? "Jump" : Time.time < Mathf.Max(responseUntil, Game.LastInteractionTime + .65f) ? "Interact" : Game.MovementSpeed > 4.2f ? "Run" : Game.MovementSpeed > .15f ? "Walk" : "Idle";
        if (PlayerAnimator != null)
        {
            PlayerAnimator.speed = speaking || (Game.Life!=null&&Game.Life.PlayerDead) ? 0 : 1;
            if (playerPose != desiredPlayer) { PlayerAnimator.CrossFadeInFixedTime(desiredPlayer, .12f); playerPose = desiredPlayer; }
        }
        string desiredStaff = combat ? "Cheer" : Game.Life!=null&&Game.Life.Battle!=null&&Game.Life.Battle.EnemySpeed>.15f?"Walk":"Idle";
        if (DanceAnimator != null)
        {
            DanceAnimator.speed = speaking || (Game.Life!=null&&Game.Life.EnemyDead) ? 0 : 1;
            if (staffPose != desiredStaff) { DanceAnimator.CrossFadeInFixedTime(desiredStaff, .15f); staffPose = desiredStaff; }
        }
        microwaveMaterial.color = Time.time < responseUntil ? Color.Lerp(microwaveColor, Color.white, .45f) : microwaveColor;
        Game.AuditionMixGain = Game.Life!=null&&Game.Life.Duel?(Game.Life.Battle.Finished?.25f:1):Station() == 4 ? 0 : Station() == 3 ? .15f : 1;
        RaveLight.color = combat ? Color.Lerp(new Color(1,.3f,.5f), new Color(.6f,.4f,1), .5f + .5f * Mathf.Sin(dancePhase)) : new Color(1,.85f,.65f);
        RaveLight.intensity = combat ? 4 : 1;
    }
    private GUIStyle TextStyle(int size, Color color, bool bold=false)
    {
        GUIStyle style=new GUIStyle(GUI.skin.label){fontSize=size,wordWrap=true,fontStyle=bold?FontStyle.Bold:FontStyle.Normal};style.normal.textColor=color;return style;
    }
    private void Panel(Rect rect, Color color) { GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,12); }
    private bool Button(Rect rect,string text,bool accent=false)
    {
        Color background=accent?new Color(.92f,.38f,.28f):new Color(.20f,.25f,.31f);
        if(rect.Contains(Event.current.mousePosition))background=Color.Lerp(background,Color.white,.12f);
        Panel(rect,background);
        GUIStyle style=TextStyle(14,Color.white,true);style.alignment=TextAnchor.MiddleCenter;
        return GUI.Button(rect,text,style);
    }
    private void OnGUI()
    {
        float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);
        Matrix4x4 previous=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float w=Screen.width/scale,h=Screen.height/scale;
        Color white=new Color(.94f,.96f,.98f),muted=new Color(.65f,.72f,.79f),panel=new Color(.04f,.065f,.09f,.78f);
        Panel(new Rect(24,24,w-48,76),panel);
        GUI.Label(new Rect(44,34,260,36),"BAD SHIFT",TextStyle(26,white,true));
        GUI.Label(new Rect(45,70,280,20),"AUDIOMW / AUDIO PLAYGROUND",TextStyle(11,muted));
        BadShiftTour tour=Game.Tour;bool watching=tour!=null&&tour.Running;
        if(Game.Life!=null&&Button(new Rect(w-586,42,124,40),Game.Life.Muted?"Sound off":"Sound on"))Game.Life.ToggleSound();
        if(Button(new Rect(w-714,42,116,40),AudioMapVisible?"Audio map on":"Audio map off"))AudioMapVisible=!AudioMapVisible;
        if(Button(new Rect(w-450,42,140,40),"Play yourself",!watching)&&tour!=null){tour.Stop();if(Game.Life!=null&&Game.Life.Duel)Game.Life.Battle.ReturnToCafe();}
        if(Button(new Rect(w-298,42,140,40),watching?"Replay demo":"Watch demo",watching)&&tour!=null)tour.WatchFromStart();
        if(Button(new Rect(w-146,42,102,40),"Reset")){BadShiftTour.StartOnLoad=false;Game.ResetScene();}
        var battle=Game.Life!=null?Game.Life.Battle:null;
        if(battle!=null&&battle.Active){BattleHUD(battle,w,h);GUI.matrix=previous;return;}
        if(battle!=null&&battle.CanInvite)
        {
            Vector3 point=Game.View.WorldToScreenPoint(Dancer.position+Vector3.up*3)/scale;
            Rect invitation=new Rect(Mathf.Clamp(point.x-210,24,w-444),Mathf.Clamp(h-point.y-74,112,h-320),420,72);
            Panel(invitation,panel);GUI.Label(new Rect(invitation.x+18,invitation.y+10,384,22),"THE BREADSTICK WARDEN",TextStyle(15,white,true));
            GUI.Label(new Rect(invitation.x+18,invitation.y+36,384,24),"E / Leave the dance floor. Enter the fight.",TextStyle(14,muted));
        }
        float bottom=h-210;
        if(guide!=null&&AudioMapVisible&&(battle==null||!battle.CanInvite))guide.Draw(w,h,bottom);
        Panel(new Rect(24,bottom,470,186),panel);
        GUI.Label(new Rect(44,bottom+16,430,26),tour!=null?tour.Heading:"Explore the cafe",TextStyle(17,white,true));
        GUI.Label(new Rect(44,bottom+48,430,86),Game.Life!=null&&(Game.Life.Duel||Game.Life.PlayerDead||Game.Life.EnemyDead)?Game.Life.CombatStatus:tour!=null?tour.Explanation:"WASD move / Shift run / Space jump",TextStyle(14,muted));
        GUI.Label(new Rect(44,bottom+142,430,25),"Ingredients "+Game.Collected+"/"+Game.Pickups.Length+"   ·   Cups "+BadShiftImpactProp.BreakCount+"/"+BadShiftImpactProp.InitialBreakableCount,TextStyle(13,white));
        Panel(new Rect(510,bottom,w-534,186),panel);
        int station=Station();MusicPlayer music=AudioSystem.Music;
        string state=station==1?(bag?"ShuffleBag":"Random")+" · "+answer:station==2?"Voice-over / mixer ducking":station==3?new[]{"Dry","Echo · 280 ms","Reverb · room tail"}[effect]:station==4?"Fridge · listener "+ListenerDistance.ToString("0.0")+" m":"Music · "+(music.CurrentTrack!=null?music.CurrentTrack.name:"Stopped");
        GUI.Label(new Rect(530,bottom+16,w-574,27),state,TextStyle(17,white,true));
        string status=music.IsTransitionPending?"Transition waiting for "+music.PendingQuantization:station==4?"Music muted · move the player to compare distance":"X interact · Q variation · V manager · P pause";
        if(Game.Door != null && Game.Door.CanInteract(Game.transform.position))status="E / "+(Game.Door.IsOpen?"Close":"Open")+" bathroom door"+(Game.Door.IsBlocked?" · step aside":"");
        if(station!=1 && Game.FridgeDoors != null && Game.FridgeDoors.CanInteract(Game.transform.position))status="E / lower door · G / upper door"+((Game.FridgeDoors.Lower.IsBlocked||Game.FridgeDoors.Upper.IsBlocked)?" · step aside":"");
        if(Game.Life!=null)status=Game.Life.Prompt;
        if(AudioSystem.VoiceOver.IsSpeaking && AudioRuntime.Instance.ActiveMixProfile!=null && AudioRuntime.Instance.ActiveMixProfile.Routing.Mixer.GetFloat("AudioMW_MusicDuckDb",out float db))status="Actual music duck: "+db.ToString("0.0")+" dB";
        GUI.Label(new Rect(530,bottom+49,w-574,46),status,TextStyle(13,muted));
        BadShiftOrbitCamera orbit=Game.OrbitCamera;
        if(orbit!=null)
        {
            float x=530,y=bottom+102;
            if(Button(new Rect(x,y,44,36),"−"))orbit.Zoom(-1);
            if(Button(new Rect(x+52,y,44,36),"+"))orbit.Zoom(1);
            if(Button(new Rect(x+104,y,44,36),"‹"))orbit.Orbit(-15,0);
            if(Button(new Rect(x+156,y,44,36),"›"))orbit.Orbit(15,0);
            if(Button(new Rect(x+208,y,80,36),"Overview"))orbit.Home();
            if(Button(new Rect(x+296,y,76,36),"Follow"))orbit.FocusPlayer();
        }
        GUI.Label(new Rect(530,bottom+146,w-574,22),"Camera: RMB drag / wheel / C follow   ·   Audio placeholders",TextStyle(12,muted));
        if(!string.IsNullOrEmpty(BossQuote))
        {
            Vector3 anchor=Game.View.WorldToScreenPoint(Boss.position+Vector3.up*4.9f)/scale;
            if(anchor.z>0){Rect bubble=new Rect(Mathf.Clamp(anchor.x-160,24,w-344),Mathf.Clamp(h-anchor.y-82,AudioMapVisible?230:112,bottom-90),320,76);Panel(bubble,new Color(.96f,.91f,.80f));GUIStyle quote=TextStyle(15,new Color(.12f,.15f,.18f));quote.alignment=TextAnchor.MiddleCenter;quote.padding=new RectOffset(14,14,8,8);GUI.Label(bubble,BossQuote,quote);}
        }
        GUI.matrix=previous;
    }
    private void HealthBar(Rect rect,string title,int health,Color accent)
    {
        Panel(rect,new Color(.035f,.045f,.075f,.94f));
        GUI.Label(new Rect(rect.x+18,rect.y+12,rect.width-36,24),title+"  /  "+health+" HP",TextStyle(15,Color.white,true));
        Rect track=new Rect(rect.x+18,rect.y+45,rect.width-36,10);Panel(track,new Color(.16f,.18f,.24f));
        if(health>0)Panel(new Rect(track.x,track.y,track.width*health/100f,track.height),accent);
    }
    private void BattleHUD(BadShiftBattle battle,float w,float h)
    {
        HealthBar(new Rect(44,120,340,72),"YOU",battle.PlayerHealth,new Color(.26f,.75f,1));
        HealthBar(new Rect(w-584,120,540,72),battle.SecondPhase?"THE WARDEN · OVERTIME":"THE BREADSTICK WARDEN",battle.EnemyHealth,new Color(1,.25f,.29f));
        if(!battle.Finished)Panel(new Rect(w*.5f-320,h-100,640,64),new Color(.035f,.045f,.075f,.90f));
        GUIStyle center=TextStyle(16,Color.white,true);center.alignment=TextAnchor.MiddleCenter;
        if(!battle.Finished)GUI.Label(new Rect(w*.5f-300,h-90,600,44),battle.Status,center);
        if(battle.PhaseTwoIntro||battle.EnemyWindingUp)
        {
            GUIStyle warning=TextStyle(15,new Color(1,.83f,.48f),true);warning.alignment=TextAnchor.MiddleRight;
            GUI.Label(new Rect(w-584,198,540,26),battle.PhaseTwoIntro?"PHASE 2 · UNPAID OVERTIME!":battle.EnemyAttackName+" · Watch the windup",warning);
            Rect track=new Rect(w-564,228,500,4);Panel(track,new Color(.16f,.18f,.24f,.8f));
            Panel(new Rect(track.x,track.y,track.width*(battle.PhaseTwoIntro?battle.PhaseTwoIntroProgress:battle.EnemyWindupProgress),4),new Color(1,.72f,.3f));
        }
        if(battle.ComboVisible)
        {
            GUIStyle combo=TextStyle(17,new Color(1,.85f,.48f),true);combo.alignment=TextAnchor.MiddleCenter;
            string chain=battle.ComboHits==3?"3 / 3 · SOUP SMASH!":battle.PlayerStrikeStep+" / 3 · "+battle.StrikeName+(battle.AttackQueued?" · Next bonk queued":" · LMB to chain");
            GUI.Label(new Rect(w*.5f-320,h-134,640,28),chain,combo);
        }
        if(!string.IsNullOrEmpty(battle.EnemyTaunt)&&battle.Phase!=BadShiftBattlePhase.Intro)
        {
            float scale=Mathf.Min(Screen.width/1600f,Screen.height/900f);Vector3 point=Game.View.WorldToScreenPoint(Dancer.position+Vector3.up*3.1f)/scale;
            Rect quote=new Rect(Mathf.Clamp(point.x-200,24,w-424),Mathf.Clamp(h-point.y-65,205,h-205),400,64);
            Panel(quote,new Color(.04f,.07f,.13f,.88f));GUI.Label(new Rect(quote.x+12,quote.y+6,376,52),battle.EnemyTaunt,center);
        }
        if(battle.Finished)
        {
            Rect card=new Rect(44,h-310,330,270);Panel(card,new Color(.035f,.045f,.075f,.92f));
            GUI.Label(new Rect(card.x+14,card.y+10,302,24),"MR BROCCOLI",center);
            Texture portrait=Game.GetComponent<BadShiftBattlePresentation>().BossPortrait;
            if(portrait!=null)GUI.DrawTexture(new Rect(card.x+14,card.y+40,302,158),portrait,ScaleMode.ScaleAndCrop);
            GUIStyle quote=TextStyle(14,new Color(.94f,.91f,.80f));quote.alignment=TextAnchor.MiddleCenter;
            GUI.Label(new Rect(card.x+14,card.y+204,302,56),battle.BossOutcomeQuote,quote);
        }
        if(battle.Phase==BadShiftBattlePhase.Intro||battle.Finished)
        {
            Rect box=battle.Finished?new Rect(w-604,h-260,560,220):new Rect(w*.5f-280,h*.5f-110,560,220);Panel(box,new Color(.035f,.045f,.075f,.94f));
            string title=battle.Phase==BadShiftBattlePhase.Intro?"LAST DANCE":battle.Phase==BadShiftBattlePhase.Victory?"VICTORY":"YOU LOST";
            GUIStyle heading=TextStyle(38,battle.Phase==BadShiftBattlePhase.Victory?new Color(.5f,1,.8f):new Color(1,.5f,.4f),true);heading.alignment=TextAnchor.MiddleCenter;
            GUI.Label(new Rect(box.x+20,box.y+24,520,52),title,heading);
            GUI.Label(new Rect(box.x+30,box.y+84,500,48),battle.Phase==BadShiftBattlePhase.Intro?"LMB attacks. Hold RMB to block.":battle.Phase==BadShiftBattlePhase.Victory?(battle.OvertimeFinish?"OVERTIME CANCELLED. Breadstick champion!":"The lunch break is yours."):"Out of bones. Ready for another shift?",center);
            if(battle.Finished)
            {
                if(Button(new Rect(box.x+72,box.y+154,196,42),"Fight again",true))battle.Rematch();
                if(Button(new Rect(box.x+292,box.y+154,196,42),"Reset demo"))Game.ResetScene();
            }
        }
    }
    private void OnDestroy() { fridgeHandle.Stop(); if (microwaveMaterial != null) Destroy(microwaveMaterial); }
}
