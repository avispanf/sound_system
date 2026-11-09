using AudioMW;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BadShiftDoor : MonoBehaviour
{
    public BoxCollider Leaf;
    public SoundEvent Sound;
    public bool IsOpen { get; private set; }
    public bool IsMoving => Mathf.Abs(angle - (IsOpen ? -95 : 0)) > .1f;
    private float angle;
    private Rigidbody body;
    private readonly Collider[] obstacles = new Collider[32];
    private void Awake() { body = GetComponent<Rigidbody>(); }
    public bool CanInteract(Vector3 position) => Vector3.Distance(Leaf.ClosestPoint(position + Vector3.up), position + Vector3.up) < 2.3f;
    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        if (Sound != null) AudioSystem.PlayTrackedAtPosition(Sound, Leaf.bounds.center);
    }
    public void Toggle() { SetOpen(!IsOpen); }
    private void FixedUpdate()
    {
        float next = Mathf.MoveTowards(angle, IsOpen ? -95 : 0, 85 * Time.fixedDeltaTime);
        if (Mathf.Approximately(next, angle)) return;
        Quaternion rotation = Quaternion.Euler(0, next, 0);
        Vector3 localCenter = transform.InverseTransformPoint(Leaf.transform.TransformPoint(Leaf.center));
        Vector3 half = Vector3.Scale(Leaf.size, Leaf.transform.lossyScale) * .5f;
        int count = Physics.OverlapBoxNonAlloc(body.position + rotation * localCenter, half, obstacles, rotation, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider obstacle = obstacles[i];
            if (obstacle.attachedRigidbody == body) continue;
            if (obstacle is CharacterController || (obstacle.attachedRigidbody != null && !obstacle.attachedRigidbody.isKinematic)) return;
        }
        angle = next; body.MoveRotation(rotation);
    }
}
