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
    private void Start()
    {
        bodies = new[] { Demo.PlayerAnimator.transform, Demo.DanceAnimator.transform, Demo.Boss };
        scales = new Vector3[bodies.Length];
        for (int i = 0; i < bodies.Length; i++) scales[i] = bodies[i].localScale;
        grounded = Demo.Game.IsGrounded;
    }
    private void LateUpdate()
    {
        if (AudioListener.pause) return;
        phase += Time.deltaTime;
        bool speaking = AudioSystem.VoiceOver.IsSpeaking;
        bool landed = !grounded && Demo.Game.IsGrounded;
        grounded = Demo.Game.IsGrounded;
        if (landed) springVelocity -= 1.8f;
        float step = Mathf.Min(Time.deltaTime, .04f);
        springVelocity += (-100 * spring - 12 * springVelocity) * step;
        spring = Mathf.Clamp(spring + springVelocity * step, -.13f, .1f);
        bool dancing = AudioSystem.Music.CurrentTrack != null && AudioSystem.Music.CurrentTrack.name == "Combat";
        for (int i = 0; i < bodies.Length; i++)
        {
            if (speaking && i < 2) continue;
            float beat = Mathf.Floor(phase * 16) / 16;
            float amount = i == 0 ? (Demo.Game.MovementSpeed > .15f ? .035f : .012f) : i == 1 ? (dancing ? .075f : .018f) : speaking ? .065f : .02f;
            float stretch = Mathf.Sin(beat * (i == 0 ? 13 : i == 1 && dancing ? 14 : speaking ? 11 : 3)) * amount + (i == 0 ? spring : 0);
            bodies[i].localScale = Vector3.Scale(scales[i], new Vector3(1 - stretch * .5f, 1 + stretch, 1 - stretch * .5f));
        }
    }
    private void OnDisable()
    {
        if (bodies == null) return;
        for (int i = 0; i < bodies.Length; i++) if (bodies[i] != null) bodies[i].localScale = scales[i];
    }
}
