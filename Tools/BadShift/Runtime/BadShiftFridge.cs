using UnityEngine;

public sealed class BadShiftFridge : MonoBehaviour
{
    public BadShiftDoor Lower;
    public BadShiftDoor Upper;
    public bool CanInteract(Vector3 position) => Lower.CanInteract(position) || Upper.CanInteract(position);
    public bool ToggleLowerNear(Vector3 position)
    {
        if (!CanInteract(position)) return false;
        Lower.Toggle(); return true;
    }
    public bool ToggleUpperNear(Vector3 position,CharacterController actor=null)
    {
        if (!CanInteract(position)) return false;
        if(actor!=null)Upper.ToggleFor(actor);else Upper.Toggle(); return true;
    }
}
