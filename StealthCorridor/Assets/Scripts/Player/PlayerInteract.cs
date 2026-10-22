using UnityEngine;
using UnityEngine.InputSystem;

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
            // only buttons have the HiddenButton script, so this is null for walls and floors
            HiddenButton button = hit.collider.GetComponent<HiddenButton>();
            if (button != null)
            {
                button.Press();
            }
        }
    }
}
