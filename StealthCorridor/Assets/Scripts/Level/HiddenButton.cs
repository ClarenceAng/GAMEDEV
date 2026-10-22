using UnityEngine;

// A button on the wall that you can only see when the flashlight is on and pointing at it.
// You can only press it while you can see it.
public class HiddenButton : MonoBehaviour
{
    public Light flashlight;
    public Material pressedMaterial;
    public bool isLit = false;
    public bool pressed = false;

    Renderer rend;
    StealthGame game;

    void Start()
    {
        rend = GetComponent<Renderer>();
        game = FindFirstObjectByType<StealthGame>();
    }

    void Update()
    {
        isLit = false;

        if (flashlight.enabled)
        {
            // is the button inside the light cone and close enough?
            Vector3 toButton = transform.position - flashlight.transform.position;
            float angle = Vector3.Angle(flashlight.transform.forward, toButton);

            if (angle < flashlight.spotAngle / 2f && toButton.magnitude < flashlight.range)
            {
                isLit = true;
            }
        }

        // hide the button when the light is not on it
        rend.enabled = isLit;
    }

    public void Press()
    {
        if (isLit && pressed == false)
        {
            pressed = true;
            rend.material = pressedMaterial;
            game.ButtonPressed();
        }
    }
}
