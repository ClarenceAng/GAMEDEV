using UnityEngine;

/// Goes on the player's camera. F toggles a spotlight. While it is on, every HiddenObject that is
///   1) close enough (range),
///   2) inside the light cone (angle check), and
///   3) not behind a wall or crate (Raycast line-of-sight check)
/// gets revealed.
public class FlashlightScanner : MonoBehaviour
{
    public float range = 16f;
    public float spotAngle = 40f;       // full cone angle, same number the Light uses
    public float intensity = 2.5f;

    /// The game turns this off while the player is dead / caught / won.
    public bool InputEnabled = true;

    public bool IsOn { get; private set; }

    Light beam;
    readonly Collider[] nearbyBuffer = new Collider[32];

    void Awake()
    {
        beam = gameObject.AddComponent<Light>();
        beam.type = LightType.Spot;
        beam.range = range;
        beam.spotAngle = spotAngle;
        beam.intensity = intensity;
        beam.color = new Color(1f, 0.96f, 0.85f);
        beam.shadows = LightShadows.None;
        beam.renderMode = LightRenderMode.ForcePixel;
        beam.enabled = false;
    }

    public void SetOn(bool on)
    {
        IsOn = on;
        beam.enabled = on;
    }

    void Update()
    {
        if (InputEnabled && GameInput.FlashlightPressed()) SetOn(!IsOn);
        if (IsOn) RevealHiddenObjectsInLightCone();
    }

    void RevealHiddenObjectsInLightCone()
    {
        Vector3 origin = transform.position;
        Vector3 forward = transform.forward;

        // Step 1: which hidden objects are near enough? (only hidden objects live on the Hidden layer)
        int count = Physics.OverlapSphereNonAlloc(origin, range, nearbyBuffer, GameLayers.HiddenMask, QueryTriggerInteraction.Collide);

        for (int i = 0; i < count; i++)
        {
            Collider candidate = nearbyBuffer[i];
            HiddenObject hidden = candidate.GetComponentInParent<HiddenObject>();
            if (hidden == null) continue;

            Vector3 toObject = candidate.bounds.center - origin;
            float distance = toObject.magnitude;

            // Step 2: is it inside the cone? (half of the full angle on each side of where we look)
            if (Vector3.Angle(forward, toObject) > spotAngle * 0.5f) continue;

            // Step 3: is a wall or crate in the way?
            bool blocked = Physics.Raycast(origin, toObject / distance, distance, GameLayers.WorldMask, QueryTriggerInteraction.Ignore);
            if (blocked) continue;

            hidden.Illuminate();
        }
    }
}
