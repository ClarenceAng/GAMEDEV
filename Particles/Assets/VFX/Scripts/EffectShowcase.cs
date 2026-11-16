using UnityEngine;
using UnityEngine.InputSystem;

// Shows one particle effect at a time.
// 1 / 2 / 3 = pick an effect, Space = play it again.
public class EffectShowcase : MonoBehaviour
{
    // the camera that jumps between the effects
    public Transform cam;

    // where the camera stands for each effect
    public Transform[] cameraSpots;

    // the particle effect for each spot (children play too)
    public ParticleSystem[] effects;

    // the name shown at the top of the screen
    public string[] effectNames;

    // Noelle's water armor, it appears when the water bubble bursts
    public GameObject armor;
    public float armorTime = 4f;

    // the camera rumbles while the demon king aura is on
    public float shake = 0.04f;

    // show the help text at the top of the screen
    public bool showHelp = true;

    public int current = 0;

    void Start()
    {
        ShowEffect(0);
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                ShowEffect(0);
            }
            if (Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                ShowEffect(1);
            }
            if (Keyboard.current.digit3Key.wasPressedThisFrame)
            {
                ShowEffect(2);
            }
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                ShowEffect(current);
            }
        }

        // put the camera on the spot of the current effect
        cam.position = cameraSpots[current].position;
        cam.rotation = cameraSpots[current].rotation;

        // the demon king aura is heavy, so the camera shakes a little
        if (current == 2)
        {
            cam.position = cam.position + Random.insideUnitSphere * shake;
        }
    }

    public void ShowEffect(int number)
    {
        // stop every effect and clear its particles
        for (int i = 0; i < effects.Length; i++)
        {
            effects[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        current = number;

        // Play(true) also plays all the child particle systems
        effects[current].Play(true);

        // Noelle starts without her armor every time
        armor.SetActive(false);
        CancelInvoke("PutOnArmor");
        if (current == 0)
        {
            Invoke("PutOnArmor", armorTime);
        }
    }

    void PutOnArmor()
    {
        armor.SetActive(true);
    }

    void OnGUI()
    {
        if (showHelp)
        {
            GUI.Label(new Rect(20, 20, 900, 30), effectNames[current] + "      1 / 2 / 3 = change effect     Space = play again");
        }
    }
}
