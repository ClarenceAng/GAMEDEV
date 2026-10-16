using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif

/// All keyboard and mouse reading lives here. Works with the old Input Manager or the new Input System package.
public static class GameInput
{
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    public static Vector2 Move()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return Vector2.zero;
        float x = Axis(keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed,
                       keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed);
        float y = Axis(keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                       keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
        return new Vector2(x, y);
    }

    public static Vector2 Look()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return Vector2.zero;
        return mouse.delta.ReadValue() * 0.1f;     // 0.1 makes it feel like the old Input Manager
    }

    public static bool JumpPressed()        { return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame; }
    public static bool SneakHeld()          { return Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed; }
    public static bool FlashlightPressed()  { return Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame; }
    public static bool CancelPressed()      { return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame; }
    public static bool ClickPressed()       { return Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame; }
#else
    public static Vector2 Move()
    {
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    public static Vector2 Look()
    {
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    public static bool JumpPressed()        { return Input.GetKeyDown(KeyCode.Space); }
    public static bool SneakHeld()          { return Input.GetKey(KeyCode.LeftShift); }
    public static bool FlashlightPressed()  { return Input.GetKeyDown(KeyCode.F); }
    public static bool CancelPressed()      { return Input.GetKeyDown(KeyCode.Escape); }
    public static bool ClickPressed()       { return Input.GetMouseButtonDown(0); }
#endif

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    static float Axis(bool positive, bool negative)
    {
        return (positive ? 1f : 0f) - (negative ? 1f : 0f);
    }
#endif
}
