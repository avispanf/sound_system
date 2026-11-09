using AudioMW;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BadShiftDoor : MonoBehaviour
{
    public BoxCollider Leaf;
    public SoundEvent Sound;
    public float OpenAngle = -95;
    public float DegreesPerSecond = 240;
    public bool IsBlocked { get; private set; }
    public bool IsOpen { get; private set; }
    public bool IsMoving => Mathf.Abs(angle - (IsOpen ? OpenAngle : 0)) > .1f;
    private float angle;
    private Rigidbody body;
    private Quaternion closedRotation;
    private Bounds closedBounds;
    private readonly Collider[] obstacles = new Collider[32];
    private void Awake() { body = GetComponent<Rigidbody>(); closedRotation = body.rotation; closedBounds = Leaf.bounds; }
    public bool CanInteract(Vector3 position)
    {
        Vector3 point = position + Vector3.up;
        return Mathf.Min(Vector3.Distance(Leaf.ClosestPoint(point), point), Vector3.Distance(closedBounds.ClosestPoint(point), point)) < 2.3f;
    }
    public void SetOpen(bool open)
    {
        if (IsOpen == open) return;
        IsOpen = open;
        if (Sound != null) AudioSystem.PlayTrackedAtPosition(Sound, Leaf.bounds.center);
    }
    public void Toggle() { SetOpen(!IsOpen); }
    private void FixedUpdate()
    {
        float target = Mathf.MoveTowards(angle, IsOpen ? OpenAngle : 0, Mathf.Max(1, DegreesPerSecond) * Time.fixedDeltaTime);
        IsBlocked = false;
        Vector3 localCenter = transform.InverseTransformPoint(Leaf.transform.TransformPoint(Leaf.center));
        Vector3 half = Vector3.Scale(Leaf.size, Leaf.transform.lossyScale) * .5f;
        while (!Mathf.Approximately(angle, target))
        {
            float next = Mathf.MoveTowards(angle, target, 2);
            Quaternion rotation = Quaternion.AngleAxis(next, Vector3.up) * closedRotation;
            int count = Physics.OverlapBoxNonAlloc(body.position + rotation * localCenter, half, obstacles, rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == obstacles.Length) { IsBlocked = true; break; }
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = obstacles[i];
                if (obstacle.attachedRigidbody == body) continue;
                if (obstacle is CharacterController || (obstacle.attachedRigidbody != null && !obstacle.attachedRigidbody.isKinematic)) { IsBlocked = true; break; }
            }
            if (IsBlocked) break;
            angle = next;
        }
        body.MoveRotation(Quaternion.AngleAxis(angle, Vector3.up) * closedRotation);
    }
}
