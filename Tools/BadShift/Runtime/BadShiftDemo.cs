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
    public string BossQuote => AudioRuntime.Exists && AudioSystem.VoiceOver.IsSpeaking ? AudioSystem.VoiceOver.CurrentLine.Subtitle : "";
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
    private bool details;
    private string answer = "Ready for questionable cuisine";
    private Vector3 danceBase;
    private float dancePhase;
    public string LastAnswer => answer;
    public int EffectMode => effect;
    public float ListenerDistance => Vector3.Distance(Game.Listener.position, Fridge.position);
    private void Start()
    {
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
            if (k.tabKey.wasPressedThisFrame) details = !details;
            if (k.qKey.wasPressedThisFrame) { if (Station() == 1) ToggleSelection(); if (Station() == 3) SetEffect((effect + 1) % 3); }
            if (k.xKey.wasPressedThisFrame) { if (Station() == 1) PlayMicrowave(); if (Station() == 2) Game.Speak(); if (Station() == 3) PlayRoom(); }
        }
        bool speaking = AudioSystem.VoiceOver.IsSpeaking;
        Game.FreezeForVoice = speaking;
        reactionTime += Time.deltaTime;
        for (int i=0;i<eyes.Length;i++) eyes[i].localScale = Vector3.Scale(eyeScales[i], new Vector3(1,Mathf.Repeat(reactionTime,4)<.12f?.15f:1,1));
        Vector3 towardPlayer = Game.transform.position - Boss.position;
        bool nearBoss = towardPlayer.sqrMagnitude < 16;
        if (nearBoss && !wasNearBoss && Time.time >= nextGreeting && !speaking)
        {
            Game.Speak(); nextGreeting = Time.time + 15;
        }
        wasNearBoss = nearBoss;
        float look = towardPlayer.sqrMagnitude < 81 ? Mathf.Clamp(Mathf.Atan2(-towardPlayer.x, -towardPlayer.z) * Mathf.Rad2Deg, -30, 30) : Mathf.Sin(reactionTime * .65f) * 12;
        bossYaw = Mathf.LerpAngle(bossYaw, look, 1 - Mathf.Exp(-3 * Time.deltaTime));
        Boss.localRotation = Quaternion.Euler(speaking ? Mathf.Sin(reactionTime*9)*5 : Mathf.Sin(reactionTime*1.2f)*1.2f, bossYaw, Mathf.Sin(reactionTime*.8f)*1.3f) * bossRest;
        MusicPlayer music = AudioSystem.Music;
        bool combat = music.CurrentTrack != null && music.CurrentTrack.name == "Combat";
        if (!speaking && !AudioListener.pause && combat)
        {
            dancePhase += Time.deltaTime * 7;
            Dancer.localRotation = dancerRest * Quaternion.Euler(0, Mathf.Sin(dancePhase) * 25, Mathf.Sin(dancePhase * 2) * 12);
            Dancer.localPosition = danceBase + Vector3.up * Mathf.Abs(Mathf.Sin(dancePhase)) * 0.15f;
        }
        if (!combat && !speaking) { Dancer.localPosition = danceBase; Dancer.localRotation = dancerRest; }
        string desiredPlayer = !Game.IsGrounded ? "Jump" : Time.time < Mathf.Max(responseUntil, Game.LastInteractionTime + .65f) ? "Interact" : Game.MovementSpeed > 4.2f ? "Run" : Game.MovementSpeed > .15f ? "Walk" : "Idle";
        if (PlayerAnimator != null)
        {
            PlayerAnimator.speed = speaking ? 0 : 1;
            if (playerPose != desiredPlayer) { PlayerAnimator.CrossFadeInFixedTime(desiredPlayer, .12f); playerPose = desiredPlayer; }
        }
        string desiredStaff = combat ? "Cheer" : "Idle";
        if (DanceAnimator != null)
        {
            DanceAnimator.speed = speaking ? 0 : 1;
            if (staffPose != desiredStaff) { DanceAnimator.CrossFadeInFixedTime(desiredStaff, .15f); staffPose = desiredStaff; }
        }
        microwaveMaterial.color = Time.time < responseUntil ? Color.Lerp(microwaveColor, Color.white, .45f) : microwaveColor;
        Game.AuditionMixGain = Station() == 4 ? 0 : Station() == 3 ? .15f : 1;
        RaveLight.color = combat ? Color.Lerp(new Color(1,.3f,.5f), new Color(.6f,.4f,1), .5f + .5f * Mathf.Sin(dancePhase)) : new Color(1,.85f,.65f);
        RaveLight.intensity = combat ? 4 : 1;
    }
    private void OnGUI()
    {
        Color ink = new Color(.15f,.18f,.2f);
        GUIStyle title = new GUIStyle(GUI.skin.label) { fontSize = 38, fontStyle = FontStyle.Bold };
        title.normal.textColor = ink;
        GUI.Label(new Rect(28,18,500,55), "BAD SHIFT", title);
        GUIStyle subtitle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold };
        subtitle.normal.textColor = ink;
        GUI.Label(new Rect(31,68,500,24), "A U D I O   P L A Y G R O U N D", subtitle);
        Color oldColor = GUI.color;
        GUI.color = new Color(.96f,.89f,.79f,.96f);
        GUI.DrawTexture(new Rect(20,Screen.height-(details ? 370 : 180),470,details ? 350 : 160), Texture2D.whiteTexture);
        GUI.color = oldColor;
        Color oldText = GUI.skin.label.normal.textColor;
        GUI.skin.label.normal.textColor = ink;
        GUILayout.BeginArea(new Rect(20,Screen.height-(details ? 370 : 180)+10,470,details ? 330 : 140));
        GUILayout.Label("BAD SHIFT  /  AUDIO PLAYGROUND");
        GUILayout.Label("Ingredients " + Game.Collected + "/" + Game.Pickups.Length + (Game.IsComplete ? "  SHIFT COMPLETE" : "  ·  E collect / blue exit"));
        int station = Station();
        if (station == 1) { GUILayout.Label("MICROWAVE  ·  X answer / Q mode"); GUILayout.Label((bag ? "ShuffleBag" : "Random") + "  ·  " + answer); }
        else if (station == 2) { GUILayout.Label("THE MANAGER  ·  X speech"); GUILayout.Label(AudioSystem.VoiceOver.IsSpeaking ? "Staff meeting. Nobody move." : "Your supervisor is a vegetable."); }
        else if (station == 3) { GUILayout.Label("STOCKROOM  ·  X same sound / Q processing"); GUILayout.Label(new[] { "Dry", "Echo · 280 ms repeats", "Reverb · room tail" }[effect]); }
        else if (station == 4) { GUILayout.Label("FRIDGE  ·  Walk towards / away"); GUILayout.Label("Music muted · listener distance " + ListenerDistance.ToString("0.0") + " m"); }
        else GUILayout.Label("Collect the glowing bananas · E picks other ingredients");
        MusicPlayer m = AudioSystem.Music;
        if (station == 0 || details) GUILayout.Label(m.IsTransitionPending ? "Music waiting for " + m.PendingQuantization + " · " + Mathf.Max(0,(float)(m.PendingStartDspTime - AudioSettings.dspTime)).ToString("0.0") + " s" : "Music " + (m.CurrentTrack != null ? m.CurrentTrack.name : "Stopped"));
        AudioMixProfile mix = AudioRuntime.Instance.ActiveMixProfile;
        if ((station == 2 || details || AudioSystem.VoiceOver.IsSpeaking) && mix != null && mix.Routing.Mixer.GetFloat("AudioMW_MusicDuckDb", out float db)) GUILayout.Label("Music duck " + db.ToString("0.0") + " dB");
        GUILayout.Label("Tab diagnostics · R reset · P pause · audio placeholders");
        if (details) { GUILayout.Label("WASD / Shift / Space · V VO · M music · L voice limits"); GUILayout.Label("Footsteps: stone / grass / metal. Q changes only the nearby station."); GUILayout.Label("Fridge: fixed 3D source, distance from player AudioListener."); GUILayout.Label("Stockroom: Unity AudioEchoFilter / AudioReverbFilter on one source."); GUILayout.Label(Game.LastDiagnostic); }
        GUILayout.EndArea();
        GUI.skin.label.normal.textColor = oldText;
        GUIStyle control = new GUIStyle(GUI.skin.button) { fontSize = 14, fontStyle = FontStyle.Bold };
        control.normal.textColor = ink; control.hover.textColor = ink; control.active.textColor = ink; control.focused.textColor = ink;
        if (GUI.Button(new Rect(Screen.width-170,22,150,38), "Reset scene", control)) Game.ResetScene();
        GUI.DrawTexture(new Rect(Screen.width-170,66,150,24),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.98f,.94f,.84f,.94f),0,5);
        GUI.Label(new Rect(Screen.width-165,66,145,24), "Cups broken " + BadShiftImpactProp.BreakCount + "/4", subtitle);
        BadShiftOrbitCamera orbit = Game.OrbitCamera;
        if (orbit != null)
        {
            float x = Screen.width - 340;
            GUI.DrawTexture(new Rect(x-6,Screen.height-91,337,78),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.98f,.94f,.84f,.94f),0,8);
            GUI.Label(new Rect(x,Screen.height-85,330,25), "Camera: RMB drag / wheel / C follow", subtitle);
            if (GUI.Button(new Rect(x,Screen.height-50,45,30), "-", control)) orbit.Zoom(-1);
            if (GUI.Button(new Rect(x+48,Screen.height-50,45,30), "+", control)) orbit.Zoom(1);
            if (GUI.Button(new Rect(x+96,Screen.height-50,45,30), "<", control)) orbit.Orbit(-15,0);
            if (GUI.Button(new Rect(x+144,Screen.height-50,45,30), ">", control)) orbit.Orbit(15,0);
            if (GUI.Button(new Rect(x+192,Screen.height-50,65,30), "Home", control)) orbit.Home();
            if (GUI.Button(new Rect(x+260,Screen.height-50,65,30), "Follow", control)) orbit.FocusPlayer();
        }
        if (!string.IsNullOrEmpty(BossQuote))
        {
            Vector3 anchor = Game.View.WorldToScreenPoint(Boss.position + Vector3.up * 4.9f);
            if (anchor.z > 0)
            {
                float width = Mathf.Min(320,Screen.width-24);
                Rect bubble = new Rect(Mathf.Clamp(anchor.x-width/2,12,Screen.width-width-12),Mathf.Clamp(Screen.height-anchor.y-75,105,Screen.height-140),width,72);
                GUIStyle quote = new GUIStyle(GUI.skin.box) { wordWrap = true, alignment = TextAnchor.MiddleCenter, fontSize = 16, padding = new RectOffset(14,14,9,9) };
                GUI.DrawTexture(new Rect(bubble.x,bubble.y+3,bubble.width,bubble.height),Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(0,0,0,.15f),0,12);
                GUI.DrawTexture(bubble,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,new Color(.98f,.94f,.84f),0,12);
                quote.normal.background = null; quote.normal.textColor = ink; GUI.Label(bubble,BossQuote,quote);
                GUI.Label(new Rect(Mathf.Clamp(anchor.x-6,bubble.x+8,bubble.xMax-20),bubble.yMax-2,20,20), "▼", subtitle);
            }
        }
    }
    private void OnDestroy() { fridgeHandle.Stop(); if (microwaveMaterial != null) Destroy(microwaveMaterial); }
}
