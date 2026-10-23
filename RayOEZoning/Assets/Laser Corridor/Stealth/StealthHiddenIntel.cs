using UnityEngine;

// Renderers start disabled. Every frame requires: light ON + inside cone + raycast LOS.
public class StealthHiddenIntel : MonoBehaviour
{
    public bool Revealed { get; private set; }
    public bool Collected { get; private set; }
    public bool CanCollect { get; private set; }
    public string IntelName { get; private set; }
    public bool IsGantryKey { get; private set; }

    private Transform eyes;
    private Transform player;
    private StealthCorridorDirector director;
    private Light flashlight;
    private Renderer[] meshes;
    private Collider pickupCollider;

    public void Initialize(string intelName, Transform cameraEyes, Transform user, Light light,
        StealthCorridorDirector owner, bool isGantryKey = false)
    {
        IsGantryKey = isGantryKey;
        IntelName = intelName;
        eyes = cameraEyes;
        player = user;
        flashlight = light;
        director = owner;
        meshes = GetComponentsInChildren<Renderer>(true);
        // Dedicated layer: raycasts from guards ignore invisible pickups.
        // Player's flashlight raycast must still hit this root collider.
        gameObject.layer = 9;
        pickupCollider = GetComponent<Collider>();
        if (pickupCollider == null) pickupCollider = gameObject.AddComponent<BoxCollider>();
        // Preserve raycast detection but do not make invisible files physically block the player.
        Collider playerCollider = player.GetComponent<Collider>();
        if (playerCollider != null) Physics.IgnoreCollision(pickupCollider, playerCollider, true);
        HideVisuals();
    }

    private void LateUpdate()
    {
        if (Collected || eyes == null || flashlight == null) return;
        // Uses the flashlight direction as the ray origin/aim reference.
        Vector3 rayOrigin = flashlight.transform.position;
        Vector3 toObject = transform.position - rayOrigin;
        float distance = toObject.magnitude;
        bool insideCone = flashlight.enabled && distance <= flashlight.range &&
            Vector3.Angle(flashlight.transform.forward, toObject) <= flashlight.spotAngle * 0.5f;
        bool unblocked = false;
        if (insideCone && Physics.Raycast(rayOrigin, toObject.normalized, out RaycastHit hit,
            distance + 0.4f, ~0, QueryTriggerInteraction.Ignore))
            unblocked = hit.collider == pickupCollider || hit.collider.transform.IsChildOf(transform);

        Revealed = insideCone && unblocked;
        foreach (Renderer r in meshes) if (r != null) r.enabled = Revealed;
        CanCollect = Revealed && Vector3.Distance(player.position, transform.position) <= 3.25f;
        if (CanCollect && UnityEngine.InputSystem.Keyboard.current != null &&
            UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            director.Collect(this);
    }

    public void MarkCollected()
    {
        Collected = true;
        CanCollect = false;
        HideVisuals();
        if (pickupCollider != null) pickupCollider.enabled = false;
    }

    public void ResetIntel()
    {
        Collected = false;
        if (pickupCollider != null)
        {
            pickupCollider.enabled = true;
            Collider userCollider = player != null ? player.GetComponent<Collider>() : null;
            if (userCollider != null) Physics.IgnoreCollision(pickupCollider, userCollider, true);
        }
        HideVisuals();
    }

    private void HideVisuals()
    {
        Revealed = false;
        CanCollect = false;
        if (meshes == null) return;
        foreach (Renderer r in meshes) if (r != null) r.enabled = false;
    }
}
