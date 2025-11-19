using UnityEngine;

/// <summary>
/// Attach to an object in scene (e.g., a Debug object). Assign 'target' in Inspector.
/// Call context menu 'ApplyBleed' to simulate applying bleed to target:
/// - Adds BleedEffect via StatusManager if present
/// - Requests HealthBarManager to add bleeding icon
/// Use this to confirm whether UI and StatusManager integration works.
/// </summary>
public class ApplyBleedTester : MonoBehaviour
{
    public GameObject target;
    public int duration = 2;
    public int dmgPerTurn = 2;

    [ContextMenu("ApplyBleed")]
    public void ApplyBleed()
    {
        if (target == null)
        {
            Debug.LogWarning("[ApplyBleedTester] No target assigned.");
            return;
        }

        Debug.Log($"[ApplyBleedTester] Applying bleed to {{target.name}}: dur={{duration}}, dmgPerTurn={{dmgPerTurn}}");

        // Try StatusManager route
        var sm = target.GetComponent<StatusManager>();
        if (sm != null)
        {
            try
            {
                sm.AddBleed(dmgPerTurn, duration);
                Debug.Log("[ApplyBleedTester] Called StatusManager.AddBleed");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[ApplyBleedTester] Exception calling StatusManager.AddBleed: " + ex);
            }
        }
        else
        {
            Debug.LogWarning("[ApplyBleedTester] StatusManager not found on target.");
        }

        // UI icon
        if (HealthBarManager.Instance != null)
        {
            var sprite = BleedingIntegrationExample.bleedingSprite ?? Resources.Load<Sprite>("StatusIcons/bleeding");
            HealthBarManager.Instance.AddStatusIconFor(target, "bleeding", sprite, "Bleeding (test)");
            Debug.Log("[ApplyBleedTester] Requested HealthBarManager.AddStatusIconFor for " + target.name);
        }
        else
        {
            Debug.LogWarning("[ApplyBleedTester] HealthBarManager.Instance is null.");
        }
    }
}