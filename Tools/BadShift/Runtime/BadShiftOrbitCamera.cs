using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(200)]
public sealed class BadShiftOrbitCamera : MonoBehaviour
{
    public AudioMWPlayground Game;
    public float Yaw { get; private set; } = 15.5f;
    public float Pitch { get; private set; } = 31;
    public float Size { get; private set; } = 12.8f;
    public bool FollowingPlayer { get; private set; }
    private Vector3 target = new Vector3(0, 1, 0);
    public void Orbit(float yaw, float pitch)
    {
        Yaw = Mathf.Repeat(Yaw + yaw, 360);
        Pitch = Mathf.Clamp(Pitch + pitch, 18, 75);
    }
    public void Zoom(float steps) { Size = Mathf.Clamp(Size * Mathf.Exp(-steps * .16f), 3.5f, 16); }
    public void FocusPlayer() { FollowingPlayer = true; Size = 6; }
    public void Home() { Yaw = 15.5f; Pitch = 31; Size = 12.8f; FollowingPlayer = false; }
    public Vector3 MovementDirection(Vector3 input)
    {
        Vector3 right = Quaternion.Euler(0, Yaw, 0) * Vector3.right;
        Vector3 forward = Quaternion.Euler(0, Yaw, 0) * Vector3.forward;
        return right * input.x + forward * input.z;
    }
    private void LateUpdate()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            if (mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                Orbit(delta.x * .18f, -delta.y * .18f);
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > .01f) Zoom(scroll / 120);
        }
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) FocusPlayer();
        Vector3 pivot = FollowingPlayer ? Game.transform.position + Vector3.up : new Vector3(0,1,0);
        target = Vector3.Lerp(target, pivot, 1 - Mathf.Exp(-8 * Time.unscaledDeltaTime));
        Quaternion rotation = Quaternion.Euler(Pitch, Yaw, 0);
        Game.View.transform.SetPositionAndRotation(target + rotation * Vector3.back * 44, rotation);
        Game.View.orthographicSize = Mathf.Lerp(Game.View.orthographicSize, Size, 1 - Mathf.Exp(-12 * Time.unscaledDeltaTime));
        Game.Listener.rotation = rotation;
    }
}
