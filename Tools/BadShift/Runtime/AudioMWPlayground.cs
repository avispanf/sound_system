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
        if (keys == null) return;
        if (keys.hKey.wasPressedThisFrame) help = !help;
        if (keys.pKey.wasPressedThisFrame)
        {
            paused = !paused; AudioSystem.SetPaused(paused); Time.timeScale = paused ? 0 : 1;
        }
        if (keys.rKey.wasPressedThisFrame) { Time.timeScale = 1; AudioSystem.SetPaused(false); SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex); return; }
        if (keys.digit1Key.wasPressedThisFrame) musicMuted = !musicMuted;
        if (keys.f8Key.wasPressedThisFrame) SaveSession();
        if (Time.unscaledTime >= stressUntil) foreach (SoundHandle handle in stress) handle.Stop();
        if (paused) return;
        Vector3 direction = new Vector3((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0), 0, (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0)).normalized;
        if (FreezeForVoice) direction = Vector3.zero;
        bool grounded = character.isGrounded;
        if (grounded && vertical < 0) vertical = -2;
        if (grounded && !FreezeForVoice && keys.spaceKey.wasPressedThisFrame) vertical = Mathf.Sqrt(2 * -Physics.gravity.y * 1.1f);
        vertical = Mathf.Max(-25, vertical + Physics.gravity.y * Time.deltaTime);
        float speed = keys.leftShiftKey.isPressed ? 6 : 3.5f;
        planarVelocity = Vector3.MoveTowards(planarVelocity, direction * speed, (grounded ? 24 : 6) * Time.deltaTime);
        if (FreezeForVoice) planarVelocity = Vector3.zero;
        character.Move((planarVelocity + Vector3.up * vertical) * Time.deltaTime);
        if (direction.sqrMagnitude > 0) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 1-Mathf.Exp(-14*Time.deltaTime));
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
        if (nowGrounded && MovementSpeed > .15f)
        {
            stride += MovementSpeed * Time.deltaTime;
            if (stride >= 1.5f) { stride = 0; AudioSystem.PlayTrackedAttached(Footsteps, transform, local); }
        }
        else stride = 0;
        bool inDanger = transform.position.z > 3.5f;
        if (inDanger != danger) { danger = inDanger; RequestMusic(danger); ChangeAmbience(danger); }
        ambienceBlend = Mathf.MoveTowards(ambienceBlend, 1, Time.deltaTime / 0.7f);
        ambience.SetParameter(AmbienceFade, ambienceBlend); retiringAmbience.SetParameter(AmbienceFade, 1 - ambienceBlend);
        if (ambienceBlend >= 1) retiringAmbience.Stop();
        intensity = Mathf.MoveTowards(intensity, danger ? 1 : 0, Time.deltaTime * 0.6f);
        AudioSystem.SetParameter(Intensity, intensity);
        AudioSystem.Music.SetVolume(musicMuted ? 0 : 0.75f * AuditionMixGain * (AudioRuntime.Instance.ActiveMixProfile != null ? 1 : Mathf.Lerp(1, 0.25f, AudioSystem.GetParameter(VoiceDuck))));
        if (keys.fKey.wasPressedThisFrame) Fire();
        if (keys.eKey.wasPressedThisFrame) Interact();
        if (keys.vKey.wasPressedThisFrame) Speak();
        if (keys.lKey.wasPressedThisFrame) StressLimit();
        if (keys.mKey.wasPressedThisFrame) RequestMusic(!requestedCombat);
        if (!complete && collected == Pickups.Length && Vector3.Distance(transform.position, Exit.position) < 2.5f)
        {
            complete = true; RequestMusic(false); AudioSystem.PlayStinger(Stinger, MusicQuantization.Bar, 0.3f); Tell("Route complete. Use R to replay, or continue audio tests.");
        }
    }

    private void LateUpdate()
    {
        if (View == null) return;
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
        PlaygroundPickup nearest = null; float distance = 2;
        foreach (PlaygroundPickup item in Pickups)
        {
            if (item == null || item.Collected) continue;
            float current = Vector3.Distance(transform.position, item.transform.position);
            if (current < distance) { distance = current; nearest = item; }
        }
        if (nearest == null) { Tell("Move close to a gold relay, then press E."); return; }
        LastInteractionTime = Time.unscaledTime;
        nearest.Collect(); collected++; AudioSystem.PlayTrackedAtPosition(Pickup, nearest.transform.position);
        AudioSystem.PlayStinger(Stinger, MusicQuantization.Beat, 0.2f);
        Tell(collected == Pickups.Length ? "All relays collected. Reach the blue exit in the red zone." : "Relay collected: " + collected + " / " + Pickups.Length);
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
