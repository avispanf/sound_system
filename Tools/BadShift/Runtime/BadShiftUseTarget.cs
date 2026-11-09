using UnityEngine;

public enum BadShiftUseKind { Microwave, Boss, Bathroom, Fridge, Spar, Sink, Toilet, Prop }
public sealed class BadShiftUseTarget : MonoBehaviour
{
    public BadShiftUseKind Kind;
    public string Label;
    public BadShiftDoor Door;
    public float Distance(Vector3 player)
    {
        if(Door!=null)return Door.InteractionDistance(player);
        Vector3 point=player+Vector3.up;
        Collider shape=GetComponent<Collider>();
        Renderer visual=GetComponent<Renderer>();
        Vector3 closest=shape!=null?shape.ClosestPoint(point):visual!=null?visual.bounds.ClosestPoint(point):transform.position+Vector3.up;
        return Vector3.ProjectOnPlane(closest-point,Vector3.up).magnitude;
    }
}
