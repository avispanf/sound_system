using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BadShiftKickable : MonoBehaviour
{
    private float nextKick;
    public bool Kick(Vector3 direction, float speed)
    {
        if (Time.time < nextKick || speed < .5f) return false;
        nextKick = Time.time + .25f;
        Rigidbody body = GetComponent<Rigidbody>();
        Vector3 target = direction * Mathf.Clamp(speed * 1.3f, 2, 6) + Vector3.up * Mathf.Sqrt(2 * -Physics.gravity.y * .32f);
        body.AddForce((target - body.linearVelocity) * body.mass, ForceMode.Impulse);
        GetComponent<BadShiftImpactProp>()?.ReportPush();
        return true;
    }
}
