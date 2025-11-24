using UnityEngine;

namespace GameplayFeatures
{
    /// <summary>
    /// ConsumableAtk - Increases ATK stat by 1-3 points.
    /// 
    /// USAGE:
    /// 1. Create via: Right-click in Project > Create > Gameplay/Consumable ATK
    /// 2. Set itemID, displayName, and icon
    /// 3. Use from inventory by calling Use(CharacterEquipment)
    /// 
    /// WIRING:
    /// - Requires the target to have stat tracking (PlayerStat or similar)
    /// - Modify the Use() method below to match your actual stat system
    /// </summary>
    [CreateAssetMenu(fileName = "ConsumableAtk", menuName = "Gameplay/Consumable ATK")]
    public class ConsumableAtk : ItemBase
    {
        [Header("ATK Boost Settings")]
        [Tooltip("Minimum ATK increase")]
        public int minBoost = 1;
        
        [Tooltip("Maximum ATK increase")]
        public int maxBoost = 3;

        public override bool Use(CharacterEquipment target)
        {
            if (target == null)
            {
                Debug.LogWarning("[ConsumableAtk] Target is null!");
                return false;
            }

            // Calculate boost amount
            int boostAmount = Random.Range(minBoost, maxBoost + 1);

            // Try to apply ATK boost
            // Note: This is a placeholder implementation since we don't have direct access to ATK stat
            // You'll need to adjust this based on your actual stat system
            
            var playerStat = target.GetComponent<PlayerStat>();
            if (playerStat != null)
            {
                try
                {
                    // Try to find atk or attack field
                    var atkField = playerStat.GetType().GetField("atk") ?? 
                                   playerStat.GetType().GetField("attack") ??
                                   playerStat.GetType().GetField("ATK") ??
                                   playerStat.GetType().GetField("Attack");
                    
                    if (atkField != null)
                    {
                        int currentAtk = (int)atkField.GetValue(playerStat);
                        int newAtk = currentAtk + boostAmount;
                        atkField.SetValue(playerStat, newAtk);
                        
                        Debug.Log($"[ConsumableAtk] {displayName} used on {target.gameObject.name}! ATK increased by {boostAmount} ({currentAtk} -> {newAtk})");
                        return true;
                    }
                    else
                    {
                        Debug.LogWarning("[ConsumableAtk] Could not find ATK field on PlayerStat. Please adjust the implementation.");
                        Debug.Log($"[ConsumableAtk] {displayName} would increase ATK by {boostAmount} (implementation needed)");
                        return true; // Return true anyway so item is consumed
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ConsumableAtk] Error applying ATK boost: {ex.Message}");
                    Debug.Log($"[ConsumableAtk] {displayName} would increase ATK by {boostAmount} (implementation needed)");
                    return true;
                }
            }
            else
            {
                Debug.LogWarning($"[ConsumableAtk] No PlayerStat found on {target.gameObject.name}");
                Debug.Log($"[ConsumableAtk] {displayName} would increase ATK by {boostAmount} (implementation needed)");
                return true;
            }
        }

        public override string GetFormattedDescription()
        {
            return $"{description}\nIncreases ATK by {minBoost}-{maxBoost} permanently.";
        }
    }
}
