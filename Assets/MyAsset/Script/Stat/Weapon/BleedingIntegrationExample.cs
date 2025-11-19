using UnityEngine;

/// <summary>
/// Example snippet: call these when Bleeding is applied/removed to show icon on healthbar.
/// Integrate into your StatusManager/Bleeding effect code where apply/expire happen.
/// </summary>
public static class BleedingIntegrationExample
{
    // Provide the sprite asset via inspector reference somewhere (e.g. a central Resources folder or StatusIconRegistry)
    public static Sprite bleedingSprite; // set this at startup (Resources.Load or assigned from inspector)

    public static void OnBleedingApplied(GameObject target, int intensity = 1)
    {
        if (HealthBarManager.Instance == null) return;
        if (bleedingSprite == null)
        {
            // Try load from Resources/StatusIcons/bleeding.png
            bleedingSprite = Resources.Load<Sprite>("StatusIcons/bleeding");
            if (bleedingSprite == null) Debug.LogWarning("[BleedingIntegrationExample] bleedingSprite not set and not found in Resources/StatusIcons/bleeding");
        }

        HealthBarManager.Instance.AddStatusIconFor(target, "bleeding", bleedingSprite, "Bleeding");
    }

    public static void OnBleedingRemoved(GameObject target)
    {
        if (HealthBarManager.Instance == null) return;
        HealthBarManager.Instance.RemoveStatusIconFor(target, "bleeding");
    }
}