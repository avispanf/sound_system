using AudioMW;
using UnityEngine;

public sealed class BadShiftCoffeeMill : MonoBehaviour
{
    public Transform Crank;
    public SoundEvent GrindLoop;
    public SoundParameter Speed;
    public float Momentum { get; private set; }
    public SoundHandle Grinding { get; private set; }

    public void Turn()
    {
        Momentum = Mathf.Clamp01(Momentum + .38f);
        if (!Grinding.IsPlaying) Grinding = AudioSystem.PlayTrackedAttached(GrindLoop, transform);
        Grinding.SetParameter(Speed, Momentum);
    }

    private void Update()
    {
        if (Momentum <= 0 || AudioListener.pause) return;
        Momentum = Mathf.MoveTowards(Momentum, 0, Time.deltaTime * .18f);
        Crank.Rotate(Vector3.up, Momentum * 570 * Time.deltaTime, Space.Self);
        Grinding.SetParameter(Speed, Momentum);
        if (Momentum <= 0) Grinding.Stop();
    }

    private void OnDisable()
    {
        Grinding.Stop();
        Momentum = 0;
    }
}
