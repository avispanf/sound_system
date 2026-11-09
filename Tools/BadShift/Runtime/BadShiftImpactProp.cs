using AudioMW;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public sealed class BadShiftImpactProp : MonoBehaviour
{
    public static event System.Action<Vector3,bool> Reacted;
    public SoundEvent ImpactSound;
    public SoundEvent BreakSound;
    public bool Breakable;
    public float BreakSpeed = 2.2f;
    public Mesh ShardMesh;
    public Material ShardMaterial;
    public PhysicsMaterial ContactMaterial;
    public bool Broken { get; private set; }
    public static int BreakCount { get; private set; }
    public static int InitialBreakableCount { get; private set; }
    private float nextImpact;
    private Rigidbody body;
    private void Awake() { body = GetComponent<Rigidbody>(); }
    public static void ResetCounters()
    {
        BreakCount = 0; InitialBreakableCount = 0;
        foreach (BadShiftImpactProp prop in FindObjectsByType<BadShiftImpactProp>(FindObjectsSortMode.None)) if (prop.Breakable) InitialBreakableCount++;
    }
    public void ReportPush()
    {
        if (Time.time < nextImpact || ImpactSound == null) return;
        nextImpact = Time.time + .5f;
        Reacted?.Invoke(transform.position,false);
        AudioSystem.PlayTrackedAtPosition(ImpactSound, transform.position);
    }
    private void OnCollisionEnter(Collision collision)
    {
        if (Broken || collision.contactCount == 0) return;
        ContactPoint contact = collision.GetContact(0);
        float normalSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
        if (Breakable && normalSpeed >= BreakSpeed) { Shatter(contact.point); return; }
        if (normalSpeed < 1.2f || Time.time < nextImpact) return;
        nextImpact = Time.time + .2f;
        Reacted?.Invoke(contact.point,false);
        if (ImpactSound != null) AudioSystem.PlayTrackedAtPosition(ImpactSound, contact.point);
    }
    private void Shatter(Vector3 point)
    {
        Broken = true; BreakCount++;
        Reacted?.Invoke(point,true);
        Vector3 velocity = body.linearVelocity;
        foreach (Collider collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        body.isKinematic = true;
        if (BreakSound != null) AudioSystem.PlayTrackedAtPosition(BreakSound, point);
        Collider[] shards = new Collider[6];
        for (int i = 0; i < shards.Length; i++)
        {
            float angle = i * Mathf.PI / 3;
            Vector3 offset = new Vector3(Mathf.Cos(angle), .3f, Mathf.Sin(angle));
            GameObject shard = new GameObject("Ceramic shard");
            shard.transform.SetPositionAndRotation(transform.position + offset * .13f, Quaternion.Euler(i * 39, i * 61, i * 17));
            shard.transform.localScale = new Vector3(.16f,.08f,.2f);
            shard.AddComponent<MeshFilter>().sharedMesh = ShardMesh;
            shard.AddComponent<MeshRenderer>().sharedMaterial = ShardMaterial;
            MeshCollider shape = shard.AddComponent<MeshCollider>(); shape.sharedMesh = ShardMesh; shape.convex = true; shape.sharedMaterial = ContactMaterial;
            Rigidbody fragment = shard.AddComponent<Rigidbody>(); fragment.mass = .05f; fragment.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            fragment.linearVelocity = velocity * .25f + offset * .6f;
            fragment.angularVelocity = offset * 3;
            shards[i] = shape;
            for (int j = 0; j < i; j++) Physics.IgnoreCollision(shards[j], shape);
            Destroy(shard, 8);
        }
        Destroy(gameObject, .1f);
    }
}
