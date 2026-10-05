using UnityEngine;

public enum BonusType
{
    Medkit,     // restores health
    Shield,     // blocks all laser damage for a few seconds
    TimeSlow,   // lasers move at a fraction of their speed for a few seconds
    SpeedBoost, // player moves faster for a few seconds
}

// A glowing floor panel that grants a bonus the first time the player steps on it.
// It goes dark once used and lights up again when the run resets.
[RequireComponent(typeof(BoxCollider))]
public class FloorPanel : MonoBehaviour
{
    [SerializeField]
    BonusType bonus;

    [SerializeField]
    Renderer panelRenderer;

    [SerializeField]
    Material usedMaterial;

    [SerializeField]
    Light glow;

    Material activeMaterial;
    bool used;

    public BonusType Bonus => bonus;
    public bool IsUsed => used;

    void Awake()
    {
        activeMaterial = panelRenderer.sharedMaterial;
    }

    void OnTriggerEnter(Collider other)
    {
        if (used || LaserCorridorManager.Instance == null || !other.TryGetComponent(out PlayerHealth _))
        {
            return;
        }

        if (LaserCorridorManager.Instance.ApplyBonus(bonus))
        {
            SetUsed(true);
        }
    }

    public void ResetPanel()
    {
        SetUsed(false);
    }

    void SetUsed(bool isUsed)
    {
        used = isUsed;
        panelRenderer.sharedMaterial = isUsed ? usedMaterial : activeMaterial;
        if (glow != null)
        {
            glow.enabled = !isUsed;
        }
    }
}
