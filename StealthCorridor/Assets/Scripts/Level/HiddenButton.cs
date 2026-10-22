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
            // how far away is the button?
            float distance = Vector3.Distance(flashlight.transform.position, transform.position);

            // how far off to the side of the light is the button? (0 = right in the middle)
            Vector3 toButton = transform.position - flashlight.transform.position;
            float angle = Vector3.Angle(flashlight.transform.forward, toButton);

            // spotAngle is the whole cone, so half of it is the edge of the light
            if (angle < flashlight.spotAngle / 2f && distance < flashlight.range)
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
