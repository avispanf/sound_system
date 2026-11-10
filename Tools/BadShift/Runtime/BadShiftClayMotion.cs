using AudioMW;
using UnityEngine;

[DefaultExecutionOrder(100)]
public sealed class BadShiftClayMotion : MonoBehaviour
{
    public BadShiftDemo Demo;
    private Transform[] bodies;
    private Vector3[] scales;
    private float phase;
    private float spring;
    private float springVelocity;
    private bool grounded;
    private Quaternion playerRest;
    private Vector3 applianceScale;
    private Quaternion applianceRest;
    private void Start()
    {
        bodies = new[] { Demo.PlayerAnimator.transform, Demo.DanceAnimator.transform, Demo.Boss };
        scales = new Vector3[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) scales[i] = bodies[i].localScale;
        grounded = Demo.Game.IsGrounded;
        playerRest = bodies[0].localRotation;
        if (Demo.MicrowaveVisual != null)
        {
            applianceScale = Demo.MicrowaveVisual.localScale;
            applianceRest = Demo.MicrowaveVisual.localRotation;
        }
    }
    private void LateUpdate()
    {
        if (AudioListener.pause) return;
        phase += Time.deltaTime;
        bool speaking = AudioSystem.VoiceOver.IsSpeaking;
        bool landed = !grounded && Demo.Game.IsGrounded;
        grounded = Demo.Game.IsGrounded;
        if (landed) springVelocity = -2.5f;
        float decay = Mathf.Exp(-6 * Time.deltaTime);
        float sine = Mathf.Sin(8 * Time.deltaTime);
        float cosine = Mathf.Cos(8 * Time.deltaTime);
        float nextSpring = decay * (spring * cosine + (springVelocity + 6 * spring) * sine / 8);
        springVelocity = decay * (springVelocity * cosine - (6 * springVelocity + 100 * spring) * sine / 8);
        spring = Mathf.Clamp(nextSpring, -.22f, .16f);
        if (spring != nextSpring) springVelocity = 0;
        bool dancing = Demo.EmployeeDancing;
        for (int i = 0; i < bodies.Length; i++)
        {
            if (speaking && i < 2) continue;
            if(i<2&&Demo.Game.Life!=null&&(i==0?Demo.Game.Life.PlayerDead:Demo.Game.Life.EnemyDead))
            {
                bodies[i].localScale=scales[i];if(i==0)bodies[i].localRotation=playerRest;continue;
            }
            float beat = Mathf.Floor(phase * 12) / 12;
            float amount = i == 0 ? (Demo.Game.MovementSpeed > .15f ? .055f : .018f) : i == 1 ? (dancing ? .11f : .025f) : speaking ? .065f : .02f;
            float rhythm = i == 1 && dancing ? (float)(AudioSystem.Music.Clock.GetBeat(AudioSettings.dspTime) * Mathf.PI * 2) : beat * (i == 0 ? 13 : speaking ? 11 : 3);
            float stretch = Mathf.Sin(rhythm) * amount + (i == 0 ? spring : 0);
            float width = 1 / Mathf.Sqrt(1 + stretch);
            bodies[i].localScale = Vector3.Scale(scales[i], new Vector3(width, 1 + stretch, width));
            if (i == 0) bodies[i].localRotation = playerRest * Quaternion.Euler(0, 0, Mathf.Sin(beat * 6.5f) * Mathf.Min(4, Demo.Game.MovementSpeed));
        }
        if (Demo.MicrowaveVisual != null)
        {
            float remaining = Mathf.Clamp01((Demo.MicrowaveResponseUntil - Time.time) / .65f);
            float kick = Mathf.Sin((1 - remaining) * Mathf.PI * 5) * remaining * .1f;
            float width = 1 / Mathf.Sqrt(1 + kick);
            Demo.MicrowaveVisual.localScale = Vector3.Scale(applianceScale, new Vector3(width, 1 + kick, width));
            Demo.MicrowaveVisual.localRotation = applianceRest * Quaternion.Euler(0, 0, kick * 35);
        }
    }
    private void OnDisable()
    {
        if (bodies == null) return;
        for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) bodies[i].localScale = scales[i];
        if (bodies[0] != null) bodies[0].localRotation = playerRest;
        if (Demo.MicrowaveVisual != null)
        {
            Demo.MicrowaveVisual.localScale = applianceScale;
            Demo.MicrowaveVisual.localRotation = applianceRest;
        }
    }
}
