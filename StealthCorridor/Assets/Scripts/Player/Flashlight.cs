using UnityEngine;
using UnityEngine.InputSystem;

// Goes on the flashlight (a spot light in front of the camera). Press F to turn it on and off.
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
