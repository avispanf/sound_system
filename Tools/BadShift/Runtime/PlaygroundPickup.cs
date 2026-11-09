using UnityEngine;

public sealed class PlaygroundPickup : MonoBehaviour
{
    public bool AutoCollect;
    public ParticleSystem CollectBurst;
    public bool Collected { get; private set; }
    private void Update()
    {
        if (Collected) return;
        Transform visual = AutoCollect && transform.childCount > 0 ? transform.GetChild(0) : transform;
        visual.Rotate(0, 60 * Time.deltaTime, 0, Space.World);
    }
    private void OnTriggerEnter(Collider other) { TryAutoCollect(other); }
    private void OnTriggerStay(Collider other) { TryAutoCollect(other); }
    private void TryAutoCollect(Collider other)
    {
        if (!AutoCollect || Collected) return;
        AudioMWPlayground game = other.GetComponentInParent<AudioMWPlayground>();
        if (game != null) game.TryCollect(this);
    }
    public void Collect()
    {
        if (Collected) return;
        Collected = true;
        foreach (ParticleSystem effect in GetComponentsInChildren<ParticleSystem>()) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (CollectBurst != null)
        {
            ParticleSystem burst = Instantiate(CollectBurst, transform.position, Quaternion.identity);
            burst.Play(); Destroy(burst.gameObject, 2);
        }
        foreach (Renderer item in GetComponentsInChildren<Renderer>()) item.enabled = false;
        foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = false;
    }
}
