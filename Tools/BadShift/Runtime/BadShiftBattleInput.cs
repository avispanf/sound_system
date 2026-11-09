using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(9)]
public sealed class BadShiftBattleInput : MonoBehaviour
{
    public BadShiftBattle Battle;
    private void Update()
    {
        var mouse=Mouse.current;
        if(mouse!=null)Battle.ReadPointer(mouse.leftButton.wasPressedThisFrame,mouse.rightButton.isPressed,mouse.position.ReadValue());
    }
    private void OnDisable(){if(Battle!=null)Battle.SetBlocking(false);}
}
