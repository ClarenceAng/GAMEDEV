using UnityEngine;

public class BonusPad : MonoBehaviour
{
    public enum BonusType
    {
        Heal,
        Shield,
        LaserSlowdown,
        SpeedBoost
    }

    private BonusType type;
    private LaserCorridorGameManager manager;
    private bool available = true;
    private Renderer[] renderers;
    private Color[] originalColors;

    public void Configure(BonusType bonusType, LaserCorridorGameManager gameManager)
    {
        type = bonusType;
        manager = gameManager;
        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        originalColors = new Color[renderers.Length];

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
                originalColors[i] = renderers[i].material.color;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!available || manager == null || !other.CompareTag("Player"))
            return;

        if (manager.ApplyBonus(type))
        {
            available = false;
            SetVisualAvailability(false);
        }
    }

    public void SetRunPosition(Vector3 position)
    {
        transform.position = position;
    }

    public void ResetPad()
    {
        available = true;
        SetVisualAvailability(true);
    }

    private void SetVisualAvailability(bool value)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r.material == null)
                continue;

            Color color = i < originalColors.Length ? originalColors[i] : Color.white;
            r.material.color = value ? color : Color.Lerp(color, Color.black, 0.7f);
        }
    }
}
