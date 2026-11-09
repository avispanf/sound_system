using System;
using System.IO;
using AudioMW;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CharacterController))]
public sealed class AudioMWPlayground : MonoBehaviour
{
    public SoundEvent Footsteps;
    public SoundEvent Landing;
    public SoundEvent Impact;
    public SoundEvent Pickup;
    public SoundEvent Ambience;
    public SoundEvent DangerAmbience;
    public SoundParameter AmbienceFade;
    public Transform Exit;
    public SoundEvent StressLoop;
    public SoundParameter Surface;
    public SoundParameter Intensity;
    public SoundParameter VoiceDuck;
    public SoundBank Bank;
    public VoiceLine[] Lines;
    public MusicStateProfile Music;
    public AudioClip Stinger;
    public Camera View;
    public bool DioramaCamera;
    public BadShiftTour Tour;
    public BadShiftDoor Door;
    public BadShiftFridge FridgeDoors;
    public BadShiftOrbitCamera OrbitCamera;
    public float JumpHeight = .55f;
    public bool IsResetting { get; private set; }
    public Transform Listener;
    public PlaygroundPickup[] Pickups;
    private CharacterController character;
    private IDisposable bankLease;
    private readonly SoundHandle[] stress = new SoundHandle[3];
    private readonly ParameterStore local = new ParameterStore();
    private SoundHandle ambience;
    private SoundHandle retiringAmbience;
    private float ambienceBlend;
    private float vertical;
    private Vector3 planarVelocity;
    public bool FreezeForVoice;
    public float LastInteractionTime { get; private set; } = -10;
    public BadShiftClayGait ClayGait;
    public BadShiftCafeLife Life;
    public void PlayFootstep() { AudioSystem.PlayTrackedAttached(Footsteps, transform, local); }
    public bool IsGrounded => character != null && character.isGrounded;
    public float MovementSpeed => character == null ? 0 : new Vector2(character.velocity.x, character.velocity.z).magnitude;
    private float airborneSeconds;
    private float stride;
    private float stressUntil;
    private float intensity;
    private bool danger;
    private bool requestedCombat;
    private bool paused;
    private bool musicMuted;
    private bool help = true;
    private bool complete;
    private int collected;
    private int lineIndex;
    private string subtitle = "";
    private string surfaceName = "Stone";
    private string notice = "Collect five gold relays with E, then reach the blue exit.";
    private float noticeUntil = 10;
    private string lastDiagnostic = "No rejected requests yet.";
    public float AuditionMixGain = 1;
    public bool IsComplete { get { return complete; } }
    public int Collected { get { return collected; } }
    public string LastDiagnostic { get { return lastDiagnostic; } }

    private void Start()
    {
        character = GetComponent<CharacterController>();
        bankLease = AudioSystem.AcquireBank(Bank);
        if (AudioRuntime.Instance.ActiveMixProfile == null)
        {
            AudioSystem.VoiceOver.DuckParameter = VoiceDuck;
            AudioSystem.VoiceOver.DuckFadeSeconds = 0.3f;
        }
        AudioSystem.VoiceOver.SubtitleChanged += Subtitle;
        ChangeAmbience(false);
        Music.Request(AudioSystem.Music, "Explore");
    }

    private void Update()
    {
        Keyboard keys = Keyboard.current;
        if ((keys != null && keys.hKey.wasPressedThisFrame)) help = !help;
        if ((keys != null && keys.pKey.wasPressedThisFrame))
        {
            paused = !paused; AudioSystem.SetPaused(paused); Time.timeScale = paused ? 0 : 1;
        }
        if ((keys != null && keys.rKey.wasPressedThisFrame)) { ResetScene(); return; }
        if ((keys != null && keys.digit1Key.wasPressedThisFrame)) musicMuted = !musicMuted;
        if ((keys != null && keys.f8Key.wasPressedThisFrame)) SaveSession();
        if (Time.unscaledTime >= stressUntil) foreach (SoundHandle handle in stress) handle.Stop();
        if (paused) return;
        Vector3 direction = new Vector3(((keys != null && keys.dKey.isPressed) ? 1 : 0) - ((keys != null && keys.aKey.isPressed) ? 1 : 0), 0, ((keys != null && keys.wKey.isPressed) ? 1 : 0) - ((keys != null && keys.sKey.isPressed) ? 1 : 0)).normalized;
        if (OrbitCamera != null) direction = OrbitCamera.MovementDirection(direction);
        if (Tour != null && Tour.Running) direction = Tour.Direction;
        if (FreezeForVoice) direction = Vector3.zero;
        MoveCharacter(direction, (Tour == null || !Tour.Running) && (keys != null && keys.spaceKey.wasPressedThisFrame), (Tour == null || !Tour.Running) && (keys != null && keys.leftShiftKey.isPressed), Time.deltaTime);
        bool nowGrounded = character.isGrounded;
        if (nowGrounded)
        {
            if (airborneSeconds >= 0.08f && vertical < 0) AudioSystem.PlayTrackedAttached(Landing, transform);
            airborneSeconds = 0;
        }
        else airborneSeconds += Time.deltaTime;
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up * 0.4f, Vector3.down, out hit, 1.2f))
        {
            PlaygroundSurface ground = hit.collider.GetComponent<PlaygroundSurface>();
            if (ground != null) { local.Set(Surface, ground.State); surfaceName = ground.Label; }
        }
        if (nowGrounded && MovementSpeed > .15f && ClayGait == null)
        {
            stride += MovementSpeed * Time.deltaTime;
            if (stride >= 1.5f) { stride = 0; AudioSystem.PlayTrackedAttached(Footsteps, transform, local); }
        }
        else stride = 0;
        bool inDanger = (Life!=null&&Life.Duel) || transform.position.z > 3.5f;
        if (inDanger != danger) { danger = inDanger; RequestMusic(danger); ChangeAmbience(danger); }
        ambienceBlend = Mathf.MoveTowards(ambienceBlend, 1, Time.deltaTime / 0.7f);
        bool battleResult=Life!=null&&Life.Battle!=null&&Life.Battle.Finished;
        ambience.SetParameter(AmbienceFade, battleResult?0:ambienceBlend); retiringAmbience.SetParameter(AmbienceFade, battleResult?0:1 - ambienceBlend);
        if (ambienceBlend >= 1) retiringAmbience.Stop();
        intensity = Mathf.MoveTowards(intensity, danger ? 1 : 0, Time.deltaTime * 0.6f);
        AudioSystem.SetParameter(Intensity, intensity);
        AudioSystem.Music.SetVolume(musicMuted || battleResult ? 0 : 0.75f * AuditionMixGain * (AudioRuntime.Instance.ActiveMixProfile != null ? 1 : Mathf.Lerp(1, 0.25f, AudioSystem.GetParameter(VoiceDuck))));
        if ((keys != null && keys.fKey.wasPressedThisFrame)) Fire();
        if ((keys != null && keys.eKey.wasPressedThisFrame)) Interact();
        if (keys != null && keys.gKey.wasPressedThisFrame && FridgeDoors != null) FridgeDoors.ToggleUpperNear(transform.position,character);
        if ((keys != null && keys.vKey.wasPressedThisFrame)) Speak();
        if ((keys != null && keys.lKey.wasPressedThisFrame)) StressLimit();
        if ((keys != null && keys.mKey.wasPressedThisFrame)) RequestMusic(!requestedCombat);
        if (!complete && collected == Pickups.Length && Vector3.Distance(transform.position, Exit.position) < 2.5f)
        {
            complete = true; RequestMusic(false); AudioSystem.PlayStinger(Stinger, MusicQuantization.Bar, 0.3f); Tell("Route complete. Use R to replay, or continue audio tests.");
        }
    }

    public void SilenceBattleBackground(){AudioSystem.Music.SetVolume(0);ambience.SetParameter(AmbienceFade,0);retiringAmbience.SetParameter(AmbienceFade,0);}
    private Vector3 impulseVelocity;
    public void AddImpulse(Vector3 velocity){impulseVelocity+=Vector3.ProjectOnPlane(velocity,Vector3.up);}
    public void MoveCharacter(Vector3 direction, bool jump, bool run, float deltaTime)
    {
        if (character == null || deltaTime <= 0 || IsResetting) return;
        if(Life!=null&&(Life.PlayerDead||(Life.Battle!=null&&Life.Battle.LocksMovement))){direction=Vector3.zero;jump=false;}
        direction = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(direction, Vector3.up), 1);
        if (FreezeForVoice) direction = Vector3.zero;
        bool grounded = character.isGrounded;
        if (grounded && vertical < 0) vertical = -2;
        if (grounded && !FreezeForVoice && jump) vertical = Mathf.Sqrt(2 * -Physics.gravity.y * JumpHeight);
        float verticalDistance = vertical * deltaTime + .5f * Physics.gravity.y * deltaTime * deltaTime;
        vertical = Mathf.Max(-35, vertical + Physics.gravity.y * deltaTime);
        float speed = (run ? 6 : 3.5f)*(Life!=null&&Life.Battle!=null&&Life.Battle.IsBlocking?.45f:1);
        planarVelocity = Vector3.MoveTowards(planarVelocity, direction * speed, (grounded ? (direction.sqrMagnitude > 0 ? 28 : 40) : 4) * deltaTime);
        if (FreezeForVoice) planarVelocity = Vector3.zero;
        CollisionFlags collision = character.Move((planarVelocity+impulseVelocity) * deltaTime + Vector3.up * verticalDistance);
        impulseVelocity=Vector3.MoveTowards(impulseVelocity,Vector3.zero,12*deltaTime);
        if ((collision & CollisionFlags.Above) != 0 && vertical > 0) vertical = 0;
        Vector3 facing=Life!=null&&Life.Duel?Vector3.ProjectOnPlane(Life.Battle.EnemyBody.transform.position-transform.position,Vector3.up):direction;
        if (facing.sqrMagnitude > 0) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(facing), 1-Mathf.Exp(-14*deltaTime));
    }

    private void LateUpdate()
    {
        if (View == null || OrbitCamera != null) return;
        if (DioramaCamera)
        {
            View.transform.position = new Vector3(-10, 23, -36);
            View.transform.LookAt(new Vector3(0, 1, 0));
            if (Listener != null) Listener.rotation = View.transform.rotation;
            return;
        }
        Vector3 target = transform.position + new Vector3(0, 14, -12);
        View.transform.position = Vector3.Lerp(View.transform.position, target, 1 - Mathf.Exp(-5 * Time.unscaledDeltaTime));
        View.transform.LookAt(transform.position + Vector3.up * 0.5f);
        if (Listener != null) Listener.rotation = View.transform.rotation;
    }

    private void ChangeAmbience(bool combat)
    {
        retiringAmbience.Stop(); retiringAmbience = ambience;
        ParameterStore start = new ParameterStore(); start.Set(AmbienceFade, 0);
        ambience = AudioSystem.PlayTracked(combat ? DangerAmbience : Ambience, start); ambienceBlend = 0;
    }

    public void Fire()
    {
        Vector3 point = transform.position + transform.forward * 3 + Vector3.up;
        AudioSystem.PlayTrackedAtPosition(Impact, point);
        GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere); flash.name = "Impact pulse";
        flash.transform.position = point; flash.transform.localScale = Vector3.one * 0.3f;
        Destroy(flash.GetComponent<Collider>()); Destroy(flash, 0.2f); Tell("Impact one-shot at the pulse position.");
    }

    public void Interact()
    {
        if(Life!=null){Life.Interact();return;}
        if (FridgeDoors != null && FridgeDoors.ToggleLowerNear(transform.position)) return;
        if (Door != null && Door.CanInteract(transform.position)) { Door.Toggle(); return; }
        PlaygroundPickup nearest = null; float distance = 2;
        foreach (PlaygroundPickup item in Pickups)
        {
            if (item == null || item.Collected) continue;
            float current = Vector3.Distance(transform.position, item.transform.position);
            if (current < distance) { distance = current; nearest = item; }
        }
        if (nearest == null) { Tell("Move close to a gold relay, then press E."); return; }
        TryCollect(nearest);
    }

    public bool TryCollect(PlaygroundPickup item)
    {
        if (item == null || item.Collected || IsResetting || paused || !Array.Exists(Pickups, candidate => candidate == item)) return false;
        if (Vector3.Distance(transform.position, item.transform.position) > 2) return false;
        LastInteractionTime = Time.time;
        item.Collect(); collected++; AudioSystem.PlayTrackedAtPosition(Pickup, item.transform.position);
        AudioSystem.PlayStinger(Stinger, MusicQuantization.Beat, 0.2f);
        Tell(collected == Pickups.Length ? "All ingredients collected. Reach the exit." : "Ingredient collected: " + collected + " / " + Pickups.Length);
        return true;
    }

    public void RespawnAt(Vector3 position,Quaternion rotation)
    {
        character.enabled=false;transform.SetPositionAndRotation(position,rotation);planarVelocity=Vector3.zero;impulseVelocity=Vector3.zero;vertical=0;character.enabled=true;
    }

    public void ResetScene()
    {
        if (IsResetting) return;
        IsResetting = true; Time.timeScale = 1; AudioSystem.SetPaused(false);
        SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        if (body == null || body.isKinematic || Mathf.Abs(hit.normal.y) > .5f) return;
        Vector3 direction = Vector3.ProjectOnPlane(hit.moveDirection, Vector3.up).normalized;
        BadShiftKickable ball = body.GetComponent<BadShiftKickable>();
        if (ball != null) { ball.Kick(direction, planarVelocity.magnitude); return; }
        bool running = planarVelocity.magnitude > 4;
        float target = Mathf.Min(running ? 3 : 1.6f, Vector3.Dot(planarVelocity, direction));
        float difference = target - Vector3.Dot(body.GetPointVelocity(hit.point), direction);
        float impulse = Mathf.Clamp(difference * body.mass, 0, (running ? 260 : 120) * Time.deltaTime);
        if (impulse <= 0) return;
        body.AddForceAtPosition(direction * impulse, hit.point, ForceMode.Impulse);
        BadShiftImpactProp prop = body.GetComponent<BadShiftImpactProp>();
        if (prop != null) prop.ReportPush();
    }

    public void Speak()
    {
        if (Lines.Length == 0) return;
        AudioSystem.VoiceOver.Play(Lines[lineIndex++ % Lines.Length], VoiceOverMode.Queue);
        Tell("Synthetic VO cue queued. Listen for ambience/music ducking and release.");
    }

    public void StressLimit()
    {
        foreach (SoundHandle handle in stress) handle.Stop();
        for (int i = 0; i < stress.Length; i++) stress[i] = AudioSystem.PlayTrackedAttached(StressLoop, transform);
        stressUntil = Time.unscaledTime + 1.2f;
        EventDebugRecord record;
        if (AudioSystem.Debugger.TryGetLast(out record)) lastDiagnostic = record.DescribeOutcome();
        Tell("Three loop requests, limit 2 per emitter. Third should reject; loops stop after 1.2 s.");
    }

    public void RequestMusic(bool combat)
    {
        requestedCombat = combat; Music.Request(AudioSystem.Music, combat ? "Combat" : "Explore");
    }

    private void Tell(string text) { notice = text; noticeUntil = Time.unscaledTime + 6; }
    private void Subtitle(string text) { subtitle = text; }

    private void OnGUI()
    {
        if (GetComponent<BadShiftDemo>() != null) return;
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(16, 16, Mathf.Min(Screen.width - 32, 650), help ? 330 : 165), GUI.skin.box);
        GUILayout.Label("AUDIOMW · RELAY FIELD / UNITY 6 URP");
        GUILayout.Label("Relays " + collected + "/" + Pickups.Length + " · " + surfaceName + " · " + (danger ? "RED ZONE" : "EXPLORE") + (paused ? " · PAUSED" : ""));
        if (AudioRuntime.Exists)
        {
            MusicPlayer player = AudioSystem.Music;
            GUILayout.Label("Music: " + (player.CurrentTrack != null ? player.CurrentTrack.name : "Stopped") + (musicMuted ? " · MUTED" : ""));
            GUILayout.Label(player.IsTransitionPending ? "Pending: " + player.PendingTrack.name + " · " + Math.Max(0, player.PendingStartDspTime - AudioSettings.dspTime).ToString("0.0") + " s · " + player.PendingQuantization : player.IsCrossfading ? "Crossfade in progress" : "No pending transition");
            AudioMixProfile mix = AudioRuntime.Instance.ActiveMixProfile;
            if (mix != null) { mix.Routing.Mixer.GetFloat("AudioMW_MusicDuckDb", out float db); GUILayout.Label("Mixer duck: " + db.ToString("0.0") + " dB · profile " + mix.name); }
            GUILayout.Label("Intensity " + intensity.ToString("0.00") + " · Duck " + AudioSystem.GetParameter(VoiceDuck).ToString("0.00") + " · Voices " + AudioSystem.ActiveVoiceCount);
        }
        if (help)
        {
            GUILayout.Label("WASD move · Shift run · Space jump · E collect · F impact");
            GUILayout.Label("V VO/duck · M music request · L voice-limit test · 1 music mute");
            GUILayout.Label("P pause · R reset · H help · F8 save test-session report");
            GUILayout.Label("Stone / grass / metal select local footstep states. Enter red zone for adaptive music.");
            GUILayout.Label("Synthetic placeholder audio. Replace clips in Playground/Audio before judging sound design.");
            GUILayout.Label("Last limit result: " + lastDiagnostic);
        }
        GUILayout.EndArea();
        if (Time.unscaledTime < noticeUntil) GUI.Box(new Rect(16, Screen.height - 110, Screen.width - 32, 38), notice);
        if (!string.IsNullOrEmpty(subtitle)) GUI.Box(new Rect(16, Screen.height - 65, Screen.width - 32, 45), subtitle);
    }

    [Serializable] private sealed class Session
    {
        public string unity;
        public int relays;
        public bool completed;
        public string surface;
        public string lastLimitReason;
        public bool mixActive;
        public float musicDuckDb;
        public float ambienceDuckDb;
        public string note = "Runtime snapshot, not listening approval. Add subjective findings using the manual checklist.";
    }
    private void SaveSession()
    {
#if UNITY_EDITOR
        string folder = Path.Combine(Application.dataPath, "..", "TestReports");
#else
        string folder = Application.persistentDataPath;
#endif
        Directory.CreateDirectory(folder);
        string path = Path.GetFullPath(Path.Combine(folder, "audiomw-playground-session.json"));
        AudioMixProfile mix = AudioRuntime.Instance.ActiveMixProfile;
        float musicDb = 0, ambienceDb = 0;
        if (mix != null) { mix.Routing.Mixer.GetFloat("AudioMW_MusicDuckDb", out musicDb); mix.Routing.Mixer.GetFloat("AudioMW_AmbienceDuckDb", out ambienceDb); }
        File.WriteAllText(path, JsonUtility.ToJson(new Session { unity = Application.unityVersion, relays = collected, completed = complete, surface = surfaceName, lastLimitReason = lastDiagnostic, mixActive = mix != null, musicDuckDb = musicDb, ambienceDuckDb = ambienceDb }, true));
        Debug.Log("AudioMW manual test report: " + path); Tell("Report saved: " + path);
    }
    private void OnDestroy()
    {
        Time.timeScale = 1; AudioSystem.SetPaused(false);
        if (AudioRuntime.Exists) { AudioSystem.VoiceOver.SubtitleChanged -= Subtitle; AudioSystem.Shutdown(); }
        if (bankLease != null) bankLease.Dispose();
    }
}
