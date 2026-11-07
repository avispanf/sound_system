using UnityEngine;

public sealed class PlaygroundPickup : MonoBehaviour
{
    public bool Collected { get; private set; }
    private void Update() { if (!Collected) transform.Rotate(0, 60 * Time.deltaTime, 0, Space.World); }
    public void Collect()
    {
        Collected = true;
        foreach (Renderer item in GetComponentsInChildren<Renderer>()) item.enabled = false;
        foreach (Collider item in GetComponentsInChildren<Collider>()) item.enabled = false;
    }
}
