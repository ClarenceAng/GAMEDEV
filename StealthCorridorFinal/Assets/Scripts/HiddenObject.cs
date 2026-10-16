using UnityEngine;

/// Intel with three steps:
///   Hidden    - invisible. Only the flashlight can find it (FlashlightScanner calls Illuminate()).
///   Located   - the light touched it once, so it now glows and stays visible: you know where it is.
///   Collected - you walked up to it. It switches off one hazard and disappears.
/// Walking past a Hidden one does nothing, so you must find it with the light first.
public class HiddenObject : MonoBehaviour
{
    enum Step { Hidden, Located, Collected }

    public float collectRadius = 1.2f;      // how close you must get (Physics.CheckSphere around the intel)

    Step step = Step.Hidden;
    Renderer[] renderers;
    Material normalMaterial;                // lit look (used while hidden)
    Material glowMaterial;                  // glowing look (used once located)
    Vector3 baseScale;

    public bool IsLocated { get { return step != Step.Hidden; } }

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        normalMaterial = renderers[0].sharedMaterial;
        baseScale = transform.localScale;
        SetVisible(false);
    }

    /// Called by the game right after creating the object, so the glow matches the intel colour.
    public void SetGlowColor(Color color)
    {
        glowMaterial = GameUtil.Unlit(Color.Lerp(color, Color.white, 0.25f));
    }

    /// Called by the flashlight every frame this object is inside the light cone with a clear line of sight.
    public void Illuminate()
    {
        if (step != Step.Hidden) return;

        step = Step.Located;
        foreach (Renderer r in renderers) r.sharedMaterial = glowMaterial;
        SetVisible(true);
        if (StealthCorridorGame.Instance != null) StealthCorridorGame.Instance.OnIntelLocated(this);
    }

    void Update()
    {
        if (step != Step.Located) return;

        // Gentle pulse so the marker is easy to notice in the dark
        transform.localScale = baseScale * (1f + 0.15f * Mathf.Sin(Time.time * 5f));

        StealthCorridorGame game = StealthCorridorGame.Instance;
        if (game == null || !game.IsPlaying) return;

        // Walk up to it to collect it
        if (Physics.CheckSphere(transform.position, collectRadius, GameLayers.PlayerMask))
        {
            step = Step.Collected;
            SetVisible(false);
            game.OnIntelCollected(this);
        }
    }

    public void ResetIntel()
    {
        step = Step.Hidden;
        transform.localScale = baseScale;
        foreach (Renderer r in renderers) r.sharedMaterial = normalMaterial;
        SetVisible(false);
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in renderers) r.enabled = visible;
    }
}
