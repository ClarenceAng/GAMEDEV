using UnityEngine;
using UnityEngine.InputSystem;

// Goes on the player. Press E to shoot a ray out of the camera and press the button you are looking at.
public class PlayerInteract : MonoBehaviour
{
    public float reach = 4f;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            PressButton();
        }
    }

    public void PressButton()
    {
        Transform cam = Camera.main.transform;
        RaycastHit hit;

        if (Physics.Raycast(cam.position, cam.forward, out hit, reach))
        {
            HiddenButton button = hit.collider.GetComponent<HiddenButton>();
            if (button != null)
            {
                button.Press();
            }
        }
    }
}
