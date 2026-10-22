using UnityEngine;
using UnityEngine.InputSystem;

public class Flashlight : MonoBehaviour
{
    Light lightCone;

    void Start()
    {
        lightCone = GetComponent<Light>();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            lightCone.enabled = !lightCone.enabled;
        }
    }
}
