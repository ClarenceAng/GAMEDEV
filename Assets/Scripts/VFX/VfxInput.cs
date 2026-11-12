using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif


public static class VfxInput
{
#if ENABLE_INPUT_SYSTEM
    static Key Map(KeyCode kc)
    {
        string s = kc.ToString();
        if (s.StartsWith("Alpha")) s = "Digit" + s.Substring(5);
        System.Enum.TryParse<Key>(s, out Key k);
        return k;
    }
    public static bool Down(KeyCode kc) { var kb = Keyboard.current; return kb != null && kb[Map(kc)].wasPressedThisFrame; }
    public static bool Held(KeyCode kc) { var kb = Keyboard.current; return kb != null && kb[Map(kc)].isPressed; }
    static UnityEngine.InputSystem.Controls.ButtonControl Btn(int b)
    {
        var m = Mouse.current; if (m == null) return null;
        return b == 0 ? m.leftButton : m.rightButton;
    }
    public static bool MouseDown(int b) { var c = Btn(b); return c != null && c.wasPressedThisFrame; }
    public static bool MouseUp(int b) { var c = Btn(b); return c != null && c.wasReleasedThisFrame; }
    public static Vector2 MousePos() { var m = Mouse.current; return m != null ? m.position.ReadValue() : Vector2.zero; }
#else
    public static bool Down(KeyCode kc) { return Input.GetKeyDown(kc); }
    public static bool Held(KeyCode kc) { return Input.GetKey(kc); }
    public static bool MouseDown(int b) { return Input.GetMouseButtonDown(b); }
    public static bool MouseUp(int b) { return Input.GetMouseButtonUp(b); }
    public static Vector2 MousePos() { return Input.mousePosition; }
#endif
}
