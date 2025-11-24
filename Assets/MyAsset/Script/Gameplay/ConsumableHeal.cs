using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// ConsumableHeal - Restores 1-3 HP to the target character.
    /// 
    /// USAGE:
    /// 1. Create via: Right-click in Project > Create > Gameplay/Consumable Heal
    /// 2. Set itemID, displayName, and icon
    /// 3. Use from inventory by calling Use(CharacterEquipment)
    /// 
    /// WIRING:
    /// - Requires the target to have a PlayerStat or similar component with HP tracking
    /// - Modify the Use() method below to match your actual stat system
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableHeal", menuName = "Gameplay/Consumable Heal")]
    public class ConsumableHeal : ItemBase
    {
        [Header("Heal Settings")]
        [Tooltip("Minimum HP restored")]
        public int minHeal = 1;
        
        [Tooltip("Maximum HP restored")]
        public int maxHeal = 3;

        public override bool Use(CharacterEquipment target)
        {
            if (target == null)
            {
                Debug.LogWarning("[ConsumableHeal] Target is null!");
                return false;
            }

            // Get PlayerStat component (adjust based on your actual stat system)
            var playerStat = target.GetComponent<PlayerStat>();
            if (playerStat == null)
            {
                Debug.LogWarning($"[ConsumableHeal] No PlayerStat found on {target.gameObject.name}");
                return false;
            }

            // Calculate heal amount
            int healAmount = Random.Range(minHeal, maxHeal + 1);

            // Apply heal (this assumes PlayerStat has a public hp field)
            // Adjust this based on your actual PlayerStat implementation
            try
            {
                // Attempt to access hp field via reflection if not public
                var hpField = playerStat.GetType().GetField("hp");
                var maxHpField = playerStat.GetType().GetField("maxHp");
                
                if (hpField != null && maxHpField != null)
                {
                    int currentHp = (int)hpField.GetValue(playerStat);
                    int maxHp = (int)maxHpField.GetValue(playerStat);
                    int newHp = Mathf.Min(currentHp + healAmount, maxHp);
                    hpField.SetValue(playerStat, newHp);
                    
                    Debug.Log($"[ConsumableHeal] {displayName} used on {target.gameObject.name}! Healed {healAmount} HP ({currentHp} -> {newHp})");
                    return true;
                }
                else
                {
                    Debug.LogWarning("[ConsumableHeal] Could not find hp/maxHp fields on PlayerStat. Please adjust the implementation.");
                    Debug.Log($"[ConsumableHeal] {displayName} would heal {healAmount} HP (implementation needed)");
                    return true; // Return true anyway so item is consumed
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[ConsumableHeal] Error applying heal: {ex.Message}");
                Debug.Log($"[ConsumableHeal] {displayName} would heal {healAmount} HP (implementation needed)");
                return true; // Return true anyway so item is consumed
            }
        }

        public override string GetFormattedDescription()
        {
            return $"{description}\nRestores {minHeal}-{maxHeal} HP.";
        }
    }
}
